# Install the official upstream Windows binary from a tagged BBP Pairings release.
# The engine is maintained by Jeremy Bierema (Apache-2.0), and is NOT authored by this project.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $base 'engines'
New-Item -ItemType Directory -Force $dest | Out-Null
$target = Join-Path $dest 'bbpPairings.exe'
if (Test-Path $target) {
  Write-Host "Движок уже установлен: $target"
  return
}
Write-Host 'Загрузка bbpPairings v6.0.0 с официального GitHub...'
$headers = @{ 'User-Agent' = 'ChessDraw-Installer'; 'Accept' = 'application/vnd.github+json' }
$release = Invoke-RestMethod -Headers $headers -Uri 'https://api.github.com/repos/BieremaBoyzProgramming/bbpPairings/releases/tags/v6.0.0'
$assets = @($release.assets)
$windowsAssets = @($assets | Where-Object {
  $_.name -match '(?i)(win|mingw|windows)' -and $_.name -match '(?i)\.(zip|exe)$'
})
$asset = $windowsAssets | Where-Object { $_.name -match '(?i)(64|x64|amd64)' } | Select-Object -First 1
if ($null -eq $asset) { $asset = $windowsAssets | Select-Object -First 1 }
if ($null -eq $asset) {
  throw "Не найдено Windows-версии в официальном релизе. Откройте https://github.com/BieremaBoyzProgramming/bbpPairings/releases/tag/v6.0.0 и поместите bbpPairings.exe вручную в папку $dest"
}
$temp = Join-Path $env:TEMP ('chessdraw-bbp-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $temp | Out-Null
try {
  $file = Join-Path $temp $asset.name
  Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $file
  if ($file.ToLowerInvariant().EndsWith('.exe')) {
    Copy-Item $file $target -Force
  } else {
    $unpacked = Join-Path $temp 'unpacked'
    Expand-Archive -Path $file -DestinationPath $unpacked
    $candidate = Get-ChildItem $unpacked -Filter '*.exe' -Recurse | Where-Object { $_.Name -match '(?i)bbp.?pairings' } | Select-Object -First 1
    if ($null -eq $candidate) { throw 'В загруженном архиве не найден bbpPairings.exe.' }
    Copy-Item $candidate.FullName $target -Force
  }
  Write-Host "Установлено: $target"
  Write-Host 'Лицензия движка: https://github.com/BieremaBoyzProgramming/bbpPairings/blob/v6.0.0/LICENSE.txt'
} finally {
  Remove-Item $temp -Force -Recurse -ErrorAction SilentlyContinue
}
