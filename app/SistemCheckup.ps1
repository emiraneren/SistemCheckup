#requires -Version 5.1
<#
  Sistem Checkup — tarama motoru
  Donanım sağlığını kontrol eder, CPU / GPU / RAM / disk teknik detaylarını ve hız testlerini
  toplar, sürücülerin güncel olup olmadığına bakar ve sonuçları HTML rapor olarak kaydeder.
  Normalde SistemCheckup.exe tarafından çalıştırılır; tek başına da çalışabilir.

  Parametreler:
    -YonetimsizCalis   Yönetici izni istemeden çalışır
    -TestAtla          Hız testlerini (disk / CPU / RAM) atlar (hızlı tarama)
    -Sessiz            Bitince raporu açmaz ve Enter beklemez
    -RaporDizini       Raporların kaydedileceği klasör (varsayılan: betiğin yanındaki Raporlar)
    -IlerlemeDosyasi   İlerlemenin satır satır yazılacağı dosya (arayüz bunu okur)
    -IptalDosyasi      Bu dosya oluşursa tarama ilk uygun noktada durur
#>
param([switch]$YonetimsizCalis, [switch]$TestAtla, [switch]$Sessiz, [string]$RaporDizini, [string]$IlerlemeDosyasi, [string]$IptalDosyasi)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$KokDizin = $PSScriptRoot
$Baslangic = Get-Date
$UygulamaSurum = 'geliştirme'
try { $UygulamaSurum = (Get-Content -Raw -Encoding UTF8 (Join-Path $KokDizin 'surum.json') -ErrorAction Stop | ConvertFrom-Json).surum } catch {}
$Utf8 = New-Object Text.UTF8Encoding($false)

