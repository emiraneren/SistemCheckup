#requires -Version 5.1
<#
  Sistem Checkup
  Donanım sağlığını kontrol eder, CPU / GPU / RAM / disk teknik detaylarını ve hız testlerini
  toplar, sürücülerin güncel olup olmadığına bakar ve sonuçları HTML rapor olarak açar.

  Parametreler:
    -YonetimsizCalis  Yönetici izni istemeden sınırlı modda çalışır
    -TestAtla         Hız testlerini (disk / CPU / RAM) atlar
    -Sessiz           Bitince raporu açmaz ve Enter beklemez
    -RaporDizini      Raporların kaydedileceği klasör (varsayılan: betiğin yanındaki Raporlar)
#>
param([switch]$YonetimsizCalis, [switch]$TestAtla, [switch]$Sessiz, [string]$RaporDizini)

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}
$KokDizin = $PSScriptRoot
$Baslangic = Get-Date
$UygulamaSurum = 'geliştirme'
try { $UygulamaSurum = (Get-Content -Raw -Encoding UTF8 (Join-Path $KokDizin 'surum.json') -ErrorAction Stop | ConvertFrom-Json).surum } catch {}

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
$ToplamAdim = 11
function Adim($metin) {
    $script:AdimNo++
    Write-Host ("[{0,2}/{1}] {2}" -f $script:AdimNo, $ToplamAdim, $metin) -ForegroundColor Cyan
}
function Bilgi($metin) { Write-Host "        $metin" -ForegroundColor DarkGray }

$Bulgular = New-Object System.Collections.ArrayList
function Bulgu {
    param($Seviye, $Kategori, $Baslik, $Detay, $Link = $null, $LinkMetin = $null)
    [void]$Bulgular.Add([ordered]@{ seviye = $Seviye; kategori = $Kategori; baslik = $Baslik; detay = $Detay; link = $Link; linkMetin = $LinkMetin })
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
Write-Host ("  Mod: " + $(if ($Admin) { 'Yönetici (tam veri)' } else { 'Standart kullanıcı (sınırlı veri)' })) -ForegroundColor Gray
Write-Host ""

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
    bilgisayar      = $env:COMPUTERNAME
    isletimSistemi  = "$($os.Caption)".Trim()
    surum           = "$($cv.DisplayVersion)"
    derleme         = "$($os.BuildNumber).$($cv.UBR)"
    calismaSuresi   = ("{0} gün {1} saat {2} dk" -f $uptime.Days, $uptime.Hours, $uptime.Minutes)
    calismaGun      = [math]::Round($uptime.TotalDays, 1)
    sistemUretici   = "$($cs.Manufacturer)".Trim()
    sistemModel     = "$($cs.Model)".Trim()
    anakartUretici  = $anakartUretici
    anakartModel    = $anakartModel
    biosSurum       = "$($bios.SMBIOSBIOSVersion)"
    biosTarih       = Tarih $bios.ReleaseDate
    biosYasGun      = $biosYas
    destekLinki     = $DESTEK
    sonWinGuncelleme = if ($sonGuncelleme) { Tarih $sonGuncelleme.InstalledOn } else { $null }
}

if ($biosYas -gt 730) {
    Bulgu 'warn' 'Anakart' "BIOS sürümü eski ($([int]($biosYas/365)) yıl)" "Kurulu BIOS $($bios.SMBIOSBIOSVersion), tarih $($sistem.biosTarih). Yeni BIOS sürümleri kararlılık, güvenlik ve RAM uyumluluğu düzeltmeleri içerebilir. Güncellemeden önce sürüm notlarını okuyun." $DESTEK 'Anakart destek sayfası'
} else {
    Bulgu 'ok' 'Anakart' "BIOS güncel görünüyor" "$anakartUretici $anakartModel, BIOS $($bios.SMBIOSBIOSVersion) ($($sistem.biosTarih))." $DESTEK 'Destek sayfası'
}
if ($uptime.TotalDays -gt 14) {
    Bulgu 'info' 'Sistem' "Bilgisayar $([int]$uptime.TotalDays) gündür yeniden başlatılmadı" "Uzun süre açık kalmak bekleyen güncellemelerin uygulanmasını geciktirir. Ara sıra yeniden başlatmak iyi olur."
}
if ($sonGuncelleme) {
    $wuYas = YasGun $sonGuncelleme.InstalledOn
    if ($wuYas -gt 60) {
        Bulgu 'warn' 'Windows' "Windows $wuYas gündür güncellenmemiş" "Son yüklenen güncelleme: $($sonGuncelleme.HotFixID) ($($sistem.sonWinGuncelleme))." 'ms-settings:windowsupdate' "Windows Update'i aç"
    }
}

# Defender
$mp = Get-MpComputerStatus
if ($mp) {
    $imzaYas = YasGun $mp.AntivirusSignatureLastUpdated
    $sistem.antivirus = [ordered]@{ acik = [bool]$mp.AntivirusEnabled; gercekZamanli = [bool]$mp.RealTimeProtectionEnabled; imzaTarihi = Tarih $mp.AntivirusSignatureLastUpdated }
    if (-not $mp.RealTimeProtectionEnabled) {
        Bulgu 'warn' 'Güvenlik' 'Windows Defender gerçek zamanlı koruma kapalı' 'Başka bir antivirüs kullanmıyorsanız korumayı açın.' 'windowsdefender:' 'Windows Güvenliği'
    } elseif ($imzaYas -gt 7) {
        Bulgu 'warn' 'Güvenlik' "Antivirüs tanımları $imzaYas gündür güncellenmemiş" '' 'windowsdefender:' 'Windows Güvenliği'
    }
}

