# ChessDraw WinForms build; UTF-8 BOM for Windows PowerShell 5.1 compatibility.
param([switch]$SelfContained, [switch]$SkipEngineDownload)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'ChessDraw/ChessDraw.csproj'
$dist = Join-Path $root 'dist'
$engine = Join-Path $root 'engines/bbpPairings.exe'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw 'Не найден dotnet. Установите .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0'
}
if (-not $SkipEngineDownload -and -not (Test-Path $engine)) {
  try {
    & (Join-Path $root 'Install-BBPPairings.ps1')
  } catch {
    Write-Warning ('Не удалось скачать bbpPairings: ' + $_.Exception.Message)
    Write-Warning 'Сборка приложения продолжится. Для швейцарской системы движок потребуется установить отдельно.'
  }
}
New-Item -ItemType Directory -Force $dist | Out-Null
Write-Host 'Сборка ChessDraw...'
if ($SelfContained) {
  Write-Host 'Автономная версия: NuGet скачает Windows Desktop Runtime, нужен стабильный интернет.'
  & dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false -o $dist
} else {
  Write-Host 'Стандартная версия: без скачивания Windows Desktop Runtime с NuGet.'
  & dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true -o $dist
}
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки ChessDraw (см. сообщения выше).' }
if (Test-Path $engine) {
  New-Item -ItemType Directory -Force (Join-Path $dist 'engines') | Out-Null
  Copy-Item $engine (Join-Path $dist 'engines/bbpPairings.exe') -Force
  Write-Host 'bbpPairings установлен рядом с приложением.'
} else {
  Write-Warning 'bbpPairings.exe не найден. Круговые турниры работают; швейцарская жеребьёвка требует движок.'
}
Write-Host ('Готово: ' + (Join-Path $dist 'ChessDraw.exe'))
Write-Host 'ВНИМАНИЕ: для стандартной сборки должен быть установлен .NET 8 Windows Desktop Runtime (SDK также подходит).'