function Ilerleme($satir) {
    if ($IlerlemeDosyasi) { try { [IO.File]::AppendAllText($IlerlemeDosyasi, ($satir -replace "[\r\n]+", ' ') + "`n", $Utf8) } catch {} }
}
trap {
    Ilerleme "HATA|$($_.Exception.Message)"
    Write-Host "Beklenmeyen hata: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

function Test-Yonetici {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Yonetici) -and -not $YonetimsizCalis) {
    Write-Host "SSD aşınma / sıcaklık gibi ayrıntılar için yönetici izni isteniyor..." -ForegroundColor Yellow
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    if ($TestAtla) { $argList += '-TestAtla' }
    if ($Sessiz) { $argList += '-Sessiz' }
    if ($RaporDizini) { $argList += @('-RaporDizini', "`"$RaporDizini`"") }
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -ErrorAction Stop
        exit
    } catch {
        Write-Host "Yönetici izni verilmedi, sınırlı modda devam ediliyor." -ForegroundColor DarkYellow
    }
}
$Admin = Test-Yonetici

# ------------------------------------------------------------------ yardımcılar
$script:AdimNo = 0
$ToplamAdim = 13
function Iptal {
    if ($IptalDosyasi -and (Test-Path $IptalDosyasi)) {
        Ilerleme 'IPTAL'
        Write-Host 'Tarama iptal edildi.' -ForegroundColor Yellow
        exit 2
    }
}
function Adim($metin) {
    Iptal
    $script:AdimNo++
    Write-Host ("[{0,2}/{1}] {2}" -f $script:AdimNo, $ToplamAdim, $metin) -ForegroundColor Cyan
    Ilerleme "ADIM|$($script:AdimNo)|$ToplamAdim|$metin"
}
function Bilgi($metin) {
    Write-Host "        $metin" -ForegroundColor DarkGray
    Ilerleme "BILGI|$metin"
}

# Bulgu türü: 'donanim' = parçaların fiziksel durumu, 'bakim' = sürücü, güncelleme, ayar, disk alanı.
$BakimKategorileri = 'Sürücüler', 'Windows', 'Güvenlik', 'Sistem', 'Ağ', 'Anakart'
$Bulgular = New-Object System.Collections.ArrayList
function Bulgu {
    param($Seviye, $Kategori, $Baslik, $Detay, $Link = $null, $LinkMetin = $null, $Tur = $null, $Rozet = $null)
    if (-not $Tur) { $Tur = if ($BakimKategorileri -contains $Kategori) { 'bakim' } else { 'donanim' } }
    # Karşılaştırma için sayılardan arındırılmış sabit anahtar
    $kod = "$Kategori|" + ($Baslik -replace '[\d\.,%]+', '#')
    [void]$Bulgular.Add([ordered]@{ seviye = $Seviye; tur = $Tur; kategori = $Kategori; baslik = $Baslik; detay = $Detay; link = $Link; linkMetin = $LinkMetin; kod = $kod; rozet = $Rozet })
}

function Tarih($d) { if ($d) { try { return ([datetime]$d).ToString('yyyy-MM-dd') } catch {} } return $null }
function YasGun($d) { if ($d) { try { return [int]((Get-Date) - [datetime]$d).TotalDays } catch {} } return $null }
function Yuvarla($x, $b = 1) { if ($null -eq $x) { return $null } return [math]::Round([double]$x, $b) }
function GB($bayt, $b = 1) { if ($null -eq $bayt) { return $null } return [math]::Round([double]$bayt / 1GB, $b) }
function Ara($q) { 'https://www.google.com/search?q=' + [uri]::EscapeDataString($q) }
function Olaylar($filtre) { @(Get-WinEvent -FilterHashtable $filtre -ErrorAction SilentlyContinue) }

$WU_LINK = 'ms-settings:windowsupdate-optionalupdates'
$INTEL_DSA = 'https://www.intel.com/content/www/us/en/support/detect.html'
$INTEL_CHIPSET = 'https://www.intel.com/content/www/us/en/download/19347/chipset-inf-utility.html'
$AMD_LINK = 'https://www.amd.com/en/support/download/drivers.html'
$NVIDIA_LINK = 'https://www.nvidia.com/tr-tr/drivers/'

Write-Host ""
Write-Host "  ███ SİSTEM CHECKUP ███" -ForegroundColor Green
Write-Host ("  Mod: " + $(if ($Admin) { 'Yönetici (tam veri)' } else { 'Standart kullanıcı (sınırlı veri)' }) + $(if ($TestAtla) { ' · hızlı tarama' } else { ' · tam tarama' })) -ForegroundColor Gray
Write-Host ""
Ilerleme "BASLADI|$([int]$Admin)|$([int](-not $TestAtla))"

# ------------------------------------------------------------------ 1. sistem
Adim "Sistem ve anakart bilgileri"
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$board = Get-CimInstance Win32_BaseBoard
$bios = Get-CimInstance Win32_BIOS
$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'

$uptime = (Get-Date) - $os.LastBootUpTime
$anakartUretici = "$($board.Manufacturer)".Trim()
$anakartModel = "$($board.Product)".Trim()

function DestekLinki {
    $sistemUretici = "$($cs.Manufacturer)"
    $q = [uri]::EscapeDataString($anakartModel)
    if ($sistemUretici -match 'Dell') { return 'https://www.dell.com/support/home/' }
    if ($sistemUretici -match 'HP|Hewlett') { return 'https://support.hp.com/drivers' }
    if ($sistemUretici -match 'Lenovo') { return 'https://pcsupport.lenovo.com/' }
    if ($sistemUretici -match 'Acer') { return 'https://www.acer.com/support' }
    if ($sistemUretici -match 'Monster') { return 'https://www.monsternotebook.com.tr/destek/' }
    if ($sistemUretici -match 'Casper') { return 'https://www.casper.com.tr/destek' }
    if ($anakartUretici -match 'ASUS') { return "https://www.asus.com/supportonly/$q/helpdesk_download/" }
    if ($anakartUretici -match 'Micro-Star|MSI') { return "https://www.msi.com/search/$q" }
    if ($anakartUretici -match 'Gigabyte') { return "https://www.gigabyte.com/Search?kw=$q" }
    if ($anakartUretici -match 'ASRock') { return "https://www.asrock.com/search/index.asp?Keyword=$q" }
    return (Ara "$anakartUretici $anakartModel driver download")
}
$DESTEK = DestekLinki

$biosYas = YasGun $bios.ReleaseDate
$sonGuncelleme = Get-HotFix | Where-Object { $_.InstalledOn } | Sort-Object InstalledOn -Descending | Select-Object -First 1

$sistem = [ordered]@{
    bilgisayar       = $env:COMPUTERNAME
    isletimSistemi   = "$($os.Caption)".Trim()
    surum            = "$($cv.DisplayVersion)"
    derleme          = "$($os.BuildNumber).$($cv.UBR)"
    calismaSuresi    = ("{0} gün {1} saat {2} dk" -f $uptime.Days, $uptime.Hours, $uptime.Minutes)
    calismaGun       = [math]::Round($uptime.TotalDays, 1)
    sistemUretici    = "$($cs.Manufacturer)".Trim()
    sistemModel      = "$($cs.Model)".Trim()
    anakartUretici   = $anakartUretici
    anakartModel     = $anakartModel
    biosSurum        = "$($bios.SMBIOSBIOSVersion)"
    biosTarih        = Tarih $bios.ReleaseDate
    biosYasGun       = $biosYas
    destekLinki      = $DESTEK
    sonWinGuncelleme = if ($sonGuncelleme) { Tarih $sonGuncelleme.InstalledOn } else { $null }
    dizustu          = $false
}

if ($biosYas -gt 730) {
    Bulgu 'info' 'Anakart' "BIOS sürümü $([int]($biosYas/365)) yıllık" "Kurulu BIOS $($bios.SMBIOSBIOSVersion) ($($sistem.biosTarih)). Bilgisayarınız sorunsuz çalışıyorsa BIOS güncellemek şart değildir. Kararsızlık veya uyumluluk sorunu yaşıyorsanız üreticinin sayfasındaki yeni sürüme bakabilirsiniz; güncelleme sırasında elektrik kesilmemelidir." $DESTEK 'Destek sayfası'
} else {
    Bulgu 'ok' 'Anakart' "BIOS güncel görünüyor" "$anakartUretici $anakartModel, BIOS $($bios.SMBIOSBIOSVersion) ($($sistem.biosTarih))." $DESTEK 'Destek sayfası'
}
if ($uptime.TotalDays -gt 14) {
    Bulgu 'info' 'Sistem' "Bilgisayar $([int]$uptime.TotalDays) gündür yeniden başlatılmadı" "Bu bir arıza değildir. Ancak bazı Windows ve sürücü güncellemeleri ancak yeniden başlatınca uygulanır; ara sıra yeniden başlatmanız önerilir."
}
if ($sonGuncelleme) {
    $wuYas = YasGun $sonGuncelleme.InstalledOn
    if ($wuYas -gt 60) {
        Bulgu 'warn' 'Windows' "Windows $wuYas gündür güncellenmemiş" "Son yüklenen güncelleme: $($sonGuncelleme.HotFixID) ($($sistem.sonWinGuncelleme)). Güvenlik güncellemeleri için Windows Update'i çalıştırmanız önerilir." 'ms-settings:windowsupdate' "Windows Update'i aç"
    }
}

$mp = Get-MpComputerStatus
if ($mp) {
    $imzaYas = YasGun $mp.AntivirusSignatureLastUpdated
    $sistem.antivirus = [ordered]@{ acik = [bool]$mp.AntivirusEnabled; gercekZamanli = [bool]$mp.RealTimeProtectionEnabled; imzaTarihi = Tarih $mp.AntivirusSignatureLastUpdated }
    if (-not $mp.RealTimeProtectionEnabled) {
        Bulgu 'warn' 'Güvenlik' 'Windows Defender gerçek zamanlı koruma kapalı' 'Başka bir antivirüs programı kullanıyorsanız bu normaldir. Kullanmıyorsanız korumayı açmanız önerilir.' 'windowsdefender:' 'Windows Güvenliği'
    } elseif ($imzaYas -gt 7) {
        Bulgu 'warn' 'Güvenlik' "Antivirüs tanımları $imzaYas gündür güncellenmemiş" 'Windows Update çalıştırıldığında tanımlar kendiliğinden güncellenir.' 'windowsdefender:' 'Windows Güvenliği'
    }
}

# ------------------------------------------------------------------ 2. pil (dizüstü)
Adim "Pil ve güç durumu"
$kasa = @((Get-CimInstance Win32_SystemEnclosure).ChassisTypes)
$piller = @(Get-CimInstance Win32_Battery)
$sistem.dizustu = ($piller.Count -gt 0) -or [bool]($kasa | Where-Object { $_ -in 8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32 })
$pil = $null
if ($piller.Count) {
    $xmlYol = Join-Path $env:TEMP ("sc_pil_{0}.xml" -f $PID)
    & powercfg.exe /batteryreport /xml /output "$xmlYol" 2>$null | Out-Null
    $kayitlar = @()
    try {
        [xml]$rapor = [IO.File]::ReadAllText($xmlYol)
        foreach ($b in @($rapor.BatteryReport.Batteries.Battery)) {
            $tasarim = [double]$b.DesignCapacity; $dolu = [double]$b.FullChargeCapacity
            $kayitlar += [ordered]@{
                ad = ("$($b.Manufacturer) $($b.Id)").Trim(); kimya = "$($b.Chemistry)"
                tasarimMWh = [int]$tasarim; doluMWh = [int]$dolu
                saglikYuzde = if ($tasarim -gt 0) { [math]::Round([math]::Min(100, 100 * $dolu / $tasarim), 0) } else { $null }
                dongu = if ("$($b.CycleCount)" -match '^\d+$' -and [int]$b.CycleCount -gt 0) { [int]$b.CycleCount } else { $null }
            }
        }
    } catch {}
    Remove-Item $xmlYol -Force -ErrorAction SilentlyContinue
    $p0 = $piller[0]
    $sarjda = $p0.BatteryStatus -in 2, 6, 7, 8, 9
    $pil = [ordered]@{ sarjYuzde = [int]$p0.EstimatedChargeRemaining; prizde = $sarjda; piller = $kayitlar }
    foreach ($k in $kayitlar) {
        if ($null -eq $k.saglikYuzde) { continue }
        $dongu = if ($k.dongu) { ", $($k.dongu) şarj döngüsü" } else { '' }
        $ozet = "Pil şu an en fazla $([int]($k.doluMWh/1000)) Wh tutabiliyor; yeniyken $([int]($k.tasarimMWh/1000)) Wh tutuyordu (kapasitenin %$($k.saglikYuzde)'i kaldı$dongu)."
        if ($k.saglikYuzde -lt 60) {
            Bulgu 'bad' 'Pil' "Pil ciddi şekilde yıpranmış (%$($k.saglikYuzde) kapasite)" "$ozet Pil süresi belirgin şekilde kısalmıştır; pil değişimi düşünülebilir. Bu, bilgisayarın geri kalanının bozuk olduğu anlamına gelmez."
        } elseif ($k.saglikYuzde -lt 80) {
            Bulgu 'warn' 'Pil' "Pil yıpranmaya başlamış (%$($k.saglikYuzde) kapasite)" "$ozet Lityum piller kullanıldıkça kapasite kaybeder; bu normaldir. %60'ın altına inerse değişim düşünülebilir."
        } else {
            Bulgu 'ok' 'Pil' "Pil sağlıklı (%$($k.saglikYuzde) kapasite)" $ozet
        }
    }
    if (-not $sarjda -and -not $TestAtla) {
        Bulgu 'info' 'Pil' 'Tarama pil ile yapıldı' 'Dizüstü bilgisayarlar pil ile çalışırken güç tasarrufu yapar; hız testleri prize takılıyken daha yüksek çıkar.'
    }
    Bilgi ("Pil: şarj %{0}, {1}" -f $pil.sarjYuzde, $(if ($sarjda) { 'prizde' } else { 'pilde' }))
} else {
    Bilgi $(if ($sistem.dizustu) { 'Pil bulunamadı' } else { 'Masaüstü bilgisayar, pil yok' })
}

# ------------------------------------------------------------------ 3. işlemci
Adim "İşlemci bilgileri"
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$cpuBilgi = [ordered]@{
    ad            = "$($cpu.Name)".Trim()
    uretici       = "$($cpu.Manufacturer)"
    cekirdek      = [int]$cpu.NumberOfCores
    izlek         = [int]$cpu.NumberOfLogicalProcessors
    tabanMHz      = [int]$cpu.MaxClockSpeed
    yuk           = [int]$cpu.LoadPercentage
    soket         = "$($cpu.SocketDesignation)"
    l2MB          = Yuvarla ($cpu.L2CacheSize / 1024) 1
    l3MB          = Yuvarla ($cpu.L3CacheSize / 1024) 1
    sanallastirma = [bool]$cpu.VirtualizationFirmwareEnabled
    durum         = "$($cpu.Status)"
    test          = $null
}
if ($cpu.Status -and $cpu.Status -ne 'OK') {
    Bulgu 'bad' 'İşlemci' "Windows işlemcide sorun bildiriyor ($($cpu.Status))" 'Bu nadir görülen bir durumdur. Bilgisayarı yeniden başlatıp tekrar tarayın; devam ederse teknik servise danışın.'
}

# ------------------------------------------------------------------ 4. ekran kartı
Adim "Ekran kartı bilgileri"
$vramReg = @{}
Get-ItemProperty 'HKLM:\SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0*' | ForEach-Object {
    $m = $_.'HardwareInformation.qwMemorySize'
    if ($_.DriverDesc -and $m) { $vramReg["$($_.DriverDesc)"] = [double]$m }
}

$nvSmi = $null
$nvCmd = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
if ($nvCmd) {
    $satir = & $nvCmd.Source --query-gpu=name,driver_version,temperature.gpu,utilization.gpu,memory.total,memory.used,power.draw,power.limit,fan.speed,clocks.gr,clocks.max.gr,clocks.mem,pcie.link.gen.current,pcie.link.gen.max,pcie.link.width.current,pcie.link.width.max,vbios_version --format=csv,noheader,nounits 2>$null | Select-Object -First 1
    if ($satir) {
        $p = $satir -split ',\s*'
        function NvSayi($v) { $d = 0.0; if ([double]::TryParse("$v", [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$d)) { return $d } return $null }
        $nvSmi = [ordered]@{
            ad = $p[0]; surucu = $p[1]; sicaklik = NvSayi $p[2]; kullanim = NvSayi $p[3]
            vramToplamMB = NvSayi $p[4]; vramKullanilanMB = NvSayi $p[5]; guc = NvSayi $p[6]; gucLimit = NvSayi $p[7]
            fan = NvSayi $p[8]; saatMHz = NvSayi $p[9]; maxSaatMHz = NvSayi $p[10]; belSaatMHz = NvSayi $p[11]
            pcieGen = NvSayi $p[12]; pcieGenMax = NvSayi $p[13]; pcieGenislik = NvSayi $p[14]; pcieGenislikMax = NvSayi $p[15]; vbios = $p[16]
        }
    }
}

$gpuListe = @()
foreach ($g in @(Get-CimInstance Win32_VideoController)) {
    $ad = "$($g.Name)".Trim()
    $sanal = ($g.PNPDeviceID -notmatch '^PCI\\')
    $vram = if ($vramReg.ContainsKey($ad)) { $vramReg[$ad] } elseif ($g.AdapterRAM -gt 0) { [double]$g.AdapterRAM } else { $null }
    $uretici = if ($g.PNPDeviceID -match 'VEN_10DE') { 'NVIDIA' } elseif ($g.PNPDeviceID -match 'VEN_1002') { 'AMD' } elseif ($g.PNPDeviceID -match 'VEN_8086') { 'Intel' } else { "$($g.AdapterCompatibility)" }
    $gpu = [ordered]@{
        ad = $ad; uretici = $uretici; sanal = $sanal
        vramGB = GB $vram 1
        surucu = "$($g.DriverVersion)"; surucuTarih = Tarih $g.DriverDate; surucuYasGun = YasGun $g.DriverDate
        cozunurluk = if ($g.CurrentHorizontalResolution) { "$($g.CurrentHorizontalResolution) x $($g.CurrentVerticalResolution)" } else { $null }
        yenileme = $g.CurrentRefreshRate
        durum = "$($g.Status)"
        canli = $null
    }
    if ($uretici -eq 'NVIDIA' -and $nvSmi) { $gpu.canli = $nvSmi }
    if ($sanal) { $gpuListe += $gpu; continue }

    if ($ad -match 'Microsoft Basic Display|Temel Görüntü') {
        Bulgu 'bad' 'Ekran Kartı' 'Ekran kartı sürücüsü yüklü değil' "Windows geçici bir temel görüntü sürücüsü kullanıyor ($ad). Ekran kartınız bozuk değildir; sürücüsü kurulunca oyunlar ve video hızlandırma çalışır." $NVIDIA_LINK 'Sürücü indir' 'bakim'
    } elseif ($g.Status -ne 'OK') {
        Bulgu 'bad' 'Ekran Kartı' "Windows ekran kartında sorun bildiriyor ($($g.Status))" 'Önce sürücüyü yeniden kurmayı deneyin; sorun devam ederse kartın bağlantısı veya kendisi kontrol edilmelidir.'
    }
    if ($gpu.canli) {
        $c = $gpu.canli
        if ($c.sicaklik -ge 85) { Bulgu 'bad' 'Ekran Kartı' "Ekran kartı çok sıcak: $($c.sicaklik) °C" 'Hafif yükte bu sıcaklık normal değildir. Fanların döndüğünü, kart üzerinde toz birikmediğini ve kasanın hava aldığını kontrol edin.' }
        elseif ($c.sicaklik -ge 75 -and $c.kullanim -lt 30) { Bulgu 'warn' 'Ekran Kartı' "Ekran kartı düşük yükte sıcak: $($c.sicaklik) °C" "Kullanım yalnızca %$($c.kullanim) iken sıcaklık beklenenden yüksek. Toz birikimi veya fan ayarı kontrol edilebilir." }
        else { Bulgu 'ok' 'Ekran Kartı' "$ad sağlıklı çalışıyor" "Sıcaklık $($c.sicaklik) °C, kullanım %$($c.kullanim), güç $($c.guc) W / $($c.gucLimit) W." }
        if ($c.pcieGenislikMax -and $c.pcieGenislik -lt $c.pcieGenislikMax) {
            Bulgu 'warn' 'Ekran Kartı' "Ekran kartı x$($c.pcieGenislik) hızında bağlı (destekleyen: x$($c.pcieGenislikMax))" 'Kart yuvaya tam oturmamış, yanlış yuvaya takılmış ya da yuva başka bir aygıtla paylaşılıyor olabilir. Kart çalışır ama tam performans vermeyebilir.'
        }
    } elseif ($g.Status -eq 'OK') {
        Bulgu 'ok' 'Ekran Kartı' "$ad çalışıyor" 'Windows aygıtın normal çalıştığını bildiriyor. (Canlı sıcaklık şu an yalnızca NVIDIA kartlarda okunabiliyor.)'
    }
    $gpuListe += $gpu
}

# ------------------------------------------------------------------ 5. bellek
Adim "Bellek (RAM) bilgileri"
$moduller = @(Get-CimInstance Win32_PhysicalMemory)
$dizi = Get-CimInstance Win32_PhysicalMemoryArray | Select-Object -First 1
$tipAdi = @{ 20 = 'DDR'; 21 = 'DDR2'; 24 = 'DDR3'; 26 = 'DDR4'; 30 = 'LPDDR4'; 34 = 'DDR5'; 35 = 'LPDDR5' }

function NominalHiz($pn) {
    $pn = "$pn".Trim()
    if ($pn -match '(\d{4})[CJ]\d{2}') { return [int]$matches[1] }
    if ($pn -match '^KF\d(\d{2})C') { return [int]$matches[1] * 100 }
    return $null
}

$modListe = @()
$kanallar = @{}
foreach ($m in $moduller) {
    $loc = "$($m.DeviceLocator) $($m.BankLabel)"
    $anahtar = $null
    if ($loc -match 'Controller\s*(\d)') { $anahtar = "C$($matches[1])" }
    if ($loc -match 'Channel\s*([A-H])') { $anahtar = "$anahtar-$($matches[1])" }
    elseif ($loc -match 'DIMM_?([A-H])\d') { $anahtar = "$anahtar-$($matches[1])" }
    if ($anahtar) { $kanallar[$anahtar] = $true }
    $modListe += [ordered]@{
        yuva = "$($m.DeviceLocator)"; uretici = "$($m.Manufacturer)".Trim(); parcaNo = "$($m.PartNumber)".Trim()
        kapasiteGB = GB $m.Capacity 0; tip = $tipAdi[[int]$m.SMBIOSMemoryType]
        hizMHz = [int]$m.Speed; ayarliMHz = [int]$m.ConfiguredClockSpeed; nominalMHz = NominalHiz $m.PartNumber
    }
}
$toplamRam = ($moduller | Measure-Object Capacity -Sum).Sum
$kullanilanKB = $os.TotalVisibleMemorySize - $os.FreePhysicalMemory
$kullanimYuzde = [math]::Round(100.0 * $kullanilanKB / $os.TotalVisibleMemorySize, 0)
$lehimli = $sistem.dizustu -and ($modListe | Where-Object { $_.tip -match '^LP' })
$kanalDurum = if ($lehimli) { 'Anakarta lehimli' } elseif ($moduller.Count -le 1) { 'Tek kanal' } elseif ($kanallar.Count -ge 2) { 'Çift kanal' } elseif ($kanallar.Count -eq 1) { 'Tek kanal' } else { 'Belirlenemedi' }

$ram = [ordered]@{
    toplamGB = GB $toplamRam 0; kullanilanGB = Yuvarla ($kullanilanKB / 1MB) 1; kullanimYuzde = $kullanimYuzde
    yuvaSayisi = [int]$dizi.MemoryDevices; maksGB = if ($dizi.MaxCapacityEx) { [int]($dizi.MaxCapacityEx / 1MB) } else { $null }
    kanal = $kanalDurum; moduller = $modListe; bantGBs = $null; tanilama = $null
}

$ilk = $modListe | Select-Object -First 1
if ($ilk) {
    $ayarli = $ilk.ayarliMHz
    $hedef = @($ilk.nominalMHz, $ilk.hizMHz) | Where-Object { $_ } | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum
    if (-not $sistem.dizustu -and $hedef -and $ayarli -and $hedef -gt ($ayarli + 100)) {
        Bulgu 'warn' 'Bellek' "RAM $ayarli MHz'de çalışıyor, desteklediği hız $hedef MHz" "Bu bir arıza değil, bir ayardır. BIOS'ta XMP / EXPO / DOCP profili açılırsa bellek kutusunda yazan hızda çalışır ve performans artar. Ayarı açtıktan sonra sistem kararsızlaşırsa tekrar kapatabilirsiniz." $DESTEK 'Anakart kılavuzu' 'bakim'
    } else {
        Bulgu 'ok' 'Bellek' "RAM sorunsuz çalışıyor ($ayarli MHz)" "$($ram.toplamGB) GB $($ilk.tip), $($moduller.Count) modül, $kanalDurum."
    }
}
if ($kanalDurum -eq 'Tek kanal' -and $moduller.Count -ge 1 -and -not $sistem.dizustu) {
    Bulgu 'warn' 'Bellek' 'RAM tek kanal çalışıyor' 'Bu bir arıza değildir. Tek modül takılı ya da modüller yanlış yuvalarda. İki modülü anakart kılavuzunda önerilen yuvalara (genelde A2 + B2) takmak bellek hızını belirgin şekilde artırır.' $null $null 'bakim'
}
if ($kullanimYuzde -ge 90) {
    Bulgu 'warn' 'Bellek' "Bellek kullanımı çok yüksek (%$kullanimYuzde)" 'Şu an açık olan programlar belleğin neredeyse tamamını kullanıyor; bu yavaşlamaya yol açar. Görev Yöneticisi''nden en çok bellek kullanan programlara bakabilirsiniz.' $null $null 'bakim'
}

$md = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-MemoryDiagnostics-Results' } | Select-Object -First 1
if ($md) {
    $hata = ($md.Id -eq 1202)
    $ram.tanilama = [ordered]@{ tarih = Tarih $md.TimeCreated; hataVar = $hata }
    if ($hata) { Bulgu 'bad' 'Bellek' 'Windows Bellek Tanılama hata buldu' "Son test ($($ram.tanilama.tarih)) bellek hatası raporladı. Hangi modülün sorunlu olduğunu bulmak için modülleri tek tek MemTest86 ile test edin." 'https://www.memtest86.com/' 'MemTest86' }
}

# ------------------------------------------------------------------ 6. depolama
Adim "Disk sağlığı ve bölümler"
$fizikselDiskler = @(Get-PhysicalDisk | Sort-Object { [int]$_.DeviceId })
$diskDrive = @(Get-CimInstance Win32_DiskDrive)
$diskListe = @()

foreach ($pd in $fizikselDiskler) {
    $dd = $diskDrive | Where-Object { $_.Index -eq [int]$pd.DeviceId } | Select-Object -First 1
    $birimler = @()
    if ($dd) {
        foreach ($part in @(Get-CimAssociatedInstance -InputObject $dd -ResultClassName Win32_DiskPartition)) {
            foreach ($ld in @(Get-CimAssociatedInstance -InputObject $part -ResultClassName Win32_LogicalDisk)) {
                $birimler += [ordered]@{ harf = $ld.DeviceID; etiket = "$($ld.VolumeName)"; boyutGB = GB $ld.Size 0; bosGB = GB $ld.FreeSpace 0; dosyaSistemi = $ld.FileSystem
                    bosYuzde = if ($ld.Size) { [math]::Round(100.0 * $ld.FreeSpace / $ld.Size, 0) } else { $null } }
            }
        }
    }
    $rel = if ($Admin) { $pd | Get-StorageReliabilityCounter } else { $null }
    $tip = "$($pd.MediaType)"
    if ($tip -eq 'Unspecified' -and $pd.SpindleSpeed -eq 0) { $tip = 'SSD' }
    $disk = [ordered]@{
        no = [int]$pd.DeviceId; ad = "$($pd.FriendlyName)".Trim(); tip = $tip; arayuz = "$($pd.BusType)"
        boyutGB = GB $pd.Size 0; saglik = "$($pd.HealthStatus)"; durum = "$($pd.OperationalStatus)"
        firmware = "$($pd.FirmwareVersion)"
        sicaklik = if ($rel -and $rel.Temperature) { [int]$rel.Temperature } else { $null }
        maksSicaklik = if ($rel -and $rel.TemperatureMax) { [int]$rel.TemperatureMax } else { $null }
        asinma = if ($rel -and $null -ne $rel.Wear) { [int]$rel.Wear } else { $null }
        calismaSaati = if ($rel -and $rel.PowerOnHours) { [int]$rel.PowerOnHours } else { $null }
        okumaHatasi = if ($rel) { [int]$rel.ReadErrorsUncorrected } else { $null }
        yazmaHatasi = if ($rel) { [int]$rel.WriteErrorsUncorrected } else { $null }
        birimler = $birimler; test = $null
    }
    $diskListe += $disk

    $etiket = "$($disk.ad) ($($disk.arayuz))"
    if ($disk.saglik -ne 'Healthy') {
        Bulgu 'bad' 'Depolama' "$etiket arıza belirtisi gösteriyor ($($disk.saglik))" 'Diskin kendi sağlık kontrolü sorun bildiriyor. Önemli dosyalarınızı hemen başka bir yere yedekleyin ve diski değiştirmeyi planlayın.'
    } else {
        $ek = @()
        if ($null -ne $disk.asinma) { $ek += "ömrünün %$($disk.asinma)'i kullanılmış" }
        if ($disk.sicaklik) { $ek += "$($disk.sicaklik) °C" }
        if ($disk.calismaSaati) { $ek += "$($disk.calismaSaati) saat çalışmış" }
        $ekMetin = if ($ek.Count) { ' — ' + ($ek -join ', ') } else { '' }
        Bulgu 'ok' 'Depolama' "$etiket sağlıklı" "Diskin kendi sağlık kontrolü sorun bildirmiyor$ekMetin."
    }
    if ($null -ne $disk.asinma) {
        if ($disk.asinma -ge 90) { Bulgu 'bad' 'Depolama' "$($disk.ad) ömrünün sonuna yaklaştı (%$($disk.asinma))" 'SSD''nin yazma ömrü neredeyse tükenmiş. Şu an çalışıyor olsa da yakında değiştirmeyi planlayın ve düzenli yedek alın.' }
        elseif ($disk.asinma -ge 70) { Bulgu 'warn' 'Depolama' "$($disk.ad) ömrünün %$($disk.asinma)'ini kullanmış" 'Disk sağlıklı çalışıyor ama yıpranma ilerlemiş. Düzenli yedek almanız yeterli.' }
    }
    if ($disk.sicaklik -ge 70) { Bulgu 'warn' 'Depolama' "$($disk.ad) sıcak: $($disk.sicaklik) °C" 'Yüksek sıcaklık diski bozmaz ama kendini yavaşlatmasına yol açar. NVMe disklerde soğutucu (heatsink) takılı olup olmadığını kontrol edin.' }
    if ($disk.okumaHatasi -gt 0 -or $disk.yazmaHatasi -gt 0) { Bulgu 'bad' 'Depolama' "$($disk.ad) düzeltilemeyen okuma/yazma hatası bildirdi" "Okuma hatası: $($disk.okumaHatasi), yazma hatası: $($disk.yazmaHatasi). Bu, verilerin bozulabileceği anlamına gelir; önemli dosyalarınızı yedekleyin." }
    foreach ($b in $birimler) {
        if ($null -ne $b.bosYuzde) {
            if ($b.bosYuzde -lt 10) { Bulgu 'warn' 'Depolama' "$($b.harf) sürücüsünde çok az boş alan kaldı (%$($b.bosYuzde))" "Yalnızca $($b.bosGB) GB boş. Disk dolduğunda yavaşlar ve Windows güncellemeleri başarısız olabilir. Gereksiz dosyaları silebilir veya Depolama Algısı'nı kullanabilirsiniz." 'ms-settings:storagesense' 'Depolama ayarları' 'bakim' }
            elseif ($b.bosYuzde -lt 20) { Bulgu 'info' 'Depolama' "$($b.harf) sürücüsü dolmak üzere (%$($b.bosYuzde) boş)" "$($b.bosGB) GB boş alan kaldı. Şimdilik sorun yok; %10'un altına inmeden yer açmanız önerilir." 'ms-settings:storagesense' 'Depolama ayarları' 'bakim' }
        }
    }
}
if (-not $Admin) {
    Bulgu 'info' 'Depolama' 'SSD ömrü ve sıcaklığı ölçülemedi' 'Bu bilgiler yalnızca yönetici izniyle okunabiliyor. Bir sonraki taramada "Yönetici izniyle tara" seçeneğini açarsanız SSD ömrü, sıcaklık ve çalışma saati de gösterilir.'
}

# ------------------------------------------------------------------ hız testleri
$benchYuklu = $false
if (-not $TestAtla) {
    try {
        Add-Type -Path (Join-Path $KokDizin 'SCBench.cs') -ErrorAction Stop
        $benchYuklu = $true
    } catch {
        Bilgi "Test modülü yüklenemedi: $($_.Exception.Message)"
    }
}

Adim "Disk hız testleri (okuma / yazma)"
if ($benchYuklu) {
    foreach ($disk in $diskListe) {
        Iptal
        if ($disk.arayuz -eq 'USB') { Bilgi "$($disk.ad): USB disk, test atlandı"; continue }
        $hedef = $disk.birimler | Where-Object { $_.bosGB -ge 3 -and $_.dosyaSistemi -eq 'NTFS' } | Select-Object -First 1
        if (-not $hedef) { Bilgi "$($disk.ad): yeterli boş alan yok, test atlandı"; continue }
        $klasor = if ($hedef.harf -eq $env:SystemDrive) { Join-Path $env:TEMP 'SistemCheckup_tmp' } else { "$($hedef.harf)\SistemCheckup_tmp" }
        New-Item -ItemType Directory -Force -Path $klasor | Out-Null
        $dosya = Join-Path $klasor 'hiz_testi.tmp'
        Bilgi "$($disk.ad) ($($hedef.harf)) test ediliyor..."
        try {
            $boyutMB = 1024
            $sw = [SCBench]::SeqWrite($dosya, $boyutMB, 8192)
            $sr = [SCBench]::SeqRead($dosya, 8192)
            $boyut = [long]$boyutMB * 1MB
            $r1 = [SCBench]::Random4K($dosya, $false, $boyut, 1, 2500)
            $r8 = [SCBench]::Random4K($dosya, $false, $boyut, 8, 2500)
            $w1 = [SCBench]::Random4K($dosya, $true, $boyut, 1, 2500)
            $disk.test = [ordered]@{
                birim = $hedef.harf
                siraliOkuma = Yuvarla $sr 0; siraliYazma = Yuvarla $sw 0
                r4kOkumaQ1 = Yuvarla $r1[0] 1; r4kOkumaQ1Iops = Yuvarla $r1[1] 0
                r4kOkumaT8 = Yuvarla $r8[0] 1; r4kOkumaT8Iops = Yuvarla $r8[1] 0
                r4kYazmaQ1 = Yuvarla $w1[0] 1; r4kYazmaQ1Iops = Yuvarla $w1[1] 0
            }
            Bilgi ("   okuma {0:N0} MB/s · yazma {1:N0} MB/s" -f $sr, $sw)

            $beklenen = if ($disk.arayuz -eq 'NVMe') { 1000 } elseif ($disk.tip -eq 'SSD') { 350 } else { 80 }
            if ($sr -lt $beklenen) {
                Bulgu 'warn' 'Depolama' "$($disk.ad) beklenenden yavaş okudu ($([int]$sr) MB/s)" "Bu tip bir diskten en az ~$beklenen MB/s beklenir. Test sırasında arka planda büyük bir indirme/kopyalama varsa sonuç düşük çıkabilir; önce tekrar tarayın. Yine düşükse disk çok dolu, aşırı ısınmış ya da yanlış porta/yuvaya bağlı olabilir."
            }
        } catch {
            Bilgi "   Test başarısız: $($_.Exception.Message)"
        } finally {
            Remove-Item -Force $dosya -ErrorAction SilentlyContinue
            Remove-Item -Force $klasor -ErrorAction SilentlyContinue
        }
    }
} else { Bilgi 'Hızlı taramada atlandı' }

Adim "İşlemci ve bellek performans testi"
if ($benchYuklu) {
    Iptal
    Bilgi 'Tek çekirdek testi...'
    $tek = [SCBench]::SingleThread(3000)
    $sure = 10
    Bilgi "Tüm çekirdekler $sure saniye tam yükte (fanlar hızlanabilir)..."
    [SCBench]::StartMulti($cpuBilgi.izlek, $sure)
    $frekanslar = @()
    Start-Sleep -Milliseconds 700
    while (-not [SCBench]::CpuDone) {
        $pi = Get-CimInstance Win32_PerfFormattedData_Counters_ProcessorInformation -Filter "Name='_Total'"
        if ($pi -and $pi.ProcessorFrequency) { $frekanslar += [math]::Round($pi.ProcessorFrequency * $pi.PercentProcessorPerformance / 100.0, 0) }
        Start-Sleep -Milliseconds 800
    }
    $ornek = @([SCBench]::CpuSamples | ForEach-Object { [math]::Round($_, 1) })
    # Çok çekirdek puanı: arka plandaki programların anlık etkisini azaltmak için ortanca değer
    $sirali = @($ornek[1..($sure - 1)] | Sort-Object)
    $cok = $sirali[[int][math]::Floor($sirali.Count / 2)]
    # Isıl kısıtlama frekans düşüşüyle ölçülür; iş miktarı başka programlar yüzünden de dalgalanabilir
    if ($frekanslar.Count -ge 4) {
        $basta = ($frekanslar[0..1] | Measure-Object -Average).Average
        $sonda = ($frekanslar[($frekanslar.Count - 3)..($frekanslar.Count - 1)] | Measure-Object -Average).Average
    } else {
        $basta = ($ornek[1..2] | Measure-Object -Average).Average
        $sonda = ($ornek[($sure - 3)..($sure - 1)] | Measure-Object -Average).Average
    }
    $dusus = if ($basta -gt 0) { [math]::Round(100.0 * ($basta - $sonda) / $basta, 1) } else { 0 }
    $dusus = [math]::Max(0, $dusus)
    $atla = if ($frekanslar.Count -gt 2) { 1 } else { 0 }
    $efektif = if ($frekanslar.Count) { [int](($frekanslar | Select-Object -Skip $atla | Measure-Object -Average).Average) } else { $null }

    $cpuBilgi.test = [ordered]@{
        tekCekirdek = Yuvarla $tek 0; cokCekirdek = Yuvarla $cok 0
        olcekleme = if ($tek -gt 0) { Yuvarla ($cok / $tek) 1 } else { $null }
        dususYuzde = $dusus; yukFrekansMHz = $efektif; frekanslar = $frekanslar; ornekler = $ornek
    }
    Bilgi ("Tek çekirdek {0:N0} · çok çekirdek {1:N0} puan · yükte ~{2} MHz" -f $tek, $cok, $efektif)

    $esik = if ($sistem.dizustu) { 15 } else { 10 }
    if ($dusus -ge $esik) {
        Bulgu 'warn' 'İşlemci' "İşlemci tam yükte %$dusus yavaşladı (ısınma olabilir)" "10 saniyelik tam yükte işlemci kendini yavaşlattı; bu genelde soğutmanın yetersiz kaldığını gösterir. Fanların çalıştığını ve toz birikmediğini kontrol edin.$(if ($sistem.dizustu) { ' Dizüstü bilgisayarlarda bir miktar yavaşlama normaldir; bilgisayarı sert ve düz bir yüzeyde kullanın.' } else { ' Soğutucunun oturması ve termal macun da kontrol edilebilir.' })"
    } elseif ($efektif -and $efektif -lt ($cpuBilgi.tabanMHz * 0.9)) {
        Bulgu 'warn' 'İşlemci' "İşlemci yük altında taban hızının altında kaldı (~$efektif MHz)" "Taban hızı $($cpuBilgi.tabanMHz) MHz. En sık sebep güç modunun 'Güç tasarrufu'na ayarlı olmasıdır$(if ($sistem.dizustu) { ' veya bilgisayarın pille çalışmasıdır' }). Bu bir arıza değildir." 'ms-settings:powersleep' 'Güç ayarları' 'bakim'
    } else {
        Bulgu 'ok' 'İşlemci' 'İşlemci tam yükte kararlı' "Tam yükte ~$efektif MHz hızda çalıştı ve 10 saniye boyunca yavaşlamadı (düşüş %$dusus). Soğutma yeterli."
    }

    Bilgi 'Bellek hız testi...'
    try {
        $bant = [SCBench]::MemCopy(256, [math]::Min(8, $cpuBilgi.izlek), 6)
        $ram.bantGBs = Yuvarla $bant 1
        Bilgi ("Bellek kopyalama {0:N1} GB/s" -f $bant)
    } catch { Bilgi "Bellek testi başarısız: $($_.Exception.Message)" }
    [GC]::Collect()
} else { Bilgi 'Hızlı taramada atlandı' }

# ------------------------------------------------------------------ olay günlükleri
Adim "Son 30 günün hata kayıtları"
$ay = (Get-Date).AddDays(-30)
function OlayOzet($liste) {
    $l = @($liste | Sort-Object TimeCreated -Descending)
    [ordered]@{ adet = $l.Count; son = if ($l.Count) { $l[0].TimeCreated.ToString('yyyy-MM-dd HH:mm') } else { $null } }
}
$kp41 = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-Kernel-Power'; Id = 41; StartTime = $ay }
$bsod = Olaylar @{ LogName = 'System'; Id = 1001; ProviderName = 'Microsoft-Windows-WER-SystemErrorReporting'; StartTime = $ay }
$wheaHata = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-WHEA-Logger'; Level = 1, 2, 3; StartTime = $ay }
$wheaDuzeltilen = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-WHEA-Logger'; Level = 4; StartTime = $ay }
$diskHata = Olaylar @{ LogName = 'System'; ProviderName = 'disk', 'stornvme', 'storahci', 'Ntfs'; Level = 1, 2, 3; StartTime = $ay }
$gpuTdr = @(Olaylar @{ LogName = 'System'; ProviderName = 'Display'; Id = 4101; StartTime = $ay }) + @(Olaylar @{ LogName = 'System'; ProviderName = 'nvlddmkm'; Level = 1, 2; StartTime = $ay })
$uygCokme = Olaylar @{ LogName = 'Application'; ProviderName = 'Application Error'; Id = 1000; StartTime = $ay }

$enCokCoken = $uygCokme | ForEach-Object { if ($_.Properties.Count) { "$($_.Properties[0].Value)" } } | Group-Object | Sort-Object Count -Descending | Select-Object -First 5 | ForEach-Object { [ordered]@{ uygulama = $_.Name; adet = $_.Count } }

$olaylar = [ordered]@{
    beklenmeyenKapanma = OlayOzet $kp41
    maviEkran = OlayOzet $bsod
    donanimHatasi = OlayOzet $wheaHata
    duzeltilenDonanim = OlayOzet $wheaDuzeltilen
    diskHatasi = OlayOzet $diskHata
    ekranSurucusu = OlayOzet $gpuTdr
    uygulamaCokmesi = OlayOzet $uygCokme
    enCokCoken = @($enCokCoken)
}

$kapanma = @($kp41).Count
if ($bsod.Count -gt 0) {
    Bulgu 'bad' 'Kararlılık' "Son 30 günde $($bsod.Count) kez mavi ekran (BSOD) oluştu" "Son: $($olaylar.maviEkran.son). Mavi ekranın en sık sebepleri sürücüler, bellek ayarları (XMP) ve güç kaynağıdır; tek seferlik olduysa endişelenmeyin. Sık tekrarlıyorsa önce sürücüleri güncelleyin." 'https://learn.microsoft.com/tr-tr/troubleshoot/windows-client/performance/stop-error-or-blue-screen-error-troubleshooting' 'Sorun giderme rehberi'
}
if ($kapanma -gt 0) {
    $lvl = if ($kapanma -ge 3) { 'warn' } else { 'info' }
    Bulgu $lvl 'Kararlılık' "Son 30 günde $kapanma kez düzgün kapatılmadan kapandı" "Son: $($olaylar.beklenmeyenKapanma.son). Elektrik kesintisi, güç tuşuna basılı tutarak kapatma veya donma sonrası zorla kapatma bu kaydı oluşturur; bunları siz yaptıysanız sorun yoktur. Bilgisayar kendiliğinden kapanıyor ya da yeniden başlıyorsa güç kaynağı veya ısınma kontrol edilmelidir."
}
if ($wheaHata.Count -gt 0) {
    Bulgu 'bad' 'Kararlılık' "$($wheaHata.Count) donanım hatası (WHEA) kaydedildi" "Son: $($olaylar.donanimHatasi.son). İşlemci, bellek veya bir PCIe aygıtı düzeltilemeyen bir hata bildirdi. Hız aşırtma veya XMP açıksa kapatıp sorunun sürüp sürmediğine bakın."
} elseif ($wheaDuzeltilen.Count -gt 50) {
    Bulgu 'info' 'Kararlılık' "$($wheaDuzeltilen.Count) düzeltilmiş donanım uyarısı (WHEA)" 'Bunlar donanımın kendi düzelttiği küçük hatalardır ve genellikle PCIe güç tasarrufundan kaynaklanır; tek başına sorun değildir. Çok sık ise BIOS ve yonga seti güncellemesi önerilir.' $DESTEK 'Anakart destek sayfası'
}
if ($diskHata.Count -gt 0) {
    Bulgu 'warn' 'Depolama' "Son 30 günde $($diskHata.Count) disk/dosya sistemi uyarısı" "Son: $($olaylar.diskHatasi.son). USB bellek veya harici diskin çekilmesi de bu kaydı oluşturabilir. Dahili bir diskte tekrar ediyorsa kablo bağlantısını kontrol edin ve yedek alın."
}
if ($gpuTdr.Count -gt 0) {
    Bulgu 'warn' 'Ekran Kartı' "Ekran sürücüsü $($gpuTdr.Count) kez yanıt vermeyi durdurdu" "Son: $($olaylar.ekranSurucusu.son). Ekran kısa süre kararıp geri geldiyse bu kayıt oluşur. Önce ekran kartı sürücüsünü güncelleyin; devam ederse sıcaklık ve güç kaynağı kontrol edilmelidir." $NVIDIA_LINK 'Sürücü indir'
}
if (-not $bsod.Count -and -not $wheaHata.Count -and $kapanma -lt 3) {
    Bulgu 'ok' 'Kararlılık' 'Sistem kararlı çalışıyor' "Son 30 günde mavi ekran veya düzeltilemeyen donanım hatası yok."
}

# ------------------------------------------------------------------ aygıtlar
Adim "Aygıt Yöneticisi kontrolü"
$kodlar = @{
    1 = 'Aygıt doğru yapılandırılmamış'; 3 = 'Sürücü bozuk veya sistem kaynakları yetersiz'; 10 = 'Aygıt başlatılamıyor'
    12 = 'Yeterli boş kaynak bulunamıyor'; 14 = 'Yeniden başlatma gerekiyor'; 18 = 'Sürücüler yeniden yüklenmeli'
    19 = 'Kayıt defteri yapılandırması bozuk'; 21 = 'Aygıt kaldırılıyor'; 22 = 'Aygıt devre dışı bırakılmış'
    24 = 'Aygıt yok veya düzgün çalışmıyor'; 28 = 'Sürücü yüklü değil'; 29 = 'Aygıt BIOS tarafından devre dışı'
    31 = 'Sürücü yüklenemedi'; 32 = 'Sürücü hizmeti devre dışı'; 37 = 'Sürücü başlatılamadı'; 39 = 'Sürücü bozuk veya eksik'
    43 = 'Aygıt bir sorun bildirdi ve durduruldu'; 45 = 'Aygıt bağlı değil'; 48 = 'Sürücü uyumsuz olduğu için engellendi'; 52 = 'Sürücü imzası doğrulanamadı'
}
$mevcutAygitlar = @(Get-PnpDevice -PresentOnly)
$sorunlu = @()
foreach ($d in ($mevcutAygitlar | Where-Object { $_.Status -in 'Error', 'Degraded' })) {
    $kod = (Get-PnpDeviceProperty -InstanceId $d.InstanceId -KeyName DEVPKEY_Device_ProblemCode).Data
    if ($kod -eq 22) { continue }
    $id = "$($d.InstanceId)"
    $donanimId = if ($id -match '^(PCI\\VEN_[0-9A-F]{4}&DEV_[0-9A-F]{4})') { $matches[1] } elseif ($id -match '^(USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4})') { $matches[1] } elseif ($id -match '^(ACPI\\[A-Z0-9]+)') { $matches[1] } else { ($id -split '\\')[0..1] -join '\' }
    $ad = if ("$($d.FriendlyName)".Trim()) { "$($d.FriendlyName)".Trim() } else { "Bilinmeyen aygıt ($donanimId)" }
    $linkler = @()
    if ($id -match 'VEN_8086|ACPI\\INTC') {
        $linkler += [ordered]@{ m = 'Intel Yonga Seti Sürücüsü'; u = $INTEL_CHIPSET }
        $linkler += [ordered]@{ m = 'Intel Sürücü Asistanı'; u = $INTEL_DSA }
    } elseif ($id -match 'VEN_1022|ACPI\\AMDI') {
        $linkler += [ordered]@{ m = 'AMD Yonga Seti Sürücüsü'; u = $AMD_LINK }
    }
    $linkler += [ordered]@{ m = 'Anakart sürücüleri'; u = $DESTEK }
    $linkler += [ordered]@{ m = 'Donanım kimliğini ara'; u = (Ara "$donanimId driver") }
    $sorunlu += [ordered]@{ ad = $ad; sinif = "$($d.Class)"; kod = $kod; aciklama = if ($kodlar.ContainsKey([int]$kod)) { $kodlar[[int]$kod] } else { "Hata kodu $kod" }; donanimId = $donanimId; linkler = $linkler }
}
$eksikSurucu = @($sorunlu | Where-Object { $_.kod -eq 28 })
$arizali = @($sorunlu | Where-Object { $_.kod -ne 28 })
if ($eksikSurucu.Count) {
    $intelMi = @($eksikSurucu | Where-Object { $_.donanimId -match '8086|INTC' }).Count
    $detay = "Sürücüsü olmayan aygıtlar: " + (($eksikSurucu | ForEach-Object { $_.ad }) -join ', ') + '. Bu aygıtlar bozuk değildir; Windows onları tanıyamadığı için kullanmıyor. Bilgisayarınız çalışır, ama güç yönetimi gibi bazı özellikler tam verimli olmayabilir.'
    if ($intelMi -ge 2) { $detay += ' Bunlar Intel yonga seti parçaları: anakart üreticisinin sayfasından "Chipset" ve "Serial IO" sürücülerini kurmak genellikle hepsini çözer.' }
    Bulgu 'warn' 'Sürücüler' "$($eksikSurucu.Count) aygıtın sürücüsü kurulu değil" $detay $DESTEK 'Anakart sürücülerini indir'
}
if ($arizali.Count) {
    $detay = (($arizali | ForEach-Object { "$($_.ad): $($_.aciklama)" }) -join '; ') + '. Önce bilgisayarı yeniden başlatmayı ve aygıtın sürücüsünü yeniden kurmayı deneyin.'
    Bulgu 'bad' 'Aygıtlar' "$($arizali.Count) aygıt hata veriyor" $detay $DESTEK 'Sürücüleri indir'
}
if (-not $sorunlu.Count) {
    Bulgu 'ok' 'Sürücüler' "Aygıt Yöneticisi'nde sorunlu aygıt yok" 'Takılı tüm aygıtların sürücüsü yüklü ve çalışıyor.'
}

# ------------------------------------------------------------------ sürücüler
Adim "Sürücüler kontrol ediliyor"
$mevcutIds = @{}
foreach ($a in $mevcutAygitlar) { $mevcutIds["$($a.InstanceId)".ToUpper()] = $true }
$imzaliSuruculer = @(Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DeviceID -and $mevcutIds.ContainsKey("$($_.DeviceID)".ToUpper()) })

function SurucuLinki($saglayici, $ad) {
    $s = "$saglayici $ad"
    if ($s -match 'NVIDIA') { return $NVIDIA_LINK }
    if ($s -match 'AMD|Advanced Micro|Radeon') { return $AMD_LINK }
    if ($s -match 'Intel') { return $INTEL_DSA }
    if ($s -match 'Realtek|Killer|MediaTek|Qualcomm|Atheros|Broadcom|ASMedia|Aquantia|Marvell|Synaptics|ELAN') { return $DESTEK }
    if ($s -match 'Logitech') { return 'https://www.logitechg.com/tr-tr/innovation/g-hub.html' }
    if ($s -match 'SteelSeries') { return 'https://steelseries.com/gg' }
    if ($s -match 'Razer') { return 'https://www.razer.com/synapse-4' }
    if ($s -match 'Corsair') { return 'https://www.corsair.com/us/en/s/downloads' }
    if ($s -match 'TP-Link') { return 'https://www.tp-link.com/tr/support/download/' }
    if ($s -match 'Samsung') { return 'https://semiconductor.samsung.com/consumer-storage/support/tools/' }
    if ($s -match 'Crucial|Micron') { return 'https://www.crucial.com/support/storage-executive' }
    return (Ara "$saglayici $ad driver download")
}

Bilgi "Windows Update'te bekleyen sürücüler aranıyor (biraz sürebilir)..."
$wuListe = @()
$wuHata = $null
try {
    $arayici = (New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher()
    $sonuc = $arayici.Search("IsInstalled=0 and Type='Driver'")
    foreach ($u in $sonuc.Updates) {
        $wuListe += [ordered]@{ baslik = "$($u.Title)"; model = "$($u.DriverModel)"; sinif = "$($u.DriverClass)"; saglayici = "$($u.DriverProvider)"; tarih = Tarih $u.DriverVerDate
            kuruluSurum = $null; yeniSurum = $null; boyut = $null; neden = $null; fayda = $null }
    }
} catch { $wuHata = "$($_.Exception.Message)" }
Bilgi "$($wuListe.Count) sürücü güncellemesi bulundu"
Iptal

$nvidia = $null
$nvGpu = $gpuListe | Where-Object { $_.uretici -eq 'NVIDIA' -and -not $_.sanal } | Select-Object -First 1
if ($nvGpu) {
    $kurulu = if ($nvSmi) { $nvSmi.surucu } else {
        $rakam = ($nvGpu.surucu -replace '\.', ''); if ($rakam.Length -ge 5) { $r = $rakam.Substring($rakam.Length - 5); "$($r.Substring(0,3)).$($r.Substring(3))" } else { $null }
    }
    $nvidia = [ordered]@{ kurulu = $kurulu; enSon = $null; tarih = $null; detay = $NVIDIA_LINK; indir = $null; guncel = $null; boyut = $null }
    try {
        Bilgi 'NVIDIA sunucusundan en son sürücü sorgulanıyor...'
        # Masaüstü ve dizüstü GeForce sürücüleri aynı sürüm numarasıyla yayınlanır
        $url = 'https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=127&pfid=1041&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1'
        $j = Invoke-RestMethod -Uri $url -TimeoutSec 20 -UseBasicParsing
        $info = $j.IDS[0].downloadInfo
        if ($info.Version) {
            $nvidia.enSon = "$($info.Version)"
            $nvidia.tarih = [uri]::UnescapeDataString("$($info.ReleaseDateTime)")
            if ($nvGpu.ad -notmatch 'Laptop') {
                if ($info.DetailsURL) { $nvidia.detay = "$($info.DetailsURL)" }
                if ($info.DownloadURL) { $nvidia.indir = "$($info.DownloadURL)" }
            }
            $nvidia.guncel = ([double]::Parse($kurulu, [Globalization.CultureInfo]::InvariantCulture) -ge [double]::Parse($nvidia.enSon, [Globalization.CultureInfo]::InvariantCulture))
        }
    } catch { Bilgi "NVIDIA sorgusu başarısız: $($_.Exception.Message)" }

    if ($nvidia.guncel -eq $true) {
        Bulgu 'ok' 'Sürücüler' "NVIDIA sürücüsü güncel ($kurulu)" "En son Game Ready sürücüsü kurulu." $nvidia.detay 'Sürüm notları'
    } elseif ($nvidia.guncel -eq $false) {
        # NVIDIA sürüm numarasının ilk kısmı sürücü serisidir (617.14 ve 617.42 aynı seri): aynı seri = küçük yama
        $seriKurulu = "$kurulu".Split('.')[0]; $seriYeni = "$($nvidia.enSon)".Split('.')[0]
        $eskiSurucu = $nvGpu.surucuYasGun -gt 180
        $nvidia.boyut = if ($seriKurulu -eq $seriYeni -and -not $eskiSurucu) { 'kucuk' } else { 'buyuk' }
        if ($nvidia.boyut -eq 'kucuk') {
            Bulgu 'info' 'Sürücüler' "NVIDIA sürücüsü için küçük bir güncelleme var: $kurulu → $($nvidia.enSon)" "Aynı sürücü serisinin ($seriYeni) hata düzeltmeli yeni sürümü, $($nvidia.tarih) tarihinde yayınlandı.`nNe kazandırır: Son çıkan oyunlarda küçük iyileştirmeler ve hata düzeltmeleri. Şu an bir sorun yaşamıyorsanız acelesi yok." $nvidia.detay 'Sürücüyü indir' -Rozet 'Küçük güncelleme'
        } else {
            $neden = if ($eskiSurucu) { "Kurulu sürücünüz $([int]($nvGpu.surucuYasGun/30)) aylık." } else { "Yeni bir sürücü serisi ($seriYeni) çıktı." }
            Bulgu 'warn' 'Sürücüler' "NVIDIA sürücüsü için büyük bir güncelleme var: $kurulu → $($nvidia.enSon)" "$neden Yeni sürüm $($nvidia.tarih) tarihinde yayınlandı.`nNe kazandırır: Yeni oyunlarda daha iyi performans, yeni özellikler, hata ve güvenlik düzeltmeleri. Güncellemeniz önerilir; NVIDIA App ile ya da bağlantıdan kurabilirsiniz." $nvidia.detay 'Sürücüyü indir' -Rozet 'Büyük güncelleme'
        }
    }
}
foreach ($g in ($gpuListe | Where-Object { -not $_.sanal -and $_.uretici -in 'AMD', 'Intel' })) {
    $link = if ($g.uretici -eq 'AMD') { $AMD_LINK } else { $INTEL_DSA }
    if ($g.surucuYasGun -gt 365) {
        Bulgu 'warn' 'Sürücüler' "$($g.ad) sürücüsü $([int]($g.surucuYasGun/30)) aylık" "Kurulu sürüm $($g.surucu) ($($g.surucuTarih)). Ekran kartı sürücüleri yılda birkaç kez büyük güncelleme alır; bu kadar eski bir sürücü için büyük ihtimalle önemli bir güncelleme vardır.`nNe kazandırır: Oyun performansı, yeni oyunlarla uyumluluk ve hata düzeltmeleri.$(if ($sistem.dizustu) { ' Dizüstü bilgisayarlarda önce üreticinin destek sayfasına bakmanız önerilir.' })" $link 'Sürücüyü indir' -Rozet 'Büyük güncelleme olabilir'
    } elseif ($g.surucuYasGun -gt 180) {
        Bulgu 'info' 'Sürücüler' "$($g.ad) sürücüsü $([int]($g.surucuYasGun/30)) aylık" "Kurulu sürüm $($g.surucu) ($($g.surucuTarih)). Bu üretici için otomatik sürüm karşılaştırması yapılamıyor; büyük ihtimalle küçük güncellemeler çıkmıştır.`nNe kazandırır: Genellikle hata düzeltmeleri ve yeni oyunlar için küçük iyileştirmeler. Acil değil." $link 'Üretici sayfası' -Rozet 'Küçük güncelleme olabilir'
    } else {
        Bulgu 'info' 'Sürücüler' "$($g.ad) sürücüsü $($g.surucuTarih) tarihli" "Kurulu sürüm $($g.surucu). Bu üretici için otomatik sürüm karşılaştırması yapılamıyor; en sonunu üreticinin sayfasından kontrol edebilirsiniz." $link 'Üretici sayfası'
    }
}

