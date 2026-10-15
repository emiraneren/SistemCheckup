using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// SistemCheckup.exe: pencereli ana uygulama. Taramayı (app\SistemCheckup.ps1) arka planda çalıştırır,
// ilerlemeyi ilerleme dosyasından okur ve sonuçları gösterir.
namespace SistemCheckup
{
    static class AnaProgram
    {
        [STAThread]
        static void Main()
        {
            Ortak.Hazirla();
            Ortak.EskileriTemizle();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Ortak.ZipIcindenCalisiyor())
            {
                MessageBox.Show("Sistem Checkup zip dosyasının içinden açıldı, bu şekilde çalışamaz.\n\n" +
                    "1. İndirdiğiniz zip dosyasına sağ tıklayın → \"Tümünü ayıkla...\"\n" +
                    "2. Çıkan klasördeki SistemCheckup.exe'yi açın.",
                    "Önce zip'i çıkarın", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(Path.Combine(Ortak.AppDizin, "SistemCheckup.ps1")))
            {
                if (MessageBox.Show("Uygulama dosyaları eksik (app klasörü bulunamadı).\n\nGüncelleyici açılıp eksik dosyalar indirilsin mi?",
                    Ortak.UygulamaAdi, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes && File.Exists(Ortak.GuncelleyiciYolu))
                    Ortak.Ac(Ortak.GuncelleyiciYolu, "--otomatik");
                return;
            }
            Application.Run(new AnaForm());
        }
    }

    // ------------------------------------------------------------------ tema ve yardımcılar
    static class Tema
    {
        public static readonly Color Arka = Color.FromArgb(244, 245, 247);
        public static readonly Color Kart = Color.White;
        public static readonly Color Kenar = Color.FromArgb(222, 226, 233);
        public static readonly Color Metin = Color.FromArgb(22, 24, 29);
        public static readonly Color Soluk = Color.FromArgb(100, 107, 120);
        public static readonly Color Vurgu = Color.FromArgb(58, 91, 217);
        public static readonly Color Yesil = Color.FromArgb(31, 138, 76);
        public static readonly Color Sari = Color.FromArgb(178, 106, 0);
        public static readonly Color Kirmizi = Color.FromArgb(198, 47, 47);
        public static readonly Color Mavi = Color.FromArgb(47, 99, 184);
        public static readonly Color HalkaArka = Color.FromArgb(232, 235, 242);

        public static float Olcek = 1f;
        public static int O(int px) { return (int)Math.Round(px * Olcek); }

        public static Font Normal = new Font("Segoe UI", 9.75f);
        public static Font Kucuk = new Font("Segoe UI", 8.25f);
        public static Font Kalin = new Font("Segoe UI Semibold", 10.5f);
        public static Font Baslik = new Font("Segoe UI Semibold", 16f);
        public static Font AltBaslik = new Font("Segoe UI Semibold", 12f);

        public static Color SeviyeRengi(string s)
        {
            switch (s) { case "bad": return Kirmizi; case "warn": return Sari; case "info": return Mavi; default: return Yesil; }
        }
        public static string SeviyeAdi(string s)
        {
            switch (s) { case "bad": return "Sorun"; case "warn": return "Uyarı"; case "info": return "Bilgi"; default: return "İyi"; }
        }
        public static Color PuanRengi(int p) { return p >= 85 ? Yesil : p >= 60 ? Sari : Kirmizi; }
        public static string PuanSozu(int p) { return p >= 90 ? "Çok iyi" : p >= 75 ? "İyi" : p >= 50 ? "Dikkat" : "Sorunlu"; }

        public static Button Buton(string metin, bool ana)
        {
            Button b = new Button();
            b.Text = metin;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(O(12), O(5), O(12), O(5));
            b.FlatStyle = FlatStyle.Flat;
            b.Font = ana ? Kalin : Normal;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            Boya(b, ana);
            b.EnabledChanged += (s, e) => Boya(b, ana);
            return b;
        }

        static void Boya(Button b, bool ana)
        {
            if (ana)
            {
                b.BackColor = b.Enabled ? Vurgu : Color.FromArgb(178, 189, 232);
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = b.BackColor;
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = b.Enabled ? Metin : Soluk;
                b.FlatAppearance.BorderColor = Color.FromArgb(205, 209, 217);
            }
        }

        public static GraphicsPath Yuvarlak(Rectangle r, int yaricap)
        {
            GraphicsPath p = new GraphicsPath();
            int d = yaricap * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Dock=Top ile yukarıdan aşağı sırayla dizer (WinForms son ekleneni en üste yerleştirir).
        public static void Diz(Control kap, params Control[] ogeler)
        {
            for (int i = ogeler.Length - 1; i >= 0; i--)
            {
                ogeler[i].Dock = DockStyle.Top;
                kap.Controls.Add(ogeler[i]);
            }
        }

        public static Panel Bosluk(int px) { return new Panel { Height = O(px), BackColor = Color.Transparent }; }
    }

