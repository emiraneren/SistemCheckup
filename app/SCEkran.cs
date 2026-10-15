using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Sistem Checkup: bağlı monitörlerin çözünürlük / yenileme hızı bilgisi.
public static class SCEkran
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, int dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    // Her aktif ekran için: ekranAdi|kartAdi|monitorAdi|monitorKimligi|genislik|yukseklik|hz|bit|birincil|buCozunurluktekiMaksHz|maksGenislik|maksYukseklik
    public static string[] Listele()
    {
        List<string> sonuc = new List<string>();
        for (int i = 0; ; i++)
        {
            DISPLAY_DEVICE kart = new DISPLAY_DEVICE(); kart.cb = Marshal.SizeOf(kart);
            if (!EnumDisplayDevices(null, i, ref kart, 0)) break;
            if ((kart.StateFlags & 1) == 0) continue; // masaüstüne bağlı değil
            DISPLAY_DEVICE mon = new DISPLAY_DEVICE(); mon.cb = Marshal.SizeOf(mon);
            string monAd = "", monId = "";
            if (EnumDisplayDevices(kart.DeviceName, 0, ref mon, 0)) { monAd = mon.DeviceString; monId = mon.DeviceID; }

            DEVMODE dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettings(kart.DeviceName, -1, ref dm)) continue;
            int maksHz = 0, maksW = 0, maksH = 0;
            for (int m = 0; ; m++)
            {
                DEVMODE d = new DEVMODE(); d.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettings(kart.DeviceName, m, ref d)) break;
                if (d.dmPelsWidth == dm.dmPelsWidth && d.dmPelsHeight == dm.dmPelsHeight && d.dmDisplayFrequency > maksHz) maksHz = d.dmDisplayFrequency;
                if ((long)d.dmPelsWidth * d.dmPelsHeight > (long)maksW * maksH) { maksW = d.dmPelsWidth; maksH = d.dmPelsHeight; }
            }
            bool birincil = (kart.StateFlags & 4) != 0;
            sonuc.Add(string.Join("|", new string[] { kart.DeviceName, kart.DeviceString, monAd, monId,
                dm.dmPelsWidth.ToString(), dm.dmPelsHeight.ToString(), dm.dmDisplayFrequency.ToString(), dm.dmBitsPerPel.ToString(),
                birincil ? "1" : "0", maksHz.ToString(), maksW.ToString(), maksH.ToString() }));
        }
        return sonuc.ToArray();
    }
}