function WuEslesir($model, $aygit) {
    if (-not $model -or -not $aygit) { return $false }
    $kelimeler = @("$model".ToLower() -split '[^a-z0-9\.\-]+' | Where-Object { $_.Length -gt 2 })
    if (-not $kelimeler.Count) { return $false }
    $a = "$aygit".ToLower()
    foreach ($k in $kelimeler) { if (-not $a.Contains($k)) { return $false } }
    return $true
}

# Güncellemenin kullanıcıya ne kazandıracağı (teknik olmayan dille)
function Fayda($w) {
    $m = "$($w.model) $($w.baslik)"
    if ($m -match 'Bluetooth') { return 'Bluetooth cihazlarla (kulaklık, kumanda) daha az bağlantı kopması ve daha iyi uyumluluk' }
    if ("$($w.sinif)" -match 'Network' -or $m -match 'Ethernet|Wi-?Fi|Wireless|WLAN|GbE|LAN') { return 'İnternet bağlantısında daha iyi hız ve kararlılık' }
    if ($m -match 'MONITOR|DisplayPort|HDMI' -and "$($w.sinif)" -notmatch 'Display') { return 'Monitör özelliklerinin (yenileme hızı, HDR, renk) doğru tanınması' }
    if ("$($w.sinif)" -match 'Display|Video') { return 'Oyun performansı ve görüntü hatalarının düzeltilmesi' }
    if ($m -match 'Audio|Sound|Voice|Microphone|Speaker') { return 'Ses ve mikrofon özelliklerinin düzgün çalışması' }
    if ($m -match 'Mouse|Keyboard|HID|Gamepad|Controller') { return 'Fare, klavye veya kumanda özelliklerinin düzgün çalışması' }
    if ("$($w.sinif)" -match 'Extension') { return "Üreticinin ayar programının ($($w.saglayici)) aygıtla uyumu" }
    if ($m -match 'INTEL|AMD|Chipset|PCI|SMBus|Serial IO|PEG') { return 'Yonga seti parçalarının doğru tanınması, güç yönetimi ve kararlılık' }
    if ("$($w.sinif)" -match 'Printer|Imaging|Camera') { return 'Yazıcı veya kamera uyumluluğu' }
    return 'Uyumluluk ve hata düzeltmeleri'
}

