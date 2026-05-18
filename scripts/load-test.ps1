# load-test.ps1
#
# Reflectively loads a built KeeChallenge.dll and exercises the same steps the
# KeePass plugin host performs: assembly load, GetTypes, Plugin subclass discovery,
# parameterless ctor, Initialize(null) sanity, and UpdateFeedSecurity.TryConfigure.
#
# Pass criterion: every section reports OK; Initialize(null) returns False
# (that is correct behavior — host==null guard); no exceptions.
#
# IMPORTANT: This probe is NECESSARY but NOT SUFFICIENT. v2.0.7 passed this probe
# yet was silently rejected by the actual KeePass plugin host. Always follow up
# with a real KeePass smoke test (open KeePass, verify the plugin appears in the
# Plugins panel) before tagging any release.
#
# Usage:
#   pwsh -File scripts/load-test.ps1 -PluginPath <path to KeeChallenge.dll> [-KeePassPath <path to KeePass.exe>]
#
# Default KeePass path: C:\Program Files\KeePass Password Safe 2\KeePass.exe
# Exit code: 0 on success, 1 on any failure.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginPath,

    [string]$KeePassPath = 'C:\Program Files\KeePass Password Safe 2\KeePass.exe'
)

$ErrorActionPreference = 'Stop'
$script:failures = @()

function Fail($msg) {
    Write-Host "FAIL: $msg" -ForegroundColor Red
    $script:failures += $msg
}

function Pass($msg) {
    Write-Host "OK:   $msg" -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $PluginPath)) { Fail "Plugin DLL not found: $PluginPath"; exit 1 }
if (-not (Test-Path -LiteralPath $KeePassPath)) { Fail "KeePass.exe not found: $KeePassPath"; exit 1 }

Write-Host "=== Plugin file ==="
$file = Get-Item -LiteralPath $PluginPath
Write-Host "  Path: $($file.FullName)"
Write-Host "  Size: $($file.Length) bytes"
Write-Host "  Modified: $($file.LastWriteTime)"
Write-Host "  FileVersion: $($file.VersionInfo.FileVersion)"
Write-Host "  ProductVersion: $($file.VersionInfo.ProductVersion)"

Write-Host "`n=== MOTW / Zone.Identifier check ==="
try {
    $null = Get-Content -LiteralPath $PluginPath -Stream Zone.Identifier -ErrorAction Stop
    Fail "Plugin has Mark-of-the-Web (Zone.Identifier ADS); unblock with Unblock-File."
} catch {
    Pass "No Zone.Identifier ADS on plugin."
}

Write-Host "`n=== Load host KeePass.exe ==="
$kpAsm = [System.Reflection.Assembly]::LoadFile($KeePassPath)
Write-Host "  Loaded: $($kpAsm.FullName)"

