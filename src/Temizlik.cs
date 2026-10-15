using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

// Geçici dosya ve önbellek temizliği. Yalnızca aşağıda adı geçen önbellek klasörlerinin İÇİNİ siler;
// klasörün kendisine, kullanıcı dosyalarına, tarayıcı geçmişine / şifrelerine / çerezlerine dokunmaz.
// Bağlantı noktalarını (junction / symlink) izlemez, kullanımdaki dosyaları atlar.
namespace SistemCheckup
{
    class TemizlikKategorisi
    {
        public string Ad;
        public string Aciklama;
        public bool Varsayilan;
        public double EnAzYasSaat;              // bundan yeni dosyalara dokunma (çalışan kurulumlar vb. için)
        public Func<IEnumerable<string>> Klasorler;
        public long Boyut;
        public int DosyaSayisi;
    }

    static class Temizlik
    {
        static string Yerel { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        static string Dolasim { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }

        // Chromium tabanlı tarayıcıların her profilindeki yalnızca önbellek klasörleri
        static IEnumerable<string> Chromium(string kullaniciVerisi)
        {
            if (!Directory.Exists(kullaniciVerisi)) yield break;
            string[] profiller;
            try { profiller = Directory.GetDirectories(kullaniciVerisi); } catch { yield break; }
            foreach (string p in profiller)
            {
                string ad = Path.GetFileName(p);
                if (ad != "Default" && !ad.StartsWith("Profile ") && ad != "Guest Profile") continue;
                yield return Path.Combine(p, "Cache");
                yield return Path.Combine(p, "Code Cache");
                yield return Path.Combine(p, "GPUCache");
                yield return Path.Combine(p, "Service Worker", "CacheStorage");
            }
            yield return Path.Combine(kullaniciVerisi, "ShaderCache");
            yield return Path.Combine(kullaniciVerisi, "GrShaderCache");
        }

        static IEnumerable<string> Tarayicilar()
        {
            foreach (string k in Chromium(Path.Combine(Yerel, "Google", "Chrome", "User Data"))) yield return k;
            foreach (string k in Chromium(Path.Combine(Yerel, "Microsoft", "Edge", "User Data"))) yield return k;
            foreach (string k in Chromium(Path.Combine(Yerel, "BraveSoftware", "Brave-Browser", "User Data"))) yield return k;
            foreach (string k in Chromium(Path.Combine(Yerel, "Vivaldi", "User Data"))) yield return k;
            foreach (string opera in new[] { "Opera Stable", "Opera GX Stable" })
            {
                yield return Path.Combine(Yerel, "Opera Software", opera, "Cache");
                yield return Path.Combine(Dolasim, "Opera Software", opera, "Code Cache");
                yield return Path.Combine(Dolasim, "Opera Software", opera, "GPUCache");
            }
            foreach (string kok in new[] { Path.Combine(Yerel, "Mozilla", "Firefox", "Profiles"), Path.Combine(Yerel, "LibreWolf", "Profiles") })
            {
                if (!Directory.Exists(kok)) continue;
                string[] profiller;
                try { profiller = Directory.GetDirectories(kok); } catch { continue; }
                foreach (string p in profiller) { yield return Path.Combine(p, "cache2"); yield return Path.Combine(p, "startupCache"); }
            }
        }

        static IEnumerable<string> Uygulamalar()
        {
            foreach (string d in new[] { "discord", "discordptb", "discordcanary" })
            {
                yield return Path.Combine(Dolasim, d, "Cache");
                yield return Path.Combine(Dolasim, d, "Code Cache");
                yield return Path.Combine(Dolasim, d, "GPUCache");
            }
            yield return Path.Combine(Yerel, "Steam", "htmlcache");
            yield return Path.Combine(Dolasim, "Microsoft", "Teams", "Cache");
            yield return Path.Combine(Yerel, "EpicGamesLauncher", "Saved", "webcache");
        }

        public static List<TemizlikKategorisi> Kategoriler()
        {
            return new List<TemizlikKategorisi>
            {
                new TemizlikKategorisi { Ad = "Geçici dosyalar", Varsayilan = true, EnAzYasSaat = 24,
                    Aciklama = "Programların kurulum ve çalışma sırasında bırakıp silmeyi unuttuğu dosyalar. Son 24 saatte oluşanlara dokunulmaz.",
                    Klasorler = () => new[] { Path.GetTempPath() } },
                new TemizlikKategorisi { Ad = "Tarayıcı önbellekleri", Varsayilan = true,
                    Aciklama = "Chrome, Edge, Brave, Opera, Firefox'un sayfaları hızlı açmak için sakladığı kopyalar. Geçmiş, şifreler, çerezler ve oturumlar silinmez. Tarayıcı açıksa bir kısmı atlanır.",
                    Klasorler = Tarayicilar },
                new TemizlikKategorisi { Ad = "Uygulama önbellekleri", Varsayilan = true,
                    Aciklama = "Discord, Steam, Teams ve Epic Games uygulamalarının önbellekleri. Hesaplarınız, ayarlarınız ve indirdiğiniz oyunlar etkilenmez.",
                    Klasorler = Uygulamalar },
                new TemizlikKategorisi { Ad = "Çökme dökümleri ve hata raporları", Varsayilan = true,
                    Aciklama = "Programlar çöktüğünde oluşan döküm dosyaları ve Windows hata raporları. Yalnızca bir çökmeyi geliştiriciye göndermek istiyorsanız saklayın.",
                    Klasorler = () => new[] { Path.Combine(Yerel, "CrashDumps"), Path.Combine(Yerel, "Microsoft", "Windows", "WER", "ReportArchive"), Path.Combine(Yerel, "Microsoft", "Windows", "WER", "ReportQueue") } },
                new TemizlikKategorisi { Ad = "Oyun gölgelendirici (shader) önbelleği", Varsayilan = false,
                    Aciklama = "Ekran kartının oyunlar için hazırladığı önbellek. Silinirse oyunlar ilk açılışta kısa süre takılabilir; yalnızca grafik hatası veya bozulma yaşıyorsanız temizleyin.",
                    Klasorler = () => new[] { Path.Combine(Yerel, "D3DSCache"), Path.Combine(Yerel, "NVIDIA", "DXCache"), Path.Combine(Yerel, "NVIDIA", "GLCache"), Path.Combine(Yerel, "AMD", "DxCache"), Path.Combine(Yerel, "AMD", "GLCache") } },
            };
        }

        static bool BaglantiMi(FileSystemInfo f) { return (f.Attributes & FileAttributes.ReparsePoint) != 0; }

        static IEnumerable<FileInfo> Dosyalar(string kok)
        {
            Stack<DirectoryInfo> yigin = new Stack<DirectoryInfo>();
            DirectoryInfo ilk;
            try { ilk = new DirectoryInfo(kok); if (!ilk.Exists || BaglantiMi(ilk)) yield break; } catch { yield break; }
            yigin.Push(ilk);
            while (yigin.Count > 0)
            {
                DirectoryInfo d = yigin.Pop();
                FileInfo[] dosyalar = null; DirectoryInfo[] altlar = null;
                try { dosyalar = d.GetFiles(); altlar = d.GetDirectories(); } catch { }
                if (dosyalar != null) foreach (FileInfo f in dosyalar) yield return f;
                if (altlar != null) foreach (DirectoryInfo a in altlar) { if (!BaglantiMi(a)) yigin.Push(a); }
            }
        }

        static IEnumerable<string> VarOlanlar(TemizlikKategorisi k)
        {
            return k.Klasorler().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static void Hesapla(TemizlikKategorisi k)
        {
            long boyut = 0; int sayi = 0;
            DateTime sinir = DateTime.Now.AddHours(-k.EnAzYasSaat);
            foreach (string kok in VarOlanlar(k))
                foreach (FileInfo f in Dosyalar(kok))
                {
                    try { if (k.EnAzYasSaat > 0 && f.LastWriteTime > sinir) continue; boyut += f.Length; sayi++; } catch { }
                }
            k.Boyut = boyut; k.DosyaSayisi = sayi;
        }

        // Silinen bayt sayısını döndürür. Kullanımdaki / izin verilmeyen dosyalar atlanır.
        public static long Temizle(TemizlikKategorisi k, Action<long> ilerleme)
        {
            long silinen = 0;
            DateTime sinir = DateTime.Now.AddHours(-k.EnAzYasSaat);
            foreach (string kok in VarOlanlar(k).ToList())
            {
                foreach (FileInfo f in Dosyalar(kok).ToList())
                {
                    try
                    {
                        if (k.EnAzYasSaat > 0 && f.LastWriteTime > sinir) continue;
                        long uzunluk = f.Length;
                        if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes &= ~FileAttributes.ReadOnly;
                        f.Delete();
                        silinen += uzunluk;
                        if (ilerleme != null) ilerleme(silinen);
                    }
                    catch { }
                }
                BoslariSil(kok, true);
            }
            return silinen;
        }

        // Boş kalan alt klasörleri siler; kök klasörün kendisini bırakır.
        static void BoslariSil(string yol, bool kok)
        {
            try
            {
                DirectoryInfo d = new DirectoryInfo(yol);
                if (BaglantiMi(d)) return;
                foreach (DirectoryInfo a in d.GetDirectories()) BoslariSil(a.FullName, false);
                if (!kok && !d.EnumerateFileSystemInfos().Any()) d.Delete();
            }
            catch { }
        }

        public static string Boyut(long b)
        {
            System.Globalization.CultureInfo tr = new System.Globalization.CultureInfo("tr-TR");
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.0", tr) + " GB";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString("0", tr) + " MB";
            if (b > 0) return Math.Max(1, b / 1024) + " KB";
            return "0 MB";
        }
    }
}