# Büyük güncelleme: Windows'un genel sürücüsünden üreticinin sürücüsüne geçiş, yeni bir ana sürüm
# veya 2 yıldan eski bir sürücünün yenilenmesi. Geri kalanlar küçük yamadır.
foreach ($w in $wuListe) {
    $yeniSurum = ([regex]::Matches($w.baslik, '\d+(\.\d+){2,3}') | Select-Object -Last 1).Value
    $es = $imzaliSuruculer | Where-Object { WuEslesir $w.model $_.DeviceName } | Select-Object -First 1
    $boyut = 'kucuk'; $neden = $null
    if ($es) {
        $w.kuruluSurum = "$($es.DriverVersion)"
        if ("$($es.DriverProviderName)" -match '^Microsoft' -and $w.saglayici -notmatch 'Microsoft') {
            $boyut = 'buyuk'; $neden = "şu an Windows'un genel sürücüsü kullanılıyor, üreticinin kendi sürücüsü kurulacak"
        } elseif ($yeniSurum -and $w.kuruluSurum -and $yeniSurum.Split('.')[0] -ne $w.kuruluSurum.Split('.')[0]) {
            $boyut = 'buyuk'; $neden = 'yeni bir ana sürüm'
        } elseif ($es.DriverDate -and $w.tarih -and ([datetime]$w.tarih - [datetime]$es.DriverDate).TotalDays -gt 730) {
            $boyut = 'buyuk'; $neden = 'kurulu sürücü 2 yıldan daha eski'
        }
    }
    $w.yeniSurum = $yeniSurum
    $w.boyut = $boyut
    $w.neden = $neden
    $w.fayda = Fayda $w
}

