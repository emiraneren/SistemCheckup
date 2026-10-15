# Exe'leri derler ve dist\ klasörünü hazırlar.
#   .\build.ps1 -Surum 1.0.1
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
Remove-Item -Recurse -Force $obj, $dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $obj, (Join-Path $dist 'app') | Out-Null

# Repo adını ve sürüm bilgisini derlemeye göm
$ortak = (Get-Content -Raw -Encoding UTF8 (Join-Path $kok 'src\Ortak.cs')).Replace('__REPO__', $ayar.repo)
[IO.File]::WriteAllText((Join-Path $obj 'Ortak.cs'), $ortak, (New-Object Text.UTF8Encoding($true)))
$v4 = ([version]$Surum).ToString()
if (([version]$Surum).Build -lt 0) { $v4 = "$Surum.0" }
$bilgi = @"
using System.Reflection;
[assembly: AssemblyProduct("Sistem Checkup")]
[assembly: AssemblyCompany("$($ayar.repo.Split('/')[0])")]
[assembly: AssemblyCopyright("MIT")]
[assembly: AssemblyVersion("$v4")]
[assembly: AssemblyFileVersion("$v4")]
[assembly: AssemblyInformationalVersion("$Surum")]
"@
[IO.File]::WriteAllText((Join-Path $obj 'SurumBilgi.cs'), $bilgi, (New-Object Text.UTF8Encoding($true)))

function Derle($cikti, $hedef, $baslik, [string[]]$kaynaklar, [string[]]$referanslar) {
    $bas = Join-Path $obj ("Baslik_" + [IO.Path]::GetFileNameWithoutExtension($cikti) + '.cs')
    [IO.File]::WriteAllText($bas, "[assembly: System.Reflection.AssemblyTitle(`"$baslik`")]", (New-Object Text.UTF8Encoding($true)))
    $argumanlar = @('/nologo', '/optimize+', '/codepage:65001', "/target:$hedef", '/platform:anycpu',
        "/out:$(Join-Path $dist $cikti)", "/win32icon:$(Join-Path $kok 'assets\ikon.ico')", "/win32manifest:$(Join-Path $kok 'src\uygulama.manifest')")
    $argumanlar += $referanslar | ForEach-Object { "/r:$_" }
    $argumanlar += $kaynaklar
    $argumanlar += @((Join-Path $obj 'Ortak.cs'), (Join-Path $obj 'SurumBilgi.cs'), $bas)
    & $csc $argumanlar
    if ($LASTEXITCODE -ne 0) { throw "$cikti derlenemedi" }
    Write-Host "  derlendi: $cikti" -ForegroundColor Green
}

Write-Host "Sistem Checkup $Surum derleniyor..." -ForegroundColor Cyan
Derle 'SistemCheckup.exe' 'exe' 'Sistem Checkup' @((Join-Path $kok 'src\Baslatici.cs')) @('System.Web.Extensions.dll')
Derle 'Guncelleyici.exe' 'winexe' 'Sistem Checkup Güncelleyici' @((Join-Path $kok 'src\Guncelleyici.cs')) @('System.Web.Extensions.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll')

Copy-Item (Join-Path $kok 'app\*') (Join-Path $dist 'app') -Exclude 'Raporlar', 'surum.json'
Remove-Item -Recurse -Force $obj
Write-Host "dist\ hazır." -ForegroundColor Green
