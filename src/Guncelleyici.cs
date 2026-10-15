using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// Guncelleyici.exe: GitHub'daki son sürümü denetler, yalnızca değişen dosyaları indirir,
// SHA256 ile doğrular ve yerine koyar. Kendi exe'sini de güncelleyebilir.
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

        public GuncelleyiciForm(bool otomatik)
        {
            this.otomatik = otomatik;
            Text = "Sistem Checkup Güncelleyici";
            Font = new Font("Segoe UI", 9.75f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(540, 430);
            MinimumSize = new Size(480, 380);
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
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblKurulu.AutoSize = true; lblKurulu.Margin = new Padding(0, 0, 0, 4);
            lblSon.AutoSize = true; lblSon.Margin = new Padding(0, 0, 0, 10);
            lblSon.Font = new Font("Segoe UI Semibold", 9.75f);

            txtNotlar.Multiline = true; txtNotlar.ReadOnly = true; txtNotlar.ScrollBars = ScrollBars.Vertical;
            txtNotlar.Dock = DockStyle.Fill; txtNotlar.BackColor = Color.FromArgb(246, 247, 250); txtNotlar.BorderStyle = BorderStyle.FixedSingle;

            bar.Dock = DockStyle.Fill; bar.Height = 14; bar.Margin = new Padding(0, 12, 0, 6);
            lblDurum.AutoSize = true; lblDurum.ForeColor = Color.DimGray; lblDurum.Margin = new Padding(0, 0, 0, 10);

            FlowLayoutPanel butonlar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0) };
            Butonla(btnGuncelle, "Güncelle", true);
            Butonla(btnBaslat, "Sistem Checkup'ı başlat", false);
            Butonla(btnDenetle, "Yeniden denetle", false);
            butonlar.Controls.Add(btnGuncelle);
            butonlar.Controls.Add(btnBaslat);
            butonlar.Controls.Add(btnDenetle);

            lnkRepo.Text = "GitHub sayfası";
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

        void KuruluGoster()
        {
            Manifest m = Ortak.YerelManifest();
            lblKurulu.Text = "Kurulu sürüm: " + (m != null ? m.surum + (string.IsNullOrEmpty(m.tarih) ? "" : "  (" + m.tarih + ")") : "kurulu değil");
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
            btnBaslat.Enabled = !m && File.Exists(Ortak.BaslaticiYolu);
            btnGuncelle.Enabled = !m && degisen.Count > 0;
            // Düz (flat) butonlar devre dışıyken renk değiştirmediği için elle soluklaştır
            btnGuncelle.BackColor = btnGuncelle.Enabled ? Vurgu : Color.FromArgb(178, 189, 232);
            btnGuncelle.FlatAppearance.BorderColor = btnGuncelle.BackColor;
            UseWaitCursor = m;
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

            lblSon.Text = "En son sürüm: " + uzak.surum + (string.IsNullOrEmpty(uzak.tarih) ? "" : "  (" + uzak.tarih + ")");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Sürüm " + uzak.surum + " notları:");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrEmpty(uzak.notlar) ? "(not yok)" : uzak.notlar.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            if (degisen.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("İndirilecek dosyalar:");
                foreach (DosyaBilgi d in degisen) sb.AppendLine("  • " + d.hedef + "  (" + Boyut(d.boyut) + ")");
            }
            txtNotlar.Text = sb.ToString();

            bool yeniSurum = Ortak.YeniMi(uzak.surum, Ortak.YerelSurum());
            if (degisen.Count == 0)
            {
                Durum("Her şey güncel ✓", Yesil);
                if (yeniSurum) SurumuKaydet(); // dosyalar zaten aynı, yalnızca sürüm bilgisini yaz
            }
            else
            {
                long toplam = degisen.Sum(d => d.boyut);
                Durum((yeniSurum ? "Yeni sürüm hazır" : "Eksik veya bozuk dosyalar onarılacak") + " — " + degisen.Count + " dosya, " + Boyut(toplam), Vurgu);
                if (otomatik) { otomatik = false; await Guncelle(); }
            }
        }

        static List<DosyaBilgi> DegisenDosyalar(Manifest m)
        {
            List<DosyaBilgi> liste = new List<DosyaBilgi>();
            foreach (DosyaBilgi d in m.dosyalar ?? new List<DosyaBilgi>())
            {
                string yol = Ortak.HedefYol(d);
                if (!File.Exists(yol) || !string.Equals(Ortak.Sha256(yol), d.sha256, StringComparison.OrdinalIgnoreCase)) liste.Add(d);
            }
            return liste;
        }

        static bool Yazilabilir()
        {
            try
            {
                string test = Path.Combine(Ortak.KokDizin, ".yazma_testi");
                File.WriteAllText(test, "1");
                File.Delete(test);
                return true;
            }
            catch { return false; }
        }

        async Task Guncelle()
        {
            if (uzak == null || degisen.Count == 0) return;

            if (!Yazilabilir())
            {
                // Program Files gibi korumalı bir klasördeyiz: yönetici olarak yeniden aç
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--otomatik") { Verb = "runas", UseShellExecute = true };
                    Process.Start(psi);
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
            long toplam = Math.Max(1, liste.Sum(d => d.boyut));
            bool kendisi = false;
            try
            {
                Directory.CreateDirectory(gecici);
                long biten = 0;
                for (int i = 0; i < liste.Count; i++)
                {
                    DosyaBilgi d = liste[i];
                    Durum("İndiriliyor (" + (i + 1) + "/" + liste.Count + "): " + d.hedef, Color.DimGray);
                    string tmp = Path.Combine(gecici, i + "_" + Path.GetFileName(d.hedef));
                    long once = biten;
                    await Task.Run(() => Ortak.Indir(Ortak.DosyaUrl(m, d), tmp, n =>
                        BeginInvoke((Action)(() => bar.Value = (int)Math.Min(100, 100 * (once + n) / toplam)))));
                    string hash = await Task.Run(() => Ortak.Sha256(tmp));
                    if (!string.Equals(hash, d.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(d.hedef + " doğrulanamadı (SHA256 uyuşmuyor). Hiçbir dosya değiştirilmedi.");
                    biten += d.boyut;
                }

                Durum("Dosyalar yerine koyuluyor...", Color.DimGray);
                for (int i = 0; i < liste.Count; i++)
                {
                    DosyaBilgi d = liste[i];
                    string hedef = Ortak.HedefYol(d);
                    string tmp = Path.Combine(gecici, i + "_" + Path.GetFileName(d.hedef));
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
                    File.Move(tmp, hedef);
                }
                SurumuKaydet();
                bar.Value = 100;
                degisen = new List<DosyaBilgi>();
                txtNotlar.Text = txtNotlar.Text.Replace("İndirilecek dosyalar:", "Güncellenen dosyalar:");
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
            if (!File.Exists(Ortak.BaslaticiYolu)) { Durum("SistemCheckup.exe bulunamadı. Önce güncelleyin.", Kirmizi); return; }
            Ortak.Ac(Ortak.BaslaticiYolu, null);
            Close();
        }

        static string Boyut(long b)
        {
            if (b >= 1048576) return (b / 1048576.0).ToString("0.0") + " MB";
            return Math.Max(1, b / 1024) + " KB";
        }
    }
}