$siniflar = @{ 'DISPLAY' = 'Ekran kartı'; 'NET' = 'Ağ'; 'MEDIA' = 'Ses'; 'SYSTEM' = 'Yonga seti'; 'HDC' = 'Depolama'; 'SCSIADAPTER' = 'Depolama'; 'BLUETOOTH' = 'Bluetooth'; 'USB' = 'USB' }
$onemli = @()
foreach ($d in $imzaliSuruculer) {
    $sinif = "$($d.DeviceClass)".ToUpper()
    if (-not $siniflar.ContainsKey($sinif)) { continue }
    if ($d.DeviceID -notmatch '^(PCI|HDAUDIO|USB)\\') { continue }
    if ($sinif -eq 'USB' -and $d.DeviceID -notmatch '^PCI\\') { continue }
    if ($sinif -eq 'SYSTEM' -and "$($d.DeviceName)" -notmatch 'Management Engine|Serial IO|SMBus|GPIO|GNA|Dynamic Tuning|Chipset|PSP|SMBUS') { continue }
    $yas = YasGun $d.DriverDate
    $inf = ($d.DriverDate -and ([datetime]$d.DriverDate).Year -lt 2000)
    $ms = ("$($d.DriverProviderName)" -match '^Microsoft')
    $wuEslesme = $wuListe | Where-Object { WuEslesir $_.model $d.DeviceName } | Select-Object -First 1
    $durum = 'ok'; $durumMetin = 'Güncel görünüyor'
    if ($wuEslesme) { if ($wuEslesme.boyut -eq 'buyuk') { $durum = 'warn'; $durumMetin = 'Büyük güncelleme var' } else { $durum = 'info'; $durumMetin = 'Küçük güncelleme var' } }
    elseif ($sinif -eq 'DISPLAY' -and $nvidia -and $d.DeviceName -match 'NVIDIA') { if ($nvidia.guncel -eq $false) { if ($nvidia.boyut -eq 'buyuk') { $durum = 'warn'; $durumMetin = "Büyük güncelleme: $($nvidia.enSon)" } else { $durum = 'info'; $durumMetin = "Küçük güncelleme: $($nvidia.enSon)" } } elseif ($nvidia.guncel) { $durumMetin = 'En son sürüm' } }
    elseif ($ms) { $durum = 'info'; $durumMetin = 'Windows genel sürücüsü' }
    elseif ($inf) { $durum = 'info'; $durumMetin = 'Yalnızca tanımlama (INF)' }
    elseif ($yas -gt 1095) { $durum = 'warn'; $durumMetin = "$([int]($yas/365)) yıllık, güncellemesi olabilir" }
    elseif ($yas -gt 545) { $durum = 'info'; $durumMetin = 'Bir yıldan eski' }
    $link = if ($wuEslesme) { $WU_LINK } elseif ($sinif -eq 'DISPLAY' -and $nvidia) { $nvidia.detay } elseif ($ms) { $DESTEK } else { SurucuLinki $d.DriverProviderName $d.DeviceName }
    $onemli += [ordered]@{
        ad = "$($d.DeviceName)"; tur = $siniflar[$sinif]; saglayici = "$($d.DriverProviderName)"; surum = "$($d.DriverVersion)"
        tarih = Tarih $d.DriverDate; yasGun = $yas; durum = $durum; durumMetin = $durumMetin; link = $link
    }
}
$turSira = @{ 'Ekran kartı' = 0; 'Yonga seti' = 1; 'Depolama' = 2; 'Ağ' = 3; 'Ses' = 4; 'Bluetooth' = 5; 'USB' = 6 }
$onemli = @($onemli | Sort-Object { $turSira[$_.tur] }, ad)

