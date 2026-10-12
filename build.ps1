# Clutch dağıtım paketi: tek dosya exe (.NET gömülü) + ffmpeg + lisans bildirimleri -> artifacts/*.zip
# Kullanım:  ./build.ps1
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\Clutch\Clutch.csproj"
[xml]$csproj = Get-Content $project
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

$ffmpeg = Join-Path $root "src\Clutch\ffmpeg\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) {
    throw "src\Clutch\ffmpeg\ffmpeg.exe bulunamadı. ddagrab içeren bir ffmpeg derlemesini oraya koy (README > Kaynaktan derleme)."
}

$publish = Join-Path $root "artifacts\publish"
$stage = Join-Path $root "artifacts\Clutch"
$zip = Join-Path $root "artifacts\Clutch-v$version-windows-x64.zip"

foreach ($path in @($publish, $stage)) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "Clutch $version derleniyor..."
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız" }

New-Item -ItemType Directory -Force $stage, (Join-Path $stage "ffmpeg") | Out-Null
Copy-Item (Join-Path $publish "Clutch.exe") $stage
Copy-Item $ffmpeg (Join-Path $stage "ffmpeg\ffmpeg.exe")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $stage "LICENSE.txt")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $stage "THIRD-PARTY-NOTICES.md")

Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Hazır: $zip ($size MB)"
