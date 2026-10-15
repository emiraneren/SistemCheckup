using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// Guncelleyici.exe: GitHub'daki son sürümü denetler. Release'teki zip paketini indirir, paketin ve içinden
// alınan her dosyanın SHA256 değerini doğrular ve yalnızca değişen dosyaları yerine koyar.
// Kendi exe'sini de güncelleyebilir (çalışan exe yeniden adlandırılıp yenisi konur).
namespace SistemCheckup
{
    static class GuncelleyiciProgram
    {
        [STAThread]
        static void Main(string[] args)
        {
            Ortak.Hazirla();
            Ortak.EskileriTemizle();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GuncelleyiciForm(args.Contains("--otomatik")));
        }
    }

    class GuncelleyiciForm : Form
    {
        readonly Label lblKurulu = new Label();
        readonly Label lblSon = new Label();
        readonly Label lblDurum = new Label();
        readonly TextBox txtNotlar = new TextBox();
        readonly ProgressBar bar = new ProgressBar();
        readonly Button btnDenetle = new Button();
        readonly Button btnGuncelle = new Button();
        readonly Button btnBaslat = new Button();
        readonly LinkLabel lnkRepo = new LinkLabel();

        Manifest uzak;
        List<DosyaBilgi> degisen = new List<DosyaBilgi>();
        bool otomatik;
        bool mesgul;

        static readonly Color Vurgu = Color.FromArgb(58, 91, 217);
        static readonly Color Yesil = Color.FromArgb(31, 138, 76);
        static readonly Color Kirmizi = Color.FromArgb(198, 47, 47);
        static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        public GuncelleyiciForm(bool otomatik)
        {
            this.otomatik = otomatik;
            Text = "Sistem Checkup Güncelleyici";
            Font = new Font("Segoe UI", 9.75f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(560, 470);
            MinimumSize = new Size(480, 400);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel ust = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Vurgu, Padding = new Padding(18, 0, 18, 0) };
            Label baslik = new Label { Text = "Sistem Checkup Güncelleyici", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 14f), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            ust.Controls.Add(baslik);

            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 14), ColumnCount = 1, RowCount = 7 };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblKurulu.AutoSize = true; lblKurulu.Margin = new Padding(0, 0, 0, 4);
            lblSon.AutoSize = true; lblSon.Margin = new Padding(0, 0, 0, 10);
            lblSon.Font = new Font("Segoe UI Semibold", 9.75f);

            txtNotlar.Multiline = true; txtNotlar.ReadOnly = true; txtNotlar.ScrollBars = ScrollBars.Vertical;
            txtNotlar.Dock = DockStyle.Fill; txtNotlar.BackColor = Color.FromArgb(246, 247, 250); txtNotlar.BorderStyle = BorderStyle.FixedSingle;

            bar.Dock = DockStyle.Fill; bar.Height = 14; bar.Margin = new Padding(0, 12, 0, 6);
            lblDurum.AutoSize = true; lblDurum.ForeColor = Color.DimGray; lblDurum.Margin = new Padding(0, 0, 0, 10);

            FlowLayoutPanel butonlar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0) };
            Butonla(btnGuncelle, "Güncelle", true);
            Butonla(btnBaslat, "Sistem Checkup'ı aç", false);
            Butonla(btnDenetle, "Yeniden denetle", false);
            butonlar.Controls.Add(btnGuncelle);
            butonlar.Controls.Add(btnBaslat);
            butonlar.Controls.Add(btnDenetle);

            lnkRepo.Text = "Tüm sürümler (GitHub)";
            lnkRepo.AutoSize = true;
            lnkRepo.Margin = new Padding(0, 8, 0, 0);
            lnkRepo.LinkClicked += (s, e) => Ortak.Ac(Ortak.RepoUrl + "/releases", null);

            t.Controls.Add(lblKurulu, 0, 0);
            t.Controls.Add(lblSon, 0, 1);
            t.Controls.Add(txtNotlar, 0, 2);
            t.Controls.Add(bar, 0, 3);
            t.Controls.Add(lblDurum, 0, 4);
            t.Controls.Add(butonlar, 0, 5);
            t.Controls.Add(lnkRepo, 0, 6);

            Controls.Add(t);
            Controls.Add(ust);

            btnDenetle.Click += async (s, e) => await Denetle();
            btnGuncelle.Click += async (s, e) => await Guncelle();
            btnBaslat.Click += (s, e) => Baslat();
            Shown += async (s, e) => await Denetle();
            FormClosing += (s, e) => { if (mesgul) { e.Cancel = true; Durum("Güncelleme sürerken kapatılamaz.", Kirmizi); } };

            KuruluGoster();
        }

        void Butonla(Button b, string metin, bool ana)
        {
            b.Text = metin;
            b.AutoSize = true;
            b.Padding = new Padding(10, 4, 10, 4);
            b.Margin = new Padding(8, 0, 0, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = ana ? Vurgu : Color.FromArgb(205, 209, 217);
            b.BackColor = ana ? Vurgu : Color.White;
            b.ForeColor = ana ? Color.White : Color.FromArgb(22, 24, 29);
            b.Cursor = Cursors.Hand;
        }

        static string GuzelTarih(string t)
        {
            DateTime d;
            return DateTime.TryParseExact(t ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("d MMMM yyyy", Tr) : (t ?? "");
        }

        void KuruluGoster()
        {
            Manifest m = Ortak.YerelManifest();
            lblKurulu.Text = "Kurulu sürüm: " + (m != null ? m.surum + (string.IsNullOrEmpty(m.tarih) ? "" : "  (" + GuzelTarih(m.tarih) + ")") : "bilinmiyor");
        }

        void Durum(string metin, Color renk)
        {
            lblDurum.Text = metin;
            lblDurum.ForeColor = renk;
        }

        void Mesgul(bool m)
        {
            mesgul = m;
            btnDenetle.Enabled = !m;
            btnBaslat.Enabled = !m && File.Exists(Ortak.AnaUygulamaYolu);
            btnGuncelle.Enabled = !m && degisen.Count > 0;
            btnGuncelle.BackColor = btnGuncelle.Enabled ? Vurgu : Color.FromArgb(178, 189, 232);
            btnGuncelle.FlatAppearance.BorderColor = btnGuncelle.BackColor;
            UseWaitCursor = m;
        }

        // Kurulu sürümden sonraki tüm sürümlerin notlarını, en yeniden eskiye anlaşılır şekilde listeler.
        string NotMetni(Manifest m, string yerel)
        {
            List<SurumNotu> gecmis = m.gecmis ?? new List<SurumNotu>();
            if (gecmis.Count == 0) gecmis.Add(new SurumNotu { surum = m.surum, tarih = m.tarih, notlar = m.notlar });
            List<SurumNotu> yeniler = gecmis.Where(g => Ortak.YeniMi(g.surum, yerel)).ToList();
            bool guncel = yeniler.Count == 0;
            if (guncel) yeniler = gecmis.Take(1).ToList();

            StringBuilder sb = new StringBuilder();
            if (!guncel && yeniler.Count > 1) sb.AppendLine("Kurulu sürümünüzden bu yana " + yeniler.Count + " güncelleme yayınlandı. Neler değişti:\r\n");
            else if (!guncel) sb.AppendLine("Bu güncellemede neler değişti:\r\n");
            else sb.AppendLine("Son sürümdeki değişiklikler:\r\n");
            foreach (SurumNotu g in yeniler.Take(10))
            {
                sb.AppendLine("Sürüm " + g.surum + "  ·  " + GuzelTarih(g.tarih));
                foreach (string n in g.notlar ?? new List<string>()) sb.AppendLine("   • " + n);
                sb.AppendLine();
            }
            if (yeniler.Count > 10) sb.AppendLine("... ve " + (yeniler.Count - 10) + " eski sürüm daha.");
            return sb.ToString();
        }

        async Task Denetle()
        {
            degisen = new List<DosyaBilgi>();
            Mesgul(true);
            bar.Style = ProgressBarStyle.Marquee;
            Durum("GitHub'dan son sürüm bilgisi alınıyor...", Color.DimGray);
            lblSon.Text = "En son sürüm: denetleniyor...";
            try
            {
                uzak = await Task.Run(() => Ortak.UzakManifest(20000));
                degisen = await Task.Run(() => DegisenDosyalar(uzak));
            }
            catch (Exception ex)
            {
                uzak = null;
                lblSon.Text = "En son sürüm: alınamadı";
                Durum("GitHub'a ulaşılamadı: " + ex.Message, Kirmizi);
                txtNotlar.Text = "İnternet bağlantınızı kontrol edip \"Yeniden denetle\"ye basın.";
                return;
            }
            finally
            {
                bar.Style = ProgressBarStyle.Continuous;
                bar.Value = 0;
                Mesgul(false);
            }

            string yerel = Ortak.YerelSurum();
            lblSon.Text = "En son sürüm: " + uzak.surum + (string.IsNullOrEmpty(uzak.tarih) ? "" : "  (" + GuzelTarih(uzak.tarih) + ")");
            string notlar = NotMetni(uzak, yerel);
            if (degisen.Count > 0)
                notlar += "Güncellenecek dosyalar:\r\n" + string.Join("\r\n", degisen.Select(d => "   " + d.hedef + "  (" + Boyut(d.boyut) + ")")) + "\r\n";
            txtNotlar.Text = notlar;

            bool yeniSurum = Ortak.YeniMi(uzak.surum, yerel);
            if (degisen.Count == 0)
            {
                Durum("Her şey güncel ✓", Yesil);
                if (yeniSurum) SurumuKaydet(); // dosyalar zaten aynı, yalnızca sürüm bilgisini yaz
            }
            else
            {
                Durum((yeniSurum ? "Yeni sürüm hazır" : "Eksik veya bozuk dosyalar onarılacak") + " · indirme boyutu " + Boyut(uzak.paket != null ? uzak.paket.boyut : 0), Vurgu);
                if (otomatik) { otomatik = false; await Guncelle(); }
            }
        }

        static List<DosyaBilgi> DegisenDosyalar(Manifest m)
        {
            List<DosyaBilgi> liste = new List<DosyaBilgi>();
            foreach (DosyaBilgi d in m.dosyalar ?? new List<DosyaBilgi>())
            {
                string yol = Ortak.HedefYol(d.hedef);
                if (!File.Exists(yol) || !string.Equals(Ortak.Sha256(yol), d.sha256, StringComparison.OrdinalIgnoreCase)) liste.Add(d);
            }
            return liste;
        }

        // Zip içindeki yolu (ör. "SistemCheckup\app\x.ps1") manifestteki biçime ("app/x.ps1") çevirir.
        static string ZipYolu(ZipArchiveEntry e)
        {
            string ad = e.FullName.Replace('\\', '/');
            int i = ad.IndexOf('/');
            return i >= 0 && ad.Substring(0, i).Equals("SistemCheckup", StringComparison.OrdinalIgnoreCase) ? ad.Substring(i + 1) : ad;
        }

        async Task Guncelle()
        {
            if (uzak == null || degisen.Count == 0 || uzak.paket == null) return;

            if (!Ortak.YazilabilirMi())
            {
                // Program Files gibi korumalı bir klasördeyiz: yönetici olarak yeniden aç
                try
                {
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--otomatik") { Verb = "runas", UseShellExecute = true });
                    mesgul = false;
                    Close();
                }
                catch (Win32Exception) { Durum("Bu klasöre yazmak için yönetici izni gerekiyor.", Kirmizi); }
                return;
            }

            Mesgul(true);
            btnGuncelle.Enabled = false;
            string gecici = Path.Combine(Path.GetTempPath(), "SistemCheckup_yama_" + Guid.NewGuid().ToString("N"));
            List<DosyaBilgi> liste = degisen;
            Manifest m = uzak;
            bool kendisi = false;
            try
            {
                Directory.CreateDirectory(gecici);
                string zip = Path.Combine(gecici, "paket.zip");
                long toplam = Math.Max(1, m.paket.boyut);
                Durum("İndiriliyor: " + m.paket.ad + " (" + Boyut(m.paket.boyut) + ")", Color.DimGray);
                await Task.Run(() => Ortak.Indir(Ortak.PaketUrl(m), zip, n =>
                    BeginInvoke((Action)(() => bar.Value = (int)Math.Min(90, 90 * n / toplam)))));
                string paketHash = await Task.Run(() => Ortak.Sha256(zip));
                if (!string.Equals(paketHash, m.paket.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("İndirilen paket doğrulanamadı (SHA256 uyuşmuyor). Hiçbir dosya değiştirilmedi.");

                Durum("Paket doğrulandı, dosyalar çıkarılıyor...", Color.DimGray);
                Dictionary<string, string> cikan = await Task.Run(() =>
                {
                    Dictionary<string, string> sonuc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using (ZipArchive arsiv = ZipFile.OpenRead(zip))
                    {
                        for (int i = 0; i < liste.Count; i++)
                        {
                            DosyaBilgi d = liste[i];
                            ZipArchiveEntry giris = arsiv.Entries.FirstOrDefault(e => string.Equals(ZipYolu(e), d.hedef, StringComparison.OrdinalIgnoreCase));
                            if (giris == null) throw new InvalidDataException(d.hedef + " pakette bulunamadı. Hiçbir dosya değiştirilmedi.");
                            string tmp = Path.Combine(gecici, i + "_" + Path.GetFileName(d.hedef));
                            giris.ExtractToFile(tmp, true);
                            if (!string.Equals(Ortak.Sha256(tmp), d.sha256, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException(d.hedef + " doğrulanamadı (SHA256 uyuşmuyor). Hiçbir dosya değiştirilmedi.");
                            sonuc[d.hedef] = tmp;
                        }
                    }
                    return sonuc;
                });
                bar.Value = 95;

                Durum("Dosyalar yerine koyuluyor...", Color.DimGray);
                foreach (DosyaBilgi d in liste)
                {
                    string hedef = Ortak.HedefYol(d.hedef);
                    Directory.CreateDirectory(Path.GetDirectoryName(hedef));
                    if (File.Exists(hedef))
                    {
                        if (hedef.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            // Çalışan bir exe silinemez ama yeniden adlandırılabilir; .old bir sonraki açılışta silinir.
                            File.Move(hedef, hedef + "." + DateTime.Now.Ticks + ".old");
                            if (string.Equals(Path.GetFullPath(hedef), Path.GetFullPath(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase)) kendisi = true;
                        }
                        else File.Delete(hedef);
                    }
                    File.Move(cikan[d.hedef], hedef);
                }
                SurumuKaydet();
                bar.Value = 100;
                degisen = new List<DosyaBilgi>();
                txtNotlar.Text = txtNotlar.Text.Replace("Güncellenecek dosyalar:", "Güncellenen dosyalar:");
                KuruluGoster();
                Durum("Güncelleme tamamlandı ✓  Sürüm " + m.surum + (kendisi ? " (güncelleyici de yenilendi)" : ""), Yesil);
            }
            catch (Exception ex)
            {
                Durum("Güncelleme başarısız: " + ex.Message, Kirmizi);
                bar.Value = 0;
            }
            finally
            {
                try { Directory.Delete(gecici, true); } catch { }
                Mesgul(false);
            }
        }

        void SurumuKaydet()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Ortak.YerelManifestYolu));
            File.WriteAllText(Ortak.YerelManifestYolu, Ortak.Serilestir(uzak), new UTF8Encoding(false));
            KuruluGoster();
        }

        void Baslat()
        {
            if (!File.Exists(Ortak.AnaUygulamaYolu)) { Durum("SistemCheckup.exe bulunamadı. Önce güncelleyin.", Kirmizi); return; }
            Ortak.Ac(Ortak.AnaUygulamaYolu, null);
            Close();
        }

        static string Boyut(long b)
        {
            if (b >= 1048576) return (b / 1048576.0).ToString("0.0", Tr) + " MB";
            return Math.Max(1, b / 1024) + " KB";
        }
    }
}