# ------------------------------------------------------------------ 2. işlemci
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
    Bulgu 'bad' 'İşlemci' "İşlemci durumu: $($cpu.Status)" 'Windows işlemcinin normal çalışmadığını bildiriyor.'
}

# ------------------------------------------------------------------ 3. ekran kartı
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
        Bulgu 'bad' 'Ekran Kartı' 'Ekran kartı sürücüsü yüklü değil' "Windows temel görüntü sürücüsünü kullanıyor ($ad). Oyun/hızlandırma çalışmaz." $NVIDIA_LINK 'Sürücü indir'
    } elseif ($g.Status -ne 'OK') {
        Bulgu 'bad' 'Ekran Kartı' "$ad durumu: $($g.Status)" 'Aygıt Yöneticisi ekran kartında sorun bildiriyor.'
    }
    if ($gpu.canli) {
        $c = $gpu.canli
        if ($c.sicaklik -ge 85) { Bulgu 'bad' 'Ekran Kartı' "GPU sıcaklığı yüksek: $($c.sicaklik) °C" 'Rölantide/hafif yükte bu sıcaklık normal değil. Fanları, toz birikimini ve kasa hava akışını kontrol edin.' }
        elseif ($c.sicaklik -ge 75 -and $c.kullanim -lt 30) { Bulgu 'warn' 'Ekran Kartı' "GPU düşük yükte sıcak: $($c.sicaklik) °C" "Kullanım %$($c.kullanim) iken sıcaklık beklenenden yüksek. Fan eğrisi veya toz kontrol edilmeli." }
        else { Bulgu 'ok' 'Ekran Kartı' "$ad sağlıklı çalışıyor" "Sıcaklık $($c.sicaklik) °C, kullanım %$($c.kullanim), güç $($c.guc) W / $($c.gucLimit) W." }
        if ($c.pcieGenislikMax -and $c.pcieGenislik -lt $c.pcieGenislikMax) {
            Bulgu 'warn' 'Ekran Kartı' "Ekran kartı x$($c.pcieGenislik) hızında bağlı (destekleyen: x$($c.pcieGenislikMax))" 'Kart yuvaya tam oturmamış, yanlış yuvada ya da yuva paylaşımlı olabilir. Performans düşebilir.'
        }
    } elseif ($g.Status -eq 'OK') {
        Bulgu 'ok' 'Ekran Kartı' "$ad çalışıyor" 'Windows aygıtın normal çalıştığını bildiriyor (canlı sıcaklık verisi yalnızca NVIDIA kartlarda okunabiliyor).'
    }
    $gpuListe += $gpu
}

# ------------------------------------------------------------------ 4. bellek
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
    $nominal = NominalHiz $m.PartNumber
    $modListe += [ordered]@{
        yuva = "$($m.DeviceLocator)"; uretici = "$($m.Manufacturer)".Trim(); parcaNo = "$($m.PartNumber)".Trim()
        kapasiteGB = GB $m.Capacity 0; tip = $tipAdi[[int]$m.SMBIOSMemoryType]
        hizMHz = [int]$m.Speed; ayarliMHz = [int]$m.ConfiguredClockSpeed; nominalMHz = $nominal
        voltaj = if ($m.ConfiguredVoltage) { $m.ConfiguredVoltage / 1000.0 } else { $null }
    }
}
$toplamRam = ($moduller | Measure-Object Capacity -Sum).Sum
$kullanilanKB = $os.TotalVisibleMemorySize - $os.FreePhysicalMemory
$kullanimYuzde = [math]::Round(100.0 * $kullanilanKB / $os.TotalVisibleMemorySize, 0)
$kanalDurum = if ($moduller.Count -le 1) { 'Tek kanal' } elseif ($kanallar.Count -ge 2) { 'Çift kanal' } elseif ($kanallar.Count -eq 1) { 'Tek kanal' } else { 'Belirlenemedi' }

$ram = [ordered]@{
    toplamGB = GB $toplamRam 0; kullanilanGB = Yuvarla ($kullanilanKB / 1MB) 1; kullanimYuzde = $kullanimYuzde
    yuvaSayisi = [int]$dizi.MemoryDevices; maksGB = if ($dizi.MaxCapacityEx) { [int]($dizi.MaxCapacityEx / 1MB) } else { $null }
    kanal = $kanalDurum; moduller = $modListe; bantGBs = $null; tanilama = $null
}