    // Genişliğe göre satır kaydırıp yüksekliğini kendisi ayarlayan etiket
    class Yazi : Label
    {
        public Yazi(string metin, Font font, Color renk)
        {
            AutoSize = false;
            Text = metin;
            Font = font;
            ForeColor = renk;
            BackColor = Color.Transparent;
            UseMnemonic = false;
        }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Yukseklik(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Yukseklik(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Yukseklik(); }
        void Yukseklik()
        {
            if (Width <= 0) return;
            Size s = TextRenderer.MeasureText(Text ?? "", Font, new Size(Math.Max(1, Width - Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = (string.IsNullOrEmpty(Text) ? 0 : s.Height) + Padding.Vertical;
            if (Height != h) Height = h;
        }
    }

    // Sonuçlardaki bir bulgu kartı
    class BulguKarti : Panel
    {
        readonly Color renk;
        readonly Panel metin = new Panel();
        readonly Panel sag = new Panel();
        readonly int bosluk = Tema.O(8);

        public BulguKarti(string seviye, string etiket, string baslik, string detay, string link, string linkMetin)
        {
            renk = Tema.SeviyeRengi(seviye);
            BackColor = Tema.Arka;
            DoubleBuffered = true;
            Padding = new Padding(Tema.O(18), Tema.O(10), Tema.O(12), Tema.O(10) + bosluk);

            Yazi y1 = new Yazi(etiket.ToUpper(new System.Globalization.CultureInfo("tr-TR")), Tema.Kucuk, Tema.Soluk) { BackColor = Tema.Kart };
            Yazi y2 = new Yazi(baslik, Tema.Kalin, Tema.Metin) { BackColor = Tema.Kart };
            Yazi y3 = new Yazi(detay ?? "", Tema.Normal, Tema.Soluk) { BackColor = Tema.Kart };
            y3.Padding = new Padding(0, Tema.O(2), 0, 0);
            metin.BackColor = Tema.Kart;
            metin.Dock = DockStyle.Fill;
            Tema.Diz(metin, y1, y2, y3);

            sag.BackColor = Tema.Kart;
            sag.Dock = DockStyle.Right;
            sag.Width = 0;
            if (!string.IsNullOrEmpty(link))
            {
                Button b = Tema.Buton(string.IsNullOrEmpty(linkMetin) ? "Aç" : linkMetin, seviye == "bad" || seviye == "warn");
                b.Location = new Point(Tema.O(12), Tema.O(4));
                b.Click += (s, e) => { try { Ortak.Ac(link, null); } catch (Exception ex) { MessageBox.Show("Açılamadı: " + ex.Message); } };
                sag.Controls.Add(b);
                sag.Width = b.PreferredSize.Width + Tema.O(14);
            }
            Controls.Add(metin);
            Controls.Add(sag);
            foreach (Control c in metin.Controls) c.SizeChanged += (s, e) => Duzenle();
        }

        void Duzenle()
        {
            int h = 0;
            foreach (Control c in metin.Controls) h += c.Height;
            int butonH = sag.Controls.Count > 0 ? sag.Controls[0].Height + Tema.O(8) : 0;
            int yeni = Padding.Vertical + Math.Max(h, butonH);
            if (Height != yeni) Height = yeni;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1 - bosluk);
            using (GraphicsPath p = Tema.Yuvarlak(r, Tema.O(8)))
            using (SolidBrush b = new SolidBrush(Tema.Kart))
            using (Pen k = new Pen(Tema.Kenar))
            {
                e.Graphics.FillPath(b, p);
                e.Graphics.DrawPath(k, p);
            }
            using (SolidBrush s = new SolidBrush(renk))
                e.Graphics.FillRectangle(s, 0, Tema.O(6), Tema.O(4), r.Height - Tema.O(12));
        }
    }

    // Halka şeklinde puan kartı
    class PuanKarti : Panel
    {
        int puan;
        readonly Yazi baslik, aciklama, ozet;

        public PuanKarti()
        {
            DoubleBuffered = true;
            BackColor = Tema.Arka;
            Padding = new Padding(Tema.O(132), Tema.O(16), Tema.O(16), Tema.O(12));
            baslik = new Yazi("", Tema.AltBaslik, Tema.Metin) { BackColor = Tema.Kart };
            aciklama = new Yazi("", Tema.Normal, Tema.Soluk) { BackColor = Tema.Kart, Padding = new Padding(0, Tema.O(2), 0, Tema.O(6)) };
            ozet = new Yazi("", Tema.Kalin, Tema.Metin) { BackColor = Tema.Kart };
            Tema.Diz(this, baslik, aciklama, ozet);
        }

        public void Ayarla(int puan, string ad, string acik, int sorun, int uyari)
        {
            this.puan = puan;
            baslik.Text = ad;
            aciklama.Text = acik;
            ozet.Text = sorun == 0 && uyari == 0 ? "✓ Sorun yok" : (sorun > 0 ? sorun + " sorun" : "") + (sorun > 0 && uyari > 0 ? ", " : "") + (uyari > 0 ? uyari + " uyarı" : "");
            ozet.ForeColor = sorun > 0 ? Tema.Kirmizi : uyari > 0 ? Tema.Sari : Tema.Yesil;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Tema.Yuvarlak(r, Tema.O(10)))
            using (SolidBrush b = new SolidBrush(Tema.Kart))
            using (Pen k = new Pen(Tema.Kenar))
            {
                g.FillPath(b, p);
                g.DrawPath(k, p);
            }
            int cap = Tema.O(96), kalinlik = Tema.O(10);
            Rectangle h = new Rectangle(Tema.O(20), (Height - cap) / 2, cap, cap);
            using (Pen arka = new Pen(Tema.HalkaArka, kalinlik))
            using (Pen on = new Pen(Tema.PuanRengi(puan), kalinlik) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawEllipse(arka, h);
                if (puan > 0) g.DrawArc(on, h, -90, 360f * puan / 100f);
            }
            using (Font f = new Font("Segoe UI Semibold", 22f))
            using (SolidBrush m = new SolidBrush(Tema.Metin))
            using (SolidBrush s = new SolidBrush(Tema.Soluk))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(puan.ToString(), f, m, new RectangleF(h.X, h.Y - Tema.O(8), h.Width, h.Height), sf);
                g.DrawString(Tema.PuanSozu(puan), Tema.Kucuk, s, new RectangleF(h.X, h.Y + Tema.O(22), h.Width, h.Height), sf);
            }
        }
    }

    // Hızlı / tam tarama seçim kartı
    class SecimKarti : Panel
    {
        bool secili;
        public event EventHandler Secildi;

        public SecimKarti(string baslik, string sure, string aciklama)
        {
            DoubleBuffered = true;
            BackColor = Tema.Arka;
            Cursor = Cursors.Hand;
            Padding = new Padding(Tema.O(44), Tema.O(14), Tema.O(16), Tema.O(12));
            Yazi y1 = new Yazi(baslik, Tema.AltBaslik, Tema.Metin) { BackColor = Tema.Kart };
            Yazi y2 = new Yazi(sure, Tema.Normal, Tema.Vurgu) { BackColor = Tema.Kart, Padding = new Padding(0, 0, 0, Tema.O(6)) };
            Yazi y3 = new Yazi(aciklama, Tema.Normal, Tema.Soluk) { BackColor = Tema.Kart };
            Tema.Diz(this, y1, y2, y3);
            foreach (Control c in Controls)
            {
                c.Cursor = Cursors.Hand;
                c.MouseDown += (s, e) => Sec();
                c.SizeChanged += (s, e) => { if (IcerikDegisti != null) IcerikDegisti(this, EventArgs.Empty); };
            }
            MouseDown += (s, e) => Sec();
        }

