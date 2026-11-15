using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

// Canlı izleme: işlemci, ekran kartı, bellek, disk, ağ ve süreçlerin anlık kullanımı.
// Windows performans sayaçları doğrudan okunur (WMI'dan çok daha hafif); her saniye birkaç milisaniye sürer.
namespace SistemCheckup
{
    class GpuOlcum
    {
        public string Luid, Ad;
        public double Kullanim;
        public double? Sicaklik, VramKullanilanGB, VramToplamGB, GucW, GucLimitW, SaatMHz, FanYuzde;
        public int FanRPM;
    }

    class DiskOlcum { public string Ad; public double Etkinlik, OkumaBps, YazmaBps; }
    class SurecOlcum { public string Ad; public double Cpu; public double RamMB; public int Adet; }

    class Olcum
    {
        public double Cpu;
        public double[] Cekirdekler = new double[0];
        public double? CpuMHz, TermalC;
        public double RamToplamGB, RamKullanilanGB, RamYuzde, TaahhutGB, TaahhutLimitGB;
        public List<GpuOlcum> Gpular = new List<GpuOlcum>();
        public List<DiskOlcum> Diskler = new List<DiskOlcum>();
        public double AgIndirBps, AgYukleBps;
        public List<SurecOlcum> Surecler; // her ölçümde değil, iki saniyede bir
    }