$ilk = $modListe | Select-Object -First 1
if ($ilk) {
    $ayarli = $ilk.ayarliMHz
    $hedef = @($ilk.nominalMHz, $ilk.hizMHz) | Where-Object { $_ } | Measure-Object -Maximum | Select-Object -ExpandProperty Maximum
    if ($hedef -and $ayarli -and $hedef -gt ($ayarli + 100)) {
        Bulgu 'warn' 'Bellek' "RAM $ayarli MHz'de çalışıyor, desteklediği hız $hedef MHz" "BIOS'ta XMP / EXPO / DOCP profili kapalı görünüyor. Açarsanız bellek performansı artar." $DESTEK 'Anakart kılavuzu'
    } else {
        Bulgu 'ok' 'Bellek' "RAM nominal hızında çalışıyor ($ayarli MHz)" "$($ram.toplamGB) GB $($ilk.tip), $($moduller.Count) modül, $kanalDurum."
    }
}
if ($kanalDurum -eq 'Tek kanal' -and $moduller.Count -ge 1) {
    Bulgu 'warn' 'Bellek' 'RAM tek kanal çalışıyor' 'Tek modül ya da yanlış yuvalar kullanılıyor. İki modülü anakart kılavuzundaki önerilen yuvalara (genelde A2 + B2) takmak bant genişliğini neredeyse ikiye katlar.'
}
if ($kullanimYuzde -ge 90) {
    Bulgu 'warn' 'Bellek' "Bellek kullanımı çok yüksek (%$kullanimYuzde)" 'Arka planda çok fazla uygulama açık olabilir. Görev Yöneticisi ile kontrol edin.'
}

$md = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-MemoryDiagnostics-Results' } | Select-Object -First 1
if ($md) {
    $hata = ($md.Id -eq 1202)
    $ram.tanilama = [ordered]@{ tarih = Tarih $md.TimeCreated; hataVar = $hata }
    if ($hata) { Bulgu 'bad' 'Bellek' 'Windows Bellek Tanılama hata buldu' "Son test ($($ram.tanilama.tarih)) bellek hatası raporladı. Modülleri tek tek test edin (MemTest86)." 'https://www.memtest86.com/' 'MemTest86' }
}

# ------------------------------------------------------------------ 5. depolama
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
        firmware = "$($pd.FirmwareVersion)"; seriNo = $null
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
        Bulgu 'bad' 'Depolama' "$etiket sağlık durumu: $($disk.saglik)" 'Disk arıza belirtisi gösteriyor. Önemli verilerinizi hemen yedekleyin.'
    } else {
        $ek = @()
        if ($null -ne $disk.asinma) { $ek += "ömür kullanımı %$($disk.asinma)" }
        if ($disk.sicaklik) { $ek += "$($disk.sicaklik) °C" }
        if ($disk.calismaSaati) { $ek += "$($disk.calismaSaati) saat çalışma" }
        $ekMetin = if ($ek.Count) { ' — ' + ($ek -join ', ') } else { '' }
        Bulgu 'ok' 'Depolama' "$etiket sağlıklı" "Windows sağlık durumu: Sağlıklı$ekMetin."
    }
    if ($null -ne $disk.asinma) {
        if ($disk.asinma -ge 90) { Bulgu 'bad' 'Depolama' "$($disk.ad) ömrünün sonuna yaklaştı (%$($disk.asinma))" 'Yazma ömrü neredeyse tükenmiş. Değiştirmeyi planlayın ve yedek alın.' }
        elseif ($disk.asinma -ge 70) { Bulgu 'warn' 'Depolama' "$($disk.ad) ömrünün %$($disk.asinma)'ini kullanmış" 'Disk sağlıklı ama yıpranma ilerlemiş, düzenli yedek alın.' }
    }
    if ($disk.sicaklik -ge 70) { Bulgu 'warn' 'Depolama' "$($disk.ad) sıcak: $($disk.sicaklik) °C" 'NVMe soğutucusu takılı mı kontrol edin; yüksek sıcaklık hız düşüşüne (throttling) yol açar.' }
    if ($disk.okumaHatasi -gt 0 -or $disk.yazmaHatasi -gt 0) { Bulgu 'bad' 'Depolama' "$($disk.ad) düzeltilemeyen hata bildirdi" "Okuma hatası: $($disk.okumaHatasi), yazma hatası: $($disk.yazmaHatasi). Yedek alın." }
    foreach ($b in $birimler) {
        if ($null -ne $b.bosYuzde) {
            if ($b.bosYuzde -lt 10) { Bulgu 'bad' 'Depolama' "$($b.harf) sürücüsünde çok az boş alan (%$($b.bosYuzde))" "Yalnızca $($b.bosGB) GB boş. SSD'ler dolduğunda yavaşlar; Windows güncellemeleri de başarısız olabilir." 'ms-settings:storagesense' 'Depolama ayarları' }
            elseif ($b.bosYuzde -lt 20) { Bulgu 'warn' 'Depolama' "$($b.harf) sürücüsü dolmak üzere (%$($b.bosYuzde) boş)" "$($b.bosGB) GB boş alan kaldı." 'ms-settings:storagesense' 'Depolama ayarları' }
        }
    }
}
if (-not $Admin) {
    Bulgu 'info' 'Depolama' 'SSD aşınma ve sıcaklık verisi okunamadı' 'Bu veriler yönetici izni gerektiriyor. Uygulamayı yönetici olarak çalıştırırsanız SSD ömrü, sıcaklık ve çalışma saati de raporlanır.'
}

# ------------------------------------------------------------------ 6. hız testleri
$benchYuklu = $false
if (-not $TestAtla) {
    try {
        Add-Type -Path (Join-Path $KokDizin 'SCBench.cs') -ErrorAction Stop
        $benchYuklu = $true
    } catch {
        Write-Host "        Test modülü yüklenemedi: $_" -ForegroundColor Red
    }
}

