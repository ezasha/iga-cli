param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x64', 'x86', 'both')]
    [string]$Architecture = 'x64',

    [ValidateSet('lightweight', 'self-contained')]
    [string]$Mode = 'lightweight',

    [switch]$Clean,

    [switch]$BuildOnly
)

$projectFile = "CLI.csproj"
$publishRoot = if ($Mode -eq 'lightweight') { "publish-lite" } else { "publish" }

function Get-RuntimeIdentifier([string]$arch) {
    if ($arch -eq 'x86') { return 'win-x86' }
    return 'win-x64'
}

function Publish-Arch([string]$arch) {
    $rid = Get-RuntimeIdentifier $arch
    $outputDir = Join-Path $publishRoot $arch

    Write-Host ""
    Write-Host "Publishing $Mode single-file for $arch ($rid)..." -ForegroundColor Cyan

    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }

    if ($BuildOnly) {
        dotnet build $projectFile -c $Configuration -p:RuntimeIdentifier=$rid -v minimal
    }
    else {
        $selfContained = if ($Mode -eq 'self-contained') { 'true' } else { 'false' }

        $publishArgs = @(
            'publish', $projectFile,
            '-c', $Configuration,
            '-r', $rid,
            '--self-contained', $selfContained,
            '-p:PublishSingleFile=true',
            '-p:DebugType=None',
            '-o', $outputDir,
            '-v', 'minimal'
        )

        if ($Mode -eq 'self-contained') {
            $publishArgs += '-p:EnableCompressionInSingleFile=true'
            $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
        }

        dotnet @publishArgs
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] $arch failed" -ForegroundColor Red
        return $false
    }

    $exePath = Join-Path $outputDir 'iga-cli.exe'
    if (Test-Path $exePath) {
        $sizeMB = [math]::Round(((Get-Item $exePath).Length / 1MB), 2)
        Write-Host "[OK] $arch ready: $exePath ($sizeMB MB)" -ForegroundColor Green
    }
    else {
        Write-Host "[WARN] $arch built but executable not found at expected path." -ForegroundColor Yellow
    }

    return $true
}

if ($Clean) {
    Write-Host "Cleaning bin/obj/publish folders..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force }
    if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force }
    if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }
    if (Test-Path "publish-lite") { Remove-Item "publish-lite" -Recurse -Force }
}

Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] restore failed" -ForegroundColor Red
    exit 1
}

$targets = @()
if ($Architecture -eq 'both') {
    $targets = @('x64', 'x86')
}
else {
    $targets = @($Architecture)
}

$okCount = 0
foreach ($arch in $targets) {
    if (Publish-Arch $arch) {
        $okCount++
    }
}

Write-Host ""
Write-Host "Build Summary: $okCount / $($targets.Count) successful" -ForegroundColor Cyan

if ($okCount -eq $targets.Count) {
    if ($BuildOnly) {
        Write-Host "Build outputs are in bin/." -ForegroundColor Green
    }
    else {
        Write-Host "Single-file executables are in $publishRoot/:" -ForegroundColor Green
        foreach ($arch in $targets) {
            Write-Host "  $publishRoot/$arch/iga-cli.exe" -ForegroundColor Green
        }
        if ($Mode -eq 'lightweight') {
            Write-Host "Note: lightweight mode requires .NET Desktop Runtime installed on target machine." -ForegroundColor Yellow
        }
    }
    exit 0
}

exit 1
