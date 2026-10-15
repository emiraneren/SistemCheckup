# Sistem Checkup

Windows için ücretsiz sistem sağlık kontrolü. Bilgisayarınızdaki parçaların sağlıklı çalışıp çalışmadığını kontrol eder, teknik detayları ve hız testlerini gösterir, sürücülerin güncel olup olmadığına bakar ve güncel olmayanlar için indirme bağlantısı verir.

**[⬇ Son sürümü indir](../../releases/latest)**: sayfadaki `SistemCheckup-vX.Y.Z.zip` dosyası.

## Neler kontrol ediliyor?

| Alan | Kontroller |
|---|---|
| **Disk / SSD** | Sağlık durumu, kalan ömür, sıcaklık, çalışma saati, doluluk, sıralı ve 4K okuma/yazma hız testi (önbelleksiz) |
| **İşlemci** | Tek / çok çekirdek puanı, yük altında hız, 10 saniyelik tam yükte ısınma (throttling) testi |
| **Ekran kartı** | Sıcaklık, kullanım, güç, VRAM, PCIe bağlantı hızı (NVIDIA kartlarda canlı veri) |
| **Bellek** | Modüller, hız, XMP/EXPO açık mı, çift kanal mı, bant genişliği testi |
| **Pil (dizüstü)** | Kalan kapasite (yeniyken tuttuğuna göre), şarj döngüsü, priz/pil durumu |
| **Kararlılık** | Son 30 günde mavi ekran, düzgün kapatılmadan kapanma, donanım hatası, disk hatası, ekran sürücüsü çökmesi |
| **Sürücüler** | NVIDIA'nın sunucusundan en son sürüm karşılaştırması, Windows Update'te bekleyen sürücüler, sürücüsü eksik aygıtlar, eski sürücüler; güncellemeler **büyük / küçük** diye ayrılır ve ne kazandıracağı yazılır |
| **Bağlı cihazlar** | Monitör (model, boyut, çözünürlük, yenileme hızı; desteklediğinden düşük Hz'de çalışıyorsa uyarı), klavye, fare, oyun kumandası, ses cihazları, kamera, Bluetooth, yazıcı |
| **Diğer** | BIOS yaşı, Windows güncellemeleri, Defender durumu, ağ bağlantı hızı |

Uygulamada ayrıca **Ana sayfa** (puan ve hız geçmişi grafikleri, geçmiş taramalar), **Sistem bilgileri** (tüm donanım ve bağlı cihazlar), **Temizlik** (geçici dosyalar ve tarayıcı / uygulama önbellekleri) ve **Nasıl çalışıyor?** sekmeleri bulunur.

Sonuçta iki ayrı puan verilir:
- **Donanım sağlığı**: parçaların fiziksel durumu. Düşükse bir parça arızalanıyor olabilir.
- **Bakım ve güncellemeler**: sürücüler, güncellemeler, disk alanı ve ayarlar. Düşük olması donanımın bozuk olduğu anlamına gelmez.

Her tarama saklanır; bir sonraki taramada puanların, hız ölçümlerinin, SSD ömrünün ve pil kapasitesinin nasıl değiştiği, hangi sorunların çözüldüğü gösterilir.

## Kurulum

1. [Releases](../../releases/latest) sayfasından `SistemCheckup-vX.Y.Z.zip` dosyasını indirin.
2. Zip'e sağ tıklayıp **Tümünü ayıkla** deyin.
3. Çıkan klasördeki `SistemCheckup.exe`'yi açın. İlk açılışta masaüstü ve Başlat menüsü kısayolu isteyip istemediğiniz sorulur.

> **"Windows bilgisayarınızı korudu" uyarısı:** Exe dijital olarak imzalı olmadığı için Windows SmartScreen ilk açılışta uyarı gösterebilir. **Ek bilgi → Yine de çalıştır** ile açabilirsiniz. Kaynak kodun tamamı bu depodadır.

## Güncelleme

Yeni sürüm çıktığında pencerenin üstünde bir bant belirir; **Şimdi güncelle**'ye basmanız yeterli. Güncelleyici zip paketini indirir, paketin ve içindeki her dosyanın SHA256 değerini doğrular ve **yalnızca değişen dosyaları** yerine koyar. Kurulu sürümden bu yana çıkan tüm sürümlerin notlarını gösterir. Eksik ya da bozulmuş dosyaları da onarır.

## Kaldırma

Klasördeki `Kaldir.exe`'yi açın (ya da programın altındaki **Kaldır** bağlantısı). Program dosyaları, raporlar, ayarlar ve kısayollar silinir. Klasöre sizin koyduğunuz başka dosyalara dokunulmaz.

## Gereksinimler

Windows 10 / 11. Başka bir şey kurmanıza gerek yok (Windows'ta hazır gelen PowerShell 5.1 ve .NET Framework 4.x kullanılır).

## Gizlilik

Tarama tamamen bilgisayarınızda yapılır. Raporlar yalnızca `Raporlar` klasörüne kaydedilir, hiçbir yere gönderilmez. İnternete yalnızca şu istekler yapılır: NVIDIA sürücü sürümü sorgusu, Windows Update sürücü taraması ve bu depodan güncelleme denetimi.

---

## Geliştirici notları

```
app/             Tarama motoru (PowerShell), hız testi modülü ve HTML rapor şablonu
src/Arayuz.cs    SistemCheckup.exe: pencereli ana uygulama
src/Guncelleyici.cs  Guncelleyici.exe
src/Kaldir.cs    Kaldir.exe
src/Ortak.cs     Ortak kod (manifest, indirme, kısayollar, ayarlar)
build.ps1        Exe'leri derler, dist\SistemCheckup\ klasörünü hazırlar
yayinla.ps1      Yeni sürüm yayınlar
manifest.json    Son sürümün dosya listesi ve SHA256 değerleri (yayinla.ps1 üretir)
surumler.json    Sürüm notları geçmişi (yayinla.ps1 günceller)
```

Yeni bir yama yayınlamak için (GitHub CLI `gh` ile giriş yapılmış olmalı):

```powershell
.\yayinla.ps1 -Surum 1.1.1 -Notlar "Birinci değişiklik", "İkinci değişiklik"
```

Notları kullanıcının anlayacağı dilde, madde madde yazın; güncelleyicide aynen görünür. Betik exe'leri derler, zip paketini ve `manifest.json`'u üretir, commit + tag atar ve GitHub Release'e yalnızca zip ile `BENIOKU.txt`'yi yükler. Güncelleyici son sürümün etiketini GitHub API'sinden alır, `manifest.json`'u o etiketten okur ve zip'ten değişen dosyaları çıkarır.

Göndermeden yalnızca derlemek için `-Taslak` ekleyin.

## Lisans

MIT