Adim "Disk hız testleri (okuma / yazma)"
if ($benchYuklu) {
    foreach ($disk in $diskListe) {
        if ($disk.arayuz -eq 'USB') { Bilgi "$($disk.ad): USB disk, test atlandı"; continue }
        $hedef = $disk.birimler | Where-Object { $_.bosGB -ge 3 -and $_.dosyaSistemi -eq 'NTFS' } | Select-Object -First 1
        if (-not $hedef) { Bilgi "$($disk.ad): yeterli boş alan yok, test atlandı"; continue }
        $sysHarf = $env:SystemDrive
        $klasor = if ($hedef.harf -eq $sysHarf) { Join-Path $env:TEMP 'SistemCheckup_tmp' } else { "$($hedef.harf)\SistemCheckup_tmp" }
        New-Item -ItemType Directory -Force -Path $klasor | Out-Null
        $dosya = Join-Path $klasor 'hiz_testi.tmp'
        Bilgi "$($disk.ad) → $($hedef.harf) üzerinde test ediliyor..."
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
            Bilgi ("   Sıralı okuma {0:N0} MB/s · yazma {1:N0} MB/s · 4K okuma {2:N1} MB/s" -f $sr, $sw, $r1[0])

            $beklenen = if ($disk.arayuz -eq 'NVMe') { 1000 } elseif ($disk.tip -eq 'SSD') { 350 } else { 80 }
            if ($sr -lt $beklenen) {
                Bulgu 'warn' 'Depolama' "$($disk.ad) beklenenden yavaş okuyor ($([int]$sr) MB/s)" "Bu tip disk için en az ~$beklenen MB/s beklenir. Disk dolu, aşırı ısınmış, arka planda yoğun kullanımda ya da yanlış porta/yuvaya bağlı olabilir."
            }
        } catch {
            Bilgi "   Test başarısız: $($_.Exception.Message)"
        } finally {
            Remove-Item -Force $dosya -ErrorAction SilentlyContinue
            Remove-Item -Force $klasor -ErrorAction SilentlyContinue
        }
    }
} else { Bilgi 'atlandı' }

Adim "İşlemci ve bellek performans testi (~15 sn)"
if ($benchYuklu) {
    Bilgi 'Tek çekirdek testi...'
    $tek = [SCBench]::SingleThread(3000)
    $sure = 10
    Bilgi "Tüm çekirdekler $sure saniye yük altında (ısı ve frekans izleniyor)..."
    [SCBench]::StartMulti($cpuBilgi.izlek, $sure)
    $frekanslar = @()
    Start-Sleep -Milliseconds 700
    while (-not [SCBench]::CpuDone) {
        $pi = Get-CimInstance Win32_PerfFormattedData_Counters_ProcessorInformation -Filter "Name='_Total'"
        if ($pi -and $pi.ProcessorFrequency) { $frekanslar += [math]::Round($pi.ProcessorFrequency * $pi.PercentProcessorPerformance / 100.0, 0) }
        Start-Sleep -Milliseconds 800
    }
    $ornek = @([SCBench]::CpuSamples | ForEach-Object { [math]::Round($_, 1) })
    $basta = ($ornek[1..2] | Measure-Object -Average).Average
    $sonda = ($ornek[($sure - 3)..($sure - 1)] | Measure-Object -Average).Average
    $cok = ($ornek[1..($sure - 1)] | Measure-Object -Average).Average
    $dusus = if ($basta -gt 0) { [math]::Round(100.0 * ($basta - $sonda) / $basta, 1) } else { 0 }
    $dusus = [math]::Max(0, $dusus)
    $atla = if ($frekanslar.Count -gt 2) { 1 } else { 0 }
    $efektif = if ($frekanslar.Count) { [int](($frekanslar | Select-Object -Skip $atla | Measure-Object -Average).Average) } else { $null }

    $cpuBilgi.test = [ordered]@{
        tekCekirdek = Yuvarla $tek 0; cokCekirdek = Yuvarla $cok 0
        olcekleme = if ($tek -gt 0) { Yuvarla ($cok / $tek) 1 } else { $null }
        dususYuzde = $dusus; yukFrekansMHz = $efektif; frekanslar = $frekanslar; ornekler = $ornek
    }
    Bilgi ("Tek çekirdek {0:N0} · çok çekirdek {1:N0} puan · yükte ~{2} MHz · düşüş %{3}" -f $tek, $cok, $efektif, $dusus)

    if ($dusus -ge 12) {
        Bulgu 'warn' 'İşlemci' "Yük altında performans %$dusus düştü (ısıl kısıtlama olabilir)" '10 saniyelik tam yükte işlemci hızı belirgin şekilde azaldı. Soğutucunun oturması, termal macun, fan hızları ve kasa hava akışı kontrol edilmeli.'
    } elseif ($efektif -and $efektif -lt ($cpuBilgi.tabanMHz * 0.9)) {
        Bulgu 'warn' 'İşlemci' "Yük altında işlemci taban hızının altında çalışıyor (~$efektif MHz)" "Taban hızı $($cpuBilgi.tabanMHz) MHz. Güç planı 'Güç tasarrufu' modunda olabilir ya da soğutma yetersiz." 'ms-settings:powersleep' 'Güç ayarları'
    } else {
        Bulgu 'ok' 'İşlemci' 'İşlemci yük altında kararlı' "Tam yükte ~$efektif MHz efektif hız, 10 saniye boyunca performans düşüşü %$dusus (normal)."
    }

    Bilgi 'Bellek bant genişliği testi...'
    try {
        $bant = [SCBench]::MemCopy(256, [math]::Min(8, $cpuBilgi.izlek), 6)
        $ram.bantGBs = Yuvarla $bant 1
        Bilgi ("Bellek kopyalama {0:N1} GB/s" -f $bant)
    } catch { Bilgi "Bellek testi başarısız: $($_.Exception.Message)" }
    [GC]::Collect()
} else { Bilgi 'atlandı' }

