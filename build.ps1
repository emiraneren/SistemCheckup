# Exe'leri derler ve dist\SistemCheckup\ klasörünü (dağıtılacak paketin içeriği) hazırlar.
#   .\build.ps1 -Surum 1.1.0
# Ek araç gerekmez: Windows'ta hazır bulunan .NET Framework derleyicisi (csc.exe) kullanılır.
param([Parameter(Mandatory = $true)][string]$Surum)

$ErrorActionPreference = 'Stop'
$kok = $PSScriptRoot
$ayar = Get-Content -Raw -Encoding UTF8 (Join-Path $kok 'proje.json') | ConvertFrom-Json
[void][version]$Surum

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe bulunamadı (.NET Framework 4.x gerekli).' }

$obj = Join-Path $kok 'obj'
$dist = Join-Path $kok 'dist'
$paket = Join-Path $dist 'SistemCheckup'
Remove-Item -Recurse -Force $obj, $dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $obj, (Join-Path $paket 'app') | Out-Null
$bom = New-Object Text.UTF8Encoding($true)

# Repo adını ve sürüm bilgisini derlemeye göm
$ortak = (Get-Content -Raw -Encoding UTF8 (Join-Path $kok 'src\Ortak.cs')).Replace('__REPO__', $ayar.repo)
[IO.File]::WriteAllText((Join-Path $obj 'Ortak.cs'), $ortak, $bom)
$v = [version]$Surum
$v4 = "{0}.{1}.{2}.0" -f $v.Major, $v.Minor, [math]::Max(0, $v.Build)
$bilgi = @"
using System.Reflection;
[assembly: AssemblyProduct("Sistem Checkup")]
[assembly: AssemblyCompany("$($ayar.repo.Split('/')[0])")]
[assembly: AssemblyCopyright("MIT")]
[assembly: AssemblyVersion("$v4")]
[assembly: AssemblyFileVersion("$v4")]
[assembly: AssemblyInformationalVersion("$Surum")]
"@
[IO.File]::WriteAllText((Join-Path $obj 'SurumBilgi.cs'), $bilgi, $bom)

function Derle($cikti, $baslik, [string[]]$kaynaklar, [string[]]$referanslar) {
    $bas = Join-Path $obj ("Baslik_" + [IO.Path]::GetFileNameWithoutExtension($cikti) + '.cs')
    [IO.File]::WriteAllText($bas, "[assembly: System.Reflection.AssemblyTitle(`"$baslik`")]", $bom)
    $argumanlar = @('/nologo', '/optimize+', '/codepage:65001', '/target:winexe', '/platform:anycpu',
        "/out:$(Join-Path $paket $cikti)", "/win32icon:$(Join-Path $kok 'assets\ikon.ico')", "/win32manifest:$(Join-Path $kok 'src\uygulama.manifest')")
    $argumanlar += @('System.Web.Extensions.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll') + $referanslar | ForEach-Object { "/r:$_" }
    $argumanlar += $kaynaklar | ForEach-Object { Join-Path $kok "src\$_" }
    $argumanlar += @((Join-Path $obj 'Ortak.cs'), (Join-Path $obj 'SurumBilgi.cs'), $bas)
    & $csc $argumanlar
    if ($LASTEXITCODE -ne 0) { throw "$cikti derlenemedi" }
    Write-Host "  derlendi: $cikti" -ForegroundColor Green
}

Write-Host "Sistem Checkup $Surum derleniyor..." -ForegroundColor Cyan
Derle 'SistemCheckup.exe' 'Sistem Checkup' @('Arayuz.cs', 'Temizlik.cs') @()
Derle 'Guncelleyici.exe' 'Sistem Checkup Güncelleyici' @('Guncelleyici.cs') @('System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll')
Derle 'Kaldir.exe' 'Sistem Checkup Kaldırıcı' @('Kaldir.cs') @()

Copy-Item (Join-Path $kok 'app\*') (Join-Path $paket 'app') -Exclude 'Raporlar', 'surum.json'
Copy-Item (Join-Path $kok 'BENIOKU.txt') $paket
Remove-Item -Recurse -Force $obj
Write-Host "dist\SistemCheckup\ hazır." -ForegroundColor Green
