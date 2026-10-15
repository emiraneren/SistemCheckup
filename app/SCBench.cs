using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// Sistem Checkup icin disk / islemci / bellek olcum rutinleri.
// Disk testleri Windows onbellegini atlamak icin FILE_FLAG_NO_BUFFERING kullanir.
public static class SCBench
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetFilePointerEx(IntPtr hFile, long liDistanceToMove, out long lpNewFilePointer, uint dwMoveMethod);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FlushFileBuffers(IntPtr hFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint SHARE_RW = 3;
    const uint CREATE_ALWAYS = 2, OPEN_EXISTING = 3;
    const uint FLAG_NO_BUFFERING = 0x20000000, FLAG_SEQUENTIAL = 0x08000000, FLAG_RANDOM = 0x10000000;
    static readonly IntPtr INVALID_HANDLE = new IntPtr(-1);

    static IntPtr Open(string path, bool write, bool create, uint flags)
    {
        uint access = write ? (GENERIC_READ | GENERIC_WRITE) : GENERIC_READ;
        IntPtr h = CreateFileW(path, access, SHARE_RW, IntPtr.Zero, create ? CREATE_ALWAYS : OPEN_EXISTING, FLAG_NO_BUFFERING | flags, IntPtr.Zero);
        if (h == INVALID_HANDLE) throw new Win32Exception(Marshal.GetLastWin32Error());
        return h;
    }

    // VirtualAlloc sayfa hizali bellek dondurur (NO_BUFFERING icin gerekli).
    static IntPtr Alloc(int size, bool randomFill)
    {
        IntPtr p = VirtualAlloc(IntPtr.Zero, new UIntPtr((uint)size), 0x3000, 0x04);
        if (p == IntPtr.Zero) throw new OutOfMemoryException();
        if (randomFill)
        {
            byte[] b = new byte[size];
            new Random(42).NextBytes(b);
            Marshal.Copy(b, 0, p, size);
        }
        return p;
    }

    static void Free(IntPtr p) { VirtualFree(p, UIntPtr.Zero, 0x8000); }

    // Sirali yazma, MB/s
    public static double SeqWrite(string path, int totalMB, int blockKB)
    {
        int block = blockKB * 1024;
        long total = (long)totalMB * 1048576L;
        IntPtr buf = Alloc(block, true);
        IntPtr h = Open(path, true, true, FLAG_SEQUENTIAL);
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            long done = 0; uint n;
            while (done < total)
            {
                if (!WriteFile(h, buf, (uint)block, out n, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
                done += n;
            }
            FlushFileBuffers(h);
            sw.Stop();
            return done / 1048576.0 / sw.Elapsed.TotalSeconds;
        }
        finally { CloseHandle(h); Free(buf); }
    }

    // Sirali okuma, MB/s
    public static double SeqRead(string path, int blockKB)
    {
        int block = blockKB * 1024;
        IntPtr buf = Alloc(block, false);
        IntPtr h = Open(path, false, false, FLAG_SEQUENTIAL);
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            long done = 0; uint n;
            while (true)
            {
                if (!ReadFile(h, buf, (uint)block, out n, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (n == 0) break;
                done += n;
            }
            sw.Stop();
            return done / 1048576.0 / sw.Elapsed.TotalSeconds;
        }
        finally { CloseHandle(h); Free(buf); }
    }

    static long RandomWorker(string path, bool write, long fileSize, int ms, int seed)
    {
        IntPtr buf = Alloc(4096, true);
        IntPtr h = Open(path, write, false, FLAG_RANDOM);
        long ops = 0;
        try
        {
            Random rnd = new Random(seed);
            int blocks = (int)(fileSize / 4096);
            Stopwatch sw = Stopwatch.StartNew();
            uint n; long pos;
            while (sw.ElapsedMilliseconds < ms)
            {
                SetFilePointerEx(h, (long)rnd.Next(blocks) * 4096L, out pos, 0);
                bool ok = write ? WriteFile(h, buf, 4096, out n, IntPtr.Zero) : ReadFile(h, buf, 4096, out n, IntPtr.Zero);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
                ops++;
            }
        }
        finally { CloseHandle(h); Free(buf); }
        return ops;
    }

    // Rastgele 4K, { MB/s, IOPS }
    public static double[] Random4K(string path, bool write, long fileSize, int threads, int ms)
    {
        long[] results = new long[threads];
        Exception err = null;
        Thread[] ts = new Thread[threads];
        for (int i = 0; i < threads; i++)
        {
            int idx = i;
            ts[i] = new Thread(() =>
            {
                try { results[idx] = RandomWorker(path, write, fileSize, ms, 1000 + idx); }
                catch (Exception e) { err = e; }
            });
            ts[i].Start();
        }
        foreach (Thread t in ts) t.Join();
        if (err != null) throw err;
        long total = 0;
        foreach (long r in results) total += r;
        double iops = total / (ms / 1000.0);
        return new double[] { iops * 4096.0 / 1048576.0, iops };
    }

    // ---- Islemci ----
    static double sink;

    static double Unit(ref ulong s)
    {
        double acc = 0;
        for (int i = 0; i < 2000; i++)
        {
            s ^= s << 13; s ^= s >> 7; s ^= s << 17;
            acc += Math.Sqrt((double)(s & 0xFFFFF)) * 1.0000001;
        }
        return acc;
    }

    // Tek cekirdek puani (bin is birimi / saniye)
    public static double SingleThread(int ms)
    {
        ulong s = 88172645463325252UL; long units = 0; double acc = 0;
        Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { acc += Unit(ref s); units++; }
        sw.Stop();
        Thread.CurrentThread.Priority = ThreadPriority.Normal;
        sink = acc;
        return units / sw.Elapsed.TotalSeconds / 1000.0;
    }

    public static volatile bool CpuDone;
    public static double[] CpuSamples;
    static long[] counters;
    static volatile bool stopFlag;

    // Tum cekirdeklerde yuk baslatir; her saniyenin is miktarini CpuSamples'a yazar.
    public static void StartMulti(int threads, int seconds)
    {
        CpuDone = false; stopFlag = false;
        counters = new long[threads * 16];
        CpuSamples = new double[seconds];
        for (int i = 0; i < threads; i++)
        {
            int idx = i * 16;
            Thread t = new Thread(() =>
            {
                ulong s = (ulong)(idx + 1) * 2654435761UL; double acc = 0;
                while (!stopFlag) { acc += Unit(ref s); counters[idx]++; }
                sink += acc;
            });
            t.IsBackground = true;
            // Dusuk oncelik: frekansi olcen WMI sorgusu yuk altinda da cevap verebilsin.
            t.Priority = ThreadPriority.BelowNormal;
            t.Start();
        }
        Thread mon = new Thread(() =>
        {
            long prev = 0;
            Stopwatch sw = Stopwatch.StartNew();
            for (int sec = 0; sec < seconds; sec++)
            {
                while (sw.ElapsedMilliseconds < (sec + 1) * 1000L) Thread.Sleep(5);
                long sum = 0;
                for (int k = 0; k < counters.Length; k += 16) sum += Interlocked.Read(ref counters[k]);
                CpuSamples[sec] = (sum - prev) / 1000.0;
                prev = sum;
            }
            stopFlag = true;
            CpuDone = true;
        });
        mon.IsBackground = true;
        mon.Priority = ThreadPriority.Highest;
        mon.Start();
    }

    // ---- Bellek ----
    // Cok izlekli bellek kopyalama, GB/s
    public static double MemCopy(int mb, int threads, int reps)
    {
        int size = mb * 1048576;
        byte[] a = new byte[size];
        byte[] b = new byte[size];
        for (int i = 0; i < size; i += 4096) { a[i] = 1; b[i] = 1; }
        Buffer.BlockCopy(a, 0, b, 0, size);
        int chunk = size / threads;
        Stopwatch sw = Stopwatch.StartNew();
        for (int r = 0; r < reps; r++)
        {
            Thread[] ts = new Thread[threads];
            for (int t = 0; t < threads; t++)
            {
                int off = t * chunk;
                ts[t] = new Thread(() => Buffer.BlockCopy(a, off, b, off, chunk));
                ts[t].Start();
            }
            foreach (Thread th in ts) th.Join();
        }
        sw.Stop();
        return (double)chunk * threads * reps / sw.Elapsed.TotalSeconds / 1e9;
    }
}
