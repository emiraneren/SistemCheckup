using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

// Ana uygulama, güncelleyici ve kaldırıcının ortak kullandığı kod.
// __REPO__ derleme sırasında build.ps1 tarafından "kullanici/repo" ile değiştirilir.
namespace SistemCheckup
{
    public class DosyaBilgi
    {
        public string hedef { get; set; }   // kurulum klasörüne göre yol (ör. app/SistemCheckup.ps1)
        public string sha256 { get; set; }
        public long boyut { get; set; }
    }

    public class PaketBilgi
    {
        public string ad { get; set; }      // release'teki zip dosyasının adı
        public string sha256 { get; set; }
        public long boyut { get; set; }
    }

    public class SurumNotu
    {
        public string surum { get; set; }
        public string tarih { get; set; }
        public List<string> notlar { get; set; }
    }

    public class Manifest
    {
        public string surum { get; set; }
        public string etiket { get; set; }
        public string tarih { get; set; }
        public List<string> notlar { get; set; }
        public List<SurumNotu> gecmis { get; set; }
        public PaketBilgi paket { get; set; }
        public List<DosyaBilgi> dosyalar { get; set; }
    }

    public class Ayarlar
    {
        public bool ilkKurulumTamam { get; set; }
        public bool yoneticiIzni { get; set; }
        public bool tamTarama { get; set; }

        public Ayarlar() { yoneticiIzni = true; tamTarama = true; }

        static string Yol { get { return Path.Combine(Ortak.KokDizin, "ayarlar.json"); } }

        public static Ayarlar Yukle()
        {
            try { if (File.Exists(Yol)) return new JavaScriptSerializer().Deserialize<Ayarlar>(File.ReadAllText(Yol, Encoding.UTF8)) ?? new Ayarlar(); }
            catch { }
            return new Ayarlar();
        }