        public event EventHandler IcerikDegisti;

        public int IcerikYuksekligi
        {
            get { int h = Padding.Vertical; foreach (Control c in Controls) h += c.Height; return h; }
        }

        public bool Secili
        {
            get { return secili; }
            set { secili = value; Invalidate(); }
        }

        void Sec()
        {
            Secili = true;
            if (Secildi != null) Secildi(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int k = secili ? Tema.O(2) : 1;
            Rectangle r = new Rectangle(k / 2, k / 2, Width - 1 - k, Height - 1 - k);
            using (GraphicsPath p = Tema.Yuvarlak(r, Tema.O(10)))
            using (SolidBrush b = new SolidBrush(Tema.Kart))
            using (Pen kalem = new Pen(secili ? Tema.Vurgu : Tema.Kenar, k))
            {
                g.FillPath(b, p);
                g.DrawPath(kalem, p);
            }
            int d = Tema.O(18);
            Rectangle c = new Rectangle(Tema.O(16), Tema.O(18), d, d);
            using (Pen kalem = new Pen(secili ? Tema.Vurgu : Color.FromArgb(160, 166, 178), Tema.O(2)))
                g.DrawEllipse(kalem, c);
            if (secili)
            {
                Rectangle ic = Rectangle.Inflate(c, -Tema.O(5), -Tema.O(5));
                using (SolidBrush b = new SolidBrush(Tema.Vurgu)) g.FillEllipse(b, ic);
            }
        }
    }

    // JSON sözlüğünden güvenli okuma
    static class J
    {
        public static object Al(object o, string anahtar)
        {
            IDictionary<string, object> d = o as IDictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(anahtar, out v) ? v : null;
        }
        public static string S(object o, string a) { object v = Al(o, a); return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); }
        public static int? I(object o, string a) { object v = Al(o, a); if (v == null) return null; try { return Convert.ToInt32(v); } catch { return null; } }
        public static bool B(object o, string a) { object v = Al(o, a); return v is bool && (bool)v; }
        public static IEnumerable<object> L(object o, string a)
        {
            object v = Al(o, a);
            if (v is IEnumerable && !(v is string) && !(v is IDictionary<string, object>)) return ((IEnumerable)v).Cast<object>();
            return v == null ? Enumerable.Empty<object>() : new[] { v };
        }
    }

    // ------------------------------------------------------------------ kısayol penceresi
    class KisayolForm : Form
    {
        readonly CheckBox chkMasaustu = new CheckBox();
        readonly CheckBox chkBaslat = new CheckBox();

        public KisayolForm(bool ilk)
        {
            Text = ilk ? "Sistem Checkup'a hoş geldiniz" : "Kısayollar";
            Font = Tema.Normal;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ClientSize = new Size(Tema.O(460), Tema.O(ilk ? 250 : 200));
            Padding = new Padding(Tema.O(20));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Yazi baslik = new Yazi(ilk ? "Hoş geldiniz!" : "Kısayollar", Tema.AltBaslik, Tema.Metin);
            Yazi aciklama = new Yazi(ilk
                ? "Sistem Checkup kurulum gerektirmez; bu klasörden çalışır. İsterseniz kolayca açabilmek için kısayol oluşturabilirsiniz. Bunu daha sonra alt kısımdaki \"Kısayollar\" bağlantısından değiştirebilirsiniz."
                : "Sistem Checkup'ı nereden açmak istediğinizi seçin.", Tema.Normal, Tema.Soluk);
            aciklama.Padding = new Padding(0, Tema.O(4), 0, Tema.O(10));

            chkMasaustu.Text = "Masaüstüne kısayol ekle";
            chkBaslat.Text = "Başlat menüsüne ekle";
            chkMasaustu.Height = chkBaslat.Height = Tema.O(28);
            chkMasaustu.Checked = ilk || Ortak.BizimKisayolMu(Ortak.MasaustuKisayolu);
            chkBaslat.Checked = ilk || Ortak.BizimKisayolMu(Ortak.BaslatKisayolu);

            Button tamam = Tema.Buton(ilk ? "Devam" : "Kaydet", true);
            tamam.Click += (s, e) => { Uygula(); DialogResult = DialogResult.OK; Close(); };
            FlowLayoutPanel alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = tamam.PreferredSize.Height + Tema.O(4) };
            alt.Controls.Add(tamam);
            if (!ilk)
            {
                Button iptal = Tema.Buton("Vazgeç", false);
                iptal.Click += (s, e) => Close();
                alt.Controls.Add(iptal);
            }
            AcceptButton = tamam;

            Controls.Add(alt);
            Tema.Diz(this, baslik, aciklama, chkMasaustu, chkBaslat);
        }