    class Izleyici
    {
        [StructLayout(LayoutKind.Sequential)]
        class BELLEK
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(BELLEK));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] BELLEK b);

        readonly Dictionary<string, CounterSample> onceki = new Dictionary<string, CounterSample>();
        readonly HashSet<string> gorulen = new HashSet<string>();
        readonly Dictionary<string, double> vramToplam = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<int, string> diskAdlari;
        readonly Dictionary<int, TimeSpan> surecOnceki = new Dictionary<int, TimeSpan>();
        readonly Dictionary<string, string> surecAciklama = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DateTime surecZamani = DateTime.MinValue;
        readonly string nvidiaSmi;
        Dictionary<string, string[]> nvSon = new Dictionary<string, string[]>();
        int tur;
        static readonly Regex LuidDeseni = new Regex(@"luid_0x[0-9A-Fa-f]{8}_0x[0-9A-Fa-f]{8}", RegexOptions.Compiled);
        static readonly Regex MotorDeseni = new Regex(@"engtype_(.+)$", RegexOptions.Compiled);

        public Izleyici(Dictionary<int, string> diskAdlari)
        {
            this.diskAdlari = diskAdlari ?? new Dictionary<int, string>();
            // Ekran kartı bellek kapasiteleri (sürücünün kayıt defterine yazdığı)
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                    if (k != null)
                        foreach (string alt in k.GetSubKeyNames().Where(a => Regex.IsMatch(a, @"^\d{4}$")))
                            using (RegistryKey s = k.OpenSubKey(alt))
                            {
                                object ad = s == null ? null : s.GetValue("DriverDesc");
                                object m = s == null ? null : s.GetValue("HardwareInformation.qwMemorySize");
                                if (ad != null && m != null) vramToplam[ad.ToString()] = Convert.ToDouble(m is byte[] ? BitConverter.ToUInt64((byte[])m, 0) : m) / 1073741824.0;
                            }
            }
            catch { }
            string smi = System.IO.Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            nvidiaSmi = System.IO.File.Exists(smi) ? smi : null;
        }

        // ---------------------------------------------------------------- performans sayaçları
        static InstanceDataCollectionCollection Kategori(string ad)
        {
            try { return new PerformanceCounterCategory(ad).ReadCategory(); } catch { return null; }
        }

        static InstanceDataCollection Sayac(InstanceDataCollectionCollection c, string ad)
        {
            if (c == null) return null;
            try { return c[ad.ToLowerInvariant()] ?? c[ad]; } catch { return null; }
        }

        // İki örnek arasındaki değeri hesaplar (ilk örnekte null)
        double? Hesapla(string anahtar, CounterSample yeni)
        {
            gorulen.Add(anahtar);
            CounterSample eski;
            bool var = onceki.TryGetValue(anahtar, out eski);
            onceki[anahtar] = yeni;
            if (!var) return null;
            try { return CounterSample.Calculate(eski, yeni); } catch { return null; }
        }

        public List<string> GpuAdlari()
        {
            List<string> l = new List<string>();
            try
            {
                foreach (string s in SCGpu.ListeleLuid())
                {
                    string[] p = s.Split('|');
                    if (p.Length >= 2 && !p[1].Contains("Basic Render")) l.Add(p[1]);
                }
            }
            catch { }
            return l;
        }

        public Olcum Olc()
        {
            tur++;
            gorulen.Clear();
            Olcum o = new Olcum();

            // İşlemci: toplam ve çekirdek başına kullanım, efektif frekans
            InstanceDataCollectionCollection pi = Kategori("Processor Information");
            InstanceDataCollection zaman = Sayac(pi, "% Processor Time");
            if (zaman != null)
            {
                List<KeyValuePair<int, double>> cekirdek = new List<KeyValuePair<int, double>>();
                foreach (InstanceData d in zaman.Values)
                {
                    double? v = Hesapla("cpu|" + d.InstanceName, d.Sample);
                    if (!v.HasValue) continue;
                    double y = Math.Max(0, Math.Min(100, v.Value));
                    if (d.InstanceName == "_Total") o.Cpu = y;
                    else if (!d.InstanceName.Contains("_Total"))
                    {
                        string[] p = d.InstanceName.Split(',');
                        int grup, no;
                        if (p.Length == 2 && int.TryParse(p[0], out grup) && int.TryParse(p[1], out no)) cekirdek.Add(new KeyValuePair<int, double>(grup * 1000 + no, y));
                    }
                }
                o.Cekirdekler = cekirdek.OrderBy(k => k.Key).Select(k => k.Value).ToArray();
            }
            InstanceDataCollection perf = Sayac(pi, "% Processor Performance"), frekans = Sayac(pi, "Processor Frequency");
            if (perf != null && frekans != null && perf.Contains("_Total") && frekans.Contains("_Total"))
            {
                double? yuzde = Hesapla("cpuperf", perf["_Total"].Sample);
                double f = frekans["_Total"].RawValue;
                if (yuzde.HasValue && f > 0) o.CpuMHz = f * yuzde.Value / 100.0;
            }

            // Termal bölge (anakart / ACPI sensörü)
            InstanceDataCollection tz = Sayac(Kategori("Thermal Zone Information"), "High Precision Temperature");
            if (tz != null)
                foreach (InstanceData d in tz.Values)
                {
                    double c = d.RawValue / 10.0 - 273.15;
                    if (c > 1 && c < 125 && (!o.TermalC.HasValue || c > o.TermalC)) o.TermalC = c;
                }

            // Bellek
            BELLEK b = new BELLEK();
            if (GlobalMemoryStatusEx(b))
            {
                o.RamToplamGB = b.ullTotalPhys / 1073741824.0;
                o.RamKullanilanGB = (b.ullTotalPhys - b.ullAvailPhys) / 1073741824.0;
                o.RamYuzde = b.dwMemoryLoad;
                o.TaahhutLimitGB = b.ullTotalPageFile / 1073741824.0;
                o.TaahhutGB = (b.ullTotalPageFile - b.ullAvailPageFile) / 1073741824.0;
            }

            // Ekran kartları: adı ve sıcaklığı Windows grafik arayüzünden, kullanım ve bellek performans sayaçlarından
            Dictionary<string, GpuOlcum> gpular = new Dictionary<string, GpuOlcum>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string s in SCGpu.ListeleLuid())
                {
                    string[] p = s.Split('|');
                    if (p.Length < 5 || p[1].Contains("Basic Render")) continue;
                    GpuOlcum g = new GpuOlcum { Luid = p[0], Ad = p[1] };
                    double t; if (double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out t) && t > 0) g.Sicaklik = t;
                    int fan; if (int.TryParse(p[3], out fan)) g.FanRPM = fan;
                    double vt; if (vramToplam.TryGetValue(g.Ad, out vt)) g.VramToplamGB = vt;
                    gpular[g.Luid] = g;
                }
            }
            catch { }
            InstanceDataCollection motor = Sayac(Kategori("GPU Engine"), "Utilization Percentage");
            if (motor != null)
            {
                Dictionary<string, double> toplam = new Dictionary<string, double>();
                foreach (InstanceData d in motor.Values)
                {
                    double? v = Hesapla("gpu|" + d.InstanceName, d.Sample);
                    if (!v.HasValue) continue;
                    Match l = LuidDeseni.Match(d.InstanceName), m = MotorDeseni.Match(d.InstanceName);
                    if (!l.Success || !m.Success) continue;
                    string k = l.Value.ToLowerInvariant() + "|" + m.Groups[1].Value;
                    double eski; toplam.TryGetValue(k, out eski);
                    toplam[k] = eski + v.Value;
                }
                // Görev Yöneticisi gibi: kartın kullanımı, en yoğun motorunun (3D, video, kopyalama...) kullanımıdır
                foreach (GpuOlcum g in gpular.Values)
                {
                    string on = g.Luid.ToLowerInvariant() + "|";
                    double en = 0;
                    foreach (KeyValuePair<string, double> kv in toplam) if (kv.Key.StartsWith(on) && kv.Value > en) en = kv.Value;
                    g.Kullanim = Math.Min(100, en);
                }
            }
            InstanceDataCollection vram = Sayac(Kategori("GPU Adapter Memory"), "Dedicated Usage");
            if (vram != null)
                foreach (InstanceData d in vram.Values)
                {
                    Match l = LuidDeseni.Match(d.InstanceName);
                    GpuOlcum g;
                    if (l.Success && gpular.TryGetValue(l.Value, out g)) g.VramKullanilanGB = d.RawValue / 1073741824.0;
                }
            // NVIDIA kartlarda ek bilgiler (güç, saat, fan) — iki saniyede bir
            if (nvidiaSmi != null && gpular.Values.Any(g => g.Ad.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (tur % 2 == 1) nvSon = NvidiaOku();
                foreach (GpuOlcum g in gpular.Values)
                {
                    string[] n;
                    if (!nvSon.TryGetValue(g.Ad, out n)) continue;
                    g.Sicaklik = Sayi(n[1]) ?? g.Sicaklik; g.GucW = Sayi(n[2]); g.GucLimitW = Sayi(n[3]); g.SaatMHz = Sayi(n[4]); g.FanYuzde = Sayi(n[5]);
                    double? kul = Sayi(n[6]), top = Sayi(n[7]);
                    if (kul.HasValue) g.VramKullanilanGB = kul / 1024.0;
                    if (top.HasValue) g.VramToplamGB = top / 1024.0;
                }
            }
            o.Gpular = gpular.Values.ToList();

            // Diskler
            InstanceDataCollectionCollection pd = Kategori("PhysicalDisk");
            InstanceDataCollection bos = Sayac(pd, "% Idle Time"), oku = Sayac(pd, "Disk Read Bytes/sec"), yaz = Sayac(pd, "Disk Write Bytes/sec");
            if (bos != null)
                foreach (InstanceData d in bos.Values)
                {
                    if (d.InstanceName == "_Total") continue;
                    double? bv = Hesapla("disk|bos|" + d.InstanceName, d.Sample);
                    double? ov = oku != null && oku.Contains(d.InstanceName) ? Hesapla("disk|oku|" + d.InstanceName, oku[d.InstanceName].Sample) : null;
                    double? yv = yaz != null && yaz.Contains(d.InstanceName) ? Hesapla("disk|yaz|" + d.InstanceName, yaz[d.InstanceName].Sample) : null;
                    if (!bv.HasValue) continue;
                    // örnek adı "1 C:" biçiminde: disk numarası + sürücü harfleri
                    string[] p = d.InstanceName.Split(new[] { ' ' }, 2);
                    int no; string ad = d.InstanceName;
                    if (int.TryParse(p[0], out no))
                    {
                        string harfler = p.Length > 1 ? p[1].Trim() : "";
                        string model; diskAdlari.TryGetValue(no, out model);
                        ad = (harfler.Length > 0 ? harfler + "  " : "") + (model ?? "Disk " + no);
                    }
                    o.Diskler.Add(new DiskOlcum { Ad = ad, Etkinlik = Math.Max(0, Math.Min(100, 100 - bv.Value)), OkumaBps = ov ?? 0, YazmaBps = yv ?? 0 });
                }
            o.Diskler = o.Diskler.OrderBy(x => x.Ad).ToList();

            // Ağ (tüm bağdaştırıcıların toplamı)
            InstanceDataCollectionCollection ag = Kategori("Network Interface");
            InstanceDataCollection al = Sayac(ag, "Bytes Received/sec"), gon = Sayac(ag, "Bytes Sent/sec");
            if (al != null) foreach (InstanceData d in al.Values) { double? v = Hesapla("ag|al|" + d.InstanceName, d.Sample); if (v.HasValue) o.AgIndirBps += v.Value; }
            if (gon != null) foreach (InstanceData d in gon.Values) { double? v = Hesapla("ag|gon|" + d.InstanceName, d.Sample); if (v.HasValue) o.AgYukleBps += v.Value; }

            // Kaybolan sayaç örneklerini (kapanan süreçlerin GPU motorları vb.) unut
            foreach (string k in onceki.Keys.Where(k => !gorulen.Contains(k)).ToList()) onceki.Remove(k);

            if (tur % 2 == 1) o.Surecler = SurecleriOlc();
            return o;
        }

        static double? Sayi(string s)
        {
            double d;
            return double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? (double?)d : null;
        }

        Dictionary<string, string[]> NvidiaOku()
        {
            Dictionary<string, string[]> s = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(nvidiaSmi, "--query-gpu=name,temperature.gpu,power.draw,power.limit,clocks.gr,fan.speed,memory.used,memory.total --format=csv,noheader,nounits")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (Process p = Process.Start(psi))
                {
                    string cikti = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (string satir in cikti.Split('\n'))
                    {
                        string[] parca = satir.Split(',').Select(x => x.Trim()).ToArray();
                        if (parca.Length >= 8) s[parca[0]] = parca;
                    }
                }
            }
            catch { }
            return s;
        }

        // Süreçler: aynı adlı süreçler (ör. tarayıcı sekmeleri) tek satırda toplanır
        List<SurecOlcum> SurecleriOlc()
        {
            DateTime simdi = DateTime.UtcNow;
            double gecen = surecZamani == DateTime.MinValue ? 0 : (simdi - surecZamani).TotalMilliseconds;
            surecZamani = simdi;
            int cekirdek = Environment.ProcessorCount;
            Dictionary<string, SurecOlcum> gruplar = new Dictionary<string, SurecOlcum>(StringComparer.OrdinalIgnoreCase);
            Dictionary<int, TimeSpan> yeni = new Dictionary<int, TimeSpan>();
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0) continue; // Sistem boşta işlemi
                    string ad = p.ProcessName;
                    double cpu = 0;
                    try
                    {
                        TimeSpan t = p.TotalProcessorTime;
                        yeni[p.Id] = t;
                        TimeSpan e;
                        if (gecen > 0 && surecOnceki.TryGetValue(p.Id, out e)) cpu = Math.Max(0, (t - e).TotalMilliseconds / (gecen * cekirdek) * 100.0);
                    }
                    catch { }
                    SurecOlcum s;
                    if (!gruplar.TryGetValue(ad, out s)) { s = new SurecOlcum { Ad = ad }; gruplar[ad] = s; }
                    s.Cpu += cpu; s.RamMB += p.WorkingSet64 / 1048576.0; s.Adet++;
                }
                catch { }
                finally { p.Dispose(); }
            }
            surecOnceki.Clear();
            foreach (KeyValuePair<int, TimeSpan> kv in yeni) surecOnceki[kv.Key] = kv.Value;
            return gruplar.Values.ToList();
        }

        // Süreç adı yerine anlaşılır uygulama adı (ör. "chrome" → "Google Chrome")
        public string UygulamaAdi(string surec)
        {
            string a;
            if (surecAciklama.TryGetValue(surec, out a)) return a;
            a = surec;
            try
            {
                foreach (Process p in Process.GetProcessesByName(surec))
                {
                    try
                    {
                        string d = p.MainModule.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrWhiteSpace(d) && d.Length <= 40) { a = d.Trim(); break; }
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            if (surec.Equals("System", StringComparison.OrdinalIgnoreCase)) a = "Windows çekirdeği (System)";
            if (surec.Equals("Memory Compression", StringComparison.OrdinalIgnoreCase)) a = "Windows bellek sıkıştırma";
            surecAciklama[surec] = a;
            return a;
        }
    }

    // ------------------------------------------------------------------ canlı kart (her şey tek parça çizilir, titreme olmaz)
    class CanliKart : Panel
    {
        public class Satir { public string Sol, Sag; public double? Cubuk; public Color? Renk; }

        public string Baslik = "", Buyuk = "—", Alt = "", Not = "";
        public Color Renk = Tema.Vurgu, Renk2 = Tema.Turuncu;
        public readonly List<double> Gecmis = new List<double>(), Gecmis2 = new List<double>();
        public bool Otomatik = false;          // false: grafik 0-100 ölçeğinde, true: en büyük değere göre
        public string GrafikEtiketi = "son 60 saniye";
        public List<Satir> Satirlar = new List<Satir>();
        readonly int satirSayisi, bosluk = Tema.O(10);
        readonly bool grafikli;
        static readonly Font BuyukFont = new Font("Segoe UI Semibold", 20f);

        public CanliKart(string baslik, int satirSayisi, bool grafikli)
        {
            Tema.CizimAyari(this);
            BackColor = Tema.Arka;
            Baslik = baslik;
            this.satirSayisi = satirSayisi;
            this.grafikli = grafikli;
            Height = GerekenYukseklik;
        }

        public int GerekenYukseklik
        {
            get { return Tema.O(84) + (grafikli ? Tema.O(78) : 0) + satirSayisi * Tema.O(23) + Tema.O(30) + bosluk; }
        }

        public void Ekle(double v, double? v2)
        {
            Gecmis.Add(v); if (Gecmis.Count > 60) Gecmis.RemoveAt(0);
            if (v2.HasValue) { Gecmis2.Add(v2.Value); if (Gecmis2.Count > 60) Gecmis2.RemoveAt(0); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Tema.KartCiz(g, new Rectangle(0, 0, Width - 1, Height - 1 - bosluk), Tema.O(10));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int x = Tema.O(20), w = Width - Tema.O(40);
            TextRenderer.DrawText(g, Baslik, Tema.Kalin, new Point(x, Tema.O(14)), Tema.Soluk, Tema.Kart);
            Size bs = TextRenderer.MeasureText(Buyuk, BuyukFont);
            TextRenderer.DrawText(g, Buyuk, BuyukFont, new Point(x - Tema.O(3), Tema.O(36)), Tema.Metin, Tema.Kart);
            TextRenderer.DrawText(g, Alt, Tema.Normal, new Rectangle(x + bs.Width + Tema.O(4), Tema.O(48), w - bs.Width, Tema.O(22)), Tema.Soluk, Tema.Kart, TextFormatFlags.EndEllipsis | TextFormatFlags.Left);

            int y = Tema.O(84);
            if (grafikli)
            {
                Rectangle a = new Rectangle(x, y, w, Tema.O(64));
                using (Pen izgara = new Pen(Tema.Izgara))
                    for (int i = 0; i <= 2; i++) g.DrawLine(izgara, a.X, a.Y + a.Height * i / 2, a.Right, a.Y + a.Height * i / 2);
                double maks = 100;
                if (Otomatik) maks = Math.Max(1, Math.Max(Gecmis.Count > 0 ? Gecmis.Max() : 0, Gecmis2.Count > 0 ? Gecmis2.Max() : 0) * 1.15);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Cizgi(g, a, Gecmis2, maks, Renk2, false);
                Cizgi(g, a, Gecmis, maks, Renk, true);
                TextRenderer.DrawText(g, GrafikEtiketi, Tema.Kucuk, new Rectangle(a.X, a.Bottom + Tema.O(1), a.Width, Tema.O(14)), Tema.SolukIkincil, Tema.Kart, TextFormatFlags.Right);
                y += Tema.O(80);
            }

            foreach (Satir s in Satirlar.Take(satirSayisi))
            {
                // Çubuklu satır: [ad | çubuk | değer] sütunları çakışmasın diye değerin genişliği önce ölçülür
                int sagW = TextRenderer.MeasureText(s.Sag ?? "", Tema.Normal).Width;
                int cubukW = Math.Max(Tema.O(40), Math.Min((int)(w * 0.22), w - sagW - Tema.O(120)));
                int solW = s.Cubuk.HasValue ? Math.Max(Tema.O(60), w - sagW - cubukW - Tema.O(20)) : w - sagW - Tema.O(10);
                TextRenderer.DrawText(g, s.Sol ?? "", Tema.Normal, new Rectangle(x, y, solW, Tema.O(22)), Tema.Soluk, Tema.Kart, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                if (s.Cubuk.HasValue)
                {
                    Rectangle c = new Rectangle(x + solW + Tema.O(8), y + Tema.O(8), cubukW, Tema.O(7));
                    using (SolidBrush arka = new SolidBrush(Tema.HalkaArka)) g.FillRectangle(arka, c);
                    double v = Math.Max(0, Math.Min(100, s.Cubuk.Value));
                    Color r = s.Renk ?? (v >= 90 ? Tema.Kirmizi : v >= 75 ? Tema.Sari : Renk);
                    using (SolidBrush on = new SolidBrush(r)) g.FillRectangle(on, c.X, c.Y, (int)(c.Width * v / 100), c.Height);
                }
                TextRenderer.DrawText(g, s.Sag ?? "", Tema.Normal, new Rectangle(x, y, w, Tema.O(22)), Tema.Metin, Tema.Kart, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                y += Tema.O(23);
            }
            if (!string.IsNullOrEmpty(Not))
                TextRenderer.DrawText(g, Not, Tema.Kucuk, new Rectangle(x, Height - bosluk - Tema.O(26), w, Tema.O(18)), Tema.SolukIkincil, Tema.Kart, TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
        }

        static void Cizgi(Graphics g, Rectangle a, List<double> d, double maks, Color renk, bool dolgu)
        {
            if (d.Count < 2) return;
            PointF[] n = new PointF[d.Count];
            for (int i = 0; i < d.Count; i++)
            {
                float px = a.Right - (float)a.Width * (d.Count - 1 - i) / 59f;
                float py = a.Bottom - (float)(Math.Max(0, Math.Min(maks, d[i])) / maks * a.Height);
                n[i] = new PointF(px, py);
            }
            if (dolgu)
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddLines(n);
                    p.AddLine(n[n.Length - 1], new PointF(n[n.Length - 1].X, a.Bottom));
                    p.AddLine(new PointF(n[n.Length - 1].X, a.Bottom), new PointF(n[0].X, a.Bottom));
                    p.CloseFigure();
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(40, renk))) g.FillPath(b, p);
                }
            using (Pen p = new Pen(renk, Tema.O(2)) { LineJoin = LineJoin.Round }) g.DrawLines(p, n);
        }
    }

    // İşlemci çekirdeklerinin kullanımı (dikey çubuklar)
    class CekirdekKarti : Panel
    {
        public double[] Degerler = new double[0];
        readonly int bosluk = Tema.O(10);

        public CekirdekKarti()
        {
            Tema.CizimAyari(this);
            BackColor = Tema.Arka;
            Height = Tema.O(170);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Tema.KartCiz(g, new Rectangle(0, 0, Width - 1, Height - 1 - bosluk), Tema.O(10));
            int x = Tema.O(20), w = Width - Tema.O(40);
            TextRenderer.DrawText(g, "İşlemci çekirdekleri (" + Degerler.Length + " izlek)", Tema.Kalin, new Point(x, Tema.O(14)), Tema.Soluk, Tema.Kart);
            if (Degerler.Length == 0) return;
            int ust = Tema.O(46), alt = Height - bosluk - Tema.O(30), h = alt - ust;
            float aralik = (float)w / Degerler.Length;
            float cw = Math.Max(2, aralik * 0.7f);
            for (int i = 0; i < Degerler.Length; i++)
            {
                double v = Math.Max(0, Math.Min(100, Degerler[i]));
                float cx = x + i * aralik + (aralik - cw) / 2;
                using (SolidBrush b = new SolidBrush(Tema.HalkaArka)) g.FillRectangle(b, cx, ust, cw, h);
                Color r = v >= 90 ? Tema.Kirmizi : v >= 70 ? Tema.Sari : Tema.Vurgu;
                float vh = (float)(h * v / 100);
                using (SolidBrush b = new SolidBrush(r)) g.FillRectangle(b, cx, alt - vh, cw, vh);
                if (aralik >= Tema.O(18))
                    TextRenderer.DrawText(g, (i + 1).ToString(), Tema.Kucuk, new Rectangle((int)(x + i * aralik), alt + Tema.O(2), (int)aralik, Tema.O(16)), Tema.SolukIkincil, Tema.Kart, TextFormatFlags.HorizontalCenter);
            }
        }
    }
}
