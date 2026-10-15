# Yeni sürüm (yama) yayınlar: derler, manifest üretir, commit + tag atar ve GitHub Release oluşturur.
# Kullanıcılar Guncelleyici.exe'ye bastığında bu sürümdeki değişen dosyaları indirir.
#
#   .\yayinla.ps1 -Surum 1.0.1 -Notlar "Disk testine sıcaklık eklendi"
#   .\yayinla.ps1 -Surum 1.0.1 -Taslak     # yalnızca derler ve dist\'i hazırlar, hiçbir şey göndermez
param(
    [Parameter(Mandatory = $true)][string]$Surum,
    [string]$Notlar = '',
    [switch]$Taslak
)

$ErrorActionPreference = 'Stop'
$kok = $PSScriptRoot
Set-Location $kok
$ayar = Get-Content -Raw -Encoding UTF8 (Join-Path $kok 'proje.json') | ConvertFrom-Json
$etiket = "v$Surum"
$yeni = [version]$Surum

if (-not $Taslak) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) bulunamadı.' }
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI oturumu yok. Önce: gh auth login' }
    git fetch --tags --quiet 2>$null
    if (git tag --list $etiket) { throw "$etiket etiketi zaten var. Daha yüksek bir sürüm numarası verin." }
    $onceki = @(git tag --list 'v*') | ForEach-Object { try { [version]$_.TrimStart('v') } catch {} } | Sort-Object | Select-Object -Last 1
    if ($onceki -and $yeni -le $onceki) { throw "Sürüm $Surum, son yayınlanan sürümden ($onceki) büyük olmalı." }
}

& (Join-Path $kok 'build.ps1') -Surum $Surum
$dist = Join-Path $kok 'dist'

# Yamalanabilir dosyalar: release'e tek tek yüklenir, güncelleyici yalnızca değişenleri indirir
$dosyalar = @(
    @{ yol = 'SistemCheckup.exe'; hedef = 'SistemCheckup.exe' }
    @{ yol = 'Guncelleyici.exe'; hedef = 'Guncelleyici.exe' }
) + @(Get-ChildItem (Join-Path $dist 'app') -File | ForEach-Object { @{ yol = "app\$($_.Name)"; hedef = "app/$($_.Name)" } })

$liste = foreach ($d in $dosyalar) {
    $tam = Join-Path $dist $d.yol
    [ordered]@{ ad = [IO.Path]::GetFileName($tam); hedef = $d.hedef; sha256 = (Get-FileHash $tam -Algorithm SHA256).Hash.ToLower(); boyut = (Get-Item $tam).Length }
}
$adlar = $liste | ForEach-Object { $_.ad }
if (($adlar | Select-Object -Unique).Count -ne $adlar.Count) { throw 'Aynı ada sahip iki dosya var; release dosya adları benzersiz olmalı.' }

$manifest = [ordered]@{ surum = $Surum; etiket = $etiket; tarih = (Get-Date -Format 'yyyy-MM-dd'); notlar = $Notlar; dosyalar = @($liste) }
$json = $manifest | ConvertTo-Json -Depth 5
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $dist 'manifest.json'), $json, $utf8)
[IO.File]::WriteAllText((Join-Path $dist 'app\surum.json'), $json, $utf8)

# İlk kurulum için tam paket
$zip = Join-Path $dist "SistemCheckup-$etiket.zip"
Compress-Archive -Path (Join-Path $dist 'SistemCheckup.exe'), (Join-Path $dist 'Guncelleyici.exe'), (Join-Path $dist 'app') -DestinationPath $zip -Force
Write-Host "Paket: $zip" -ForegroundColor Green

if ($Taslak) { Write-Host 'Taslak mod: hiçbir şey gönderilmedi.' -ForegroundColor Yellow; return }

# Değişiklik günlüğü
$gunluk = Join-Path $kok 'DEGISIKLIKLER.md'
$eski = if (Test-Path $gunluk) { [IO.File]::ReadAllText($gunluk, [Text.Encoding]::UTF8) -replace '^# Değişiklikler\s*', '' } else { '' }
$giris = "## $Surum — $($manifest.tarih)`n`n$(if ($Notlar) { $Notlar } else { '-' })`n`n"
[IO.File]::WriteAllText($gunluk, "# Değişiklikler`n`n$giris$eski", $utf8)

git add -A
git commit -m "Sürüm $Surum" -m $(if ($Notlar) { $Notlar } else { 'Yeni sürüm' }) | Out-Null
git tag -a $etiket -m "Sistem Checkup $Surum"
$dal = git rev-parse --abbrev-ref HEAD
git push origin $dal
git push origin $etiket

$varliklar = @($zip, (Join-Path $dist 'manifest.json')) + @($dosyalar | ForEach-Object { Join-Path $dist $_.yol })
$notMetni = "$(if ($Notlar) { $Notlar } else { 'Yeni sürüm.' })`n`n**İlk kurulum:** ``SistemCheckup-$etiket.zip`` dosyasını indirip bir klasöre çıkarın ve ``SistemCheckup.exe``'yi çalıştırın.`n**Güncelleme:** ``Guncelleyici.exe``'yi açın, değişen dosyalar otomatik indirilir."
gh release create $etiket @varliklar --repo $ayar.repo --title "Sistem Checkup $Surum" --notes $notMetni --latest
if ($LASTEXITCODE -ne 0) { throw 'GitHub Release oluşturulamadı.' }
Write-Host "Yayınlandı: https://github.com/$($ayar.repo)/releases/tag/$etiket" -ForegroundColor Green
