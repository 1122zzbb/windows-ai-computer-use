using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RealMouse
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public POINT pt; }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION U; }

    [StructLayout(LayoutKind.Sequential)]
    public class WinInfo
    {
        public IntPtr hwnd;
        public uint pid;
        public int left, top, right, bottom;
        public bool foreground;
        public bool topmost;
        public bool minimized;
        public string cls;
        public string title;
        public int Width { get { return right - left; } }
        public int Height { get { return bottom - top; } }
        public int CX { get { return left + (right - left) / 2; } }
        public int CY { get { return top + (bottom - top) / 2; } }
    }

    public class Native
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);

        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr dc, int i);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO p);
        [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] i, int sz);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint f);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int s);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, UIntPtr e);

        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetThreadDesktop(IntPtr hDesktop);

        public static void AttachToDefaultDesktop()
        {
            try
            {
                IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                if (hDesk != IntPtr.Zero)
                {
                    SetThreadDesktop(hDesk);
                }
            }
            catch { }
        }

        public static void MakeDpiAware()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }
        public static int PhysW() { IntPtr d = GetDC(IntPtr.Zero); int v = GetDeviceCaps(d, 118); ReleaseDC(IntPtr.Zero, d); return v; }
        public static int PhysH() { IntPtr d = GetDC(IntPtr.Zero); int v = GetDeviceCaps(d, 117); ReleaseDC(IntPtr.Zero, d); return v; }

        public static bool MoveTo(int x, int y) { return SetCursorPos(x, y); }
        public static POINT Where() { POINT p; GetCursorPos(out p); return p; }

        public static void Mouse(uint flag, int data)
        {
            INPUT[] a = new INPUT[1];
            a[0].type = 0; a[0].U.mi.dwFlags = flag; a[0].U.mi.mouseData = (uint)data;
            SendInput(1, a, Marshal.SizeOf(typeof(INPUT)));
        }
        public static void LeftDown() { Mouse(0x0002, 0); }
        public static void LeftUp() { Mouse(0x0004, 0); }
        public static void RightDown() { Mouse(0x0008, 0); }
        public static void RightUp() { Mouse(0x0010, 0); }
        public static void Wheel(int c) { Mouse(0x0800, c * 120); }

        public static void SendUnicodeChar(char c)
        {
            INPUT[] a = new INPUT[2];
            a[0].type = 1;
            a[0].U.ki.wScan = (ushort)c;
            a[0].U.ki.dwFlags = 0x0004;          // KEYEVENTF_UNICODE
            a[0].U.ki.wVk = 0;
            a[1].type = 1;
            a[1].U.ki.wScan = (ushort)c;
            a[1].U.ki.dwFlags = 0x0004 | 0x0002; // UNICODE + KEYUP
            a[1].U.ki.wVk = 0;
            SendInput(2, a, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendVk(ushort vk, bool up)
        {
            INPUT[] a = new INPUT[1];
            a[0].type = 1;
            a[0].U.ki.wVk = vk;
            a[0].U.ki.dwFlags = up ? 2u : 0u;
            SendInput(1, a, Marshal.SizeOf(typeof(INPUT)));
        }

        public static readonly Dictionary<string, ushort> VkMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            {"enter",0x0D},{"tab",0x09},{"esc",0x1B},{"space",0x20},{"back",0x08},{"delete",0x2E},
            {"up",0x26},{"down",0x28},{"left",0x25},{"right",0x27},{"home",0x24},{"end",0x23},
            {"pgup",0x21},{"pgdn",0x22},{"f1",0x70},{"f2",0x71},{"f3",0x72},{"f4",0x73},{"f5",0x74},
            {"f6",0x75},{"f7",0x76},{"f8",0x77},{"f9",0x78},{"f10",0x79},{"f11",0x7A},{"f12",0x7B},
            {"ctrl",0x11},{"alt",0x12},{"shift",0x10},{"win",0x5B}
        };

        public static void SendKeyCombo(string expr)
        {
            var parts = expr.Split('+');
            var mods = new List<ushort>();
            ushort main = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (i < parts.Length - 1 && VkMap.ContainsKey(p)) { mods.Add(VkMap[p]); continue; }
                if (VkMap.ContainsKey(p)) main = VkMap[p];
                else if (p.Length == 1) main = (ushort)char.ToUpperInvariant(p[0]);
            }
            foreach (var m in mods) SendVk(m, false);
            if (main != 0) { SendVk(main, false); System.Threading.Thread.Sleep(15); SendVk(main, true); }
            for (int i = mods.Count - 1; i >= 0; i--) SendVk(mods[i], true);
        }

        public static void TypeText(string text, bool wantSlow = false)
        {
            if (text.Length > 24 && !wantSlow)
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(text);
                    System.Threading.Thread.Sleep(80);
                    SendKeyCombo("ctrl+v");
                    System.Threading.Thread.Sleep(100);
                    return;
                }
                catch { }
            }
            foreach (char c in text)
            {
                SendUnicodeChar(c);
                System.Threading.Thread.Sleep(HumanLevel == 0 ? 2 : 8);
            }
        }

        public static IntPtr RootAt(int x, int y)
        {
            POINT p; p.X = x; p.Y = y;
            IntPtr h = WindowFromPoint(p);
            if (h == IntPtr.Zero) return IntPtr.Zero;
            IntPtr top = GetAncestor(h, 2);
            return top == IntPtr.Zero ? h : top;
        }
        public static string Describe(IntPtr h)
        {
            if (h == IntPtr.Zero) return "(none)";
            StringBuilder t = new StringBuilder(512); GetWindowText(h, t, 512);
            StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256);
            RECT r; GetWindowRect(h, out r);
            return "hwnd=" + h + " class=" + c + " rect=(" + r.Left + "," + r.Top + ")-(" + r.Right + "," + r.Bottom + ") title=" + t;
        }

        public static bool ForceForeground(IntPtr h)
        {
            if (h == IntPtr.Zero) return false;
            // 最小化窗口必须先还原，且要等它真正退出 iconic 状态
            if (IsIconic(h))
            {
                ShowWindow(h, 9);            // SW_RESTORE
                for (int i = 0; i < 20 && IsIconic(h); i++) System.Threading.Thread.Sleep(30);
            }
            ShowWindow(h, 5);                // SW_SHOW
            if (GetForegroundWindow() == h) return true;

            IntPtr fg = GetForegroundWindow();
            uint fgPid; uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
            uint myThread = GetCurrentThreadId();
            bool attached = false;
            if (fgThread != 0 && fgThread != myThread) attached = AttachThreadInput(myThread, fgThread, true);
            BringWindowToTop(h);
            SetForegroundWindow(h);
            SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040); // HWND_TOP
            if (attached) AttachThreadInput(myThread, fgThread, false);
            System.Threading.Thread.Sleep(120);
            if (GetForegroundWindow() == h) return true;
            // 再试一次：点一下窗口客户区激活（有些应用只认鼠标激活）
            RECT r; GetWindowRect(h, out r);
            SetCursorPos(r.Left + 30, r.Top + 30);
            return GetForegroundWindow() == h;
        }

        public static List<WinInfo> Windows()
        {
            List<WinInfo> res = new List<WinInfo>();
            IntPtr fg = GetForegroundWindow();
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder t = new StringBuilder(512); GetWindowText(h, t, 512);
                if (t.Length == 0) return true;
                RECT r;
                if (!GetWindowRect(h, out r)) return true;
                int cloaked = 0; DwmGetWindowAttribute(h, 14, out cloaked, 4);
                if (cloaked != 0) return true;
                WinInfo w = new WinInfo();
                w.hwnd = h;
                uint pid; GetWindowThreadProcessId(h, out pid); w.pid = pid;
                w.left = r.Left; w.top = r.Top; w.right = r.Right; w.bottom = r.Bottom;
                w.foreground = (h == fg);
                int ex = GetWindowLong(h, -20);
                w.topmost = (ex & 0x8) != 0;
                w.minimized = IsIconic(h);
                StringBuilder c = new StringBuilder(256); GetClassName(h, c, 256);
                w.cls = c.ToString();
                w.title = t.ToString();
                res.Add(w);
                return true;
            }, IntPtr.Zero);
            return res;
        }

        public static IntPtr FindByTitle(string sub)
        {
            foreach (WinInfo w in Windows())
                if (w.title.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0) return w.hwnd;
            return IntPtr.Zero;
        }

        // ================= 人类化动作引擎 =================
        // HumanLevel: 0=机械快速  1=标准(默认)  2=高拟真(慢但更像真人)
        public static int HumanLevel = 1;
        static readonly Random Rnd = new Random();
        static readonly object RndLock = new object();

        static double U() { lock (RndLock) { return Rnd.NextDouble(); } }
        static int Ri(int a, int b) { lock (RndLock) { return Rnd.Next(a, b); } }

        // Box-Muller 高斯分布，用来模拟落点误差
        static double Gauss()
        {
            double u1 = 1.0 - U(), u2 = U();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        // 二次贝塞尔轨迹 + 随机弧线 + 过冲 + 末端微调
        public static void HumanMove(int tx, int ty, int ms)
        {
            POINT p = Where();
            int sx = p.X, sy = p.Y;
            if (sx == tx && sy == ty) return;

            double dx = tx - sx, dy = ty - sy;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 2) { SetCursorPos(tx, ty); return; }

            if (HumanLevel == 0)
            {
                SetCursorPos(tx, ty);
                return;
            }

            // 控制点：沿垂直方向随机弓起，产生自然弧线
            double nx = -dy, ny = dx;
            double nlen = Math.Sqrt(nx * nx + ny * ny);
            if (nlen < 1) nlen = 1;
            double bow = (U() - 0.5) * Math.Min(70.0, dist * 0.18);
            double cx = (sx + tx) / 2.0 + nx / nlen * bow;
            double cy = (sy + ty) / 2.0 + ny / nlen * bow;

            // 过冲：越过目标 0~6px 再收回来
            double over = HumanLevel == 2 ? 1.0 : 0.4;
            double ox = tx + dx / dist * (U() * 6 - 1) * over;
            double oy = ty + dy / dist * (U() * 6 - 1) * over;

            int steps = Math.Max(6, (int)(dist / (HumanLevel == 2 ? 9 : 14)));
            int baseDelay = HumanLevel == 2 ? 5 : 3;

            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                double e = t * t * (3 - 2 * t);          // smoothstep：加速-匀速-减速
                double it = 1 - e;
                double x = it * it * sx + 2 * it * e * cx + e * e * ox;
                double y = it * it * sy + 2 * it * e * cy + e * e * oy;
                SetCursorPos((int)Math.Round(x), (int)Math.Round(y));
                System.Threading.Thread.Sleep(baseDelay + Ri(0, 3));
            }

            // 末端微调：先到过冲点，再回到真实目标
            SetCursorPos((int)Math.Round(ox), (int)Math.Round(oy));
            System.Threading.Thread.Sleep(Ri(12, 45));
            SetCursorPos(tx, ty);
        }

        static void Dwell()
        {
            // 按下前的“犹豫”，以及按住时长
            int pre = HumanLevel == 2 ? Ri(60, 220) : Ri(20, 80);
            System.Threading.Thread.Sleep(pre);
        }

        public static void Click(int x, int y, int moveMs)
        {
            // 落点高斯抖动：真人不会每次都点正中
            int jx = 0, jy = 0;
            if (HumanLevel > 0)
            {
                double s = HumanLevel == 2 ? 2.5 : 1.2;
                jx = (int)Math.Round(Gauss() * s);
                jy = (int)Math.Round(Gauss() * s);
            }
            HumanMove(x + jx, y + jy, moveMs);
            Dwell();
            LeftDown();
            System.Threading.Thread.Sleep(HumanLevel == 2 ? Ri(55, 145) : Ri(35, 80));
            LeftUp();
        }

        public static void RightClick(int x, int y, int moveMs)
        {
            HumanMove(x + (int)Math.Round(Gauss() * 1.2), y + (int)Math.Round(Gauss() * 1.2), moveMs);
            Dwell();
            RightDown();
            System.Threading.Thread.Sleep(Ri(35, 90));
            RightUp();
        }

        public static void DoubleClick(int x, int y, int moveMs)
        {
            HumanMove(x + (int)Math.Round(Gauss() * 1.2), y + (int)Math.Round(Gauss() * 1.2), moveMs);
            Dwell();
            LeftDown(); System.Threading.Thread.Sleep(Ri(25, 55)); LeftUp();
            System.Threading.Thread.Sleep(Ri(45, 95));
            LeftDown(); System.Threading.Thread.Sleep(Ri(25, 55)); LeftUp();
        }

        // 等待窗口出现/标题变化，比固定 sleep 更快也更稳
        public static IntPtr WaitWindow(string sub, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                IntPtr h = FindByTitle(sub);
                if (h != IntPtr.Zero) return h;
                System.Threading.Thread.Sleep(60);
                waited += 60;
            }
            return IntPtr.Zero;
        }
    }
}