$eskiler = @()
foreach ($d in $imzaliSuruculer) {
    if ("$($d.DriverProviderName)" -match '^Microsoft' -or -not $d.DriverDate) { continue }
    if (([datetime]$d.DriverDate).Year -lt 2000) { continue }
    $yas = YasGun $d.DriverDate
    if ($yas -gt 1095) {
        $eskiler += [ordered]@{ ad = "$($d.DeviceName)"; saglayici = "$($d.DriverProviderName)"; surum = "$($d.DriverVersion)"; tarih = Tarih $d.DriverDate; yasGun = $yas; link = SurucuLinki $d.DriverProviderName $d.DeviceName }
    }
}
$eskiler = @($eskiler | Sort-Object yasGun -Descending | Group-Object ad | ForEach-Object { $_.Group[0] })

if ($wuListe.Count) {
    $nasil = "`nYüklemek için: Ayarlar → Windows Update → Gelişmiş seçenekler → İsteğe bağlı güncelleştirmeler."
    $satir = { param($w) "• $($w.model): $($w.fayda)" + $(if ($w.neden) { " ($($w.neden))" } else { '' }) }
    $buyukler = @($wuListe | Where-Object { $_.boyut -eq 'buyuk' })
    $kucukler = @($wuListe | Where-Object { $_.boyut -ne 'buyuk' })
    if ($buyukler.Count) {
        Bulgu 'warn' 'Sürücüler' "Windows Update'te $($buyukler.Count) önemli sürücü güncellemesi var" ("Yüklemeniz önerilir. Ne kazandırır:`n" + (($buyukler | ForEach-Object { & $satir $_ }) -join "`n") + $nasil) $WU_LINK 'İsteğe bağlı güncellemeleri aç' -Rozet 'Büyük güncelleme'
    }
    if ($kucukler.Count) {
        Bulgu 'info' 'Sürücüler' "Windows Update'te $($kucukler.Count) küçük sürücü güncellemesi var" ("Küçük iyileştirmelerdir, acil değildir. Ne kazandırır:`n" + (($kucukler | ForEach-Object { & $satir $_ }) -join "`n") + $nasil) $WU_LINK 'İsteğe bağlı güncellemeleri aç' -Rozet 'Küçük güncelleme'
    }
} elseif (-not $wuHata) {
    Bulgu 'ok' 'Sürücüler' "Windows Update'te bekleyen sürücü yok" 'Microsoft kataloğundaki tüm sürücüler güncel.'
}
if ($eskiler.Count) {
    $ornekler = ($eskiler | Select-Object -First 4 | ForEach-Object { "$($_.ad) ($($_.tarih))" }) -join ', '
    Bulgu 'info' 'Sürücüler' "$($eskiler.Count) sürücü 3 yıldan eski" "$ornekler. Bu genellikle sorun değildir; bazı aygıtlar için yeni sürüm hiç çıkmaz. Aygıt düzgün çalışıyorsa dokunmanıza gerek yok."
}