        void Uygula()
        {
            try
            {
                Tek(chkMasaustu.Checked, Ortak.MasaustuKisayolu);
                Tek(chkBaslat.Checked, Ortak.BaslatKisayolu);
            }
            catch (Exception ex) { MessageBox.Show("Kısayol oluşturulamadı: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        static void Tek(bool iste, string yol)
        {
            bool var = Ortak.BizimKisayolMu(yol);
            if (iste && !var) Ortak.KisayolOlustur(yol);
            else if (!iste && var) Ortak.KisayolSil(yol);
        }
    }

    // ------------------------------------------------------------------ ana pencere
    class AnaForm : Form
    {
        readonly Ayarlar ayarlar = Ayarlar.Yukle();

        // sayfalar
        readonly Panel sayfaBaslangic = new Panel();
        readonly Panel sayfaTarama = new Panel();
        readonly Panel sayfaSonuc = new Panel();

        // başlangıç
        SecimKarti secHizli, secTam;
        readonly CheckBox chkYonetici = new CheckBox();
        Button btnBasla;
        readonly Yazi lblSonTarama = new Yazi("", Tema.Normal, Tema.Soluk);
        readonly LinkLabel lnkSonTarama = new LinkLabel();
        readonly Yazi lblBaslangicHata = new Yazi("", Tema.Normal, Tema.Kirmizi);

        // tarama
        readonly Yazi lblTaramaBaslik = new Yazi("Taranıyor...", Tema.Baslik, Tema.Metin);
        readonly Yazi lblAdim = new Yazi("Başlatılıyor...", Tema.Kalin, Tema.Vurgu);
        readonly Yazi lblSure = new Yazi("", Tema.Normal, Tema.Soluk);
        readonly ProgressBar bar = new ProgressBar();
        readonly ListBox lstKayit = new ListBox();
        Button btnIptal;

        // güncelleme bandı
        readonly Panel bant = new Panel();
        readonly Label lblBant = new Label();

        // tarama durumu
        Process surec;
        readonly Timer zamanlayici = new Timer();
        string ilerlemeYolu, iptalYolu;
        long okunan;
        string yarimSatir = "";
        DateTime baslamaZamani;
        bool taramaSuruyor, bitti, iptalIstendi;
        int adimNo, adimToplam = 12;

        public AnaForm()
        {
            using (Graphics g = CreateGraphics()) Tema.Olcek = g.DpiX / 96f;
            SuspendLayout();
            Text = Ortak.UygulamaAdi;
            Font = Tema.Normal;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Tema.Arka;
            ClientSize = new Size(Tema.O(940), Tema.O(680));
            MinimumSize = new Size(Tema.O(760), Tema.O(560));
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            foreach (Panel p in new[] { sayfaBaslangic, sayfaTarama, sayfaSonuc })
            {
                p.Dock = DockStyle.Fill;
                p.AutoScroll = true;
                p.Padding = new Padding(Tema.O(32), Tema.O(24), Tema.O(32), Tema.O(24));
                p.Visible = false;
                Controls.Add(p);
            }
            BaslangicKur();
            TaramaKur();
            Controls.Add(BantKur());
            Controls.Add(UstKur());
            Controls.Add(AltKur());
            ResumeLayout(false);
            PerformLayout();

            zamanlayici.Interval = 250;
            zamanlayici.Tick += (s, e) => IlerlemeOku();
            Shown += async (s, e) =>
            {
                if (!ayarlar.ilkKurulumTamam)
                {
                    using (KisayolForm f = new KisayolForm(true)) f.ShowDialog(this);
                    ayarlar.ilkKurulumTamam = true;
                    ayarlar.Kaydet();
                }
                await GuncellemeDenetle();
            };
            FormClosing += Kapaniyor;
            SonTaramayiGoster();
            Goster(sayfaBaslangic);
        }

        void Goster(Panel p)
        {
            foreach (Panel s in new[] { sayfaBaslangic, sayfaTarama, sayfaSonuc }) s.Visible = s == p;
            p.BringToFront();
        }

        // ---------------------------------------------------------------- çerçeve
        Control UstKur()
        {
            Panel ust = new Panel { Dock = DockStyle.Top, Height = Tema.O(58), BackColor = Tema.Vurgu, Padding = new Padding(Tema.O(28), 0, Tema.O(24), 0) };
            Label ad = new Label { Text = "Sistem Checkup", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            LinkLabel denetle = new LinkLabel { Text = "Güncellemeleri denetle", AutoSize = false, Dock = DockStyle.Right, Width = Tema.O(190), TextAlign = ContentAlignment.MiddleRight, LinkColor = Color.White, ActiveLinkColor = Color.White, LinkBehavior = LinkBehavior.HoverUnderline };
            denetle.LinkClicked += (s, e) => GuncelleyiciAc(false);
            ust.Controls.Add(ad);
            ust.Controls.Add(denetle);
            return ust;
        }

        Control BantKur()
        {
            bant.Dock = DockStyle.Top;
            bant.Height = Tema.O(46);
            bant.BackColor = Color.FromArgb(253, 241, 221);
            bant.Padding = new Padding(Tema.O(28), Tema.O(6), Tema.O(24), Tema.O(6));
            bant.Visible = false;
            lblBant.Dock = DockStyle.Fill;
            lblBant.TextAlign = ContentAlignment.MiddleLeft;
            lblBant.ForeColor = Color.FromArgb(110, 70, 0);
            lblBant.Font = Tema.Kalin;
            Button b = Tema.Buton("Şimdi güncelle", true);
            b.Dock = DockStyle.Right;
            b.Click += (s, e) => GuncelleyiciAc(true);
            bant.Controls.Add(lblBant);
            bant.Controls.Add(b);
            return bant;
        }

        Control AltKur()
        {
            FlowLayoutPanel alt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Tema.O(38), BackColor = Color.White, Padding = new Padding(Tema.O(24), Tema.O(9), Tema.O(24), 0) };
            alt.Paint += (s, e) => { using (Pen p = new Pen(Tema.Kenar)) e.Graphics.DrawLine(p, 0, 0, alt.Width, 0); };
            Action<string, Action> link = (metin, is_) =>
            {
                LinkLabel l = new LinkLabel { Text = metin, AutoSize = true, LinkColor = Tema.Soluk, ActiveLinkColor = Tema.Vurgu, LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(0, 0, Tema.O(20), 0) };
                l.LinkClicked += (s, e) => is_();
                alt.Controls.Add(l);
            };
            link("Raporlar klasörü", () => { Directory.CreateDirectory(Ortak.RaporDizin); Ortak.Ac("explorer.exe", "\"" + Ortak.RaporDizin + "\""); });
            link("Kısayollar", () => { using (KisayolForm f = new KisayolForm(false)) f.ShowDialog(this); });
            link("Güncelleyici", () => GuncelleyiciAc(false));
            link("GitHub", () => Ortak.Ac(Ortak.RepoUrl, null));
            link("Kaldır", Kaldir);
            alt.Controls.Add(new Label { Text = "Sürüm " + Ortak.YerelSurum(), AutoSize = true, ForeColor = Tema.Soluk, Margin = new Padding(Tema.O(20), 0, 0, 0) });
            return alt;
        }

        // ---------------------------------------------------------------- başlangıç sayfası
        void BaslangicKur()
        {
            Yazi h1 = new Yazi("Bilgisayarınızın sağlığını kontrol edin", Tema.Baslik, Tema.Metin);
            Yazi alt = new Yazi("Disk ve SSD sağlığı, ekran kartı, bellek, sürücüler ve hata kayıtları incelenir. Tarama bilgisayarınızda yapılır; hiçbir kişisel veri dışarı gönderilmez.", Tema.Normal, Tema.Soluk);
            alt.Padding = new Padding(0, Tema.O(4), 0, Tema.O(18));

            secHizli = new SecimKarti("Hızlı tarama", "yaklaşık 30-60 saniye",
                "Parçaların sağlık durumu, sürücüler ve son 30 günün hata kayıtları. Bilgisayarı yormaz; istediğiniz zaman çalıştırabilirsiniz.");
            secTam = new SecimKarti("Tam tarama", "yaklaşık 2 dakika",
                "Hızlı taramaya ek olarak disk, işlemci ve bellek hız testleri. Her diske geçici olarak 1 GB dosya yazılıp hemen silinir; işlemci 10 saniye tam güçte çalışır, fanlar kısa süre hızlanabilir. Bu sırada başka iş yapmamanız sonuçları doğru kılar.");
            secHizli.Secili = !ayarlar.tamTarama;
            secTam.Secili = ayarlar.tamTarama;
            secHizli.Secildi += (s, e) => secTam.Secili = false;
            secTam.Secildi += (s, e) => secHizli.Secili = false;

            TableLayoutPanel secimler = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = Tema.O(206), BackColor = Color.Transparent };
            secimler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            secimler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            secHizli.Dock = DockStyle.Fill; secHizli.Margin = new Padding(0, 0, Tema.O(8), 0);
            secTam.Dock = DockStyle.Fill; secTam.Margin = new Padding(Tema.O(8), 0, 0, 0);
            secimler.Controls.Add(secHizli, 0, 0);
            secimler.Controls.Add(secTam, 1, 0);
            EventHandler yukseklik = (s, e) =>
            {
                int h = Math.Max(secHizli.IcerikYuksekligi, secTam.IcerikYuksekligi) + Tema.O(4);
                if (h > Tema.O(60) && secimler.Height != h) secimler.Height = h;
            };
            secHizli.IcerikDegisti += yukseklik;
            secTam.IcerikDegisti += yukseklik;

            chkYonetici.Text = "Yönetici izniyle tara (önerilir)";
            chkYonetici.Font = Tema.Kalin;
            chkYonetici.Checked = ayarlar.yoneticiIzni;
            chkYonetici.Height = Tema.O(30);
            Yazi yoneticiAcik = new Yazi("SSD'nin kalan ömrü, disk sıcaklığı ve çalışma saati yalnızca yönetici izniyle okunabilir. Taramayı başlattığınızda Windows \"Bu uygulamanın cihazınızda değişiklik yapmasına izin vermek istiyor musunuz?\" diye sorar. Sistem Checkup yalnızca bilgi okur, hiçbir ayarı değiştirmez. \"Hayır\" derseniz tarama bu bilgiler olmadan devam eder.", Tema.Normal, Tema.Soluk);
            yoneticiAcik.Padding = new Padding(Tema.O(20), 0, 0, Tema.O(18));

            btnBasla = Tema.Buton("Taramayı başlat", true);
            btnBasla.Font = new Font("Segoe UI Semibold", 12f);
            btnBasla.Padding = new Padding(Tema.O(26), Tema.O(8), Tema.O(26), Tema.O(8));
            btnBasla.Click += (s, e) => TaramaBaslat(chkYonetici.Checked);
            Panel butonSatiri = new Panel { Height = btnBasla.PreferredSize.Height + Tema.O(4) };
            butonSatiri.Controls.Add(btnBasla);

            lblBaslangicHata.Padding = new Padding(0, Tema.O(10), 0, 0);
            lblSonTarama.Padding = new Padding(0, Tema.O(22), 0, Tema.O(2));
            lnkSonTarama.Text = "Son taramanın sonuçlarını göster";
            lnkSonTarama.Height = Tema.O(24);
            lnkSonTarama.LinkClicked += (s, e) => SonuclariGoster();

            Tema.Diz(sayfaBaslangic, h1, alt, secimler, Tema.Bosluk(16), chkYonetici, yoneticiAcik, butonSatiri, lblBaslangicHata, lblSonTarama, lnkSonTarama);
        }

        void SonTaramayiGoster()
        {
            object v = SonucuYukle();
            if (v == null) { lblSonTarama.Text = ""; lnkSonTarama.Visible = false; return; }
            object meta = J.Al(v, "meta"), puan = J.Al(v, "puan");
            string metin = "Son tarama: " + J.S(meta, "tarih");
            if (puan is IDictionary<string, object>) metin += "  ·  Donanım sağlığı " + J.I(puan, "donanim") + "  ·  Bakım " + J.I(puan, "bakim");
            lblSonTarama.Text = metin;
            lnkSonTarama.Visible = true;
        }

        // ---------------------------------------------------------------- tarama sayfası
        void TaramaKur()
        {
            lblAdim.Padding = new Padding(0, Tema.O(8), 0, Tema.O(4));
            lblSure.Padding = new Padding(0, 0, 0, Tema.O(12));
            bar.Height = Tema.O(14);
            bar.Style = ProgressBarStyle.Continuous;
            bar.Maximum = 1000;
            lstKayit.Dock = DockStyle.Fill;
            lstKayit.BorderStyle = BorderStyle.None;
            lstKayit.BackColor = Color.White;
            lstKayit.ForeColor = Tema.Soluk;
            lstKayit.IntegralHeight = false;
            lstKayit.SelectionMode = SelectionMode.None;
            Panel kayitKap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(Tema.O(12)) };
            kayitKap.Controls.Add(lstKayit);

            btnIptal = Tema.Buton("İptal", false);
            btnIptal.Click += (s, e) => IptalEt();
            Panel alt = new Panel { Dock = DockStyle.Bottom, Height = btnIptal.PreferredSize.Height + Tema.O(16) };
            btnIptal.Location = new Point(0, Tema.O(12));
            alt.Controls.Add(btnIptal);

            Panel ust = new Panel { Dock = DockStyle.Top, Height = 10 };
            Tema.Diz(ust, lblTaramaBaslik, lblAdim, bar, Tema.Bosluk(8), lblSure);
            ust.Layout += (s, e) => { int h = 0; foreach (Control c in ust.Controls) h += c.Height; if (ust.Height != h) ust.Height = h; };

            sayfaTarama.AutoScroll = false;
            sayfaTarama.Controls.Add(kayitKap);
            sayfaTarama.Controls.Add(alt);
            sayfaTarama.Controls.Add(ust);
        }

        void TaramaBaslat(bool yonetici)
        {
            ayarlar.tamTarama = secTam.Secili;
            ayarlar.yoneticiIzni = chkYonetici.Checked;
            ayarlar.Kaydet();
            lblBaslangicHata.Text = "";

            Directory.CreateDirectory(Ortak.RaporDizin);
            ilerlemeYolu = Path.Combine(Ortak.RaporDizin, ".ilerleme.txt");
            iptalYolu = Path.Combine(Ortak.RaporDizin, ".iptal");
            try { File.Delete(ilerlemeYolu); File.Delete(iptalYolu); } catch { }
            File.WriteAllText(ilerlemeYolu, "");

            string ps1 = Path.Combine(Ortak.AppDizin, "SistemCheckup.ps1");
            string arg = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ps1 + "\" -YonetimsizCalis -Sessiz" +
                " -RaporDizini \"" + Ortak.RaporDizin + "\" -IlerlemeDosyasi \"" + ilerlemeYolu + "\" -IptalDosyasi \"" + iptalYolu + "\"" +
                (ayarlar.tamTarama ? "" : " -TestAtla");
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", arg);
            psi.WorkingDirectory = Ortak.AppDizin;
            if (yonetici && !Ortak.Yonetici())
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
            }

            try { surec = Process.Start(psi); }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    if (MessageBox.Show(this, "Yönetici izni verilmedi.\n\nTarama yönetici izni olmadan yapılsın mı? (SSD ömrü ve sıcaklığı ölçülemez, diğer her şey taranır.)",
                        Ortak.UygulamaAdi, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        TaramaBaslat(false);
                    return;
                }
                lblBaslangicHata.Text = "Tarama başlatılamadı: " + ex.Message;
                return;
            }

            okunan = 0; yarimSatir = ""; adimNo = 0; bitti = false; iptalIstendi = false;
            taramaSuruyor = true;
            baslamaZamani = DateTime.Now;
            lstKayit.Items.Clear();
            bar.Value = 0;
            lblTaramaBaslik.Text = ayarlar.tamTarama ? "Tam tarama yapılıyor..." : "Hızlı tarama yapılıyor...";
            lblAdim.Text = "Başlatılıyor...";
            lblSure.Text = ayarlar.tamTarama ? "Hız testleri sırasında başka iş yapmamanız sonuçları daha doğru kılar." : "";
            btnIptal.Enabled = true;
            btnIptal.Text = "İptal";
            Goster(sayfaTarama);
            zamanlayici.Start();
        }