        public void Kaydet()
        {
            try { File.WriteAllText(Yol, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false)); } catch { }
        }
    }

    public static class Ortak
    {
        public const string Repo = "__REPO__";
        public const string UygulamaAdi = "Sistem Checkup";

        public static string RepoUrl { get { return "https://github.com/" + Repo; } }
        // Kurulum klasörü. Güncelleyici app\ içinde durduğu için onun klasörü değil, bir üstü kökdür.
        public static string KokDizin
        {
            get
            {
                string b = AppDomain.CurrentDomain.BaseDirectory;
                string ad = Path.GetFileName(b.TrimEnd('\\'));
                if (string.Equals(ad, "app", StringComparison.OrdinalIgnoreCase))
                {
                    string ust = Path.GetDirectoryName(b.TrimEnd('\\'));
                    if (ust != null) return ust + "\\";
                }
                return b;
            }
        }
        public static string AppDizin { get { return Path.Combine(KokDizin, "app"); } }
        public static string RaporDizin { get { return Path.Combine(KokDizin, "Raporlar"); } }
        public static string YerelManifestYolu { get { return Path.Combine(AppDizin, "surum.json"); } }
        public static string AnaUygulamaYolu { get { return Path.Combine(KokDizin, "SistemCheckup.exe"); } }
        public static string GuncelleyiciYolu { get { return Path.Combine(AppDizin, "Guncelleyici.exe"); } }
        // 1.3.0 öncesi sürümlerde güncelleyici kök klasördeydi
        public static string EskiGuncelleyiciYolu { get { return Path.Combine(KokDizin, "Guncelleyici.exe"); } }
        public static string KaldiriciYolu { get { return Path.Combine(KokDizin, "Kaldir.exe"); } }
        public static string MasaustuKisayolu { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), UygulamaAdi + ".lnk"); } }
        public static string BaslatKisayolu { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), UygulamaAdi + ".lnk"); } }

        public static void Hazirla()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        // ---------------------------------------------------------------- sürüm / manifest
        public static Manifest Coz(string json) { return new JavaScriptSerializer().Deserialize<Manifest>(json); }
        public static string Serilestir(Manifest m) { return new JavaScriptSerializer().Serialize(m); }

        public static Manifest YerelManifest()
        {
            try { if (File.Exists(YerelManifestYolu)) return Coz(File.ReadAllText(YerelManifestYolu, Encoding.UTF8)); }
            catch { }
            return null;
        }

        public static string YerelSurum()
        {
            Manifest m = YerelManifest();
            return (m != null && !string.IsNullOrEmpty(m.surum)) ? m.surum : "0.0.0";
        }

        static HttpWebRequest Istek(string url, int zamanAsimiMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = zamanAsimiMs;
            req.ReadWriteTimeout = zamanAsimiMs;
            req.UserAgent = "SistemCheckup-Guncelleyici";
            req.AllowAutoRedirect = true;
            req.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            if (url.StartsWith("https://api.github.com")) req.Accept = "application/vnd.github+json";
            return req;
        }

        static string Oku(string url, int zamanAsimiMs)
        {
            using (WebResponse resp = Istek(url, zamanAsimiMs).GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // Son sürümün etiketini GitHub API'sinden alır, manifesti o etiketteki commit'ten okur
        // (etiketli dosyalar değişmediği için önbellek sorunu olmaz). API sınırına takılırsa ana daldaki manifeste düşer.
        public static Manifest UzakManifest(int zamanAsimiMs)
        {
            try
            {
                Dictionary<string, object> rel = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                    Oku("https://api.github.com/repos/" + Repo + "/releases/latest", zamanAsimiMs));
                object etiket;
                if (rel.TryGetValue("tag_name", out etiket) && etiket != null)
                    return Coz(Oku("https://raw.githubusercontent.com/" + Repo + "/" + Uri.EscapeDataString(etiket.ToString()) + "/manifest.json", zamanAsimiMs));
            }
            catch (WebException) { }
            return Coz(Oku("https://raw.githubusercontent.com/" + Repo + "/main/manifest.json", zamanAsimiMs));
        }

        public static string PaketUrl(Manifest m)
        {
            return RepoUrl + "/releases/download/" + Uri.EscapeDataString(m.etiket) + "/" + Uri.EscapeDataString(m.paket.ad);
        }

        public static void Indir(string url, string hedefDosya, Action<long> ilerleme)
        {
            using (WebResponse resp = Istek(url, 60000).GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (FileStream fs = File.Create(hedefDosya))
            {
                byte[] buf = new byte[81920];
                int n; long toplam = 0;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, n);
                    toplam += n;
                    if (ilerleme != null) ilerleme(toplam);
                }
            }
        }

        public static bool YeniMi(string uzak, string yerel)
        {
            Version a, b;
            if (!Version.TryParse(uzak ?? "", out a)) return false;
            if (!Version.TryParse(yerel ?? "", out b)) return true;
            return a > b;
        }

        public static string Sha256(string yol)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(yol))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }

        // Manifestteki hedef yolunu kurulum klasörü içinde güvenli bir tam yola çevirir.
        public static string HedefYol(string hedef)
        {
            string goreli = (hedef ?? "").Replace('/', Path.DirectorySeparatorChar);
            if (goreli.Length == 0 || Path.IsPathRooted(goreli) || goreli.Contains(".."))
                throw new InvalidDataException("Geçersiz dosya yolu: " + hedef);
            return Path.Combine(KokDizin, goreli);
        }

        // ---------------------------------------------------------------- sistem
        public static bool Yonetici()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Kendini güncelleme sırasında kenara alınan eski exe'leri siler.
        public static void EskileriTemizle()
        {
            try
            {
                foreach (string d in new[] { KokDizin, AppDizin })
                {
                    if (!Directory.Exists(d)) continue;
                    foreach (string f in Directory.GetFiles(d, "*.old", SearchOption.TopDirectoryOnly))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                // Yeni yerinde (app\) güncelleyici varsa kök klasörde kalan eskisini kaldır
                if (File.Exists(GuncelleyiciYolu) && File.Exists(EskiGuncelleyiciYolu))
                {
                    try { File.Delete(EskiGuncelleyiciYolu); }
                    catch { try { File.Move(EskiGuncelleyiciYolu, EskiGuncelleyiciYolu + "." + DateTime.Now.Ticks + ".old"); } catch { } }
                }
            }
            catch { }
        }

        // Kullanıcı exe'yi zip'i çıkarmadan, zip'in içinden açtıysa Windows onu geçici bir klasöre tek başına çıkarır.
        public static bool ZipIcindenCalisiyor()
        {
            if (File.Exists(Path.Combine(AppDizin, "SistemCheckup.ps1"))) return false;
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');
            return KokDizin.StartsWith(temp, StringComparison.OrdinalIgnoreCase);
        }

        public static void Ac(string yolVeyaUrl, string argumanlar)
        {
            ProcessStartInfo psi = new ProcessStartInfo(yolVeyaUrl);
            if (argumanlar != null) psi.Arguments = argumanlar;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        public static bool YazilabilirMi()
        {
            try
            {
                string test = Path.Combine(KokDizin, ".yazma_testi");
                File.WriteAllText(test, "1");
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        // ---------------------------------------------------------------- kısayollar (WScript.Shell)
        static object Kabuk()
        {
            return Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        }

        public static void KisayolOlustur(string lnkYolu)
        {
            object kabuk = Kabuk();
            object lnk = kabuk.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, kabuk, new object[] { lnkYolu });
            Type t = lnk.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { AnaUygulamaYolu });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { KokDizin.TrimEnd('\\') });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Bilgisayarın sağlık kontrolü" });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { AnaUygulamaYolu + ",0" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }

        public static string KisayolHedefi(string lnkYolu)
        {
            try
            {
                if (!File.Exists(lnkYolu)) return null;
                object kabuk = Kabuk();
                object lnk = kabuk.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, kabuk, new object[] { lnkYolu });
                return (string)lnk.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, lnk, null);
            }
            catch { return null; }
        }

        // Yalnızca bu kuruluma işaret eden kısayolu sayar / siler (başka bir kurulumun kısayoluna dokunmaz).
        public static bool BizimKisayolMu(string lnkYolu)
        {
            string hedef = KisayolHedefi(lnkYolu);
            return hedef != null && string.Equals(Path.GetFullPath(hedef), Path.GetFullPath(AnaUygulamaYolu), StringComparison.OrdinalIgnoreCase);
        }

        public static void KisayolSil(string lnkYolu)
        {
            try { if (BizimKisayolMu(lnkYolu)) File.Delete(lnkYolu); } catch { }
        }
    }
}