$suruculer = [ordered]@{ windowsUpdate = $wuListe; wuHata = $wuHata; nvidia = $nvidia; onemli = $onemli; eski = $eskiler; sorunluAygitlar = $sorunlu; toplam = $imzaliSuruculer.Count }

# ------------------------------------------------------------------ bağlı cihazlar
Adim "Bağlı cihazlar (monitör, klavye, fare, ses)"
function Ozellik($id, $anahtar) { try { (Get-PnpDeviceProperty -InstanceId $id -KeyName $anahtar -ErrorAction Stop).Data } catch { $null } }
$VidUretici = @{ '1038' = 'SteelSeries'; '046D' = 'Logitech'; '045E' = 'Microsoft'; '0951' = 'HyperX'; '03F0' = 'HP'; '1532' = 'Razer'; '1B1C' = 'Corsair'
    '3434' = 'Keychron'; '0B05' = 'ASUS'; '1E7D' = 'ROCCAT'; '2516' = 'Cooler Master'; '24AE' = 'Rapoo'; '413C' = 'Dell'; '17EF' = 'Lenovo'; '05AC' = 'Apple'
    '054C' = 'Sony'; '057E' = 'Nintendo'; '28DE' = 'Valve'; '2DC8' = '8BitDo'; '1462' = 'MSI'; '1044' = 'Gigabyte'; '0D8C' = 'C-Media'; '3554' = 'Pulsar'; '361D' = 'Lamzu'; '373E' = 'Endgame Gear' }
$PnpUretici = @{ GSM = 'LG'; SAM = 'Samsung'; SEC = 'Samsung'; DEL = 'Dell'; AUS = 'ASUS'; ACR = 'Acer'; AOC = 'AOC'; BNQ = 'BenQ'; MSI = 'MSI'; HWP = 'HP'; HPN = 'HP'
    LEN = 'Lenovo'; PHL = 'Philips'; VSC = 'ViewSonic'; GBT = 'Gigabyte'; HKC = 'HKC'; XMI = 'Xiaomi'; SNY = 'Sony'; BOE = 'BOE'; AUO = 'AU Optronics'
    LGD = 'LG Display'; SHP = 'Sharp'; CMN = 'Innolux'; IVO = 'InfoVision'; SDC = 'Samsung Display'; APP = 'Apple'; HSD = 'HannStar'; MZI = 'Monster' }
$cihazlar = [ordered]@{ monitorler = @(); klavyeler = @(); fareler = @(); digerGiris = @(); kumandalar = @(); sesCikis = @(); sesGiris = @(); kameralar = @(); bluetooth = @(); yazicilar = @() }

# Monitörler: model adı ve boyut EDID'den (WMI), çözünürlük ve yenileme hızı Windows ekran ayarlarından
$wmiMon = @{}
foreach ($m in @(Get-CimInstance -Namespace root\wmi WmiMonitorID)) {
    $kod = ("$($m.InstanceName)" -split '\\')[1]
    $wmiMon[$kod] = [ordered]@{ ad = (-join (@($m.UserFriendlyName) | Where-Object { $_ } | ForEach-Object { [char]$_ })).Trim(); uretici = (-join (@($m.ManufacturerName) | Where-Object { $_ } | ForEach-Object { [char]$_ })).Trim(); yil = [int]$m.YearOfManufacture; inc = $null }
}
foreach ($b in @(Get-CimInstance -Namespace root\wmi WmiMonitorBasicDisplayParams)) {
    $kod = ("$($b.InstanceName)" -split '\\')[1]
    if ($wmiMon.ContainsKey($kod) -and $b.MaxHorizontalImageSize -gt 0) { $wmiMon[$kod].inc = [math]::Round([math]::Sqrt([math]::Pow($b.MaxHorizontalImageSize, 2) + [math]::Pow($b.MaxVerticalImageSize, 2)) / 2.54, 0) }
}
$ekranlar = @()
try { Add-Type -Path (Join-Path $KokDizin 'SCEkran.cs') -ErrorAction Stop; $ekranlar = @([SCEkran]::Listele()) } catch { Bilgi "Ekran bilgisi okunamadı: $($_.Exception.Message)" }
foreach ($s in $ekranlar) {
    $p = $s -split '\|'
    $kod = ("$($p[3])" -split '\\')[1]
    $w = if ($kod -and $wmiMon.ContainsKey($kod)) { $wmiMon[$kod] } else { $null }
    $ad = if ($w -and $w.ad) { $w.ad } else { $p[2] }
    $uretici = if ($w -and $PnpUretici.ContainsKey($w.uretici)) { $PnpUretici[$w.uretici] } elseif ($w) { $w.uretici } else { $null }
    $tamAd = if ($uretici -and $ad -notmatch [regex]::Escape($uretici)) { "$uretici $ad" } else { $ad }
    $hz = [int]$p[6]; $maksHz = [int]$p[9]
    $cihazlar.monitorler += [ordered]@{ ad = $tamAd; boyutInc = $(if ($w) { $w.inc } else { $null }); uretimYili = $(if ($w -and $w.yil -gt 1990) { $w.yil } else { $null })
        cozunurluk = "$($p[4]) x $($p[5])"; hz = $hz; maksHz = $maksHz; enYuksekCozunurluk = "$($p[10]) x $($p[11])"; renkBit = [int]$p[7]; birincil = ($p[8] -eq '1'); ekranKarti = $p[1] }
    if ($maksHz -gt $hz + 5) {
        Bulgu 'warn' 'Ekran' "$tamAd $hz Hz'de çalışıyor, $maksHz Hz destekliyor" "Bu bir arıza değil, bir ayardır: monitörünüz daha akıcı bir görüntü verebiliyor ama Windows düşük hızda kullanıyor.`nNe kazandırır: Oyunlarda ve fare hareketinde belirgin akıcılık.`nNasıl: Ayarlar → Sistem → Ekran → Gelişmiş ekran → Yenileme hızı → $maksHz Hz. Seçenek görünmüyorsa kablo sınırlıyor olabilir (DisplayPort veya HDMI 2.0 kablo kullanın)." 'ms-settings:display-advanced' 'Ekran ayarlarını aç' -Tur 'bakim' -Rozet 'Ayar'
    } elseif ($hz -gt 0) {
        Bulgu 'ok' 'Ekran' "$tamAd en yüksek yenileme hızında ($hz Hz)" "$($p[4]) x $($p[5]) çözünürlükte $hz Hz."
    }
}

# Klavye ve fare: her fiziksel cihaz bir kez (bir klavye makro tuşları için 'fare' olarak da görünebilir)
$fiziksel = [ordered]@{}
foreach ($d in @(Get-PnpDevice -PresentOnly -Class Keyboard, Mouse | Where-Object { $_.Status -eq 'OK' })) {
    $id = $d.InstanceId; $kok = $null; $ad = $null; $baglanti = $null; $vid = $null
    for ($i = 0; $i -lt 6 -and $id; $i++) {
        if ($id -match '^(ROOT|LGHUBDEVICE|SWD|HTREE)\\') { $kok = $null; break }   # sanal aygıt
        if ($id -match '^USB\\VID_([0-9A-F]{4})&PID_[0-9A-F]{4}') {
            $vid = $matches[1]; $br = Ozellik $id 'DEVPKEY_Device_BusReportedDeviceDesc'
            if ($br) { $ad = "$br".Trim() }
            if ($id -notmatch '&MI_') { $kok = $id; $baglanti = 'USB'; break }      # bileşik cihazın kendisi
        }
        if ($id -match '^(BTHLE|BTHENUM)\\DEV_|^BTHENUM\\.*_DEV_') { $kok = $id; $baglanti = 'Bluetooth'; $ad = (Get-PnpDevice -InstanceId $id).FriendlyName; break }
        if ($id -match '^BTHLEDEVICE\\.*VID&02([0-9A-F]{4})') { $vid = $matches[1] }
        if ($id -match '^ACPI\\') { $kok = "dahili-$($d.Class)"; $baglanti = 'Dahili'; $ad = if ($d.Class -eq 'Keyboard') { 'Dahili klavye' } else { 'Dahili dokunmatik yüzey' }; break }
        $id = Ozellik $id 'DEVPKEY_Device_Parent'
    }
    if (-not $kok) { continue }
    if (-not $fiziksel.Contains($kok)) { $fiziksel[$kok] = [ordered]@{ ad = $ad; vid = $vid; baglanti = $baglanti; siniflar = @() } }
    if ($ad -and -not $fiziksel[$kok].ad) { $fiziksel[$kok].ad = $ad }
    $fiziksel[$kok].siniflar += "$($d.Class)"
}
$klavyeAdi = 'Keyboard|Klavye|Apex|Alloy|BlackWidow|Huntsman|Keychron|Vulcan|Ornata|Cynosa|Strafe|\bK\d{2,3}\b|TKL'
$fareAdi = 'Mouse|Fare|DeathAdder|Viper|Basilisk|Rival|Aerox|Prime|Pulsefire|Model [ODI]\b|MX Master|MX Anywhere|Superlight|Dokunmatik|Touchpad|\bG\d{3}\b|G Pro'
foreach ($f in $fiziksel.Values) {
    $uretici = if ($f.vid -and $VidUretici.ContainsKey($f.vid)) { $VidUretici[$f.vid] } else { $null }
    $ad = if ($f.ad) { $f.ad } else { 'Bilinmeyen cihaz' }
    if ($uretici -and $ad -notmatch [regex]::Escape($uretici)) { $ad = "$uretici $ad" }
    $kayit = [ordered]@{ ad = $ad; baglanti = $f.baglanti }
    $k = $f.siniflar -contains 'Keyboard'; $m = $f.siniflar -contains 'Mouse'
    if ($ad -match $klavyeAdi) { $cihazlar.klavyeler += $kayit }
    elseif ($ad -match $fareAdi) { $cihazlar.fareler += $kayit }
    elseif ($k -and -not $m) { $cihazlar.klavyeler += $kayit }
    elseif ($m -and -not $k) { $cihazlar.fareler += $kayit }
    else { $cihazlar.digerGiris += $kayit }
}

