# Sistem Checkup

Windows için ücretsiz sistem sağlık kontrolü. Bilgisayarınızdaki parçaların sağlıklı çalışıp çalışmadığını kontrol eder, teknik detayları ve hız testlerini gösterir, sürücülerin güncel olup olmadığına bakar ve güncel olmayanlar için indirme bağlantısı verir. Sonuçlar tarayıcıda açılan bir HTML raporda gösterilir.

## Neler kontrol ediliyor?

| Alan | Kontroller |
|---|---|
| **Disk / SSD** | Sağlık durumu, ömür kullanımı (aşınma), sıcaklık, çalışma saati, doluluk, sıralı ve 4K okuma/yazma hız testi (önbelleksiz) |
| **İşlemci** | Tek / çok çekirdek puanı, yük altında efektif frekans, 10 saniyelik tam yükte ısıl kısıtlama (throttling) testi |
| **Ekran kartı** | Sıcaklık, kullanım, güç, VRAM, PCIe bağlantı hızı (NVIDIA kartlarda canlı veri) |
| **Bellek** | Modüller, hız, XMP/EXPO açık mı, çift kanal mı, bant genişliği testi |
| **Kararlılık** | Son 30 günde mavi ekran, beklenmeyen kapanma, WHEA donanım hatası, disk hatası, ekran sürücüsü çökmesi |
| **Sürücüler** | NVIDIA'nın sunucusundan en son sürüm karşılaştırması, Windows Update'te bekleyen sürücüler, sürücüsü eksik aygıtlar, eski sürücüler — her biri için indirme bağlantısı |
| **Diğer** | BIOS yaşı, Windows güncellemeleri, Defender durumu, ağ bağlantı hızı |

## Kurulum

1. [Releases](../../releases/latest) sayfasından `SistemCheckup-vX.Y.Z.zip` dosyasını indirin.
2. Bir klasöre çıkarın (ör. `Belgeler\SistemCheckup`).
3. `SistemCheckup.exe` dosyasını çalıştırın. Yönetici izni verirseniz SSD ömrü ve sıcaklık bilgileri de raporlanır.

> **"Windows bilgisayarınızı korudu" uyarısı:** Exe dijital olarak imzalı olmadığı için Windows SmartScreen ilk açılışta uyarı gösterebilir. **Ek bilgi → Yine de çalıştır** ile açabilirsiniz. Kaynak kodun tamamı bu depodadır.

## Güncelleme

`Guncelleyici.exe` dosyasını açın. Son sürümü GitHub'dan denetler, **yalnızca değişen dosyaları** indirir, SHA256 ile doğrular ve yerine koyar. `SistemCheckup.exe` de her açılışta yeni sürüm olup olmadığını sorar.

Güncelleyici eksik veya bozulmuş dosyaları da onarır.

## Gereksinimler

Windows 10 / 11. Başka bir şey kurmanıza gerek yok (Windows'ta hazır gelen PowerShell 5.1 ve .NET Framework 4.x kullanılır).

## Gizlilik

Tarama tamamen bilgisayarınızda yapılır. Raporlar yalnızca `Raporlar` klasörüne kaydedilir, hiçbir yere gönderilmez. İnternete yalnızca şu istekler yapılır: NVIDIA sürücü sürümü sorgusu, Windows Update sürücü taraması ve bu depodan güncelleme denetimi.

---

## Geliştirici notları

```
app/            Tarama betiği, test modülü ve rapor şablonu (yamalanan dosyalar)
src/            Başlatıcı (SistemCheckup.exe) ve güncelleyici (Guncelleyici.exe) kaynak kodu
assets/         Simge
build.ps1       Exe'leri derler, dist\ klasörünü hazırlar
yayinla.ps1     Yeni sürüm yayınlar (derle + commit + tag + GitHub Release)
proje.json      GitHub depo adı
```

Yeni bir yama yayınlamak için (GitHub CLI `gh` ile giriş yapılmış olmalı):

```powershell
.\yayinla.ps1 -Surum 1.0.1 -Notlar "Yapılan değişikliklerin açıklaması"
```

Betik exe'leri derler, her dosyanın SHA256 değeriyle `manifest.json` üretir, commit + tag atar ve dosyaları GitHub Release'e yükler. Güncelleyici `releases/latest/download/manifest.json` adresini okuyup değişen dosyaları indirir.

Göndermeden yalnızca derlemek için: `.\yayinla.ps1 -Surum 1.0.1 -Taslak`

## Lisans

MIT