# Install resolver so plugin's KeePass v2.x.y.0 reference binds to the running KeePass.
$resolver = [System.ResolveEventHandler] {
    param($sender, $args)
    $simple = ($args.Name -split ',')[0]
    if ($simple -eq 'KeePass') { return $kpAsm }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

Write-Host "`n=== LoadFile plugin ==="
$asm = $null
try {
    $asm = [System.Reflection.Assembly]::LoadFile($PluginPath)
    Pass "Plugin loaded: $($asm.FullName)"
} catch {
    Fail "Plugin LoadFile threw: $($_.Exception.GetType().FullName): $($_.Exception.Message)"
    exit 1
}

Write-Host "`n=== Referenced assemblies ==="
$asm.GetReferencedAssemblies() | ForEach-Object {
    $pk = $_.GetPublicKeyToken()
    $tok = if ($pk -and $pk.Length -gt 0) { -join ($pk | ForEach-Object { $_.ToString('x2') }) } else { '(null)' }
    Write-Host ("  {0,-26} Version={1} PublicKeyToken={2}" -f $_.Name, $_.Version, $tok)
}

Write-Host "`n=== GetTypes() ==="
$types = @()
try {
    $types = $asm.GetTypes()
    Pass "GetTypes returned $($types.Length) types"
} catch [System.Reflection.ReflectionTypeLoadException] {
    Fail "ReflectionTypeLoadException"
    foreach ($le in $_.Exception.LoaderExceptions) {
        Write-Host "  LoaderException: $($le.GetType().FullName): $($le.Message)" -ForegroundColor Red
    }
    $types = @($_.Exception.Types | Where-Object { $_ -ne $null })
} catch {
    Fail "GetTypes threw: $($_.Exception.GetType().FullName): $($_.Exception.Message)"
}

Write-Host "`n=== Plugin subclass discovery ==="
$pluginBase = $kpAsm.GetType('KeePass.Plugins.Plugin', $false)
if ($null -eq $pluginBase) { Fail "Could not resolve KeePass.Plugins.Plugin in host assembly."; exit 1 }
$subs = @($types | Where-Object {
    $_ -ne $null -and $_.IsClass -and -not $_.IsAbstract -and $pluginBase.IsAssignableFrom($_)
})
if ($subs.Count -eq 0) { Fail "No Plugin subclass found in $PluginPath" }
elseif ($subs.Count -gt 1) { Fail "Multiple Plugin subclasses found ($($subs.Count))" }
else { Pass "Plugin subclass: $($subs[0].FullName)" }

if ($subs.Count -ge 1) {
    Write-Host "`n=== Activator.CreateInstance ==="
    try {
        $inst = [System.Activator]::CreateInstance($subs[0])
        Pass "ctor returned instance: $($inst.GetType().FullName)"
    } catch {
        Fail "ctor threw: $($_.Exception.GetType().FullName): $($_.Exception.Message)"
        $ie = $_.Exception.InnerException
        while ($ie) {
            Write-Host "  Inner: $($ie.GetType().FullName): $($ie.Message)" -ForegroundColor Red
            $ie = $ie.InnerException
        }
        $inst = $null
    }

    if ($inst) {
        Write-Host "`n=== Initialize(null) host-guard check ==="
        $init = $subs[0].GetMethod('Initialize')
        try {
            $r = $init.Invoke($inst, @($null))
            if ($r -eq $false) {
                Pass "Initialize(null) returned False (expected)"
            } else {
                Fail "Initialize(null) returned $r; expected False"
            }
        } catch {
            Fail "Initialize(null) threw: $($_.Exception.GetType().FullName): $($_.Exception.Message)"
            $ie = $_.Exception.InnerException
            while ($ie) {
                Write-Host "  Inner: $($ie.GetType().FullName): $($ie.Message)" -ForegroundColor Red
                $ie = $ie.InnerException
            }
        }
    }
}

Write-Host "`n=== Optional: UpdateFeedSecurity.TryConfigure ==="
$ufs = $asm.GetType('KeeChallenge.UpdateFeedSecurity', $false)
if ($null -eq $ufs) {
    Write-Host "  (skipped — UpdateFeedSecurity type not present in this build)"
} else {
    $tryConfig = $ufs.GetMethod('TryConfigure', [System.Reflection.BindingFlags]'NonPublic,Static,Public')
    if ($null -eq $tryConfig) {
        Fail "UpdateFeedSecurity exists but TryConfigure method not found"
    } else {
        $callArgs = [object[]]::new(1)
        $callArgs[0] = ''
        try {
            $result = $tryConfig.Invoke($null, $callArgs)
            if ($result) { Pass "TryConfigure returned True" }
            else { Fail "TryConfigure returned False; error='$($callArgs[0])'" }
        } catch {
            Fail "TryConfigure threw: $($_.Exception.GetType().FullName): $($_.Exception.Message)"
            $ie = $_.Exception.InnerException
            while ($ie) {
                Write-Host "  Inner: $($ie.GetType().FullName): $($ie.Message)" -ForegroundColor Red
                $ie = $ie.InnerException
            }
        }
    }
}

Write-Host ""
if ($script:failures.Count -eq 0) {
    Write-Host "RESULT: PASS (all checks succeeded)" -ForegroundColor Green
    exit 0
} else {
    Write-Host "RESULT: FAIL ($($script:failures.Count) failure(s))" -ForegroundColor Red
    foreach ($f in $script:failures) { Write-Host "  - $f" -ForegroundColor Red }
    exit 1
}