# Bluetooth cihazları ve oyun kumandaları
foreach ($b in @(Get-PnpDevice -PresentOnly -Class Bluetooth | Where-Object { $_.Status -eq 'OK' -and $_.InstanceId -match '^(BTHENUM|BTHLE)\\DEV_' })) {
    if ($b.FriendlyName) { $cihazlar.bluetooth += [ordered]@{ ad = "$($b.FriendlyName)" } }
}
$kumandaAdi = 'Controller|Gamepad|Kumanda|DualSense|DualShock|Joy-Con|XINPUT'
$kumandaAdlari = @{}
foreach ($c in @(Get-PnpDevice -PresentOnly | Where-Object { $_.Status -eq 'OK' -and ($_.Class -in 'XboxComposite', 'XnaComposite' -or ($_.Class -eq 'HIDClass' -and $_.FriendlyName -match $kumandaAdi)) })) {
    $ad = "$($c.FriendlyName)"; $id = $c.InstanceId; $bag = 'USB'
    for ($i = 0; $i -lt 5 -and $id; $i++) {
        if ($id -match '^(BTHLE|BTHENUM)\\DEV_') { $ad = (Get-PnpDevice -InstanceId $id).FriendlyName; $bag = 'Bluetooth'; break }
        if ($id -match '^USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}\\' -and $id -notmatch '&MI_') { $br = Ozellik $id 'DEVPKEY_Device_BusReportedDeviceDesc'; if ($br) { $ad = "$br" }; break }
        $id = Ozellik $id 'DEVPKEY_Device_Parent'
    }
    if (-not $kumandaAdlari.ContainsKey($ad)) { $kumandaAdlari[$ad] = $true; $cihazlar.kumandalar += [ordered]@{ ad = $ad; baglanti = $bag } }
}
foreach ($b in $cihazlar.bluetooth) { if ($b.ad -match $kumandaAdi -and -not $kumandaAdlari.ContainsKey($b.ad)) { $kumandaAdlari[$b.ad] = $true; $cihazlar.kumandalar += [ordered]@{ ad = $b.ad; baglanti = 'Bluetooth' } } }

# Ses, kamera, yazıcı
foreach ($a in @(Get-PnpDevice -PresentOnly -Class AudioEndpoint | Where-Object { $_.Status -eq 'OK' } | Sort-Object FriendlyName)) {
    $kayit = [ordered]@{ ad = "$($a.FriendlyName)" }
    if ($a.InstanceId -match '\{0\.0\.1\.') { $cihazlar.sesGiris += $kayit } else { $cihazlar.sesCikis += $kayit }
}
foreach ($k in @(Get-PnpDevice -PresentOnly -Class Camera, Image | Where-Object { $_.Status -eq 'OK' })) {
    $cihazlar.kameralar += [ordered]@{ ad = "$($k.FriendlyName)"; sanal = ($k.InstanceId -match '^(ROOT|SWD)\\') }
}
foreach ($y in @(Get-CimInstance Win32_Printer | Where-Object { $_.Name -notmatch 'PDF|XPS|OneNote|Fax|Send To|Microsoft Print' })) {
    $cihazlar.yazicilar += [ordered]@{ ad = "$($y.Name)"; varsayilan = [bool]$y.Default }
}
Bilgi ("{0} monitör, {1} klavye, {2} fare, {3} ses cihazı" -f $cihazlar.monitorler.Count, $cihazlar.klavyeler.Count, $cihazlar.fareler.Count, ($cihazlar.sesCikis.Count + $cihazlar.sesGiris.Count))

# ------------------------------------------------------------------ rapor
Adim "Sonuçlar hazırlanıyor"
$ag = @(Get-NetAdapter -Physical | ForEach-Object { [ordered]@{ ad = "$($_.Name)"; aciklama = "$($_.InterfaceDescription)"; durum = "$($_.Status)"; hiz = "$($_.LinkSpeed)"; surucu = "$($_.DriverVersion)" } })
foreach ($n in (Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -match '2\.5G|5G|10G' -and $_.LinkSpeed -match '^(100 Mbps|1 Gbps)' })) {
    Bulgu 'info' 'Ağ' "Ağ kartı $($n.LinkSpeed) hızında bağlı" "$($n.InterfaceDescription) daha yüksek hızı destekliyor. Modeminiz veya switch'iniz 1 Gbps ise bu normaldir ve internet hızınızı etkilemez."
}

$termal = @()
foreach ($t in @(Get-CimInstance Win32_PerfFormattedData_Counters_ThermalZoneInformation)) {
    $k = if ($t.HighPrecisionTemperature) { $t.HighPrecisionTemperature / 10.0 } else { [double]$t.Temperature }
    if ($k -gt 200) { $termal += [ordered]@{ ad = "Anakart ($($t.Name -replace '\\_TZ\.', ''))"; sicaklik = [math]::Round($k - 273.15, 0) } }
}
if ($nvSmi) { $termal += [ordered]@{ ad = 'Ekran kartı'; sicaklik = $nvSmi.sicaklik } }
foreach ($d in $diskListe) { if ($d.sicaklik) { $termal += [ordered]@{ ad = $d.ad; sicaklik = $d.sicaklik } } }

# İki ayrı puan: donanım arızası ile bakım işleri karıştırılmasın
function PuanHesapla($tur, $kotu, $uyari) {
    $p = 100
    foreach ($b in $Bulgular) { if ($b.tur -eq $tur) { if ($b.seviye -eq 'bad') { $p -= $kotu } elseif ($b.seviye -eq 'warn') { $p -= $uyari } } }
    return [math]::Max(0, $p)
}
$puan = [ordered]@{ donanim = PuanHesapla 'donanim' 20 7; bakim = PuanHesapla 'bakim' 15 6 }

# Önceki taramayla karşılaştırma
$raporDizin = if ($RaporDizini) { $RaporDizini } else { Join-Path $KokDizin 'Raporlar' }
$gecmisDizin = Join-Path $raporDizin 'gecmis'
New-Item -ItemType Directory -Force -Path $gecmisDizin | Out-Null
$karsilastirma = $null
$onceki = $null
$oncekiDosya = Get-ChildItem $gecmisDizin -Filter '*.json' | Sort-Object Name -Descending | Select-Object -First 1
if ($oncekiDosya) { try { $onceki = [IO.File]::ReadAllText($oncekiDosya.FullName, [Text.Encoding]::UTF8) | ConvertFrom-Json } catch {} }
if ($onceki -and $onceki.puan -and $onceki.puan.donanim -ne $null) {
    $degisim = @()
    function Degisim($ad, $once, $simdi, $birim, $yuksekIyi, $esikYuzde) {
        if ($null -eq $once -or $null -eq $simdi -or [double]$once -eq 0) { return }
        $fark = 100.0 * ([double]$simdi - [double]$once) / [double]$once
        if ([math]::Abs($fark) -lt $esikYuzde) { $yon = 'ayni' } elseif (($fark -gt 0) -eq $yuksekIyi) { $yon = 'iyi' } else { $yon = 'kotu' }
        $script:degisim += [ordered]@{ ad = $ad; once = $once; simdi = $simdi; birim = $birim; farkYuzde = [math]::Round($fark, 1); yon = $yon }
    }
    foreach ($d in $diskListe) {
        $od = @($onceki.diskler) | Where-Object { $_.ad -eq $d.ad } | Select-Object -First 1
        if (-not $od) { continue }
        if ($null -ne $d.asinma -and $null -ne $od.asinma -and $d.asinma -ne $od.asinma) {
            $script:degisim += [ordered]@{ ad = "$($d.ad) ömür kullanımı"; once = $od.asinma; simdi = $d.asinma; birim = '%'; farkYuzde = $null; yon = if ($d.asinma - $od.asinma -ge 5) { 'kotu' } else { 'ayni' } }
        }
        if ($d.test -and $od.test) { Degisim "$($d.ad) okuma hızı" $od.test.siraliOkuma $d.test.siraliOkuma 'MB/s' $true 15 }
    }
    if ($cpuBilgi.test -and $onceki.cpu.test) {
        Degisim 'İşlemci tek çekirdek' $onceki.cpu.test.tekCekirdek $cpuBilgi.test.tekCekirdek 'puan' $true 8
        Degisim 'İşlemci çok çekirdek' $onceki.cpu.test.cokCekirdek $cpuBilgi.test.cokCekirdek 'puan' $true 8
    }
    if ($ram.bantGBs -and $onceki.ram.bantGBs) { Degisim 'Bellek hızı' $onceki.ram.bantGBs $ram.bantGBs 'GB/s' $true 10 }
    if ($pil -and $onceki.pil) {
        $po = @($onceki.pil.piller)[0]; $ps = @($pil.piller)[0]
        if ($po -and $ps) { Degisim 'Pil kapasitesi' $po.doluMWh $ps.doluMWh 'mWh' $true 2 }
    }
    $oncekiKodlar = @{}
    foreach ($b in @($onceki.bulgular)) { if ($b.seviye -in 'bad', 'warn' -and $b.kod) { $oncekiKodlar[$b.kod] = $b.baslik } }
    $simdikiKodlar = @{}
    foreach ($b in $Bulgular) { if ($b.seviye -in 'bad', 'warn') { $simdikiKodlar[$b.kod] = $b.baslik } }
    $cozulen = @($oncekiKodlar.Keys | Where-Object { -not $simdikiKodlar.ContainsKey($_) } | ForEach-Object { $oncekiKodlar[$_] })
    $yeni = @($simdikiKodlar.Keys | Where-Object { -not $oncekiKodlar.ContainsKey($_) } | ForEach-Object { $simdikiKodlar[$_] })
    $karsilastirma = [ordered]@{
        tarih = $onceki.meta.tarih; tamTarama = [bool]$onceki.meta.testYapildi
        donanimOnce = $onceki.puan.donanim; bakimOnce = $onceki.puan.bakim
        degisimler = @($degisim); cozulen = $cozulen; yeni = $yeni
    }
}

$sira = @{ bad = 0; warn = 1; info = 2; ok = 3 }
$zaman = Get-Date
$damga = $zaman.ToString('yyyy-MM-dd_HH-mm-ss')
$raporYolu = Join-Path $raporDizin ("SistemCheckup_{0}.html" -f $damga)
$veri = [ordered]@{
    meta = [ordered]@{ tarih = $zaman.ToString('dd.MM.yyyy HH:mm'); yonetici = $Admin; sureSn = [int]($zaman - $Baslangic).TotalSeconds; testYapildi = $benchYuklu; surum = $UygulamaSurum; raporYolu = $raporYolu }
    puan = $puan
    sistem = $sistem; pil = $pil; cpu = $cpuBilgi; gpu = @($gpuListe); ram = $ram; diskler = @($diskListe)
    olaylar = $olaylar; suruculer = $suruculer; cihazlar = $cihazlar; ag = $ag; termal = @($termal)
    karsilastirma = $karsilastirma
    bulgular = @($Bulgular | Sort-Object { $sira[$_.seviye] })
}

$jsonKompakt = $veri | ConvertTo-Json -Depth 10 -Compress
$sablon = [IO.File]::ReadAllText((Join-Path $KokDizin 'rapor_sablon.html'), [Text.Encoding]::UTF8)
$html = $sablon.Replace('/*__VERI__*/null', ($jsonKompakt -replace '</', '<\/'))
New-Item -ItemType Directory -Force -Path $raporDizin | Out-Null
[IO.File]::WriteAllText($raporYolu, $html, $Utf8)
[IO.File]::WriteAllText((Join-Path $raporDizin 'son_rapor.json'), $jsonKompakt, $Utf8)
[IO.File]::WriteAllText((Join-Path $gecmisDizin "$damga.json"), $jsonKompakt, $Utf8)
# Geçmişte en fazla 30 tarama tut
Get-ChildItem $gecmisDizin -Filter '*.json' | Sort-Object Name -Descending | Select-Object -Skip 30 | Remove-Item -Force
Get-ChildItem $raporDizin -Filter 'SistemCheckup_*.html' | Sort-Object Name -Descending | Select-Object -Skip 30 | Remove-Item -Force

Write-Host ""
Write-Host ("  Donanım sağlığı: {0}/100   ·   Bakım: {1}/100" -f $puan.donanim, $puan.bakim) -ForegroundColor $(if ($puan.donanim -ge 80) { 'Green' } elseif ($puan.donanim -ge 60) { 'Yellow' } else { 'Red' })
Write-Host "  Rapor: $raporYolu" -ForegroundColor Gray
Write-Host ""
Ilerleme "BITTI|$raporYolu"

if (-not $Sessiz) {
    Start-Process $raporYolu
    Read-Host "Kapatmak için Enter'a basın" | Out-Null
}
