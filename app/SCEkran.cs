using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Sistem Checkup: ekran kartı sıcaklığı / fan / bellek frekansı. Görev Yöneticisi'nin kullandığı
// Windows grafik arayüzünden (D3DKMT) okunur; NVIDIA, AMD ve Intel kartların hepsinde çalışır
// (sürücü WDDM 2.4+ destekliyorsa). Desteklenmeyen kartlarda 0 döner.
public static class SCGpu
{
    [StructLayout(LayoutKind.Sequential)] struct LUID { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] struct ADAPTERINFO { public uint hAdapter; public LUID AdapterLuid; public uint NumOfSources; public int bPrecisePresentRegionsPreferred; }
    [StructLayout(LayoutKind.Sequential)] struct ENUMADAPTERS2 { public uint NumAdapters; public IntPtr pAdapters; }
    [StructLayout(LayoutKind.Sequential)] struct QUERYADAPTERINFO { public uint hAdapter; public int Type; public IntPtr pPrivateDriverData; public uint PrivateDriverDataSize; }
    [StructLayout(LayoutKind.Sequential)] struct CLOSEADAPTER { public uint hAdapter; }
    [StructLayout(LayoutKind.Sequential)]
    struct ADAPTER_PERFDATA
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency, MaxMemoryFrequency, MaxMemoryFrequencyOC, MemoryBandwidth, PCIEBandwidth;
        public uint FanRPM, Power, Temperature;
        public byte PowerStateOverride;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ADAPTERREGISTRYINFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string AdapterString;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string BiosString;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DacType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ChipType;
    }

    [DllImport("gdi32.dll")] static extern int D3DKMTEnumAdapters2(ref ENUMADAPTERS2 e);
    [DllImport("gdi32.dll")] static extern int D3DKMTQueryAdapterInfo(ref QUERYADAPTERINFO q);
    [DllImport("gdi32.dll")] static extern int D3DKMTCloseAdapter(ref CLOSEADAPTER c);

    const int KMTQAITYPE_ADAPTERREGISTRYINFO = 8, KMTQAITYPE_ADAPTERPERFDATA = 62;

    static bool Sorgula<T>(uint h, int tur, ref T veri) where T : struct
    {
        int boyut = Marshal.SizeOf(typeof(T));
        IntPtr p = Marshal.AllocHGlobal(boyut);
        try
        {
            Marshal.StructureToPtr(veri, p, false);
            QUERYADAPTERINFO q = new QUERYADAPTERINFO { hAdapter = h, Type = tur, pPrivateDriverData = p, PrivateDriverDataSize = (uint)boyut };
            if (D3DKMTQueryAdapterInfo(ref q) != 0) return false;
            veri = (T)Marshal.PtrToStructure(p, typeof(T));
            return true;
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    // Her ekran kartı için: ad|sicaklikC|fanRPM|bellekMHz   (desteklenmeyen değerler 0)
    public static string[] Listele()
    {
        List<string> sonuc = new List<string>();
        ENUMADAPTERS2 e = new ENUMADAPTERS2();
        if (D3DKMTEnumAdapters2(ref e) != 0 || e.NumAdapters == 0) return sonuc.ToArray();
        int boyut = Marshal.SizeOf(typeof(ADAPTERINFO));
        e.pAdapters = Marshal.AllocHGlobal(boyut * (int)e.NumAdapters);
        try
        {
            if (D3DKMTEnumAdapters2(ref e) != 0) return sonuc.ToArray();
            for (int i = 0; i < e.NumAdapters; i++)
            {
                ADAPTERINFO a = (ADAPTERINFO)Marshal.PtrToStructure(new IntPtr(e.pAdapters.ToInt64() + (long)i * boyut), typeof(ADAPTERINFO));
                try
                {
                    ADAPTERREGISTRYINFO kayit = new ADAPTERREGISTRYINFO();
                    string ad = Sorgula(a.hAdapter, KMTQAITYPE_ADAPTERREGISTRYINFO, ref kayit) ? (kayit.AdapterString ?? "") : "";
                    ADAPTER_PERFDATA perf = new ADAPTER_PERFDATA();
                    double sicaklik = 0; uint fan = 0; ulong bellek = 0;
                    if (Sorgula(a.hAdapter, KMTQAITYPE_ADAPTERPERFDATA, ref perf))
                    {
                        if (perf.Temperature > 0 && perf.Temperature < 1500) sicaklik = perf.Temperature / 10.0;
                        fan = perf.FanRPM;
                        bellek = perf.MemoryFrequency / 1000000;
                    }
                    if (ad.Length > 0) sonuc.Add(ad + "|" + sicaklik.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + fan + "|" + bellek);
                }
                finally { CLOSEADAPTER c = new CLOSEADAPTER { hAdapter = a.hAdapter }; D3DKMTCloseAdapter(ref c); }
            }
        }
        finally { Marshal.FreeHGlobal(e.pAdapters); }
        return sonuc.ToArray();
    }
}

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
