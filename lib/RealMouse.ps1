# RealMouse.ps1 —— 面向"像真人一样操作 Windows"的核心库
# 设计原则：
#   1) 统一使用物理像素坐标（进程强制 Per-Monitor V2 DPI Aware）
#   2) 每次动作前必须能回答"我现在要操作哪个窗口"，动作后必须能验证
#   3) 移动轨迹、按下/抬起、输入节奏都带人类特征，避免瞬移与 0ms 间隔
#   4) 不依赖 MainWindowHandle（UWP/WinUI 应用该值常为空），一律枚举顶层窗口

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ("RM" -as [type])) {
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

[StructLayout(LayoutKind.Sequential)] public struct RMPOINT { public int X, Y; }
[StructLayout(LayoutKind.Sequential)] public struct RMRECT { public int Left, Top, Right, Bottom; }
[StructLayout(LayoutKind.Sequential)] public struct RMCURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public RMPOINT pt; }
[StructLayout(LayoutKind.Sequential)] public struct RMMOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Sequential)] public struct RMKEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
[StructLayout(LayoutKind.Explicit)] public struct RMINPUTUNION { [FieldOffset(0)] public RMMOUSEINPUT mi; [FieldOffset(0)] public RMKEYBDINPUT ki; }
[StructLayout(LayoutKind.Sequential)] public struct RMINPUT { public uint type; public RMINPUTUNION U; }

[StructLayout(LayoutKind.Sequential)]
public class RMWININFO {
    public IntPtr hwnd;
    public uint pid;
    public int left, top, right, bottom;
    public bool foreground;
    public bool topmost;
    public bool minimized;
    public bool cloaked;
    public string cls;
    public string title;
    public int Width { get { return right - left; } }
    public int Height { get { return bottom - top; } }
    public string Key { get { return hwnd.ToInt64() + "|" + title; } }
}

public class RM2 {
    public delegate bool EnumProc(IntPtr h, IntPtr l);

    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int i);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out RMPOINT p);
    [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref RMCURSORINFO p);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, RMINPUT[] i, int sz);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(RMPOINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RMRECT r);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int w, int h, int step, IntPtr br, int f);

    public static void MakeDpiAware() {
        try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch {}
        try { SetProcessDPIAware(); } catch {}
    }
    public static int PhysW() { IntPtr d = GetDC(IntPtr.Zero); int v = GetDeviceCaps(d, 118); ReleaseDC(IntPtr.Zero, d); return v; }
    public static int PhysH() { IntPtr d = GetDC(IntPtr.Zero); int v = GetDeviceCaps(d, 117); ReleaseDC(IntPtr.Zero, d); return v; }
    public static int SysW() { return GetSystemMetrics(0); }
    public static int SysH() { return GetSystemMetrics(1); }

    public static bool MoveTo(int x, int y) { return SetCursorPos(x, y); }
    public static RMPOINT Where() { RMPOINT p; GetCursorPos(out p); return p; }
    public static string CursorText() {
        RMCURSORINFO ci = new RMCURSORINFO();
        ci.cbSize = Marshal.SizeOf(typeof(RMCURSORINFO));
        GetCursorInfo(ref ci);
        return ci.pt.X + "," + ci.pt.Y + " showing=" + (ci.flags & 1);
    }

    public static void SendMouse(uint flag, int data) {
        RMINPUT[] a = new RMINPUT[1];
        a[0].type = 0; a[0].U.mi.dwFlags = flag; a[0].U.mi.mouseData = (uint)data;
        SendInput(1, a, Marshal.SizeOf(typeof(RMINPUT)));
    }
    public static void LeftDown() { SendMouse(0x0002, 0); }
    public static void LeftUp()   { SendMouse(0x0004, 0); }
    public static void RightDown(){ SendMouse(0x0008, 0); }
    public static void RightUp()  { SendMouse(0x0010, 0); }
    public static void MiddleDown(){ SendMouse(0x0020, 0); }
    public static void MiddleUp()  { SendMouse(0x0040, 0); }
    public static void Wheel(int c) { SendMouse(0x0800, c * 120); }
    public static void HWheel(int c) { SendMouse(0x1000, c * 120); }

    public static IntPtr RootAt(int x, int y) {
        RMPOINT p; p.X = x; p.Y = y;
        IntPtr h = WindowFromPoint(p);
        if (h == IntPtr.Zero) return IntPtr.Zero;
        IntPtr top = GetAncestor(h, 2);
        return top == IntPtr.Zero ? h : top;
    }
    public static string Describe(IntPtr h) {
        if (h == IntPtr.Zero) return "(none)";
        StringBuilder t = new StringBuilder(512); GetWindowText(h, t, 512);
        StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256);
        uint pid; GetWindowThreadProcessId(h, out pid);
        RMRECT r; GetWindowRect(h, out r);
        return "hwnd=" + h + " pid=" + pid + " class=" + c + " rect=(" + r.Left + "," + r.Top + ")-(" + r.Right + "," + r.Bottom + ") title=" + t;
    }
    public static string AtPoint(int x, int y) { return Describe(RootAt(x, y)); }

    // 前台激活的可靠实现（Windows 会阻止非前台进程抢焦点，必须 AttachThreadInput）
    public static bool ForceForeground(IntPtr h) {
        if (h == IntPtr.Zero) return false;
        if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
        IntPtr fg = GetForegroundWindow();
        if (fg == h) return true;
        uint fgPid; uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
        uint myThread = GetCurrentThreadId();
        bool attached = false;
        if (fgThread != 0 && fgThread != myThread) attached = AttachThreadInput(myThread, fgThread, true);
        BringWindowToTop(h);
        SetForegroundWindow(h);
        SetWindowPos(h, IntPtr.Zero, 0,0,0,0, 0x0001 | 0x0002 | 0x0040); // NOSIZE|NOMOVE|SHOWWINDOW
        if (attached) AttachThreadInput(myThread, fgThread, false);
        return GetForegroundWindow() == h;
    }

    public static List<RMWININFO> Windows() {
        List<RMWININFO> res = new List<RMWININFO>();
        IntPtr fg = GetForegroundWindow();
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            if (!IsWindowVisible(h)) return true;
            StringBuilder t = new StringBuilder(512); GetWindowText(h, t, 512);
            if (t.Length == 0) return true;
            RMRECT r;
            if (!GetWindowRect(h, out r)) return true;
            int cloaked = 0; DwmGetWindowAttribute(h, 14, out cloaked, 4);
            if (cloaked != 0) return true;
            RMWININFO w = new RMWININFO();
            w.hwnd = h;
            uint pid; GetWindowThreadProcessId(h, out pid); w.pid = pid;
            w.left = r.Left; w.top = r.Top; w.right = r.Right; w.bottom = r.Bottom;
            w.foreground = (h == fg);
            int ex = GetWindowLong(h, -20);
            w.topmost = (ex & 0x8) != 0;
            w.minimized = IsIconic(h);
            w.cloaked = cloaked != 0;
            StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256);
            w.cls = c.ToString();
            w.title = t.ToString();
            res.Add(w);
            return true;
        }, IntPtr.Zero);
        return res;
    }

    public static IntPtr FindWindowByTitle(string sub) {
        foreach (RMWININFO w in Windows()) {
            if (w.title.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0) return w.hwnd;
        }
        return IntPtr.Zero;
    }
}
"@
}
[RM2]::MakeDpiAware() | Out-Null
