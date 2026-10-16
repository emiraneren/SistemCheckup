# Değişiklikler

## 1.4.0 — 2026-10-16

- Ekran kartı sıcaklığı artık AMD ve Intel kartlarda da (dizüstü tümleşik grafik dahil) okunuyor
- GTX 10/900 ve daha eski NVIDIA kartlarda yanlış "güncelleme var" uyarısı düzeltildi; bu kartlar için doğru sürücü serisi öneriliyor
- Sağlık bilgisi vermeyen USB bellek / RAID diskler artık "arıza" olarak gösterilmiyor; sanal diskler test edilmiyor
- Bir programın kurduğu sanal aygıtlar (sanal ses kablosu, VPN vb.) donanım arızası sayılmıyor
- Dizüstü: dahili ekran tanınıyor, pildeyken düşük yenileme hızı yanlış uyarı vermiyor; masaüstündeki UPS pil sanılmıyor
- Windows Update takılırsa tarama donmuyor (2,5 dakika sınırı); başka antivirüs kullananlarda antivirüs adı gösteriliyor
- Uygulama hatalarında anlaşılır mesaj ve hata günlüğü; korumalı klasörde veya antivirüs engelinde ne yapılacağı söyleniyor

## 1.3.1 — 2026-10-16

- Düzeltme: NVIDIA ekran kartı olmayan bilgisayarlarda tarama "Parametre hatalı / The parameter is incorrect" hatasıyla duruyordu
- Bir kontrol beklenmedik şekilde başarısız olursa artık tarama durmuyor; o kontrol atlanıp raporda belirtiliyor

## 1.3.0 — 2026-10-16

- Klasör sadeleştirildi: artık yalnızca SistemCheckup.exe (açmanız gereken), Kaldir.exe ve BENIOKU.txt görünüyor; güncelleyici app klasörüne taşındı
- Güncelleme sırasında neyin değiştiği kısaca gösteriliyor
- Yeni açıklayıcı README ve ekran görüntüleri

## 1.2.0 — 2026-10-16

- Yeni Ana sayfa: sağlık puanlarının, işlemci ve disk hızının zaman içindeki değişimi grafiklerle gösteriliyor; geçmiş taramaların raporları tek tıkla açılıyor
- Yeni Sistem bilgileri sekmesi: tüm donanım ayrıntıları ile bağlı monitör (model, boyut, Hz), klavye, fare, kumanda, ses cihazları, kamera ve Bluetooth cihazları
- Yeni Temizlik sekmesi: geçici dosyaları ve tarayıcı / uygulama önbelleklerini silerek yer açar; belgelere, şifrelere ve geçmişe dokunmaz
- Sürücü güncellemeleri artık "büyük" ve "küçük" diye ayrılıyor ve her birinin ne kazandıracağı yazıyor; küçük güncellemeler puanı düşürmüyor
- Monitör desteklediğinden düşük yenileme hızında çalışıyorsa uyarıyor ve nasıl düzeltileceğini anlatıyor
- Yeni "Nasıl çalışıyor?" sekmesi ve puan kartlarında açıklama balonları
- Pencere boyutu değişirken kartların kenarlarında iz kalması düzeltildi

## 1.1.1 — 2026-10-15

- "Yeni sürüm hazır" bandındaki düğme artık tam görünüyor
- Tarama iptal edildiğinde mesaj hata gibi kırmızı değil, normal gösteriliyor

## 1.1.0 — 2026-10-15

- Yeni pencereli arayüz: tarama ilerlemesi canlı görünüyor, sonuçlar uygulama içinde gösteriliyor
- Hızlı tarama (30-60 sn) ve tam tarama (hız testleriyle, yaklaşık 2 dk) seçeneği
- Tek puan yerine iki puan: Donanım sağlığı ve Bakım/güncellemeler; açıklamalar daha anlaşılır
- Her tarama saklanıyor; önceki taramaya göre nelerin değiştiği ve hangi sorunların çözüldüğü gösteriliyor
- Dizüstü bilgisayarlar için pil sağlığı (kalan kapasite ve şarj döngüsü)
- İlk açılışta masaüstü ve Başlat menüsü kısayolu seçeneği
- Kaldir.exe ile programı tüm dosyaları ve kısayollarıyla kaldırma
- Güncelleyici artık tek bir zip paketi indiriyor ve atlanan tüm sürümlerin notlarını gösteriyor
- İşlemci ısınma testi artık frekansa bakıyor; arka plandaki programlar yüzünden yanlış uyarı vermiyor

## 1.0.2 — 2026-10-15

- Güncelleyici yeni sürümleri artık beklemeden görüyor.

## 1.0.1 — 2026-10-15

- Güncelleyici penceresinde küçük görünüm düzeltmeleri.

## 1.0.0 — 2026-10-15

- İlk sürüm: disk, ekran kartı, bellek ve sürücü kontrolü, hız testleri ve otomatik güncelleme.
