using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// SistemCheckup.exe: güncelleme denetler, yönetici izni ister ve taramayı çalıştırır.
namespace SistemCheckup
{
    static class Baslatici
    {
        static int Main(string[] args)
        {
            Ortak.Hazirla();
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.Title = "Sistem Checkup " + Ortak.YerelSurum();
            Ortak.EskileriTemizle();

            bool yukseltildi = args.Contains("--yonetici");
            bool denetleme = !args.Contains("--guncelleme-denetleme");
            // "--" ile başlamayanlar taramaya aynen geçirilir (ör. -TestAtla, -Sessiz)
            string ekler = string.Join(" ", args.Where(a => !a.StartsWith("--")).Select(a => "\"" + a + "\""));
            string ps1 = Path.Combine(Ortak.KokDizin, "app", "SistemCheckup.ps1");

            if (!File.Exists(ps1))
            {
                Console.WriteLine("Uygulama dosyaları eksik (app\\SistemCheckup.ps1 bulunamadı).");
                Console.Write("Güncelleyici açılıp dosyalar indirilsin mi? [E/H] ");
                if (EvetMi()) GuncelleyiciyiAc();
                return 1;
            }

            if (!yukseltildi)
            {
                if (denetleme && GuncellemeVarsaSor()) return 0;

                if (!Ortak.Yonetici())
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(Ortak.BaslaticiYolu, ("--yonetici " + ekler).Trim());
                        psi.Verb = "runas";
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                        return 0;
                    }
                    catch (Win32Exception)
                    {
                        Console.WriteLine("Yönetici izni verilmedi, sınırlı modda devam ediliyor.");
                    }
                }
            }

            string raporlar = Path.Combine(Ortak.KokDizin, "Raporlar");
            ProcessStartInfo ps = new ProcessStartInfo("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\" -YonetimsizCalis -RaporDizini \"" + raporlar + "\" " + ekler);
            ps.UseShellExecute = false;
            ps.WorkingDirectory = Ortak.KokDizin;
            using (Process p = Process.Start(ps))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        // true dönerse güncelleyici açıldı ve başlatıcı kapanmalı.
        static bool GuncellemeVarsaSor()
        {
            Manifest uzak;
            try { uzak = Ortak.UzakManifest(5000); }
            catch { return false; } // çevrimdışı ya da henüz sürüm yok: sessizce devam et

            string yerel = Ortak.YerelSurum();
            if (!Ortak.YeniMi(uzak.surum, yerel)) return false;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Yeni sürüm mevcut: " + uzak.surum + " (kurulu: " + yerel + ")");
            Console.ResetColor();
            if (!string.IsNullOrEmpty(uzak.notlar)) Console.WriteLine("  " + uzak.notlar.Replace("\n", "\n  "));
            Console.Write("  Şimdi güncellensin mi? [E/H] ");
            if (!EvetMi()) return false;
            GuncelleyiciyiAc();
            return true;
        }

        static void GuncelleyiciyiAc()
        {
            if (!File.Exists(Ortak.GuncelleyiciYolu))
            {
                Console.WriteLine("Guncelleyici.exe bulunamadı. Son sürümü buradan indirebilirsiniz: " + Ortak.RepoUrl + "/releases/latest");
                Console.ReadLine();
                return;
            }
            Ortak.Ac(Ortak.GuncelleyiciYolu, "--otomatik");
        }

        static bool EvetMi()
        {
            if (Console.IsInputRedirected)
            {
                string s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                return s == "e" || s == "y";
            }
            ConsoleKeyInfo k = Console.ReadKey();
            Console.WriteLine();
            return k.Key == ConsoleKey.E || k.Key == ConsoleKey.Y || k.Key == ConsoleKey.Enter;
        }
    }
}