# ------------------------------------------------------------------ 7. kararlılık / olay günlükleri
Adim "Son 30 günün hata kayıtları"
$ay = (Get-Date).AddDays(-30)
function OlayOzet($liste) {
    $l = @($liste | Sort-Object TimeCreated -Descending)
    [ordered]@{ adet = $l.Count; son = if ($l.Count) { $l[0].TimeCreated.ToString('yyyy-MM-dd HH:mm') } else { $null } }
}
$kp41 = Olaylar @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-Kernel-Power'; Id = 41; StartTime = $ay }
$e6008 = Olaylar @{ LogName = 'System'; ProviderName = 'EventLog'; Id = 6008; StartTime = $ay }
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
    Bulgu 'bad' 'Kararlılık' "Son 30 günde $($bsod.Count) mavi ekran (BSOD)" "Son: $($olaylar.maviEkran.son). Sık BSOD genelde sürücü, RAM (XMP kararsızlığı) veya güç kaynağı kaynaklıdır." 'https://learn.microsoft.com/tr-tr/troubleshoot/windows-client/performance/stop-error-or-blue-screen-error-troubleshooting' 'Sorun giderme rehberi'
}
if ($kapanma -gt 0) {
    $lvl = if ($kapanma -ge 3) { 'warn' } else { 'info' }
    Bulgu $lvl 'Kararlılık' "Son 30 günde $kapanma beklenmeyen kapanma" "Son: $($olaylar.beklenmeyenKapanma.son). Elektrik kesintisi, güç tuşuna basılı tutma veya donma sonrası zorla kapatma olabilir. Kendiliğinden oluyorsa güç kaynağı/ısı kontrol edilmeli."
}
if ($wheaHata.Count -gt 0) {
    Bulgu 'bad' 'Kararlılık' "$($wheaHata.Count) donanım hatası (WHEA) kaydedildi" "Son: $($olaylar.donanimHatasi.son). İşlemci, RAM veya PCIe aygıtlarında düzeltilemeyen hata. Hız aşırtma/XMP varsa kapatıp deneyin."
} elseif ($wheaDuzeltilen.Count -gt 50) {
    Bulgu 'warn' 'Kararlılık' "$($wheaDuzeltilen.Count) düzeltilmiş donanım uyarısı (WHEA)" 'Genellikle PCIe güç tasarrufu (ASPM) kaynaklıdır ve zararsızdır, ama çok sık ise BIOS/yonga seti güncellemesi önerilir.' $DESTEK 'Anakart destek sayfası'
}
if ($diskHata.Count -gt 0) {
    Bulgu 'warn' 'Depolama' "Son 30 günde $($diskHata.Count) disk/dosya sistemi hatası" "Son: $($olaylar.diskHatasi.son). Kablo bağlantısını kontrol edin; tekrar ediyorsa 'chkdsk' çalıştırın ve yedek alın."
}
if ($gpuTdr.Count -gt 0) {
    Bulgu 'warn' 'Ekran Kartı' "Ekran sürücüsü $($gpuTdr.Count) kez yanıt vermeyi durdurdu" "Son: $($olaylar.ekranSurucusu.son). Sürücüyü temiz kurulumla güncelleyin; devam ederse GPU sıcaklığı/güç kaynağı kontrol edilmeli." $NVIDIA_LINK 'Sürücü indir'
}
if (-not $bsod.Count -and -not $wheaHata.Count -and $kapanma -lt 3) {
    Bulgu 'ok' 'Kararlılık' 'Sistem kararlı çalışıyor' "Son 30 günde mavi ekran veya düzeltilemeyen donanım hatası yok."
}

# ------------------------------------------------------------------ 8. sorunlu aygıtlar
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
if ($sorunlu.Count) {
    $eksik = @($sorunlu | Where-Object { $_.kod -eq 28 }).Count
    $intelMi = @($sorunlu | Where-Object { $_.donanimId -match '8086|INTC' }).Count
    $detay = ($sorunlu | ForEach-Object { "$($_.ad) — $($_.aciklama)" }) -join '; '
    if ($intelMi -ge 2) {
        $detay += '. Bu aygıtlar Intel yonga seti parçaları (SMBus, PCI, RAID/VMD, GPIO). Anakart üreticisinin sayfasından "Chipset" ve "Intel Serial IO / RST" sürücülerini kurmak genellikle hepsini çözer.'
    }
    Bulgu 'bad' 'Sürücüler' "$($sorunlu.Count) aygıt düzgün çalışmıyor ($eksik tanesinin sürücüsü yok)" $detay $DESTEK 'Anakart sürücülerini indir'
} else {
    Bulgu 'ok' 'Sürücüler' "Aygıt Yöneticisi'nde hatalı aygıt yok" 'Takılı tüm aygıtların sürücüsü yüklü ve çalışıyor.'
}

