[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string[]]$Configuration = @('Debug', 'Release'),

    [ValidateSet('x86', 'x64')]
    [string[]]$Architecture = @('x86', 'x64')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tests\KeeChallenge.RegressionTests\KeeChallenge.RegressionTests.csproj'

foreach ($config in $Configuration) {
    foreach ($arch in $Architecture) {
        # Rebuild because the architecture outputs share compiler intermediate files.
        & dotnet build $project -t:Rebuild -c $config "-p:PlatformTarget=$arch" --nologo
        if ($LASTEXITCODE -ne 0) { throw "Regression build failed: $config / $arch" }
        $runner = Join-Path $root "tests\KeeChallenge.RegressionTests\bin\$arch\$config\net48\KeeChallenge.RegressionTests.exe"
        if (-not (Test-Path -LiteralPath $runner)) {
            throw "Regression executable is missing: $runner"
        }
        $output = & $runner (Join-Path $root 'VERSION') 2>&1
        $exitCode = $LASTEXITCODE
        $output | Write-Output
        if ($exitCode -ne 0 -or @($output) -notcontains 'RESULT: PASS (0 failed)') {
            throw "Regressions did not report a verified pass: $config / $arch (exit $exitCode)"
        }
        $bits = if ($arch -eq 'x86') { 32 } else { 64 }
        if (@($output) -notcontains "Running .NET Framework regressions ($bits-bit).") {
            throw "Regression runner did not execute with the requested architecture: $arch"
        }
    }
}
