param(
    [Parameter(Mandatory = $true)]
    [string]$PrivateKeyPath,

    [string]$VersionPath = "..\VERSION"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not [System.IO.Path]::IsPathRooted($VersionPath))
{
    $scriptRoot = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptRoot) -and -not [string]::IsNullOrEmpty($MyInvocation.MyCommand.Path))
    {
        $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $basePath = if ([string]::IsNullOrEmpty($scriptRoot))
    {
        (Get-Location).Path
    }
    else
    {
        $scriptRoot
    }

    $VersionPath = Join-Path $basePath $VersionPath
}

$resolvedVersionPath = (Resolve-Path $VersionPath).Path
$resolvedKeyPath = (Resolve-Path $PrivateKeyPath).Path

$rawLines = Get-Content -Path $resolvedVersionPath
$lines = @()
foreach ($line in $rawLines)
{
    if (-not [string]::IsNullOrWhiteSpace($line))
    {
        $lines += $line.Trim()
    }
}

if ($lines.Count -lt 3)
{
    throw "VERSION feed must contain header, at least one component line, and footer."
}

$separator = $lines[0][0]
$components = New-Object System.Collections.Generic.List[string]

for ($i = 1; $i -lt $lines.Count; ++$i)
{
    $current = $lines[$i]
    if ($current.Length -gt 0 -and $current[0] -eq $separator)
    {
        break
    }

    $components.Add($current)
}

if ($components.Count -eq 0)
{
    throw "VERSION feed does not contain any component entries to sign."
}

$canonicalBuilder = New-Object System.Text.StringBuilder
foreach ($entry in $components)
{
    [void]$canonicalBuilder.Append($entry)
    [void]$canonicalBuilder.Append("`n")
}
$canonicalContent = $canonicalBuilder.ToString()

$privateXml = Get-Content -Path $resolvedKeyPath -Raw

$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp = $false
$rsa.FromXmlString($privateXml)

$sigBytes = $rsa.SignData(
    [System.Text.Encoding]::UTF8.GetBytes($canonicalContent),
    [System.Security.Cryptography.SHA512]::Create())
$signature = [Convert]::ToBase64String($sigBytes)

$publicXml = $rsa.ToXmlString($false)
$hashAlgo = [System.Security.Cryptography.SHA256]::Create()
$keyHash = $hashAlgo.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($publicXml))
$fingerprint = ([BitConverter]::ToString($keyHash)).Replace("-", "")

$outLines = @()
$outLines += ("{0}{1}" -f $separator, $signature)
$outLines += $components
$outLines += [string]$separator

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedVersionPath, (($outLines -join "`r`n") + "`r`n"), $utf8NoBom)

Write-Host ("Signed VERSION feed: {0}" -f $resolvedVersionPath)
Write-Host ("Pinned public key SHA256: {0}" -f $fingerprint)