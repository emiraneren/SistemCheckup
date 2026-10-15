using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// SistemCheckup.exe: pencereli ana uygulama. Taramayı (app\SistemCheckup.ps1) arka planda çalıştırır,
// ilerlemeyi ilerleme dosyasından okur; sonuçları, geçmişi, sistem bilgilerini ve temizliği gösterir.
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
        public static readonly Color Mor = Color.FromArgb(132, 72, 196);
        public static readonly Color HalkaArka = Color.FromArgb(232, 235, 242);
        public static readonly Color[] SeriRenkleri = { Vurgu, Color.FromArgb(214, 120, 30), Yesil, Mor, Kirmizi, Color.FromArgb(20, 150, 170) };
        public static readonly CultureInfo Tr = new CultureInfo("tr-TR");

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
            int d = Math.Max(1, yaricap * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void KartCiz(Graphics g, Rectangle r, int yaricap)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Yuvarlak(r, yaricap))
            using (SolidBrush b = new SolidBrush(Kart))
            using (Pen k = new Pen(Kenar))
            {
                g.FillPath(b, p);
                g.DrawPath(k, p);
            }
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

        // Yeniden boyutlanınca tamamını yeniden çizen (iz bırakmayan) kontrol ayarları
        public static void CizimAyari(Control c)
        {
            typeof(Control).GetMethod("SetStyle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(c, new object[] { ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true });
        }
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
        protected override void OnPaddingChanged(EventArgs e) { base.OnPaddingChanged(e); Yukseklik(); }
        void Yukseklik()
        {
            if (Width <= 0) return;
            Size s = TextRenderer.MeasureText(Text ?? "", Font, new Size(Math.Max(1, Width - Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = (string.IsNullOrEmpty(Text) ? 0 : s.Height) + Padding.Vertical;
            if (Height != h) Height = h;
        }
    }

    // Beyaz, köşeleri yuvarlak, içeriğine göre uzayan kart. İçerik Ekle() ile yukarıdan aşağı dizilir.
    class Kart : Panel
    {
        protected readonly Panel icerik = new Panel();
        protected readonly int bosluk = Tema.O(10);

        public Kart()
        {
            Tema.CizimAyari(this);
            BackColor = Tema.Arka;
            Padding = new Padding(Tema.O(20), Tema.O(14), Tema.O(20), Tema.O(14) + bosluk);
            icerik.BackColor = Tema.Kart;
            icerik.Dock = DockStyle.Fill;
            Controls.Add(icerik);
        }

        public void Ekle(params Control[] ogeler)
        {
            foreach (Control c in ogeler) { if (c.BackColor == Color.Transparent || c is Yazi) c.BackColor = Tema.Kart; }
            Tema.Diz(icerik, ogeler);
            foreach (Control c in ogeler) c.SizeChanged += (s, e) => Duzenle();
            Duzenle();
        }

        protected virtual int IcerikYuksekligi() { int h = 0; foreach (Control c in icerik.Controls) h += c.Height; return h; }

        protected void Duzenle()
        {
            int yeni = Padding.Vertical + IcerikYuksekligi();
            if (Height != yeni) Height = yeni;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Tema.KartCiz(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1 - bosluk), Tema.O(10));
        }
    }

    // Etiket: değer satırlarından oluşan bilgi kartı (Sistem bilgileri, geçmiş listesi)
    class BilgiKarti : Kart
    {
        class Satir { public Control Sol, Sag, Link; public bool Baslik; }
        readonly List<Satir> satirlar = new List<Satir>();
        readonly Panel tablo = new Panel { BackColor = Tema.Kart };
        int etiketGenislik = Tema.O(200);

        public BilgiKarti(string baslik, string altYazi)
        {
            Yazi b = new Yazi(baslik, Tema.AltBaslik, Tema.Metin) { Padding = new Padding(0, 0, 0, Tema.O(4)) };
            List<Control> ogeler = new List<Control> { b };
            if (!string.IsNullOrEmpty(altYazi)) ogeler.Add(new Yazi(altYazi, Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(6)) });
            ogeler.Add(tablo);
            tablo.Height = 1;
            tablo.Layout += (s, e) => Diz();
            Ekle(ogeler.ToArray());
        }

        public int EtiketGenislik { set { etiketGenislik = value; } }

        public void Ekle(string etiket, string deger) { Ekle(etiket, deger, null, null); }

        public void Ekle(string etiket, string deger, string linkMetin, Action tikla)
        {
            if (string.IsNullOrEmpty(deger)) return;
            Satir s = new Satir();
            s.Sol = new Yazi(etiket, Tema.Normal, Tema.Soluk) { BackColor = Tema.Kart };
            s.Sag = new Yazi(deger, Tema.Normal, Tema.Metin) { BackColor = Tema.Kart };
            tablo.Controls.Add(s.Sol); tablo.Controls.Add(s.Sag);
            if (linkMetin != null)
            {
                LinkLabel l = new LinkLabel { Text = linkMetin, AutoSize = true, BackColor = Tema.Kart, LinkColor = Tema.Vurgu, LinkBehavior = LinkBehavior.HoverUnderline };
                l.LinkClicked += (o, e) => tikla();
                tablo.Controls.Add(l);
                s.Link = l;
            }
            satirlar.Add(s);
            s.Sol.SizeChanged += (o, e) => tablo.PerformLayout();
            s.Sag.SizeChanged += (o, e) => tablo.PerformLayout();
        }

        public void AraBaslik(string metin)
        {
            Satir s = new Satir { Baslik = true };
            s.Sol = new Yazi(metin, Tema.Kalin, Tema.Metin) { BackColor = Tema.Kart, Padding = new Padding(0, Tema.O(10), 0, Tema.O(2)) };
            tablo.Controls.Add(s.Sol);
            satirlar.Add(s);
            s.Sol.SizeChanged += (o, e) => tablo.PerformLayout();
        }

        bool diziliyor;
        void Diz()
        {
            if (diziliyor) return;
            diziliyor = true;
            try
            {
                int w = tablo.Width, y = 0, ara = Tema.O(4);
                int etW = Math.Min(etiketGenislik, Math.Max(Tema.O(90), w / 3));
                foreach (Satir s in satirlar)
                {
                    if (s.Baslik) { s.Sol.SetBounds(0, y, w, s.Sol.Height); y += s.Sol.Height; continue; }
                    int linkW = s.Link != null ? s.Link.PreferredSize.Width + Tema.O(12) : 0;
                    s.Sol.SetBounds(0, y, etW, s.Sol.Height);
                    s.Sag.SetBounds(etW, y, Math.Max(Tema.O(40), w - etW - linkW), s.Sag.Height);
                    if (s.Link != null) s.Link.Location = new Point(w - s.Link.PreferredSize.Width, y);
                    y += Math.Max(s.Sol.Height, s.Sag.Height) + ara;
                }
                if (tablo.Height != y) tablo.Height = y;
            }
            finally { diziliyor = false; }
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
            Tema.CizimAyari(this);
            renk = Tema.SeviyeRengi(seviye);
            BackColor = Tema.Arka;
            Padding = new Padding(Tema.O(18), Tema.O(10), Tema.O(12), Tema.O(10) + bosluk);

            Yazi y1 = new Yazi(etiket.ToUpper(Tema.Tr), Tema.Kucuk, Tema.Soluk) { BackColor = Tema.Kart };
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
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1 - bosluk);
            Tema.KartCiz(e.Graphics, r, Tema.O(8));
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
            Tema.CizimAyari(this);
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

        public int GerekenYukseklik { get { return Padding.Vertical + baslik.Height + aciklama.Height + ozet.Height; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Tema.KartCiz(g, new Rectangle(0, 0, Width - 1, Height - 1), Tema.O(10));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
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
        public event EventHandler IcerikDegisti;

        public SecimKarti(string baslik, string sure, string aciklama)
        {
            Tema.CizimAyari(this);
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

    // Çizgi grafik: tarama geçmişi. Fareyle üzerine gelince o taramanın değerlerini gösterir.
    class Grafik : Panel
    {
        public class Seri { public string Ad; public Color Renk; public double?[] D; }

        readonly string baslik;
        readonly string[] xEtiket;
        readonly List<Seri> seriler = new List<Seri>();
        readonly double? ySabitMin, ySabitMax;
        readonly Func<double, string> bicim;
        int secili = -1;
        readonly int bosluk = Tema.O(10);

        public Grafik(string baslik, string[] xEtiket, double? yMin, double? yMax, Func<double, string> bicim)
        {
            Tema.CizimAyari(this);
            BackColor = Tema.Arka;
            this.baslik = baslik; this.xEtiket = xEtiket; ySabitMin = yMin; ySabitMax = yMax;
            this.bicim = bicim ?? (v => v.ToString("N0", Tema.Tr));
            Height = Tema.O(230);
        }

        public void SeriEkle(string ad, double?[] d)
        {
            seriler.Add(new Seri { Ad = ad, Renk = Tema.SeriRenkleri[seriler.Count % Tema.SeriRenkleri.Length], D = d });
        }

        public bool VeriVar { get { return seriler.Any(s => s.D.Any(v => v.HasValue)); } }

        Rectangle Alan()
        {
            int sol = Tema.O(64), ust = Tema.O(72), sag = Tema.O(24), alt = Tema.O(34);
            return new Rectangle(sol, ust, Math.Max(10, Width - sol - sag), Math.Max(10, Height - bosluk - ust - alt));
        }

        int X(Rectangle a, int i) { int n = xEtiket.Length; return n <= 1 ? a.X + a.Width / 2 : a.X + (int)((long)a.Width * i / (n - 1)); }

        void Aralik(out double min, out double max)
        {
            List<double> hepsi = seriler.SelectMany(s => s.D).Where(v => v.HasValue).Select(v => v.Value).ToList();
            min = ySabitMin ?? (hepsi.Count > 0 ? hepsi.Min() : 0);
            max = ySabitMax ?? (hepsi.Count > 0 ? hepsi.Max() : 1);
            if (!ySabitMin.HasValue || !ySabitMax.HasValue)
            {
                double ara = max - min;
                if (ara < 1e-9) ara = Math.Max(1, Math.Abs(max) * 0.1);
                if (!ySabitMin.HasValue) min = Math.Max(0, min - ara * 0.25);
                if (!ySabitMax.HasValue) max = max + ara * 0.25;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Tema.KartCiz(g, new Rectangle(0, 0, Width - 1, Height - 1 - bosluk), Tema.O(10));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (SolidBrush m = new SolidBrush(Tema.Metin)) g.DrawString(baslik, Tema.Kalin, m, Tema.O(20), Tema.O(14));

            // açıklama: başlığın altında, soldan sağa (uzun disk adları başlıkla çakışmasın)
            int lx = Tema.O(20), ly = Tema.O(38);
            foreach (Seri sr in seriler)
            {
                string ad = sr.Ad;
                SizeF sz = g.MeasureString(ad, Tema.Kucuk);
                if (lx + sz.Width + Tema.O(14) > Width - Tema.O(20) && lx > Tema.O(20)) break; // sığmayanları gösterme
                using (SolidBrush s = new SolidBrush(sr.Renk)) g.FillEllipse(s, lx, ly + Tema.O(4), Tema.O(9), Tema.O(9));
                using (SolidBrush s = new SolidBrush(Tema.Soluk)) g.DrawString(ad, Tema.Kucuk, s, lx + Tema.O(13), ly);
                lx += Tema.O(13) + (int)sz.Width + Tema.O(16);
            }

            Rectangle a = Alan();
            if (!VeriVar)
            {
                using (SolidBrush s = new SolidBrush(Tema.Soluk))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Henüz veri yok", Tema.Normal, s, a, sf);
                return;
            }
            double min, max; Aralik(out min, out max);
            Func<double, int> Y = v => a.Bottom - (int)((v - min) / (max - min) * a.Height);

            using (Pen izgara = new Pen(Color.FromArgb(236, 238, 242)))
            using (SolidBrush s = new SolidBrush(Tema.Soluk))
            using (StringFormat sag = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i <= 4; i++)
                {
                    double v = min + (max - min) * i / 4;
                    int y = Y(v);
                    g.DrawLine(izgara, a.X, y, a.Right, y);
                    g.DrawString(bicim(v), Tema.Kucuk, s, new RectangleF(0, y - Tema.O(8), a.X - Tema.O(8), Tema.O(16)), sag);
                }
                int n = xEtiket.Length, adim = Math.Max(1, (int)Math.Ceiling(n / 6.0));
                using (StringFormat orta = new StringFormat { Alignment = StringAlignment.Center })
                    for (int i = 0; i < n; i++)
                        if (i % adim == 0 || i == n - 1)
                        {
                            // Etiket kartın dışına taşmasın
                            float lx0 = Math.Max(Tema.O(8), Math.Min(Width - Tema.O(8) - Tema.O(84), X(a, i) - Tema.O(42)));
                            g.DrawString(xEtiket[i], Tema.Kucuk, s, new RectangleF(lx0, a.Bottom + Tema.O(8), Tema.O(84), Tema.O(16)), orta);
                        }
            }

            if (secili >= 0)
                using (Pen p = new Pen(Color.FromArgb(200, 205, 214)) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, X(a, secili), a.Y, X(a, secili), a.Bottom);

            foreach (Seri sr in seriler)
            {
                using (Pen p = new Pen(sr.Renk, Tema.O(2)) { LineJoin = LineJoin.Round })
                using (SolidBrush b = new SolidBrush(sr.Renk))
                {
                    Point? onceki = null;
                    for (int i = 0; i < sr.D.Length; i++)
                    {
                        if (!sr.D[i].HasValue) { onceki = null; continue; }
                        Point pt = new Point(X(a, i), Y(sr.D[i].Value));
                        if (onceki.HasValue) g.DrawLine(p, onceki.Value, pt);
                        int r = i == secili ? Tema.O(5) : Tema.O(3);
                        g.FillEllipse(b, pt.X - r, pt.Y - r, 2 * r, 2 * r);
                        onceki = pt;
                    }
                }
            }

            if (seriler.All(s => s.D.Count(v => v.HasValue) < 2))
                using (SolidBrush s = new SolidBrush(Tema.Soluk))
                    g.DrawString("Değişimi görmek için en az iki tarama gerekir", Tema.Kucuk, s, a.X + Tema.O(6), a.Y + Tema.O(2));

            if (secili >= 0)
            {
                List<string> satir = new List<string> { xEtiket[secili] };
                foreach (Seri sr in seriler) satir.Add(sr.Ad + ": " + (sr.D[secili].HasValue ? bicim(sr.D[secili].Value) : "—"));
                string metin = string.Join("\n", satir);
                SizeF sz = g.MeasureString(metin, Tema.Kucuk);
                int kx = X(a, secili) + Tema.O(10), ky = a.Y + Tema.O(4);
                if (kx + sz.Width + Tema.O(16) > a.Right) kx = X(a, secili) - (int)sz.Width - Tema.O(26);
                Rectangle kutu = new Rectangle(kx, ky, (int)sz.Width + Tema.O(16), (int)sz.Height + Tema.O(10));
                using (GraphicsPath p = Tema.Yuvarlak(kutu, Tema.O(6)))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 255, 255, 255)))
                using (Pen k = new Pen(Tema.Kenar))
                { g.FillPath(b, p); g.DrawPath(k, p); }
                using (SolidBrush s = new SolidBrush(Tema.Metin)) g.DrawString(metin, Tema.Kucuk, s, kx + Tema.O(8), ky + Tema.O(5));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (xEtiket.Length == 0 || !VeriVar) return;
            Rectangle a = Alan();
            int en = -1, enUzak = int.MaxValue;
            for (int i = 0; i < xEtiket.Length; i++) { int d = Math.Abs(X(a, i) - e.X); if (d < enUzak) { enUzak = d; en = i; } }
            if (e.Y < a.Y - Tema.O(20) || e.Y > a.Bottom + Tema.O(10)) en = -1;
            if (en != secili) { secili = en; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (secili != -1) { secili = -1; Invalidate(); }
        }
    }

    // Üst gezinme çubuğundaki sekme
    class Sekme : Label
    {
        bool aktif;
        public Sekme(string metin)
        {
            Tema.CizimAyari(this);
            Text = metin;
            AutoSize = false;
            Font = Tema.Kalin;
            TextAlign = ContentAlignment.MiddleCenter;
            Cursor = Cursors.Hand;
            BackColor = Color.White;
            ForeColor = Tema.Soluk;
            Width = TextRenderer.MeasureText(metin, Tema.Kalin).Width + Tema.O(28);
            Margin = new Padding(0);
        }
        public bool Aktif { get { return aktif; } set { aktif = value; ForeColor = value ? Tema.Vurgu : Tema.Soluk; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (!aktif) ForeColor = Tema.Metin; }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!aktif) ForeColor = Tema.Soluk; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (aktif) using (SolidBrush b = new SolidBrush(Tema.Vurgu)) e.Graphics.FillRectangle(b, Tema.O(10), Height - Tema.O(3), Width - Tema.O(20), Tema.O(3));
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
        public static string S(object o, string a) { object v = Al(o, a); return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture); }
        public static int? I(object o, string a) { object v = Al(o, a); if (v == null) return null; try { return Convert.ToInt32(v); } catch { return null; } }
        public static double? D(object o, string a) { object v = Al(o, a); if (v == null) return null; try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return null; } }
        public static bool B(object o, string a) { object v = Al(o, a); return v is bool && (bool)v; }
        public static IEnumerable<object> L(object o, string a)
        {
            object v = Al(o, a);
            if (v is IEnumerable && !(v is string) && !(v is IDictionary<string, object>)) return ((IEnumerable)v).Cast<object>();
            return v == null ? Enumerable.Empty<object>() : new[] { v };
        }
        public static string Sayi(double? v, int ondalik) { return v.HasValue ? v.Value.ToString(ondalik == 0 ? "N0" : "N" + ondalik, Tema.Tr) : null; }
    }

    // Raporlar\gecmis klasöründeki taramaların özeti
    class TaramaOzeti
    {
        public DateTime Zaman;
        public string Tarih;
        public bool Tam, Yonetici;
        public int? Donanim, Bakim;
        public double? CpuCok, CpuTek, Bellek;
        public Dictionary<string, double?> DiskOkuma = new Dictionary<string, double?>();
        public Dictionary<string, double?> DiskAsinma = new Dictionary<string, double?>();
        public double? Pil;
        public int Sorun, Uyari;
        public string RaporYolu;

        public static List<TaramaOzeti> Yukle()
        {
            List<TaramaOzeti> liste = new List<TaramaOzeti>();
            string dizin = Path.Combine(Ortak.RaporDizin, "gecmis");
            if (!Directory.Exists(dizin)) return liste;
            JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (string f in Directory.GetFiles(dizin, "*.json").OrderBy(x => x))
            {
                try
                {
                    object v = js.DeserializeObject(File.ReadAllText(f, Encoding.UTF8));
                    object meta = J.Al(v, "meta"), puan = J.Al(v, "puan"), cpu = J.Al(J.Al(v, "cpu"), "test");
                    TaramaOzeti t = new TaramaOzeti();
                    DateTime z;
                    t.Zaman = DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out z) ? z : File.GetLastWriteTime(f);
                    t.Tarih = J.S(meta, "tarih");
                    t.Tam = J.B(meta, "testYapildi");
                    t.Yonetici = J.B(meta, "yonetici");
                    t.RaporYolu = J.S(meta, "raporYolu");
                    if (puan is IDictionary<string, object>) { t.Donanim = J.I(puan, "donanim"); t.Bakim = J.I(puan, "bakim"); }
                    t.CpuCok = J.D(cpu, "cokCekirdek"); t.CpuTek = J.D(cpu, "tekCekirdek");
                    t.Bellek = J.D(J.Al(v, "ram"), "bantGBs");
                    foreach (object d in J.L(v, "diskler"))
                    {
                        string ad = J.S(d, "ad");
                        if (ad == null) continue;
                        t.DiskOkuma[ad] = J.D(J.Al(d, "test"), "siraliOkuma");
                        t.DiskAsinma[ad] = J.D(d, "asinma");
                    }
                    object pil = J.L(J.Al(v, "pil"), "piller").FirstOrDefault();
                    t.Pil = J.D(pil, "saglikYuzde");
                    foreach (object b in J.L(v, "bulgular")) { string s = J.S(b, "seviye"); if (s == "bad") t.Sorun++; else if (s == "warn") t.Uyari++; }
                    liste.Add(t);
                }
                catch { }
            }
            return liste;
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
        readonly ToolTip ipucu = new ToolTip { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100 };

        // sayfalar
        readonly Dictionary<string, Panel> sayfalar = new Dictionary<string, Panel>();
        readonly Dictionary<string, Sekme> sekmeler = new Dictionary<string, Sekme>();
        string aktifSayfa;

        // tarama seçenekleri
        SecimKarti secHizli, secTam;
        readonly CheckBox chkYonetici = new CheckBox();
        Button btnBasla;
        readonly Yazi lblBaslangicHata = new Yazi("", Tema.Normal, Tema.Kirmizi);

        // tarama ilerlemesi
        readonly Yazi lblTaramaBaslik = new Yazi("Taranıyor...", Tema.Baslik, Tema.Metin);
        readonly Yazi lblAdim = new Yazi("Başlatılıyor...", Tema.Kalin, Tema.Vurgu);
        readonly Yazi lblSure = new Yazi("", Tema.Normal, Tema.Soluk);
        readonly ProgressBar bar = new ProgressBar();
        readonly ListBox lstKayit = new ListBox();
        Button btnIptal;

        // temizlik
        List<TemizlikKategorisi> temizlikKategorileri;
        readonly Dictionary<TemizlikKategorisi, CheckBox> temizlikSecim = new Dictionary<TemizlikKategorisi, CheckBox>();
        readonly Dictionary<TemizlikKategorisi, Label> temizlikBoyut = new Dictionary<TemizlikKategorisi, Label>();
        Button btnTemizle, btnYenidenHesapla;
        readonly Yazi lblTemizlikDurum = new Yazi("", Tema.Kalin, Tema.Soluk);
        bool temizlikSuruyor;

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
        int adimNo, adimToplam = 13;

        const string IpucuDonanim = "Nasıl hesaplanır: 100'den başlar. Donanımla ilgili her sorun −20, her uyarı −7 puan düşürür.\n\"Bilgi\" maddeleri puanı etkilemez. Ayrıntı için \"Nasıl çalışıyor?\" sekmesine bakın.";
        const string IpucuBakim = "Nasıl hesaplanır: 100'den başlar. Bakımla ilgili her sorun −15, her uyarı −6 puan düşürür.\nKüçük güncellemeler ve \"Bilgi\" maddeleri puanı etkilemez.";

        public AnaForm()
        {
            using (Graphics g = CreateGraphics()) Tema.Olcek = g.DpiX / 96f;
            SuspendLayout();
            Text = Ortak.UygulamaAdi;
            Font = Tema.Normal;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Tema.Arka;
            ClientSize = new Size(Tema.O(980), Tema.O(720));
            MinimumSize = new Size(Tema.O(780), Tema.O(580));
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            foreach (string ad in new[] { "ana", "tara", "ilerleme", "sonuc", "bilgi", "temizlik", "nasil" })
            {
                Panel p = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false, BackColor = Tema.Arka };
                p.Padding = new Padding(Tema.O(32), Tema.O(22), Tema.O(32), Tema.O(24));
                sayfalar[ad] = p;
                Controls.Add(p);
            }
            TaramaSecenekleriKur();
            IlerlemeKur();
            NasilKur();
            Controls.Add(BantKur());
            Controls.Add(SekmeKur());
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
            Git("ana");
        }

        // ---------------------------------------------------------------- gezinme
        void Git(string ad)
        {
            if (ad == "tara" && taramaSuruyor) ad = "ilerleme";
            if (ad == "sonuc" && SonucuYukle() == null) ad = "ana";
            switch (ad)
            {
                case "ana": AnaSayfaKur(); break;
                case "sonuc": SonuclariKur(); break;
                case "bilgi": BilgiKur(); break;
                case "temizlik": TemizlikKur(); break;
            }
            aktifSayfa = ad;
            foreach (KeyValuePair<string, Panel> kv in sayfalar) kv.Value.Visible = kv.Key == ad;
            sayfalar[ad].BringToFront();
            string sekme = ad == "ilerleme" ? "tara" : ad;
            foreach (KeyValuePair<string, Sekme> kv in sekmeler) kv.Value.Aktif = kv.Key == sekme;
        }

        // Sayfanın içeriğini temizleyip yeniden oluşturur
        Panel Bosalt(string ad)
        {
            Panel p = sayfalar[ad];
            p.SuspendLayout();
            foreach (Control c in p.Controls.Cast<Control>().ToList()) c.Dispose();
            p.Controls.Clear();
            p.AutoScrollPosition = new Point(0, 0);
            return p;
        }

        void Bitir(Panel p, List<Control> ogeler)
        {
            Tema.Diz(p, ogeler.ToArray());
            p.ResumeLayout(true);
            p.AutoScrollPosition = new Point(0, 0);
        }

        void IpucuVer(Control c, string metin)
        {
            ipucu.SetToolTip(c, metin);
            foreach (Control a in c.Controls) IpucuVer(a, metin);
        }

        // ---------------------------------------------------------------- çerçeve
        Control UstKur()
        {
            Panel ust = new Panel { Dock = DockStyle.Top, Height = Tema.O(54), BackColor = Tema.Vurgu, Padding = new Padding(Tema.O(28), 0, Tema.O(24), 0) };
            Label ad = new Label { Text = "Sistem Checkup", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            LinkLabel denetle = new LinkLabel { Text = "Güncellemeleri denetle", AutoSize = false, Dock = DockStyle.Right, Width = Tema.O(190), TextAlign = ContentAlignment.MiddleRight, LinkColor = Color.White, ActiveLinkColor = Color.White, LinkBehavior = LinkBehavior.HoverUnderline };
            denetle.LinkClicked += (s, e) => GuncelleyiciAc(false);
            ust.Controls.Add(ad);
            ust.Controls.Add(denetle);
            return ust;
        }

        Control SekmeKur()
        {
            FlowLayoutPanel cubuk = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Tema.O(44), BackColor = Color.White, Padding = new Padding(Tema.O(18), 0, Tema.O(18), 0), WrapContents = false };
            cubuk.Paint += (s, e) => { using (Pen p = new Pen(Tema.Kenar)) e.Graphics.DrawLine(p, 0, cubuk.Height - 1, cubuk.Width, cubuk.Height - 1); };
            foreach (string[] t in new[] { new[] { "ana", "Ana sayfa" }, new[] { "tara", "Yeni tarama" }, new[] { "sonuc", "Son sonuç" }, new[] { "bilgi", "Sistem bilgileri" }, new[] { "temizlik", "Temizlik" }, new[] { "nasil", "Nasıl çalışıyor?" } })
            {
                Sekme s = new Sekme(t[1]) { Height = Tema.O(44) };
                string ad = t[0];
                s.MouseDown += (o, e) => Git(ad);
                sekmeler[ad] = s;
                cubuk.Controls.Add(s);
            }
            return cubuk;
        }

        Control BantKur()
        {
            Button b = Tema.Buton("Şimdi güncelle", true);
            b.Dock = DockStyle.Right;
            b.Click += (s, e) => GuncelleyiciAc(true);
            bant.Dock = DockStyle.Top;
            bant.Padding = new Padding(Tema.O(28), Tema.O(7), Tema.O(24), Tema.O(7));
            bant.Height = b.PreferredSize.Height + bant.Padding.Vertical;
            bant.BackColor = Color.FromArgb(253, 241, 221);
            bant.Visible = false;
            lblBant.Dock = DockStyle.Fill;
            lblBant.TextAlign = ContentAlignment.MiddleLeft;
            lblBant.AutoEllipsis = true;
            lblBant.Padding = new Padding(0, 0, Tema.O(12), 0);
            lblBant.ForeColor = Color.FromArgb(110, 70, 0);
            lblBant.Font = Tema.Kalin;
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

        Control ButonSatiri(params Button[] butonlar)
        {
            FlowLayoutPanel f = new FlowLayoutPanel { Height = butonlar.Length > 0 ? butonlar[0].PreferredSize.Height + Tema.O(8) : 0, BackColor = Color.Transparent, WrapContents = false };
            for (int i = 0; i < butonlar.Length; i++) { butonlar[i].Margin = new Padding(i == 0 ? 0 : Tema.O(8), 0, 0, 0); f.Controls.Add(butonlar[i]); }
            return f;
        }

        Button Buton(string metin, bool ana, Action is_)
        {
            Button b = Tema.Buton(metin, ana);
            b.Click += (s, e) => is_();
            return b;
        }

        TableLayoutPanel IkiSutun(Control sol, Control sag, int yukseklik)
        {
            TableLayoutPanel t = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Height = yukseklik, BackColor = Color.Transparent };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            sol.Dock = DockStyle.Fill; sol.Margin = new Padding(0, 0, Tema.O(8), 0);
            t.Controls.Add(sol, 0, 0);
            if (sag != null) { sag.Dock = DockStyle.Fill; sag.Margin = new Padding(Tema.O(8), 0, 0, 0); t.Controls.Add(sag, 1, 0); }
            return t;
        }

        // ---------------------------------------------------------------- ana sayfa
        void AnaSayfaKur()
        {
            Panel p = Bosalt("ana");
            List<Control> o = new List<Control>();
            List<TaramaOzeti> gecmis = TaramaOzeti.Yukle();
            object son = SonucuYukle();

            if (son == null || gecmis.Count == 0)
            {
                o.Add(new Yazi("Hoş geldiniz!", Tema.Baslik, Tema.Metin));
                o.Add(new Yazi("Sistem Checkup bilgisayarınızın parçalarının sağlıklı çalışıp çalışmadığını kontrol eder, sürücülerinizin güncel olup olmadığına bakar ve gereksiz dosyaları temizler. Başlamak için bir tarama yapın; sonuçlar ve zaman içindeki değişim burada görünecek.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(4), 0, Tema.O(18)) });
                o.Add(ButonSatiri(Buton("İlk taramayı başlat", true, () => Git("tara")), Buton("Nasıl çalışıyor?", false, () => Git("nasil"))));
                Bitir(p, o);
                return;
            }

            TaramaOzeti sonT = gecmis[gecmis.Count - 1];
            object meta = J.Al(son, "meta"), puan = J.Al(son, "puan"), sistem = J.Al(son, "sistem");
            List<object> bulgular = J.L(son, "bulgular").ToList();
            int donanim = J.I(puan, "donanim") ?? 0, bakim = J.I(puan, "bakim") ?? 0;
            Func<string, string, int> say = (tur, sev) => bulgular.Count(b => J.S(b, "tur") == tur && J.S(b, "seviye") == sev);

            o.Add(new Yazi(Karar(donanim, bakim), Tema.Baslik, Tema.Metin));
            o.Add(new Yazi("Son tarama: " + J.S(meta, "tarih") + "  ·  " + (J.B(meta, "testYapildi") ? "tam tarama" : "hızlı tarama") + "  ·  toplam " + gecmis.Count + " tarama kaydı", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(2), 0, Tema.O(12)) });

            PuanKarti pk1 = new PuanKarti(), pk2 = new PuanKarti();
            pk1.Ayarla(donanim, "Donanım sağlığı", "Parçaların fiziksel durumu. Düşükse bir parça arızalanıyor olabilir.", say("donanim", "bad"), say("donanim", "warn"));
            pk2.Ayarla(bakim, "Bakım ve güncellemeler", "Sürücüler, güncellemeler, disk alanı ve ayarlar. Düşük olması bozukluk anlamına gelmez.", say("bakim", "bad"), say("bakim", "warn"));
            IpucuVer(pk1, IpucuDonanim);
            IpucuVer(pk2, IpucuBakim);
            o.Add(IkiSutun(pk1, pk2, Tema.O(140)));
            o.Add(Tema.Bosluk(12));
            o.Add(ButonSatiri(Buton("Yeni tarama", true, () => Git("tara")), Buton("Son sonucu göster", false, () => Git("sonuc")), Buton("Sistem bilgileri", false, () => Git("bilgi")), Buton("Temizlik", false, () => Git("temizlik"))));
            o.Add(Tema.Bosluk(16));

            // Grafikler: son 30 tarama
            string[] x = gecmis.Select(t => t.Zaman.ToString("dd.MM HH:mm", Tema.Tr)).ToArray();
            Grafik saglik = new Grafik("Sağlık puanları", x, 0, 100, v => v.ToString("N0", Tema.Tr));
            saglik.SeriEkle("Donanım", gecmis.Select(t => (double?)t.Donanim).ToArray());
            saglik.SeriEkle("Bakım", gecmis.Select(t => (double?)t.Bakim).ToArray());
            ipucu.SetToolTip(saglik, "Her nokta bir tarama. Üzerine gelince o taramanın puanları görünür.");
            o.Add(saglik);

            List<TaramaOzeti> tamlar = gecmis.Where(t => t.Tam).ToList();
            string[] xt = tamlar.Select(t => t.Zaman.ToString("dd.MM", Tema.Tr)).ToArray();
            List<Control> kucukler = new List<Control>();
            if (tamlar.Count > 0)
            {
                Grafik cpu = new Grafik("İşlemci performansı (puan)", xt, null, null, v => v.ToString("N0", Tema.Tr));
                cpu.SeriEkle("Çok çekirdek", tamlar.Select(t => t.CpuCok).ToArray());
                ipucu.SetToolTip(cpu, "Tam taramalardaki 10 saniyelik işlemci testi. Belirgin düşüş ısınma veya arka planda yük olduğunu gösterebilir.");
                kucukler.Add(cpu);
                Grafik disk = new Grafik("Disk okuma hızı (MB/s)", xt, 0, null, v => v.ToString("N0", Tema.Tr));
                foreach (string ad in tamlar.SelectMany(t => t.DiskOkuma.Keys).Distinct())
                    disk.SeriEkle(ad, tamlar.Select(t => t.DiskOkuma.ContainsKey(ad) ? t.DiskOkuma[ad] : null).ToArray());
                if (disk.VeriVar) kucukler.Add(disk);
            }
            if (gecmis.Any(t => t.DiskAsinma.Values.Any(v => v.HasValue)))
            {
                Grafik asinma = new Grafik("SSD ömür kullanımı (%)", x, 0, null, v => "%" + v.ToString("N0", Tema.Tr));
                foreach (string ad in gecmis.SelectMany(t => t.DiskAsinma.Where(kv => kv.Value.HasValue).Select(kv => kv.Key)).Distinct())
                    asinma.SeriEkle(ad, gecmis.Select(t => t.DiskAsinma.ContainsKey(ad) ? t.DiskAsinma[ad] : null).ToArray());
                kucukler.Add(asinma);
            }
            if (gecmis.Any(t => t.Pil.HasValue))
            {
                Grafik pil = new Grafik("Pil kalan kapasite (%)", x, 0, 100, v => "%" + v.ToString("N0", Tema.Tr));
                pil.SeriEkle("Pil", gecmis.Select(t => t.Pil).ToArray());
                kucukler.Add(pil);
            }
            for (int i = 0; i < kucukler.Count; i += 2)
                o.Add(IkiSutun(kucukler[i], i + 1 < kucukler.Count ? kucukler[i + 1] : null, Tema.O(230)));
            if (tamlar.Count == 0)
                o.Add(new Yazi("İşlemci ve disk hız grafiklerini görmek için bir \"Tam tarama\" yapın.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(10)) });

            // Geçmiş listesi
            BilgiKarti liste = new BilgiKarti("Geçmiş taramalar", "En yeni üstte. Her taramanın ayrıntılı raporunu açabilirsiniz.");
            liste.EtiketGenislik = Tema.O(150);
            foreach (TaramaOzeti t in Enumerable.Reverse(gecmis).Take(15))
            {
                string ozet = (t.Tam ? "Tam" : "Hızlı") + " tarama  ·  Donanım " + (t.Donanim.HasValue ? t.Donanim.ToString() : "—") + "  ·  Bakım " + (t.Bakim.HasValue ? t.Bakim.ToString() : "—") +
                    (t.Sorun + t.Uyari > 0 ? "  ·  " + (t.Sorun > 0 ? t.Sorun + " sorun " : "") + (t.Uyari > 0 ? t.Uyari + " uyarı" : "") : "  ·  sorun yok");
                string yol = t.RaporYolu;
                if (yol != null && File.Exists(yol)) liste.Ekle(t.Zaman.ToString("dd.MM.yyyy HH:mm", Tema.Tr), ozet, "Raporu aç", () => Ortak.Ac(yol, null));
                else liste.Ekle(t.Zaman.ToString("dd.MM.yyyy HH:mm", Tema.Tr), ozet);
            }
            o.Add(liste);
            Bitir(p, o);
        }

        static string Karar(int donanim, int? bakim)
        {
            return donanim >= 85 ? (bakim.HasValue && bakim < 75 ? "Donanımınız sağlıklı, birkaç bakım işi var" : "Bilgisayarınız sağlıklı görünüyor")
                : donanim >= 60 ? "Donanımda dikkat edilmesi gereken noktalar var" : "Donanımda ciddi sorun belirtileri var";
        }

        // ---------------------------------------------------------------- tarama seçenekleri
        void TaramaSecenekleriKur()
        {
            Panel sayfa = sayfalar["tara"];
            Yazi h1 = new Yazi("Yeni tarama", Tema.Baslik, Tema.Metin);
            Yazi alt = new Yazi("Disk ve SSD sağlığı, ekran kartı, bellek, sürücüler, bağlı cihazlar ve hata kayıtları incelenir. Tarama bilgisayarınızda yapılır; hiçbir kişisel veri dışarı gönderilmez.", Tema.Normal, Tema.Soluk);
            alt.Padding = new Padding(0, Tema.O(4), 0, Tema.O(18));

            secHizli = new SecimKarti("Hızlı tarama", "yaklaşık 30-60 saniye",
                "Parçaların sağlık durumu, sürücüler, bağlı cihazlar ve son 30 günün hata kayıtları. Bilgisayarı yormaz; istediğiniz zaman çalıştırabilirsiniz.");
            secTam = new SecimKarti("Tam tarama", "yaklaşık 2 dakika",
                "Hızlı taramaya ek olarak disk, işlemci ve bellek hız testleri. Her diske geçici olarak 1 GB dosya yazılıp hemen silinir; işlemci 10 saniye tam güçte çalışır, fanlar kısa süre hızlanabilir. Bu sırada başka iş yapmamanız sonuçları doğru kılar.");
            secHizli.Secili = !ayarlar.tamTarama;
            secTam.Secili = ayarlar.tamTarama;
            secHizli.Secildi += (s, e) => secTam.Secili = false;
            secTam.Secildi += (s, e) => secHizli.Secili = false;
            TableLayoutPanel secimler = IkiSutun(secHizli, secTam, Tema.O(206));
            Action ayarla = () =>
            {
                int h = Math.Max(secHizli.IcerikYuksekligi, secTam.IcerikYuksekligi) + Tema.O(4);
                if (h > Tema.O(60) && secimler.Height != h) secimler.Height = h;
            };
            // Yerleşim bitince yeniden hesapla: hızlı yeniden boyutlandırmada iki kartın yazıları farklı anlarda kaydığı için
            EventHandler yukseklik = (s, e) => { if (secimler.IsHandleCreated) secimler.BeginInvoke(ayarla); else ayarla(); };
            secHizli.IcerikDegisti += yukseklik;
            secTam.IcerikDegisti += yukseklik;
            secimler.SizeChanged += yukseklik;

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
            Tema.Diz(sayfa, h1, alt, secimler, Tema.Bosluk(16), chkYonetici, yoneticiAcik, butonSatiri, lblBaslangicHata);
        }

        void BaslangicMesaji(string metin, bool hata)
        {
            lblBaslangicHata.ForeColor = hata ? Tema.Kirmizi : Tema.Soluk;
            lblBaslangicHata.Text = metin;
        }

        // ---------------------------------------------------------------- tarama ilerlemesi
        void IlerlemeKur()
        {
            Panel sayfa = sayfalar["ilerleme"];
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

            sayfa.AutoScroll = false;
            sayfa.Controls.Add(kayitKap);
            sayfa.Controls.Add(alt);
            sayfa.Controls.Add(ust);
        }

        void TaramaBaslat(bool yonetici)
        {
            if (temizlikSuruyor) { BaslangicMesaji("Temizlik bitince taramayı başlatabilirsiniz.", true); return; }
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
                BaslangicMesaji("Tarama başlatılamadı: " + ex.Message, true);
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
            Git("ilerleme");
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
            if (adimNo > 0 && adimNo <= adimToplam)
            {
                int taban = 1000 * (adimNo - 1) / adimToplam, tavan = 1000 * adimNo / adimToplam;
                if (bar.Value < tavan - 10) bar.Value = Math.Max(taban, Math.Min(tavan - 10, bar.Value + 3));
            }

            bool kapandi = false;
            try { kapandi = surec != null && surec.HasExited; } catch { }
            if (kapandi && !bitti)
            {
                System.Threading.Thread.Sleep(150);
                if (!bitti) IlerlemeSonu();
            }
        }

        void IlerlemeSonu()
        {
            try
            {
                string tum = File.ReadAllText(ilerlemeYolu, Encoding.UTF8);
                if (tum.Contains("\nBITTI|") || tum.StartsWith("BITTI|") || tum.Contains("\nIPTAL") || tum.Contains("\nHATA|")) return;
            }
            catch { }
            TaramaBitti();
            BaslangicMesaji("Tarama beklenmedik şekilde durdu. Tekrar deneyin; sorun sürerse \"Yönetici izniyle tara\" seçeneğini kapatıp deneyin.", true);
            Git("tara");
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
                    Git("sonuc");
                    break;
                case "IPTAL":
                    bitti = true;
                    TaramaBitti();
                    BaslangicMesaji("Tarama iptal edildi.", false);
                    Git("tara");
                    break;
                case "HATA":
                    bitti = true;
                    TaramaBitti();
                    BaslangicMesaji("Tarama sırasında hata oluştu: " + (p.Length > 1 ? p[1] : ""), true);
                    Git("tara");
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
            if (temizlikSuruyor) { e.Cancel = true; MessageBox.Show(this, "Temizlik bitene kadar bekleyin.", Ortak.UygulamaAdi); return; }
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

        string RaporYolu(object meta)
        {
            string raporYolu = J.S(meta, "raporYolu");
            if (string.IsNullOrEmpty(raporYolu) || !File.Exists(raporYolu))
                raporYolu = Directory.Exists(Ortak.RaporDizin) ? Directory.GetFiles(Ortak.RaporDizin, "SistemCheckup_*.html").OrderByDescending(f => f).FirstOrDefault() : null;
            return raporYolu;
        }

        void SonuclariKur()
        {
            object v = SonucuYukle();
            Panel p = Bosalt("sonuc");
            List<Control> liste = new List<Control>();

            object meta = J.Al(v, "meta"), puan = J.Al(v, "puan"), sistem = J.Al(v, "sistem");
            List<object> bulgular = J.L(v, "bulgular").ToList();
            int donanim = puan is IDictionary<string, object> ? (J.I(puan, "donanim") ?? 0) : (J.I(v, "puan") ?? 0);
            int? bakim = puan is IDictionary<string, object> ? J.I(puan, "bakim") : null;
            Func<string, string, int> say = (tur, sev) => bulgular.Count(b => J.S(b, "tur") == tur && J.S(b, "seviye") == sev);

            liste.Add(new Yazi(Karar(donanim, bakim), Tema.Baslik, Tema.Metin));
            liste.Add(new Yazi(J.S(meta, "tarih") + "  ·  " + (J.B(meta, "testYapildi") ? "tam tarama" : "hızlı tarama") + "  ·  " +
                (J.B(meta, "yonetici") ? "yönetici izniyle" : "yönetici izni olmadan"), Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(2), 0, Tema.O(14)) });

            PuanKarti pk1 = new PuanKarti(), pk2 = new PuanKarti();
            pk1.Ayarla(donanim, "Donanım sağlığı", "Parçaların fiziksel durumu: disk ve SSD sağlığı, sıcaklıklar, donanım hata kayıtları" +
                (J.B(sistem, "dizustu") ? ", pil" : "") + ". Düşükse bir parça arızalanıyor olabilir.", say("donanim", "bad"), say("donanim", "warn"));
            pk2.Ayarla(bakim ?? 0, "Bakım ve güncellemeler", "Sürücüler, Windows güncellemeleri, disk alanı ve ayarlar. Düşük olması donanımın bozuk olduğu anlamına gelmez; birkaç tıkla düzeltilebilecek işlerdir.",
                say("bakim", "bad"), say("bakim", "warn"));
            IpucuVer(pk1, IpucuDonanim);
            IpucuVer(pk2, IpucuBakim);
            liste.Add(IkiSutun(pk1, pk2, Tema.O(150)));

            string raporYolu = RaporYolu(meta);
            Button rapor = Buton("Detaylı raporu aç", true, () => Ortak.Ac(raporYolu, null));
            rapor.Enabled = raporYolu != null;
            liste.Add(Tema.Bosluk(12));
            liste.Add(ButonSatiri(rapor, Buton("Ana sayfa", false, () => Git("ana")), Buton("Yeni tarama", false, () => Git("tara"))));

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
                liste.Add(new Yazi("Önceki taramaya göre (" + J.S(k, "tarih") + ")", Tema.Kalin, Tema.Metin) { Padding = new Padding(0, Tema.O(18), 0, Tema.O(2)) });
                liste.Add(new Yazi(sb.ToString(), Tema.Normal, Tema.Soluk));
            }

            List<object> yapilacak = bulgular.Where(b => J.S(b, "seviye") != "ok").ToList();
            liste.Add(new Yazi(yapilacak.Count > 0 ? "Yapılması gerekenler" : "Yapılması gereken bir şey yok ✓", Tema.AltBaslik, Tema.Metin) { Padding = new Padding(0, Tema.O(20), 0, Tema.O(2)) });
            if (yapilacak.Count > 0)
                liste.Add(new Yazi("Önem sırasına göre. \"Bilgi\" olanlar ve küçük güncellemeler sorun değildir, yalnızca bilgilendirme amaçlıdır.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(10)) });
            foreach (object b in yapilacak)
            {
                string rozet = J.S(b, "rozet");
                string etiket = J.S(b, "kategori") + "  ·  " + (J.S(b, "tur") == "bakim" ? "Bakım" : "Donanım") + "  ·  " + Tema.SeviyeAdi(J.S(b, "seviye")) + (string.IsNullOrEmpty(rozet) ? "" : "  ·  " + rozet);
                liste.Add(new BulguKarti(J.S(b, "seviye"), etiket, J.S(b, "baslik"), J.S(b, "detay"), J.S(b, "link"), J.S(b, "linkMetin")));
            }
            List<string> iyiler = bulgular.Where(b => J.S(b, "seviye") == "ok").Select(b => "✓ " + J.S(b, "baslik")).ToList();
            if (iyiler.Count > 0)
            {
                liste.Add(new Yazi("Sağlıklı bulunanlar", Tema.Kalin, Tema.Yesil) { Padding = new Padding(0, Tema.O(14), 0, Tema.O(4)) });
                liste.Add(new Yazi(string.Join("\n", iyiler), Tema.Normal, Tema.Soluk) { Padding = new Padding(0, 0, 0, Tema.O(20)) });
            }
            Bitir(p, liste);
        }

        static string Fark(int? once, int? simdi)
        {
            if (!once.HasValue || !simdi.HasValue || once == simdi) return "";
            return simdi > once ? "  (↑ " + (simdi - once) + ")" : "  (↓ " + (once - simdi) + ")";
        }

        // ---------------------------------------------------------------- sistem bilgileri
        static string Liste(IEnumerable<object> xs, Func<object, string> f)
        {
            List<string> l = xs.Select(f).Where(s => !string.IsNullOrEmpty(s)).ToList();
            return l.Count > 0 ? string.Join("\n", l) : null;
        }

        static string Bag(object x) { string b = J.S(x, "baglanti"); return J.S(x, "ad") + (string.IsNullOrEmpty(b) ? "" : "  ·  " + b); }

        void BilgiKur()
        {
            Panel p = Bosalt("bilgi");
            List<Control> o = new List<Control>();
            object v = SonucuYukle();
            o.Add(new Yazi("Sistem bilgileri", Tema.Baslik, Tema.Metin));
            if (v == null)
            {
                o.Add(new Yazi("Bilgisayarınızın donanım bilgilerini görmek için önce bir tarama yapın (hızlı tarama yeterli).", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(4), 0, Tema.O(14)) });
                o.Add(ButonSatiri(Buton("Hızlı tarama yap", true, () => { secHizli.Secili = true; secTam.Secili = false; Git("tara"); })));
                Bitir(p, o);
                return;
            }
            object meta = J.Al(v, "meta"), sis = J.Al(v, "sistem"), cpu = J.Al(v, "cpu"), ram = J.Al(v, "ram"), ch = J.Al(v, "cihazlar");
            o.Add(new Yazi("Son taramadan (" + J.S(meta, "tarih") + ") alınan bilgiler. Güncel hali için yeni bir tarama yapın." +
                (ch == null ? " Bağlı cihazlar (monitör, klavye, fare...) bu sürümle eklendi; görmek için yeni bir tarama yapın." : ""), Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(4), 0, Tema.O(14)) });

            BilgiKarti b = new BilgiKarti("Bilgisayar", null);
            b.Ekle("Bilgisayar adı", J.S(sis, "bilgisayar"));
            b.Ekle("Tür", J.B(sis, "dizustu") ? "Dizüstü" : "Masaüstü");
            b.Ekle("Üretici / model", (J.S(sis, "sistemUretici") + " " + J.S(sis, "sistemModel")).Trim());
            b.Ekle("İşletim sistemi", J.S(sis, "isletimSistemi") + " " + J.S(sis, "surum") + "  (derleme " + J.S(sis, "derleme") + ")");
            b.Ekle("Çalışma süresi", J.S(sis, "calismaSuresi"));
            b.Ekle("Anakart", (J.S(sis, "anakartUretici") + " " + J.S(sis, "anakartModel")).Trim());
            b.Ekle("BIOS", J.S(sis, "biosSurum") + "  (" + J.S(sis, "biosTarih") + ")");
            b.Ekle("Son Windows güncellemesi", J.S(sis, "sonWinGuncelleme"));
            object av = J.Al(sis, "antivirus");
            if (av != null) b.Ekle("Antivirüs", (J.B(av, "gercekZamanli") ? "Windows Defender açık" : "Gerçek zamanlı koruma kapalı") + "  ·  tanımlar " + J.S(av, "imzaTarihi"));
            o.Add(b);

            BilgiKarti c = new BilgiKarti("İşlemci", null);
            c.Ekle("Model", J.S(cpu, "ad"));
            c.Ekle("Çekirdek / izlek", J.S(cpu, "cekirdek") + " çekirdek / " + J.S(cpu, "izlek") + " izlek");
            c.Ekle("Taban hızı", J.S(cpu, "tabanMHz") + " MHz");
            c.Ekle("Önbellek", "L2 " + J.Sayi(J.D(cpu, "l2MB"), 1) + " MB  ·  L3 " + J.Sayi(J.D(cpu, "l3MB"), 1) + " MB");
            c.Ekle("Soket", J.S(cpu, "soket"));
            c.Ekle("Sanallaştırma (VT)", J.B(cpu, "sanallastirma") ? "Açık" : "Kapalı (Hyper-V/WSL açıksa böyle görünür)");
            object ct = J.Al(cpu, "test");
            if (ct != null) c.Ekle("Son test", "Tek çekirdek " + J.Sayi(J.D(ct, "tekCekirdek"), 0) + " · çok çekirdek " + J.Sayi(J.D(ct, "cokCekirdek"), 0) + " puan · yükte ~" + J.Sayi(J.D(ct, "yukFrekansMHz"), 0) + " MHz");
            o.Add(c);

            BilgiKarti g = new BilgiKarti("Ekran kartı", null);
            foreach (object gpu in J.L(v, "gpu").Where(x => !J.B(x, "sanal")))
            {
                g.AraBaslik(J.S(gpu, "ad"));
                g.Ekle("Bellek (VRAM)", J.D(gpu, "vramGB").HasValue ? J.Sayi(J.D(gpu, "vramGB"), 0) + " GB" : null);
                object cn = J.Al(gpu, "canli");
                g.Ekle("Sürücü", (cn != null ? J.S(cn, "surucu") + "  ·  " : "") + J.S(gpu, "surucu") + "  (" + J.S(gpu, "surucuTarih") + ")");
                g.Ekle("Ekran modu", J.S(gpu, "cozunurluk") != null ? J.S(gpu, "cozunurluk") + " @ " + J.S(gpu, "yenileme") + " Hz" : null);
                if (cn != null)
                {
                    g.Ekle("En yüksek saat hızı", J.Sayi(J.D(cn, "maxSaatMHz"), 0) + " MHz");
                    g.Ekle("Güç limiti", J.Sayi(J.D(cn, "gucLimit"), 0) + " W");
                    g.Ekle("PCIe bağlantısı", "Gen " + J.S(cn, "pcieGenMax") + " x" + J.S(cn, "pcieGenislikMax") + " destekliyor, şu an Gen " + J.S(cn, "pcieGen") + " x" + J.S(cn, "pcieGenislik"));
                    g.Ekle("VBIOS", J.S(cn, "vbios"));
                    g.Ekle("Tarama anında", J.S(cn, "sicaklik") + " °C  ·  %" + J.S(cn, "kullanim") + " kullanım  ·  " + J.Sayi(J.D(cn, "guc"), 0) + " W");
                }
            }
            o.Add(g);

            BilgiKarti r = new BilgiKarti("Bellek (RAM)", null);
            List<object> moduller = J.L(ram, "moduller").ToList();
            object m0 = moduller.FirstOrDefault();
            r.Ekle("Toplam", J.S(ram, "toplamGB") + " GB " + J.S(m0, "tip") + "  ·  " + J.S(m0, "ayarliMHz") + " MHz  ·  " + J.S(ram, "kanal"));
            r.Ekle("Yuvalar", moduller.Count + " / " + J.S(ram, "yuvaSayisi") + " dolu" + (J.I(ram, "maksGB").HasValue ? "  ·  en fazla " + J.S(ram, "maksGB") + " GB desteklenir" : ""));
            r.Ekle("Modüller", Liste(moduller, x => J.S(x, "uretici") + " " + J.S(x, "parcaNo") + "  ·  " + J.S(x, "kapasiteGB") + " GB  ·  " + J.S(x, "ayarliMHz") + " MHz  (" + J.S(x, "yuva") + ")"));
            if (J.D(ram, "bantGBs").HasValue) r.Ekle("Son hız testi", J.Sayi(J.D(ram, "bantGBs"), 1) + " GB/s");
            o.Add(r);

            BilgiKarti d = new BilgiKarti("Depolama", null);
            foreach (object disk in J.L(v, "diskler"))
            {
                d.AraBaslik(J.S(disk, "ad"));
                d.Ekle("Tür", J.S(disk, "tip") + "  ·  " + J.S(disk, "arayuz") + "  ·  " + J.S(disk, "boyutGB") + " GB");
                d.Ekle("Sağlık", J.S(disk, "saglik") == "Healthy" ? "Sağlıklı" : J.S(disk, "saglik"));
                d.Ekle("Firmware", J.S(disk, "firmware"));
                if (J.D(disk, "asinma").HasValue) d.Ekle("Ömür kullanımı", "%" + J.S(disk, "asinma"));
                if (J.D(disk, "sicaklik").HasValue) d.Ekle("Sıcaklık", J.S(disk, "sicaklik") + " °C");
                if (J.D(disk, "calismaSaati").HasValue) d.Ekle("Çalışma süresi", J.Sayi(J.D(disk, "calismaSaati"), 0) + " saat");
                d.Ekle("Bölümler", Liste(J.L(disk, "birimler"), x => J.S(x, "harf") + " " + J.S(x, "etiket") + "  ·  " + J.S(x, "bosGB") + " GB boş / " + J.S(x, "boyutGB") + " GB"));
                object dt = J.Al(disk, "test");
                if (dt != null) d.Ekle("Son hız testi", "okuma " + J.Sayi(J.D(dt, "siraliOkuma"), 0) + " MB/s  ·  yazma " + J.Sayi(J.D(dt, "siraliYazma"), 0) + " MB/s");
            }
            o.Add(d);

            if (ch != null)
            {
                BilgiKarti mon = new BilgiKarti("Monitörler", null);
                foreach (object m in J.L(ch, "monitorler"))
                {
                    mon.AraBaslik(J.S(m, "ad") + (J.I(m, "boyutInc").HasValue ? "  ·  " + J.S(m, "boyutInc") + " inç" : "") + (J.B(m, "birincil") ? "  ·  ana ekran" : ""));
                    mon.Ekle("Çözünürlük", J.S(m, "cozunurluk") + (J.S(m, "enYuksekCozunurluk") != J.S(m, "cozunurluk") ? "  (en yüksek " + J.S(m, "enYuksekCozunurluk") + ")" : ""));
                    int hz = J.I(m, "hz") ?? 0, maks = J.I(m, "maksHz") ?? 0;
                    mon.Ekle("Yenileme hızı", hz + " Hz" + (maks > hz + 5 ? "  ·  " + maks + " Hz destekliyor (ayarlardan yükseltilebilir)" : "  (bu çözünürlükte en yüksek)"));
                    mon.Ekle("Renk derinliği", J.S(m, "renkBit") + " bit");
                    mon.Ekle("Bağlı olduğu kart", J.S(m, "ekranKarti"));
                    mon.Ekle("Üretim yılı", J.S(m, "uretimYili"));
                }
                o.Add(mon);

                BilgiKarti giris = new BilgiKarti("Klavye, fare ve kumanda", null);
                giris.Ekle("Klavye", Liste(J.L(ch, "klavyeler"), Bag) ?? "bulunamadı");
                giris.Ekle("Fare", Liste(J.L(ch, "fareler"), Bag) ?? "bulunamadı");
                giris.Ekle("Oyun kumandası", Liste(J.L(ch, "kumandalar"), Bag));
                giris.Ekle("Diğer giriş cihazları", Liste(J.L(ch, "digerGiris"), Bag));
                o.Add(giris);

                BilgiKarti ses = new BilgiKarti("Ses ve diğer cihazlar", null);
                ses.Ekle("Ses çıkışı", Liste(J.L(ch, "sesCikis"), x => J.S(x, "ad")));
                ses.Ekle("Mikrofon", Liste(J.L(ch, "sesGiris"), x => J.S(x, "ad")));
                ses.Ekle("Kamera", Liste(J.L(ch, "kameralar"), x => J.S(x, "ad") + (J.B(x, "sanal") ? "  ·  yazılım kamerası" : "")));
                ses.Ekle("Bluetooth (eşleştirilmiş)", Liste(J.L(ch, "bluetooth"), x => J.S(x, "ad")));
                ses.Ekle("Yazıcı", Liste(J.L(ch, "yazicilar"), x => J.S(x, "ad") + (J.B(x, "varsayilan") ? "  ·  varsayılan" : "")));
                o.Add(ses);
            }

            BilgiKarti ag = new BilgiKarti("Ağ", null);
            foreach (object a in J.L(v, "ag")) ag.Ekle(J.S(a, "ad"), J.S(a, "aciklama") + "  ·  " + (J.S(a, "durum") == "Up" ? J.S(a, "hiz") + " bağlı" : "bağlı değil"));
            o.Add(ag);

            object pil = J.Al(v, "pil");
            if (pil != null)
            {
                BilgiKarti pk = new BilgiKarti("Pil", null);
                pk.Ekle("Şu an", "%" + J.S(pil, "sarjYuzde") + "  ·  " + (J.B(pil, "prizde") ? "prize takılı" : "pille çalışıyor"));
                foreach (object pp in J.L(pil, "piller"))
                {
                    pk.AraBaslik(J.S(pp, "ad"));
                    pk.Ekle("Kalan kapasite", "%" + J.S(pp, "saglikYuzde") + "  (" + J.Sayi(J.D(pp, "doluMWh") / 1000, 1) + " Wh / yeniyken " + J.Sayi(J.D(pp, "tasarimMWh") / 1000, 1) + " Wh)");
                    pk.Ekle("Şarj döngüsü", J.S(pp, "dongu") ?? "bildirilmiyor");
                }
                o.Add(pk);
            }
            Bitir(p, o);
        }

        // ---------------------------------------------------------------- temizlik
        void TemizlikKur()
        {
            if (temizlikKategorileri != null) return; // bir kez kurulur; boyutlar butonla yenilenir
            Panel p = Bosalt("temizlik");
            List<Control> o = new List<Control>();
            o.Add(new Yazi("Temizlik", Tema.Baslik, Tema.Metin));
            o.Add(new Yazi("Geçici dosyaları ve önbellekleri silerek disk alanı açar. Yalnızca seçtiğiniz önbellek klasörlerinin içi silinir; belgeleriniz, oyunlarınız, tarayıcı geçmişiniz, şifreleriniz ve oturumlarınız etkilenmez. Kullanımdaki dosyalar atlanır. Silinen dosyalar Geri Dönüşüm Kutusu'na gitmez.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(4), 0, Tema.O(14)) });

            temizlikKategorileri = Temizlik.Kategoriler();
            foreach (TemizlikKategorisi k in temizlikKategorileri)
            {
                Kart kart = new Kart();
                Panel ust = new Panel { Height = Tema.O(28), BackColor = Tema.Kart };
                CheckBox chk = new CheckBox { Text = k.Ad, Font = Tema.Kalin, Checked = k.Varsayilan, AutoSize = true, Location = new Point(0, Tema.O(2)), BackColor = Tema.Kart };
                Label boyut = new Label { Text = "hesaplanıyor...", Font = Tema.Kalin, ForeColor = Tema.Soluk, AutoSize = false, Dock = DockStyle.Right, Width = Tema.O(150), TextAlign = ContentAlignment.MiddleRight, BackColor = Tema.Kart };
                ust.Controls.Add(chk);
                ust.Controls.Add(boyut);
                chk.CheckedChanged += (s, e) => TemizlikToplam();
                temizlikSecim[k] = chk;
                temizlikBoyut[k] = boyut;
                kart.Ekle(ust, new Yazi(k.Aciklama, Tema.Normal, Tema.Soluk) { Padding = new Padding(Tema.O(20), 0, 0, 0) });
                o.Add(kart);
            }
            btnTemizle = Buton("Seçilenleri temizle", true, () => TemizlikYap());
            btnYenidenHesapla = Buton("Yeniden hesapla", false, () => TemizlikHesapla());
            o.Add(Tema.Bosluk(4));
            o.Add(ButonSatiri(btnTemizle, btnYenidenHesapla));
            lblTemizlikDurum.Padding = new Padding(0, Tema.O(10), 0, 0);
            o.Add(lblTemizlikDurum);
            Bitir(p, o);
            TemizlikHesapla();
        }

        void TemizlikToplam()
        {
            long toplam = temizlikKategorileri.Where(k => temizlikSecim[k].Checked).Sum(k => k.Boyut);
            btnTemizle.Text = "Seçilenleri temizle  (" + Temizlik.Boyut(toplam) + ")";
            btnTemizle.Enabled = !temizlikSuruyor && toplam > 0;
        }

        async void TemizlikHesapla()
        {
            if (temizlikSuruyor) return;
            temizlikSuruyor = true;
            btnTemizle.Enabled = btnYenidenHesapla.Enabled = false;
            lblTemizlikDurum.ForeColor = Tema.Soluk;
            lblTemizlikDurum.Text = "Boyutlar hesaplanıyor...";
            foreach (TemizlikKategorisi k in temizlikKategorileri)
            {
                temizlikBoyut[k].Text = "hesaplanıyor...";
                TemizlikKategorisi kk = k;
                await Task.Run(() => Temizlik.Hesapla(kk));
                temizlikBoyut[k].Text = Temizlik.Boyut(k.Boyut);
                temizlikBoyut[k].ForeColor = k.Boyut > 0 ? Tema.Metin : Tema.Soluk;
            }
            temizlikSuruyor = false;
            btnYenidenHesapla.Enabled = true;
            lblTemizlikDurum.Text = "";
            TemizlikToplam();
        }

        async void TemizlikYap()
        {
            List<TemizlikKategorisi> secilen = temizlikKategorileri.Where(k => temizlikSecim[k].Checked && k.Boyut > 0).ToList();
            if (secilen.Count == 0 || temizlikSuruyor) return;
            long toplam = secilen.Sum(k => k.Boyut);
            if (MessageBox.Show(this, "Şunlar kalıcı olarak silinecek (yaklaşık " + Temizlik.Boyut(toplam) + "):\n\n" + string.Join("\n", secilen.Select(k => "• " + k.Ad + "  (" + Temizlik.Boyut(k.Boyut) + ")")) +
                "\n\nTarayıcılar ve Discord açıksa önbelleklerinin bir kısmı atlanır; tam temizlik için önce kapatabilirsiniz.\n\nDevam edilsin mi?", "Temizlik", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            temizlikSuruyor = true;
            btnTemizle.Enabled = btnYenidenHesapla.Enabled = false;
            long silinen = 0;
            foreach (TemizlikKategorisi k in secilen)
            {
                lblTemizlikDurum.ForeColor = Tema.Soluk;
                lblTemizlikDurum.Text = k.Ad + " temizleniyor...";
                TemizlikKategorisi kk = k;
                long onceki = silinen;
                silinen += await Task.Run(() => Temizlik.Temizle(kk, n => BeginInvoke((Action)(() => lblTemizlikDurum.Text = kk.Ad + " temizleniyor... " + Temizlik.Boyut(onceki + n)))));
            }
            temizlikSuruyor = false;
            long kalan = toplam - silinen;
            TemizlikHesapla();
            lblTemizlikDurum.ForeColor = Tema.Yesil;
            lblTemizlikDurum.Text = "✓ " + Temizlik.Boyut(silinen) + " boş alan açıldı." + (kalan > 1 << 20 ? "  " + Temizlik.Boyut(kalan) + " kullanımda olduğu için atlandı." : "");
        }

        // ---------------------------------------------------------------- nasıl çalışıyor
        void NasilKur()
        {
            Panel p = sayfalar["nasil"];
            List<Control> o = new List<Control>();
            o.Add(new Yazi("Nasıl çalışıyor?", Tema.Baslik, Tema.Metin));
            o.Add(new Yazi("Sistem Checkup neye bakıyor, performansı nasıl ölçüyor ve neleri ölçemiyor.", Tema.Normal, Tema.Soluk) { Padding = new Padding(0, Tema.O(4), 0, Tema.O(14)) });
            Action<string, string> bolum = (baslik, metin) =>
            {
                Kart k = new Kart();
                k.Ekle(new Yazi(baslik, Tema.AltBaslik, Tema.Metin) { Padding = new Padding(0, 0, 0, Tema.O(6)) }, new Yazi(metin, Tema.Normal, Tema.Soluk));
                o.Add(k);
            };
            bolum("Bu bir check-up, stres testi değil",
                "Doktordaki genel kontrol gibi: parçaların ve Windows'un zaten tuttuğu kayıtları okur, yanında birkaç kısa ölçüm yapar. Saatlerce zorlayıp \"çökecek mi\" diye bakmaz.");
            bolum("1. Parçaların kendi raporları (en güvenilir kısım)",
                "• SSD ve diskler kendi sağlıklarını sürekli ölçer (SMART): kalan ömür, sıcaklık, çalışma saati, düzeltilemeyen hata sayısı. Bir SSD bozulmaya başladığında bunu en erken bu değerler gösterir.\n" +
                "• NVIDIA ekran kartı sıcaklığını, gücünü ve yuvaya kaç hatla (x16/x8) bağlı olduğunu bildirir.\n" +
                "• Pil, yeniyken ve şimdi ne kadar enerji tuttuğunu bildirir; ikisi karşılaştırılır.\n" +
                "• Monitör, model adını ve desteklediği en yüksek yenileme hızını bildirir; Windows'un daha düşük hızda kullanıp kullanmadığına bakılır.");
            bolum("2. Windows'un hata kayıtları (son 30 gün)",
                "• Mavi ekranlar ve düzgün kapatılmadan kapanmalar.\n" +
                "• Donanım hataları (WHEA): işlemci, bellek veya PCIe aygıtı bir hata yakaladığında bunu kendisi bildirir. Gerçek arızanın en ciddi işaretlerinden biridir.\n" +
                "• Disk hataları ve ekran sürücüsü çökmeleri (ekranın kararıp geri gelmesi).\n" +
                "• Aygıt Yöneticisi'nde sürücüsü olmayan ya da hata veren aygıtlar.");
            bolum("3. Performans nasıl ölçülüyor? (yalnızca tam taramada)",
                "• Disk: Diske 1 GB'lık geçici bir dosya yazılıp okunur. Windows'un önbelleği bilerek atlanır, yoksa disk değil bellek ölçülmüş olurdu. Büyük dosya (sıralı) ve küçük dosya (4K) hızı ölçülür; disk türüne göre beklenen en düşük hızla karşılaştırılır (NVMe ~1000, SATA SSD ~350 MB/s).\n" +
                "• İşlemci: Önce tek çekirdekte 3 saniye, sonra tüm çekirdeklerde 10 saniye hesap yaptırılır ve işlemcinin hızı (MHz) her saniye ölçülür. Hız 10 saniye içinde belirgin düşerse işlemci ısınıp kendini yavaşlatıyor demektir (soğutma sorunu).\n" +
                "• Bellek: 256 MB veri birkaç kez kopyalanıp hız ölçülür. Modüllerin kutuda yazan hızla (XMP) ve çift kanal çalışıp çalışmadığı kontrol edilir.\n" +
                "Puanlar bu programa özeldir: başka bilgisayarlarla değil, kendi bilgisayarınızın zaman içindeki değişimiyle karşılaştırmak içindir (Ana sayfadaki grafikler).");
            bolum("Puanlar nasıl hesaplanıyor?",
                "İki puan da 100'den başlar.\n• Donanım sağlığı: her sorun −20, her uyarı −7.\n• Bakım ve güncellemeler: her sorun −15, her uyarı −6.\n" +
                "\"Bilgi\" maddeleri ve küçük güncellemeler puanı düşürmez. 90 ve üstü çok iyi, 75 ve üstü iyi, 50 ve üstü dikkat, altı sorunlu.");
            bolum("Sürücü güncellemeleri nasıl değerlendiriliyor?",
                "• NVIDIA sürücüsü NVIDIA'nın sunucusundaki son sürümle karşılaştırılır.\n• Diğer sürücüler için Microsoft'un Windows Update kataloğuna bakılır.\n• AMD / Intel ekran kartı ve BIOS için sürücünün yaşına bakılıp üreticinin sayfasına yönlendirilir.\n\n" +
                "Küçük güncelleme: aynı sürücü serisinin hata düzeltmeli sürümü. Faydalıdır ama acil değildir.\n" +
                "Büyük güncelleme: Windows'un genel sürücüsünden üreticinin kendi sürücüsüne geçiş, yeni bir ana sürüm ya da 2 yıldan eski bir sürücünün yenilenmesi. Kurulması önerilir.");
            bolum("Neleri ölçemez?",
                "• Belleği hata için test etmez (MemTest86 gerekir, saatler sürer).\n• Ekran kartını oyun yükü altında, işlemciyi uzun süre test etmez (FurMark, 3DMark, OCCT, Prime95).\n" +
                "• İşlemci çekirdek sıcaklığı ve fan hızları Windows tarafından verilmez (HWiNFO ile görülebilir).\n• Güç kaynağı hiç ölçülemez; yalnızca dolaylı belirtileri (kendiliğinden kapanma) görülür.");
            bolum("Gizlilik",
                "Tarama tamamen bilgisayarınızda yapılır; raporlar yalnızca Raporlar klasörüne kaydedilir ve hiçbir yere gönderilmez. İnternete yalnızca NVIDIA sürücü sürümü sorgusu, Windows Update sürücü taraması ve GitHub'dan güncelleme denetimi için bağlanılır.");
            Tema.Diz(p, o.ToArray());
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
            if (taramaSuruyor || temizlikSuruyor) { MessageBox.Show(this, "Lütfen önce devam eden işlemin bitmesini bekleyin.", Ortak.UygulamaAdi); return; }
            if (!File.Exists(Ortak.GuncelleyiciYolu)) { Ortak.Ac(Ortak.RepoUrl + "/releases/latest", null); return; }
            Ortak.Ac(Ortak.GuncelleyiciYolu, otomatik ? "--otomatik" : null);
            if (otomatik) Close();
        }

        void Kaldir()
        {
            if (taramaSuruyor || temizlikSuruyor) { MessageBox.Show(this, "Lütfen önce devam eden işlemin bitmesini bekleyin.", Ortak.UygulamaAdi); return; }
            if (!File.Exists(Ortak.KaldiriciYolu)) { MessageBox.Show(this, "Kaldir.exe bulunamadı.", Ortak.UygulamaAdi); return; }
            Ortak.Ac(Ortak.KaldiriciYolu, null);
            Close();
        }
    }
}