        void IlerlemeOku()
        {
            try
            {
                using (FileStream fs = new FileStream(ilerlemeYolu, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length > okunan)
                    {
                        fs.Seek(okunan, SeekOrigin.Begin);
                        byte[] buf = new byte[fs.Length - okunan];
                        int n = fs.Read(buf, 0, buf.Length);
                        okunan += n;
                        string metin = yarimSatir + Encoding.UTF8.GetString(buf, 0, n);
                        string[] satirlar = metin.Split('\n');
                        yarimSatir = satirlar[satirlar.Length - 1];
                        for (int i = 0; i < satirlar.Length - 1; i++) SatirIsle(satirlar[i].TrimEnd('\r'));
                    }
                }
            }
            catch (IOException) { }

            if (!taramaSuruyor) return;
            TimeSpan t = DateTime.Now - baslamaZamani;
            string sure = string.Format("Geçen süre {0}:{1:00}", (int)t.TotalMinutes, t.Seconds);
            if (iptalIstendi) sure += "  ·  İptal ediliyor, mevcut adım bitince duracak...";
            else if (ayarlar.tamTarama && adimNo >= 7 && adimNo <= 8) sure += "  ·  Hız testleri sürüyor, lütfen bekleyin";
            lblSure.Text = sure;
            // Adım içinde de çubuk biraz ilerlesin
            if (adimNo > 0 && adimNo <= adimToplam)
            {
                int taban = 1000 * (adimNo - 1) / adimToplam, tavan = 1000 * adimNo / adimToplam;
                if (bar.Value < tavan - 10) bar.Value = Math.Max(taban, Math.Min(tavan - 10, bar.Value + 3));
            }

            bool kapandi = false;
            try { kapandi = surec != null && surec.HasExited; } catch { }
            if (kapandi && !bitti)
            {
                // son satırları da oku
                System.Threading.Thread.Sleep(150);
                if (!bitti) { IlerlemeSonu(); }
            }
        }

