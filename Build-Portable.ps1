# Автономный комплект для передачи на другой компьютер с Windows x64.
param()
$ErrorActionPreference = 'Stop'
$portableRoot = $PSScriptRoot
$releaseRoot = Join-Path $portableRoot 'releases'
$buildFolder = Join-Path $releaseRoot ('build-' + [Guid]::NewGuid().ToString('N'))
$publishFolder = Join-Path $buildFolder 'publish'
$packageFolder = Join-Path $buildFolder 'TUSUR-ChessDraw'
$engineFile = Join-Path $portableRoot 'engines\bbpPairings.exe'
if (-not (Test-Path -LiteralPath $engineFile)) { throw 'Не найден engines\bbpPairings.exe.' }
New-Item -ItemType Directory -Force -Path $publishFolder, $packageFolder | Out-Null
& dotnet publish (Join-Path $portableRoot 'ChessDraw\ChessDraw.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -o $publishFolder
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать автономное приложение.' }
$portableExe = Join-Path $packageFolder 'ТУСУР — шахматная жеребьёвка.exe'
Copy-Item -LiteralPath (Join-Path $publishFolder 'ChessDraw.exe') -Destination $portableExe
New-Item -ItemType Directory -Path (Join-Path $packageFolder 'engines'), (Join-Path $packageFolder 'licenses') | Out-Null
Copy-Item -LiteralPath $engineFile -Destination (Join-Path $packageFolder 'engines\bbpPairings.exe')
$licenseCache = Join-Path $releaseRoot 'licenses'
New-Item -ItemType Directory -Force -Path $licenseCache | Out-Null
foreach ($licenseName in @('LICENSE.txt', 'Apache-2.0.txt')) {
    $cachedName = if ($licenseName -eq 'LICENSE.txt') { 'bbpPairings-LICENSE.txt' } else { $licenseName }
    $cachedFile = Join-Path $licenseCache $cachedName
    if (-not (Test-Path -LiteralPath $cachedFile)) {
        Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/BieremaBoyzProgramming/bbpPairings/v6.0.0/' + $licenseName) -OutFile $cachedFile
    }
    Copy-Item -LiteralPath $cachedFile -Destination (Join-Path $packageFolder ('licenses\' + $cachedName))
}
$nugetFolder = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$runtimeConfig = Get-Content -LiteralPath (Join-Path $portableRoot 'ChessDraw\bin\Release\net8.0-windows\win-x64\ChessDraw.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $packageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $runtimePackage = Join-Path (Join-Path $nugetFolder $packageName) $framework.version
    foreach ($runtimeLicense in @('LICENSE.TXT', 'LICENSE', 'THIRD-PARTY-NOTICES.TXT')) {
        $sourceLicense = Join-Path $runtimePackage $runtimeLicense
        if (Test-Path -LiteralPath $sourceLicense) {
            Copy-Item -LiteralPath $sourceLicense -Destination (Join-Path $packageFolder ('licenses\' + $framework.name + '-' + $runtimeLicense + '.txt'))
        }
    }
}
@'
ТУСУР — шахматная жеребьёвка

1. Распакуйте ZIP целиком в удобную папку.
2. Откройте «ТУСУР — шахматная жеребьёвка.exe» двойным щелчком.

Установка .NET не требуется. Комплект для Windows 10/11 x64.
Папку engines оставьте рядом с программой: она нужна для швейцарской жеребьёвки.
Для экспорта PDF должен быть включён стандартный компонент Windows Microsoft Print to PDF.
Турниры сохраняются на вашем компьютере: %LOCALAPPDATA%\ChessDraw.
'@ | Set-Content -LiteralPath (Join-Path $packageFolder 'Как запустить.txt') -Encoding UTF8
Write-Output ('PORTABLE_FOLDER=' + $packageFolder)
Write-Output ('PORTABLE_EXE=' + $portableExe)
$portableTest = Start-Process -FilePath $portableExe -ArgumentList '--self-test', '--quiet' -WindowStyle Hidden -PassThru -Wait
if ($portableTest.ExitCode -ne 0) { throw ('Самопроверка автономной сборки не пройдена: ' + (Join-Path $packageFolder 'self-test-failure.txt')) }
# Keep verification output outside the folder sent to a friend.
$checkFolder = Join-Path $buildFolder 'checks'
New-Item -ItemType Directory -Path $checkFolder | Out-Null
foreach ($checkFile in Get-ChildItem -LiteralPath $packageFolder -File | Where-Object { $_.Name -eq 'self-test-results.txt' -or $_.Name -eq 'ui-preview.png' -or $_.Name -like 'pdf-preview-*.png' }) {
    Move-Item -LiteralPath $checkFile.FullName -Destination $checkFolder
}
$zipFile = Join-Path $releaseRoot 'TUSUR-ChessDraw-Windows-x64.zip'
Compress-Archive -LiteralPath $packageFolder -DestinationPath $zipFile -Force
Write-Output ('PORTABLE_ZIP=' + $zipFile)
