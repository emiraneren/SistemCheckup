using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;

// Başlatıcı ve güncelleyicinin ortak kullandığı sürüm / indirme kodu.
// __REPO__ derleme sırasında build.ps1 tarafından "kullanici/repo" ile değiştirilir.
namespace SistemCheckup
{
    public class DosyaBilgi
    {
        public string ad { get; set; }      // release'teki dosya adı
        public string hedef { get; set; }   // kurulum klasörüne göre yol (ör. app/SistemCheckup.ps1)
        public string sha256 { get; set; }
        public long boyut { get; set; }
    }

    public class Manifest
    {
        public string surum { get; set; }
        public string etiket { get; set; }
        public string tarih { get; set; }
        public string notlar { get; set; }
        public List<DosyaBilgi> dosyalar { get; set; }
    }

    public static class Ortak
    {
        public const string Repo = "__REPO__";

        public static string RepoUrl { get { return "https://github.com/" + Repo; } }
        public static string ManifestUrl { get { return RepoUrl + "/releases/latest/download/manifest.json"; } }
        public static string KokDizin { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        public static string YerelManifestYolu { get { return Path.Combine(KokDizin, "app", "surum.json"); } }
        public static string BaslaticiYolu { get { return Path.Combine(KokDizin, "SistemCheckup.exe"); } }
        public static string GuncelleyiciYolu { get { return Path.Combine(KokDizin, "Guncelleyici.exe"); } }

        public static void Hazirla()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static string DosyaUrl(Manifest m, DosyaBilgi d)
        {
            return RepoUrl + "/releases/download/" + Uri.EscapeDataString(m.etiket) + "/" + Uri.EscapeDataString(d.ad);
        }

        public static Manifest Coz(string json)
        {
            return new JavaScriptSerializer().Deserialize<Manifest>(json);
        }

        public static string Serilestir(Manifest m)
        {
            return new JavaScriptSerializer().Serialize(m);
        }

        public static Manifest YerelManifest()
        {
            try
            {
                if (File.Exists(YerelManifestYolu)) return Coz(File.ReadAllText(YerelManifestYolu, Encoding.UTF8));
            }
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
            return req;
        }

        static string Oku(string url, int zamanAsimiMs)
        {
            HttpWebRequest req = Istek(url, zamanAsimiMs);
            if (url.StartsWith("https://api.github.com")) req.Accept = "application/vnd.github+json";
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        // Önce GitHub API'sinden son sürümün etiketini alır (anında güncellenir);
        // API sınırına takılırsa releases/latest yönlendirmesine düşer (birkaç dakika gecikebilir).
        public static Manifest UzakManifest(int zamanAsimiMs)
        {
            try
            {
                Dictionary<string, object> rel = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                    Oku("https://api.github.com/repos/" + Repo + "/releases/latest", zamanAsimiMs));
                object etiket;
                if (rel.TryGetValue("tag_name", out etiket) && etiket != null)
                    return Coz(Oku(RepoUrl + "/releases/download/" + Uri.EscapeDataString(etiket.ToString()) + "/manifest.json", zamanAsimiMs));
            }
            catch (WebException) { }
            return Coz(Oku(ManifestUrl, zamanAsimiMs));
        }

        // ilerleme: indirilen bayt sayısı
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
        public static string HedefYol(DosyaBilgi d)
        {
            string goreli = (d.hedef ?? "").Replace('/', Path.DirectorySeparatorChar);
            if (goreli.Length == 0 || Path.IsPathRooted(goreli) || goreli.Contains(".."))
                throw new InvalidDataException("Geçersiz dosya yolu: " + d.hedef);
            return Path.Combine(KokDizin, goreli);
        }

        public static bool Yonetici()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Kendini güncelleme sırasında kenara alınan eski exe'leri siler.
        public static void EskileriTemizle()
        {
            try
            {
                foreach (string f in Directory.GetFiles(KokDizin, "*.old", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        public static void Ac(string yolVeyaUrl, string argumanlar)
        {
            ProcessStartInfo psi = new ProcessStartInfo(yolVeyaUrl);
            if (argumanlar != null) psi.Arguments = argumanlar;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
    }
}
