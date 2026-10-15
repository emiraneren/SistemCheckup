# Yeni sürüm (yama) yayınlar: derler, zip paketini ve manifest.json'u üretir, commit + tag atar ve GitHub Release oluşturur.
# Release sayfasında yalnızca zip ve BENIOKU.txt görünür. Güncelleyici manifesti etiketli commit'ten okur,
# zip'i indirir ve yalnızca değişen dosyaları yerine koyar.
#
#   .\yayinla.ps1 -Surum 1.1.1 -Notlar "Disk testine sıcaklık eklendi", "Raporda yazım hataları düzeltildi"
#   .\yayinla.ps1 -Surum 1.1.1 -Notlar "..." -Taslak     # yalnızca derler ve dist\'i hazırlar, hiçbir şey göndermez
#
# Notları kullanıcının anlayacağı dilde, madde madde yazın (güncelleyicide aynen görünür).
param(
    [Parameter(Mandatory = $true)][string]$Surum,
    [Parameter(Mandatory = $true)][string[]]$Notlar,
    [switch]$Taslak
)

$ErrorActionPreference = 'Stop'
$kok = $PSScriptRoot
Set-Location $kok
$ayar = Get-Content -Raw -Encoding UTF8 (Join-Path $kok 'proje.json') | ConvertFrom-Json
$etiket = "v$Surum"
$yeni = [version]$Surum
$tarih = Get-Date -Format 'yyyy-MM-dd'
$utf8 = New-Object Text.UTF8Encoding($false)
$Notlar = @($Notlar | ForEach-Object { "$_".Trim() } | Where-Object { $_ })

if (-not $Taslak) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) bulunamadı.' }
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI oturumu yok. Önce: gh auth login' }
    git fetch --tags --quiet 2>$null
    if (git tag --list $etiket) { throw "$etiket etiketi zaten var. Daha yüksek bir sürüm numarası verin." }
    $onceki = @(git tag --list 'v*') | ForEach-Object { try { [version]$_.TrimStart('v') } catch {} } | Sort-Object | Select-Object -Last 1
    if ($onceki -and $yeni -le $onceki) { throw "Sürüm $Surum, son yayınlanan sürümden ($onceki) büyük olmalı." }
}

# Sürüm geçmişi (en yeni başta)
$gecmisYol = Join-Path $kok 'surumler.json'
$gecmis = @()
if (Test-Path $gecmisYol) { $gecmis = @([IO.File]::ReadAllText($gecmisYol, [Text.Encoding]::UTF8) | ConvertFrom-Json) }
$gecmis = @([ordered]@{ surum = $Surum; tarih = $tarih; notlar = $Notlar }) + @($gecmis | Where-Object { $_.surum -ne $Surum })

& (Join-Path $kok 'build.ps1') -Surum $Surum
$dist = Join-Path $kok 'dist'
$paket = Join-Path $dist 'SistemCheckup'

# Paketteki her dosyanın özeti (surum.json hariç; onu güncelleyici kendisi yazar)
$dosyalar = @(Get-ChildItem $paket -Recurse -File | ForEach-Object {
    $goreli = $_.FullName.Substring($paket.Length + 1).Replace('\', '/')
    [ordered]@{ hedef = $goreli; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(); boyut = $_.Length }
} | Sort-Object { $_.hedef })

$manifest = [ordered]@{ surum = $Surum; etiket = $etiket; tarih = $tarih; notlar = $Notlar; gecmis = @($gecmis | Select-Object -First 30); paket = $null; dosyalar = $dosyalar }
[IO.File]::WriteAllText((Join-Path $paket 'app\surum.json'), ($manifest | ConvertTo-Json -Depth 6), $utf8)

# Zip: içinde tek bir "SistemCheckup" klasörü olur, çıkarınca dağılmaz
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipAd = "SistemCheckup-$etiket.zip"
$zip = Join-Path $dist $zipAd
[IO.Compression.ZipFile]::CreateFromDirectory($paket, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
$manifest.paket = [ordered]@{ ad = $zipAd; sha256 = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower(); boyut = (Get-Item $zip).Length }
[IO.File]::WriteAllText((Join-Path $kok 'manifest.json'), ($manifest | ConvertTo-Json -Depth 6), $utf8)
Copy-Item (Join-Path $kok 'BENIOKU.txt') $dist
Write-Host "Paket: $zip" -ForegroundColor Green

if ($Taslak) { Write-Host 'Taslak mod: hiçbir şey gönderilmedi (manifest.json yerelde güncellendi).' -ForegroundColor Yellow; return }

[IO.File]::WriteAllText($gecmisYol, (ConvertTo-Json -InputObject @($gecmis) -Depth 4), $utf8)
$md = "# Değişiklikler`n`n" + (($gecmis | ForEach-Object { "## $($_.surum) — $($_.tarih)`n`n" + ((@($_.notlar) | ForEach-Object { "- $_" }) -join "`n") }) -join "`n`n") + "`n"
[IO.File]::WriteAllText((Join-Path $kok 'DEGISIKLIKLER.md'), $md, $utf8)

git add -A
git commit -q -m "Sürüm $Surum" -m (($Notlar | ForEach-Object { "- $_" }) -join "`n")
git tag -a $etiket -m "Sistem Checkup $Surum"
$dal = git rev-parse --abbrev-ref HEAD
git push -q origin $dal
git push -q origin $etiket

$notMetni = "## Bu sürümde neler var`n`n" + (($Notlar | ForEach-Object { "- $_" }) -join "`n") + @"


## Nasıl kurulur?

1. Aşağıdan **$zipAd** dosyasını indirin.
2. Zip'e sağ tıklayıp **Tümünü ayıkla** deyin.
3. Çıkan klasördeki **SistemCheckup.exe**'yi açın.

Zaten kuruluysa indirmenize gerek yok: Sistem Checkup yeni sürümü kendisi bildirir, **Şimdi güncelle**'ye basmanız yeterli.

Ayrıntılar için **BENIOKU.txt** dosyasına bakın.
"@
gh release create $etiket $zip (Join-Path $dist 'BENIOKU.txt') --repo $ayar.repo --title "Sistem Checkup $Surum" --notes $notMetni --latest
if ($LASTEXITCODE -ne 0) { throw 'GitHub Release oluşturulamadı.' }
Write-Host "Yayınlandı: https://github.com/$($ayar.repo)/releases/tag/$etiket" -ForegroundColor Green