        void IlerlemeSonu()
        {
            // İşlem bitti ama BITTI/IPTAL/HATA satırı gelmediyse
            try
            {
                string tum = File.ReadAllText(ilerlemeYolu, Encoding.UTF8);
                if (tum.Contains("\nBITTI|") || tum.StartsWith("BITTI|") || tum.Contains("IPTAL") || tum.Contains("HATA|")) return;
            }
            catch { }
            TaramaBitti();
            lblBaslangicHata.Text = "Tarama beklenmedik şekilde durdu. Tekrar deneyin; sorun sürerse \"Yönetici izniyle tara\" seçeneğini kapatıp deneyin.";
            Goster(sayfaBaslangic);
        }

        void SatirIsle(string satir)
        {
            if (satir.Length == 0) return;
            string[] p = satir.Split(new[] { '|' }, 4);
            switch (p[0])
            {
                case "ADIM":
                    int n, t;
                    if (p.Length >= 4 && int.TryParse(p[1], out n) && int.TryParse(p[2], out t))
                    {
                        adimNo = n; adimToplam = t;
                        lblAdim.Text = "Adım " + n + " / " + t + "  ·  " + p[3];
                        bar.Value = Math.Min(1000, 1000 * (n - 1) / t);
                        Kayit("● " + p[3]);
                    }
                    break;
                case "BILGI":
                    if (p.Length >= 2) Kayit("     " + p[1]);
                    break;
                case "BITTI":
                    bitti = true;
                    bar.Value = 1000;
                    TaramaBitti();
                    SonTaramayiGoster();
                    SonuclariGoster();
                    break;
                case "IPTAL":
                    bitti = true;
                    TaramaBitti();
                    lblBaslangicHata.Text = "Tarama iptal edildi.";
                    Goster(sayfaBaslangic);
                    break;
                case "HATA":
                    bitti = true;
                    TaramaBitti();
                    lblBaslangicHata.Text = "Tarama sırasında hata oluştu: " + (p.Length > 1 ? p[1] : "");
                    Goster(sayfaBaslangic);
                    break;
            }
        }