# ------------------------------------------------------------------ 9. sürücüler
Adim "Sürücüler kontrol ediliyor"
$mevcutIds = @{}
foreach ($a in $mevcutAygitlar) { $mevcutIds["$($a.InstanceId)".ToUpper()] = $true }
$imzaliSuruculer = @(Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DeviceID -and $mevcutIds.ContainsKey("$($_.DeviceID)".ToUpper()) })

function SurucuLinki($saglayici, $ad, $sinif) {
    $s = "$saglayici $ad"
    if ($s -match 'NVIDIA') { return $NVIDIA_LINK }
    if ($s -match 'AMD|Advanced Micro|Radeon') { return $AMD_LINK }
    if ($s -match 'Intel') { return $INTEL_DSA }
    if ($s -match 'Realtek|Killer|MediaTek|Qualcomm|Atheros|Broadcom|ASMedia|Aquantia|Marvell') { return $DESTEK }
    if ($s -match 'Logitech') { return 'https://www.logitechg.com/tr-tr/innovation/g-hub.html' }
    if ($s -match 'SteelSeries') { return 'https://steelseries.com/gg' }
    if ($s -match 'Razer') { return 'https://www.razer.com/synapse-4' }
    if ($s -match 'Corsair') { return 'https://www.corsair.com/us/en/s/downloads' }
    if ($s -match 'TP-Link') { return 'https://www.tp-link.com/tr/support/download/' }
    if ($s -match 'Samsung') { return 'https://semiconductor.samsung.com/consumer-storage/support/tools/' }
    if ($s -match 'Crucial|Micron') { return 'https://www.crucial.com/support/storage-executive' }
    return (Ara "$saglayici $ad driver download")
}

# 9a. Windows Update üzerinde bekleyen sürücüler
Bilgi "Windows Update'te bekleyen sürücü güncellemeleri aranıyor (biraz sürebilir)..."
$wuListe = @()
$wuHata = $null
try {
    $arayici = (New-Object -ComObject Microsoft.Update.Session).CreateUpdateSearcher()
    $sonuc = $arayici.Search("IsInstalled=0 and Type='Driver'")
    foreach ($u in $sonuc.Updates) {
        $wuListe += [ordered]@{ baslik = "$($u.Title)"; model = "$($u.DriverModel)"; sinif = "$($u.DriverClass)"; saglayici = "$($u.DriverProvider)"; tarih = Tarih $u.DriverVerDate }
    }
} catch { $wuHata = "$($_.Exception.Message)" }
Bilgi "$($wuListe.Count) güncelleme bulundu"

# 9b. NVIDIA en son sürüm kontrolü
$nvidia = $null
$nvGpu = $gpuListe | Where-Object { $_.uretici -eq 'NVIDIA' -and -not $_.sanal } | Select-Object -First 1
if ($nvGpu) {
    $kurulu = if ($nvSmi) { $nvSmi.surucu } else {
        $rakam = ($nvGpu.surucu -replace '\.', ''); if ($rakam.Length -ge 5) { $r = $rakam.Substring($rakam.Length - 5); "$($r.Substring(0,3)).$($r.Substring(3))" } else { $null }
    }
    $nvidia = [ordered]@{ kurulu = $kurulu; enSon = $null; tarih = $null; detay = $NVIDIA_LINK; indir = $null; guncel = $null }
    try {
        Bilgi 'NVIDIA sunucusundan en son sürücü sorgulanıyor...'
        $notebook = $nvGpu.ad -match 'Laptop'
        $psid = if ($notebook) { 129 } else { 127 }
        $pfid = if ($notebook) { 1036 } else { 1041 }
        $url = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=$psid&pfid=$pfid&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1"
        $j = Invoke-RestMethod -Uri $url -TimeoutSec 20 -UseBasicParsing
        $info = $j.IDS[0].downloadInfo
        if ($info.Version) {
            $nvidia.enSon = "$($info.Version)"
            $nvidia.tarih = [uri]::UnescapeDataString("$($info.ReleaseDateTime)")
            if ($info.DetailsURL) { $nvidia.detay = "$($info.DetailsURL)" }
            if ($info.DownloadURL) { $nvidia.indir = "$($info.DownloadURL)" }
            $nvidia.guncel = ([double]::Parse($kurulu, [Globalization.CultureInfo]::InvariantCulture) -ge [double]::Parse($nvidia.enSon, [Globalization.CultureInfo]::InvariantCulture))
        }
    } catch { Bilgi "NVIDIA sorgusu başarısız: $($_.Exception.Message)" }

    if ($nvidia.guncel -eq $true) {
        Bulgu 'ok' 'Sürücüler' "NVIDIA sürücüsü güncel ($kurulu)" "En son Game Ready sürücüsü kurulu." $nvidia.detay 'Sürüm notları'
    } elseif ($nvidia.guncel -eq $false) {
        Bulgu 'warn' 'Sürücüler' "NVIDIA sürücüsü güncellenebilir: $kurulu → $($nvidia.enSon)" "Yeni sürüm $($nvidia.tarih) tarihinde yayınlandı. NVIDIA App üzerinden ya da aşağıdaki bağlantıdan güncelleyebilirsiniz." $nvidia.detay 'Sürücüyü indir'
    }
}
foreach ($g in ($gpuListe | Where-Object { -not $_.sanal -and $_.uretici -in 'AMD', 'Intel' })) {
    $link = if ($g.uretici -eq 'AMD') { $AMD_LINK } else { $INTEL_DSA }
    if ($g.surucuYasGun -gt 180) {
        Bulgu 'warn' 'Sürücüler' "$($g.ad) sürücüsü $([int]($g.surucuYasGun/30)) aylık" "Kurulu sürüm $($g.surucu) ($($g.surucuTarih)). Ekran kartı sürücüleri sık güncellenir, yeni sürüm muhtemelen mevcut." $link 'Sürücüyü indir'
    } else {
        Bulgu 'info' 'Sürücüler' "$($g.ad) sürücüsü $($g.surucuTarih) tarihli" "Kurulu sürüm $($g.surucu). En sonunu üreticinin sayfasından kontrol edebilirsiniz." $link 'Üretici sayfası'
    }
}

