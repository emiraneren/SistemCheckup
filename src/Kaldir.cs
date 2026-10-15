using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Kaldir.exe: Sistem Checkup'ı bu bilgisayardan kaldırır.
// Yalnızca uygulamanın kendi dosyalarını siler; klasöre kullanıcının koyduğu başka dosyalara dokunmaz
// ve yalnızca bu kuruluma işaret eden kısayolları kaldırır.
namespace SistemCheckup
{
    static class Kaldirici
    {
        static readonly string[] Dosyalar = { "SistemCheckup.exe", "Guncelleyici.exe", "BENIOKU.txt", "ayarlar.json", ".yazma_testi" };
        static readonly string[] Klasorler = { "app", "Raporlar" };

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            string kok = Ortak.KokDizin.TrimEnd('\\');
            bool onayli = args.Contains("--onayli");

            if (!onayli)
            {
                string mesaj = "Sistem Checkup bu bilgisayardan kaldırılacak.\n\nSilinecekler:\n" +
                    "  • Program dosyaları (" + kok + ")\n" +
                    "  • Tüm tarama raporları ve geçmişi\n" +
                    "  • Ayarlar\n" +
                    "  • Masaüstü ve Başlat menüsü kısayolları\n\n" +
                    "Bu işlem geri alınamaz. Devam edilsin mi?";
                if (MessageBox.Show(mesaj, "Sistem Checkup'ı kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
            }

            if (!Ortak.YazilabilirMi())
            {
                try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--onayli") { Verb = "runas", UseShellExecute = true }); }
                catch (Win32Exception) { MessageBox.Show("Bu klasörden silmek için yönetici izni gerekiyor.", "Sistem Checkup'ı kaldır", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }

            // Bu klasörden çalışan Sistem Checkup pencerelerini kapat
            foreach (string ad in new[] { "SistemCheckup", "Guncelleyici" })
            {
                foreach (Process p in Process.GetProcessesByName(ad))
                {
                    try
                    {
                        if (p.MainModule.FileName.StartsWith(kok, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); }
                    }
                    catch { }
                }
            }

            Ortak.KisayolSil(Ortak.MasaustuKisayolu);
            Ortak.KisayolSil(Ortak.BaslatKisayolu);

            List<string> silinemeyen = new List<string>();
            foreach (string k in Klasorler)
            {
                string yol = Path.Combine(kok, k);
                try { if (Directory.Exists(yol)) Directory.Delete(yol, true); } catch { silinemeyen.Add(yol); }
            }
            foreach (string d in Dosyalar)
            {
                string yol = Path.Combine(kok, d);
                try { if (File.Exists(yol)) File.Delete(yol); } catch { silinemeyen.Add(yol); }
            }
            foreach (string eski in Directory.GetFiles(kok, "*.old")) { try { File.Delete(eski); } catch { } }

            if (!args.Contains("--sessiz"))
            {
                if (silinemeyen.Count > 0)
                    MessageBox.Show("Sistem Checkup kaldırıldı, ancak bazı öğeler silinemedi (kullanımda olabilirler):\n\n" + string.Join("\n", silinemeyen), "Sistem Checkup'ı kaldır", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    MessageBox.Show("Sistem Checkup bu bilgisayardan kaldırıldı.", "Sistem Checkup'ı kaldır", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Kaldir.exe kendini çalışırken silemez: kapandıktan sonra silinsin, klasör boş kaldıysa o da silinsin
            // (rd /s kullanılmaz; klasörde başka dosya varsa dokunulmaz).
            string komut = "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + Application.ExecutablePath + "\" & rd \"" + kok + "\"";
            Process.Start(new ProcessStartInfo("cmd.exe", komut) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetTempPath() });
        }
    }
}
