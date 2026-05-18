# bisect-build.ps1
#
# Builds a single commit into an isolated worktree and stages the resulting
# KeeChallenge.dll for manual KeePass smoke testing.
#
# Workflow per commit:
#   1. Create a git worktree at .tmp/bisect/<sha-short>/
#   2. dotnet build Release/net48
#   3. Copy resulting KeeChallenge.dll to .tmp/bisect/staged/<sha-short>.dll
#   4. Print the path; YOU manually copy it into the KeePass Plugins folder,
#      restart KeePass, check Tools -> Plugins.
#
# Cleanup: pass -Cleanup to remove all worktrees.
#
# Usage:
#   pwsh -File scripts/bisect-build.ps1 -Commit <sha>
#   pwsh -File scripts/bisect-build.ps1 -Cleanup

[CmdletBinding()]
param(
    [string]$Commit,
    [switch]$Cleanup
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $bisectRoot = Join-Path $repoRoot '.tmp\bisect'
    $stagedDir  = Join-Path $bisectRoot 'staged'

    if ($Cleanup) {
        Write-Host "Removing worktrees under $bisectRoot ..."
        git worktree list | Where-Object { $_ -match '\.tmp\\bisect\\' } | ForEach-Object {
            $wt = ($_ -split '\s+')[0]
            Write-Host "  worktree remove --force $wt"
            git worktree remove --force $wt
        }
        if (Test-Path $bisectRoot) { Remove-Item $bisectRoot -Recurse -Force }
        Write-Host "Cleanup complete."
        return
    }

    if (-not $Commit) { throw 'Specify -Commit <sha> or -Cleanup.' }

    $sha = (git rev-parse --short=10 $Commit).Trim()
    $full = (git rev-parse $Commit).Trim()
    $wtPath = Join-Path $bisectRoot $sha

    if (-not (Test-Path $stagedDir)) { New-Item -ItemType Directory -Path $stagedDir | Out-Null }

    if (-not (Test-Path $wtPath)) {
        Write-Host "Creating worktree at $wtPath for $full ..."
        git worktree add --detach $wtPath $full | Out-Null
    } else {
        Write-Host "Worktree already exists: $wtPath"
    }

    $csproj = Join-Path $wtPath 'KeeChallenge\src\KeeChallenge.csproj'
    if (-not (Test-Path $csproj)) { throw "csproj not found at $csproj" }

    Write-Host "`nBuilding Release/net48 at $sha ..."
    dotnet build $csproj -c Release /nologo /clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw "build failed for $sha" }

    $built = Join-Path $wtPath 'KeeChallenge\src\bin\Release\net48\KeeChallenge.dll'
    if (-not (Test-Path $built)) { throw "expected output missing: $built" }

    $staged = Join-Path $stagedDir "$sha.dll"
    Copy-Item $built $staged -Force

    $fileInfo = Get-Item $staged
    Write-Host ""
    Write-Host "=== READY FOR MANUAL SMOKE TEST ===" -ForegroundColor Cyan
    Write-Host "Commit:        $full"
    Write-Host "Staged DLL:    $staged"
    Write-Host "Size:          $($fileInfo.Length) bytes"
    Write-Host "FileVersion:   $($fileInfo.VersionInfo.FileVersion)"
    Write-Host ""
    Write-Host "Next steps (manual):"
    Write-Host "  1. Close KeePass if it is running."
    Write-Host "  2. Copy `"$staged`" to `"C:\Program Files\KeePass Password Safe 2\Plugins\KeeChallenge.dll`""
    Write-Host "     (requires admin; back up the existing file first)."
    Write-Host "  3. Launch KeePass; open Tools -> Plugins."
    Write-Host "  4. Report: GOOD (plugin appears) or BAD (panel empty / not listed)."
}
finally {
    Pop-Location
}