# Windows Update model adının tüm kelimeleri aygıt adında geçiyorsa eşleşmiş say
function WuEslesir($model, $aygit) {
    if (-not $model -or -not $aygit) { return $false }
    $kelimeler = @("$model".ToLower() -split '[^a-z0-9\.\-]+' | Where-Object { $_.Length -gt 2 })
    if (-not $kelimeler.Count) { return $false }
    $a = "$aygit".ToLower()
    foreach ($k in $kelimeler) { if (-not $a.Contains($k)) { return $false } }
    return $true
}

# 9c. Önemli sürücüler tablosu
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
    if ($wuEslesme) { $durum = 'warn'; $durumMetin = "Windows Update'te yeni sürüm var" }
    elseif ($sinif -eq 'DISPLAY' -and $nvidia -and $d.DeviceName -match 'NVIDIA') { if ($nvidia.guncel -eq $false) { $durum = 'warn'; $durumMetin = "Yeni sürüm: $($nvidia.enSon)" } elseif ($nvidia.guncel) { $durumMetin = 'En son sürüm' } }
    elseif ($ms) { $durum = 'info'; $durumMetin = 'Windows genel sürücüsü' }
    elseif ($inf) { $durum = 'info'; $durumMetin = 'Yalnızca tanımlama (INF)' }
    elseif ($yas -gt 1095) { $durum = 'warn'; $durumMetin = "$([int]($yas/365)) yıllık, güncellemesi olabilir" }
    elseif ($yas -gt 545) { $durum = 'info'; $durumMetin = 'Bir yıldan eski' }
    $link = if ($wuEslesme) { $WU_LINK } elseif ($sinif -eq 'DISPLAY' -and $nvidia) { $nvidia.detay } elseif ($ms) { $DESTEK } else { SurucuLinki $d.DriverProviderName $d.DeviceName $sinif }
    $onemli += [ordered]@{
        ad = "$($d.DeviceName)"; tur = $siniflar[$sinif]; saglayici = "$($d.DriverProviderName)"; surum = "$($d.DriverVersion)"
        tarih = Tarih $d.DriverDate; yasGun = $yas; durum = $durum; durumMetin = $durumMetin; link = $link
    }
}
$turSira = @{ 'Ekran kartı' = 0; 'Yonga seti' = 1; 'Depolama' = 2; 'Ağ' = 3; 'Ses' = 4; 'Bluetooth' = 5; 'USB' = 6 }
$onemli = @($onemli | Sort-Object { $turSira[$_.tur] }, ad)

# 9d. Tüm üçüncü taraf sürücülerde eski olanlar
$eskiler = @()
foreach ($d in $imzaliSuruculer) {
    if ("$($d.DriverProviderName)" -match '^Microsoft' -or -not $d.DriverDate) { continue }
    if (([datetime]$d.DriverDate).Year -lt 2000) { continue }
    $yas = YasGun $d.DriverDate
    if ($yas -gt 1095) {
        $eskiler += [ordered]@{ ad = "$($d.DeviceName)"; saglayici = "$($d.DriverProviderName)"; surum = "$($d.DriverVersion)"; tarih = Tarih $d.DriverDate; yasGun = $yas; link = SurucuLinki $d.DriverProviderName $d.DeviceName $d.DeviceClass }
    }
}
$eskiler = @($eskiler | Sort-Object yasGun -Descending | Group-Object ad | ForEach-Object { $_.Group[0] })