        void Kayit(string s)
        {
            lstKayit.Items.Add(s);
            lstKayit.TopIndex = Math.Max(0, lstKayit.Items.Count - 1);
        }

        void TaramaBitti()
        {
            taramaSuruyor = false;
            zamanlayici.Stop();
            try { File.Delete(iptalYolu); } catch { }
        }

        void IptalEt()
        {
            if (!taramaSuruyor) return;
            iptalIstendi = true;
            btnIptal.Enabled = false;
            btnIptal.Text = "İptal ediliyor...";
            try { File.WriteAllText(iptalYolu, "1"); } catch { }
        }

        void Kapaniyor(object s, FormClosingEventArgs e)
        {
            if (!taramaSuruyor) return;
            if (MessageBox.Show(this, "Tarama sürüyor. Kapatırsanız tarama iptal edilecek.\n\nKapatılsın mı?", Ortak.UygulamaAdi,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
            try { File.WriteAllText(iptalYolu, "1"); } catch { }
        }

        // ---------------------------------------------------------------- sonuç sayfası
        static object SonucuYukle()
        {
            try
            {
                string yol = Path.Combine(Ortak.RaporDizin, "son_rapor.json");
                if (!File.Exists(yol)) return null;
                JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                return js.DeserializeObject(File.ReadAllText(yol, Encoding.UTF8));
            }
            catch { return null; }
        }

        void SonuclariGoster()
        {
            object v = SonucuYukle();
            if (v == null) { lblBaslangicHata.Text = "Sonuç dosyası okunamadı."; Goster(sayfaBaslangic); return; }

            sayfaSonuc.SuspendLayout();
            foreach (Control c in sayfaSonuc.Controls.Cast<Control>().ToList()) c.Dispose();
            sayfaSonuc.Controls.Clear();

            object meta = J.Al(v, "meta"), puan = J.Al(v, "puan"), sistem = J.Al(v, "sistem");
            List<object> bulgular = J.L(v, "bulgular").ToList();
            int donanim = puan is IDictionary<string, object> ? (J.I(puan, "donanim") ?? 0) : (J.I(v, "puan") ?? 0);
            int? bakim = puan is IDictionary<string, object> ? J.I(puan, "bakim") : null;
            Func<string, string, int> say = (tur, sev) => bulgular.Count(b => J.S(b, "tur") == tur && J.S(b, "seviye") == sev);

            string karar = donanim >= 85 ? (bakim.HasValue && bakim < 75 ? "Donanımınız sağlıklı, birkaç bakım işi var" : "Bilgisayarınız sağlıklı görünüyor")
                : donanim >= 60 ? "Donanımda dikkat edilmesi gereken noktalar var" : "Donanımda ciddi sorun belirtileri var";
            Yazi h1 = new Yazi(karar, Tema.Baslik, Tema.Metin);
            Yazi alt = new Yazi(J.S(meta, "tarih") + "  ·  " + (J.B(meta, "testYapildi") ? "tam tarama" : "hızlı tarama") + "  ·  " +
                (J.B(meta, "yonetici") ? "yönetici izniyle" : "yönetici izni olmadan"), Tema.Normal, Tema.Soluk);
            alt.Padding = new Padding(0, Tema.O(2), 0, Tema.O(14));

            PuanKarti pk1 = new PuanKarti(), pk2 = new PuanKarti();
            pk1.Ayarla(donanim, "Donanım sağlığı", "Parçaların fiziksel durumu: disk ve SSD sağlığı, sıcaklıklar, donanım hata kayıtları" +
                (J.B(sistem, "dizustu") ? ", pil" : "") + ". Düşükse bir parça arızalanıyor olabilir.", say("donanim", "bad"), say("donanim", "warn"));
            pk2.Ayarla(bakim ?? 0, "Bakım ve güncellemeler", "Sürücüler, Windows güncellemeleri, disk alanı ve ayarlar. Düşük olması donanımın bozuk olduğu anlamına gelmez; birkaç tıkla düzeltilebilecek işlerdir.",
                say("bakim", "bad"), say("bakim", "warn"));
            TableLayoutPanel puanlar = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = Tema.O(150), BackColor = Color.Transparent };
            puanlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            puanlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pk1.Dock = DockStyle.Fill; pk1.Margin = new Padding(0, 0, Tema.O(8), 0);
            pk2.Dock = DockStyle.Fill; pk2.Margin = new Padding(Tema.O(8), 0, 0, 0);
            puanlar.Controls.Add(pk1, 0, 0);
            puanlar.Controls.Add(pk2, 1, 0);

            FlowLayoutPanel butonlar = new FlowLayoutPanel { Height = Tema.O(52), Padding = new Padding(0, Tema.O(12), 0, 0), BackColor = Color.Transparent };
            Button rapor = Tema.Buton("Detaylı raporu aç", true);
            string raporYolu = J.S(meta, "raporYolu");
            if (string.IsNullOrEmpty(raporYolu) || !File.Exists(raporYolu))
                raporYolu = Directory.Exists(Ortak.RaporDizin) ? Directory.GetFiles(Ortak.RaporDizin, "SistemCheckup_*.html").OrderByDescending(f => f).FirstOrDefault() : null;
            rapor.Enabled = raporYolu != null;
            rapor.Click += (s, e) => Ortak.Ac(raporYolu, null);
            Button yeni = Tema.Buton("Yeni tarama", false);
            yeni.Margin = new Padding(Tema.O(8), 0, 0, 0);
            yeni.Click += (s, e) => { SonTaramayiGoster(); Goster(sayfaBaslangic); };
            butonlar.Controls.Add(rapor);
            butonlar.Controls.Add(yeni);

            List<Control> liste = new List<Control> { h1, alt, puanlar, butonlar };

            // Önceki taramayla karşılaştırma
            object k = J.Al(v, "karsilastirma");
            if (k != null)
            {
                StringBuilder sb = new StringBuilder();
                int? dOnce = J.I(k, "donanimOnce"), bOnce = J.I(k, "bakimOnce");
                sb.Append("Donanım sağlığı " + dOnce + " → " + donanim + Fark(dOnce, donanim));
                if (bakim.HasValue) sb.Append("     Bakım " + bOnce + " → " + bakim + Fark(bOnce, bakim));
                List<string> cozulen = J.L(k, "cozulen").Select(x => Convert.ToString(x)).ToList();
                List<string> yeniler = J.L(k, "yeni").Select(x => Convert.ToString(x)).ToList();
                if (cozulen.Count > 0) sb.Append("\n✓ Çözülen: " + string.Join("; ", cozulen));
                if (yeniler.Count > 0) sb.Append("\n• Yeni: " + string.Join("; ", yeniler));
                foreach (object d in J.L(k, "degisimler"))
                {
                    string yon = J.S(d, "yon");
                    if (yon == "ayni") continue;
                    sb.Append("\n" + (yon == "iyi" ? "↑ " : "↓ ") + J.S(d, "ad") + ": " + J.S(d, "once") + " → " + J.S(d, "simdi") + " " + J.S(d, "birim"));
                }
                if (cozulen.Count == 0 && yeniler.Count == 0) sb.Append("\nSorun listesinde değişiklik yok.");
                Yazi kb = new Yazi("Önceki taramaya göre (" + J.S(k, "tarih") + ")", Tema.Kalin, Tema.Metin) { Padding = new Padding(0, Tema.O(18), 0, Tema.O(2)) };
                Yazi kd = new Yazi(sb.ToString(), Tema.Normal, Tema.Soluk);
                liste.Add(kb);
                liste.Add(kd);
            }

            List<object> yapilacak = bulgular.Where(b => J.S(b, "seviye") != "ok").ToList();
            Yazi yb = new Yazi(yapilacak.Count > 0 ? "Yapılması gerekenler" : "Yapılması gereken bir şey yok ✓", Tema.AltBaslik, Tema.Metin) { Padding = new Padding(0, Tema.O(20), 0, Tema.O(2)) };
            liste.Add(yb);
            if (yapilacak.Count > 0)
            {
                Yazi ya = new Yazi("Önem sırasına göre. \"Bilgi\" olanlar sorun değildir, yalnızca bilgilendirme amaçlıdır.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(10)) };
                liste.Add(ya);
            }
            foreach (object b in yapilacak)
            {
                string etiket = J.S(b, "kategori") + "  ·  " + (J.S(b, "tur") == "bakim" ? "Bakım" : "Donanım") + "  ·  " + Tema.SeviyeAdi(J.S(b, "seviye"));
                liste.Add(new BulguKarti(J.S(b, "seviye"), etiket, J.S(b, "baslik"), J.S(b, "detay"), J.S(b, "link"), J.S(b, "linkMetin")));
            }
            List<string> iyiler = bulgular.Where(b => J.S(b, "seviye") == "ok").Select(b => "✓ " + J.S(b, "baslik")).ToList();
            if (iyiler.Count > 0)
            {
                liste.Add(new Yazi("Sağlıklı bulunanlar", Tema.Kalin, Tema.Yesil) { Padding = new Padding(0, Tema.O(14), 0, Tema.O(4)) });
                liste.Add(new Yazi(string.Join("\n", iyiler), Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(20)) });
            }

            Tema.Diz(sayfaSonuc, liste.ToArray());
            sayfaSonuc.ResumeLayout(true);
            sayfaSonuc.AutoScrollPosition = new Point(0, 0);
            Goster(sayfaSonuc);
        }

        static string Fark(int? once, int? simdi)
        {
            if (!once.HasValue || !simdi.HasValue || once == simdi) return "";
            return simdi > once ? "  (↑ " + (simdi - once) + ")" : "  (↓ " + (once - simdi) + ")";
        }

        // ---------------------------------------------------------------- güncelleme / kaldırma
        async Task GuncellemeDenetle()
        {
            Manifest m = null;
            try { m = await Task.Run(() => Ortak.UzakManifest(8000)); } catch { }
            if (m == null || !Ortak.YeniMi(m.surum, Ortak.YerelSurum())) return;
            string ilk = m.notlar != null && m.notlar.Count > 0 ? "  " + m.notlar[0] : "";
            lblBant.Text = "Yeni sürüm hazır: " + m.surum + "." + ilk;
            bant.Visible = true;
        }

        void GuncelleyiciAc(bool otomatik)
        {
            if (taramaSuruyor) { MessageBox.Show(this, "Lütfen önce taramanın bitmesini bekleyin.", Ortak.UygulamaAdi); return; }
            if (!File.Exists(Ortak.GuncelleyiciYolu)) { Ortak.Ac(Ortak.RepoUrl + "/releases/latest", null); return; }
            Ortak.Ac(Ortak.GuncelleyiciYolu, otomatik ? "--otomatik" : null);
            if (otomatik) Close();
        }

        void Kaldir()
        {
            if (taramaSuruyor) { MessageBox.Show(this, "Lütfen önce taramanın bitmesini bekleyin.", Ortak.UygulamaAdi); return; }
            if (!File.Exists(Ortak.KaldiriciYolu)) { MessageBox.Show(this, "Kaldir.exe bulunamadı.", Ortak.UygulamaAdi); return; }
            Ortak.Ac(Ortak.KaldiriciYolu, null);
            Close();
        }
    }
}