if ($wuListe.Count) {
    $ozet = ($wuListe | ForEach-Object { "$($_.model) ($($_.saglayici))" }) -join ', '
    Bulgu 'warn' 'Sürücüler' "Windows Update'te $($wuListe.Count) sürücü güncellemesi bekliyor" "$ozet. Ayarlar → Windows Update → Gelişmiş seçenekler → İsteğe bağlı güncelleştirmeler bölümünden yükleyebilirsiniz." $WU_LINK 'İsteğe bağlı güncellemeleri aç'
} elseif (-not $wuHata) {
    Bulgu 'ok' 'Sürücüler' "Windows Update'te bekleyen sürücü yok" 'Microsoft kataloğundaki tüm sürücüler güncel.'
}
if ($eskiler.Count) {
    $ornekler = ($eskiler | Select-Object -First 4 | ForEach-Object { "$($_.ad) ($($_.tarih))" }) -join ', '
    Bulgu 'info' 'Sürücüler' "$($eskiler.Count) sürücü 3 yıldan eski" "$ornekler. Bu her zaman sorun değildir; bazı aygıtlar yeni sürüm almaz. Liste Sürücüler bölümünde."
}

$suruculer = [ordered]@{ windowsUpdate = $wuListe; wuHata = $wuHata; nvidia = $nvidia; onemli = $onemli; eski = $eskiler; sorunluAygitlar = $sorunlu; toplam = $imzaliSuruculer.Count }

# ------------------------------------------------------------------ 10. diğer
Adim "Ağ, sıcaklık ve rapor"
$ag = @(Get-NetAdapter -Physical | ForEach-Object { [ordered]@{ ad = "$($_.Name)"; aciklama = "$($_.InterfaceDescription)"; durum = "$($_.Status)"; hiz = "$($_.LinkSpeed)"; mac = $null; surucu = "$($_.DriverVersion)" } })
foreach ($n in (Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -match '2\.5G|5G|10G' -and $_.LinkSpeed -match '^(100 Mbps|1 Gbps)' })) {
    Bulgu 'info' 'Ağ' "$($n.InterfaceDescription) $($n.LinkSpeed) bağlı" 'Kart daha yüksek hızı destekliyor; modem/switch 1 Gbps ise bu normaldir. Değilse kabloyu (Cat5e/Cat6) kontrol edin.'
}

$termal = @()
foreach ($t in @(Get-CimInstance Win32_PerfFormattedData_Counters_ThermalZoneInformation)) {
    $k = if ($t.HighPrecisionTemperature) { $t.HighPrecisionTemperature / 10.0 } else { [double]$t.Temperature }
    if ($k -gt 200) { $termal += [ordered]@{ ad = "Anakart ($($t.Name -replace '\\_TZ\.', ''))"; sicaklik = [math]::Round($k - 273.15, 0) } }
}
if ($nvSmi) { $termal += [ordered]@{ ad = 'Ekran kartı'; sicaklik = $nvSmi.sicaklik } }
foreach ($d in $diskListe) { if ($d.sicaklik) { $termal += [ordered]@{ ad = $d.ad; sicaklik = $d.sicaklik } } }

# puan
$puan = 100
foreach ($b in $Bulgular) { if ($b.seviye -eq 'bad') { $puan -= 12 } elseif ($b.seviye -eq 'warn') { $puan -= 5 } }
$puan = [math]::Max(0, $puan)

$sira = @{ bad = 0; warn = 1; info = 2; ok = 3 }
$veri = [ordered]@{
    meta = [ordered]@{ tarih = (Get-Date).ToString('dd.MM.yyyy HH:mm'); yonetici = $Admin; sureSn = [int]((Get-Date) - $Baslangic).TotalSeconds; testYapildi = $benchYuklu; surum = $UygulamaSurum }
    puan = $puan
    sistem = $sistem; cpu = $cpuBilgi; gpu = @($gpuListe); ram = $ram; diskler = @($diskListe)
    olaylar = $olaylar; suruculer = $suruculer; ag = $ag; termal = @($termal)
    bulgular = @($Bulgular | Sort-Object { $sira[$_.seviye] })
}

$json = ($veri | ConvertTo-Json -Depth 10 -Compress) -replace '</', '<\/'
$sablon = [IO.File]::ReadAllText((Join-Path $KokDizin 'rapor_sablon.html'), [Text.Encoding]::UTF8)
$html = $sablon.Replace('/*__VERI__*/null', $json)
$raporDizin = if ($RaporDizini) { $RaporDizini } else { Join-Path $KokDizin 'Raporlar' }
New-Item -ItemType Directory -Force -Path $raporDizin | Out-Null
$raporYolu = Join-Path $raporDizin ("SistemCheckup_{0}.html" -f (Get-Date -Format 'yyyy-MM-dd_HH-mm'))
[IO.File]::WriteAllText($raporYolu, $html, (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $raporDizin 'son_rapor.json'), ($veri | ConvertTo-Json -Depth 10), (New-Object Text.UTF8Encoding($false)))

$kotu = @($Bulgular | Where-Object { $_.seviye -eq 'bad' }).Count
$uyari = @($Bulgular | Where-Object { $_.seviye -eq 'warn' }).Count
Write-Host ""
Write-Host ("  Sağlık puanı: {0}/100   ·   {1} sorun, {2} uyarı" -f $puan, $kotu, $uyari) -ForegroundColor $(if ($puan -ge 80) { 'Green' } elseif ($puan -ge 60) { 'Yellow' } else { 'Red' })
Write-Host "  Rapor: $raporYolu" -ForegroundColor Gray
Write-Host ""

if (-not $Sessiz) {
    Start-Process $raporYolu
    Read-Host "Kapatmak için Enter'a basın" | Out-Null
}
