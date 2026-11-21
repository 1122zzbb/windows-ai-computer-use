using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace RealMouse
{
    public class Program
    {
        static void SendUnicodeChar(char c) { Native.SendUnicodeChar(c); }
        static void SendVk(ushort vk, bool up) { Native.SendVk(vk, up); }
        static void SendKeyCombo(string expr) { Native.SendKeyCombo(expr); }
        public static void SendCombo(string expr) { Native.SendKeyCombo(expr); }
        public static void TypeText(string text, bool wantSlow = false) { Native.TypeText(text, wantSlow); }

        // ---------- 截图 ----------
        static string Shot(string path, bool grid, int cell, double scale)
        {
            int w = Native.PhysW(), h = Native.PhysH();
            using (var bmp = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(0, 0, 0, 0, new Size(w, h));

                if (grid)
                {
                    using (var g = Graphics.FromImage(bmp))
                    using (var font = new Font("Consolas", 18))
                    using (var brush = new SolidBrush(Color.FromArgb(220, 255, 40, 40)))
                    using (var pen = new Pen(Color.FromArgb(80, 255, 0, 0), 1))
                    {
                        for (int x = 0; x < w; x += cell) { g.DrawLine(pen, x, 0, x, h); g.DrawString(x.ToString(), font, brush, x + 3, 3); }
                        for (int y = 0; y < h; y += cell) { g.DrawLine(pen, 0, y, w, y); g.DrawString(y.ToString(), font, brush, 3, y + 1); }
                    }
                }

                if (scale > 0 && scale < 1.0)
                {
                    int nw = Math.Max(1, (int)(w * scale)), nh = Math.Max(1, (int)(h * scale));
                    using (var small = new Bitmap(nw, nh))
                    {
                        using (var gs = Graphics.FromImage(small))
                        {
                            gs.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            gs.DrawImage(bmp, 0, 0, nw, nh);
                        }
                        small.Save(path, ImageFormat.Png);
                    }
                    Console.WriteLine("# scaled " + nw + "x" + nh + " ; point-in-image * " + Math.Round(1 / scale, 3) + " = physical");
                }
                else bmp.Save(path, ImageFormat.Png);
            }
            return path;
        }

        static string Crop(int x, int y, int cw, int ch, double scale, string path)
        {
            int w = Native.PhysW(), h = Native.PhysH();
            int nw = Math.Max(1, (int)(cw * scale)), nh = Math.Max(1, (int)(ch * scale));
            using (var full = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(full)) g.CopyFromScreen(0, 0, 0, 0, new Size(w, h));
                using (var c = new Bitmap(nw, nh))
                {
                    using (var gc = Graphics.FromImage(c))
                    {
                        gc.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        gc.DrawImage(full, new Rectangle(0, 0, nw, nh), new Rectangle(x, y, cw, ch), GraphicsUnit.Pixel);
                    }
                    c.Save(path, ImageFormat.Png);
                }
            }
            return path;
        }

        // ---------- 辅助 ----------
        static int Pct(string expr, int size, int origin)
        {
            if (string.IsNullOrEmpty(expr)) return origin;
            if (expr.EndsWith("%")) return origin + (int)(size * double.Parse(expr.TrimEnd('%')) / 100.0);
            return (int)double.Parse(expr);
        }

        static IntPtr Target(string title, long hwnd)
        {
            if (hwnd != 0) return new IntPtr(hwnd);
            if (!string.IsNullOrEmpty(title))
            {
                IntPtr h = Native.FindByTitle(title);
                if (h == IntPtr.Zero) throw new Exception("找不到窗口: " + title);
                return h;
            }
            throw new Exception("需要窗口标题或 hwnd");
        }

        static Color GetPixelColor(int x, int y)
        {
            using (var b = new Bitmap(1, 1))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.CopyFromScreen(x, y, 0, 0, new Size(1, 1));
                }
                return b.GetPixel(0, 0);
            }
        }

        static void Usage()
        {
            Console.WriteLine(@"rm.exe <command> [args]

  screen                                       屏幕/DPI/当前焦点
  where                                        光标位置 + 其下窗口
  windows                                      列出可见窗口
  find   <标题子串>                            查找窗口
  focus  <标题子串>                            激活窗口
  move   <x> <y> [ms]                          移动（缓动）
  click  <x> <y> [ms]                          左键点击
  clickw <标题子串> <rx> <ry> [ms]             窗口内相对点击（支持 50%）
  dclick <x> <y> [ms]                          双击
  rclick <x> <y> [ms]                          右键
  wheel  <n>                                   滚轮（正上负下）
  type   <文本> [标题子串]                     输入文本（Unicode 直发，支持中文）
  key    <组合键> [标题子串]                   如 ctrl+v / enter / f6
  shot   <文件> [--grid] [--cell 200] [--scale 0.5]
  crop   <x> <y> <w> <h> <scale> <文件>
  ocr    <文本> [选项]                         全屏内存高速 OCR 定位/点击（支持并行切片）
  ocrw   <文本> [选项]                         当前窗口 ROI 极速 OCR 定位/点击（零磁盘 I/O）
         选项: --click / --dclick / --rclick / --tclick
               --toright [dist=55] / --toleft [dist=30] / --below [dist=35]
               --offsetx <px> / --offsety <px>
               --type <文本> / --enter / --tab / --window <窗口> / --parallel
  waitocr <文本> [timeoutMs]                   等待文字出现在屏幕上（自适应轮询）
  waitpix <x> <y> [timeoutMs]                  等待像素颜色变化（画面加载感知）
  drag   <x1> <y1> <x2> <y2>                  拖选（按住左键拖过一段）
  tclick <x> <y>                              三击（选中段落/整格）
  findclick <描述> [文本]                      一步到位：截图+视觉接地+点击（+粘贴）
  ground <描述>                              只做视觉接地，返回坐标不点击
  grid   <x> <y> <w> <h> | --file <png>       自动检测表格线，输出每个单元格矩形与中心
  sleep  <ms>                                  延时（脚本里用）
  wait   <窗口标题> [timeoutMs]               等待窗口出现
  locate <窗口标题> [关键词] [--type button] [--limit 20]   UIA 定位元素（免截图）
  clickel <窗口标题> <关键词>                   定位并点击第一个匹配元素
  human  [0|1|2]                               拟人档位（0快速 1标准 2高拟真）
  probe  <x> <y>                              锚点校准（回读实际落点与窗口）
  verify <前图> <后图> [阈值%]                判断操作后画面是否变化
  findai <描述>                              用 Qwen3-VL 视觉定位返回坐标（兜底）
  script <文件>                                批量执行脚本
  bench                                        自检耗时");
        }

        // ---------- 批处理脚本 ----------
        static int RunScript(string file)
        {
            if (!File.Exists(file)) { Console.WriteLine("脚本不存在: " + file); return 2; }
            int lineNo = 0, fail = 0;
            foreach (var raw in File.ReadAllLines(file))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                Console.WriteLine("[{0}] {1}", lineNo, line);
                var sw = Stopwatch.StartNew();
                try { Exec(line.Split(new[] { ' ' }, 2)[0].ToLower(), Tokenize(line)); }
                catch (Exception ex) { Console.WriteLine("  ERR: " + ex.Message); fail++; }
                Console.WriteLine("  -> " + sw.ElapsedMilliseconds + "ms");
            }
            return fail;
        }

        // 支持 "带空格 的参数" 这种引号
        static string[] Tokenize(string line)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            foreach (char c in line)
            {
                if (c == '"') { q = !q; continue; }
                if (c == ' ' && !q) { if (sb.Length > 0) { res.Add(sb.ToString()); sb.Clear(); } continue; }
                sb.Append(c);
            }
            if (sb.Length > 0) res.Add(sb.ToString());
            if (res.Count > 0) res.RemoveAt(0); // 去掉命令名
            return res.ToArray();
        }

        // ---------- 主分派 ----------
        static void Exec(string cmd, string[] a)
        {
            switch (cmd)
            {
                case "screen":
                    Console.WriteLine("物理像素 : " + Native.PhysW() + " x " + Native.PhysH());
                    Console.WriteLine("系统度量 : " + Native.GetSystemMetrics(0) + " x " + Native.GetSystemMetrics(1));
                    Console.WriteLine("焦点窗口 : " + Native.Describe(Native.GetForegroundWindow()));
                    break;

                case "where":
                {
                    POINT p = Native.Where();
                    Console.WriteLine("光标: " + p.X + "," + p.Y);
                    Console.WriteLine("窗口: " + Native.Describe(Native.RootAt(p.X, p.Y)));
                    break;
                }

                case "windows":
                    foreach (var w in Native.Windows())
                    {
                        Console.WriteLine("{0} hwnd={1} pid={2} ({3},{4})-({5},{6}) {7}x{8} min={9} top={10} {11} | {12}",
                            w.foreground ? "<<" : "  ", w.hwnd, w.pid, w.left, w.top, w.right, w.bottom,
                            w.Width, w.Height, w.minimized, w.topmost, w.cls, w.title);
                    }
                    break;

                case "find":
                    Console.WriteLine(Native.Describe(Target(a[0], 0)));
                    break;

                case "focus":
                {
                    IntPtr h = Target(a[0], 0);
                    bool ok = Native.ForceForeground(h);
                    Console.WriteLine("active=" + ok + " | " + Native.Describe(Native.GetForegroundWindow()));
                    break;
                }

                case "move":
                    Native.HumanMove(int.Parse(a[0]), int.Parse(a[1]), a.Length > 2 ? int.Parse(a[2]) : 120);
                    Console.WriteLine("moved " + a[0] + "," + a[1]);
                    break;

                case "click":
                    Native.Click(int.Parse(a[0]), int.Parse(a[1]), a.Length > 2 ? int.Parse(a[2]) : 120);
                    Console.WriteLine("click " + a[0] + "," + a[1] + " -> " + Native.Describe(Native.RootAt(int.Parse(a[0]), int.Parse(a[1]))));
                    break;

                case "clickfast":
                {
                    int fx = int.Parse(a[0]), fy = int.Parse(a[1]);
                    Native.SetCursorPos(fx, fy);
                    Native.LeftDown();
                    Thread.Sleep(5);
                    Native.LeftUp();
                    Console.WriteLine("clickfast " + fx + "," + fy);
                    break;
                }

                case "clicktype":
                {
                    int tx = int.Parse(a[0]), ty = int.Parse(a[1]);
                    string text = a[2];
                    Native.Click(tx, ty, 60);
                    Thread.Sleep(80);
                    Native.TypeText(text, false);
                    Console.WriteLine("clicktyped " + text + " at " + tx + "," + ty);
                    break;
                }

                case "clickw":
                {
                    IntPtr h = Target(a[0], 0);
                    Native.ForceForeground(h);
                    Thread.Sleep(120);
                    RECT r; Native.GetWindowRect(h, out r);
                    int tx = Pct(a[1], r.Right - r.Left, r.Left);
                    int ty = Pct(a[2], r.Bottom - r.Top, r.Top);
                    Native.Click(tx, ty, a.Length > 3 ? int.Parse(a[3]) : 120);
                    Console.WriteLine("clickw " + tx + "," + ty + " -> " + Native.Describe(Native.RootAt(tx, ty)));
                    break;
                }

                case "dclick":
                    Native.DoubleClick(int.Parse(a[0]), int.Parse(a[1]), a.Length > 2 ? int.Parse(a[2]) : 100);
                    Console.WriteLine("dclick done");
                    break;

                case "rclick":
                    Native.RightClick(int.Parse(a[0]), int.Parse(a[1]), a.Length > 2 ? int.Parse(a[2]) : 100);
                    Console.WriteLine("rclick done");
                    break;

                case "wheel":
                {
                    if (a.Length >= 3)
                    {
                        Native.HumanMove(int.Parse(a[1]), int.Parse(a[2]), 60);
                    }
                    else
                    {
                        POINT cur = Native.Where();
                        if (cur.Y < 150)
                        {
                            Native.HumanMove(cur.X > 200 ? cur.X : 500, 500, 60);
                        }
                    }
                    Native.Wheel(int.Parse(a[0]));
                    Thread.Sleep(80);
                    Console.WriteLine("wheel " + a[0]);
                    break;
                }

                case "type":
                {
                    string text = a[0];
                    if (a.Length > 1) { Native.ForceForeground(Target(a[1], 0)); Thread.Sleep(80); }

                    // 长文本走剪贴板 + Ctrl+V：真人本来也这么粘贴，比逐字快两个数量级
                    bool wantSlow = Array.Exists(a, x => x == "--slow");
                    if (text.Length > 24 && !wantSlow)
                    {
                        try
                        {
                            System.Windows.Forms.Clipboard.SetText(text);
                            Thread.Sleep(120);
                            SendKeyCombo("ctrl+v");
                            Thread.Sleep(250);
                            Console.WriteLine("pasted " + text.Length + " chars via clipboard -> " + Native.Describe(Native.GetForegroundWindow()));
                            break;
                        }
                        catch (Exception ex) { Console.WriteLine("clipboard failed(" + ex.Message + "), fallback to keystrokes"); }
                    }

                    var rnd = new Random();
                    int mistypes = 0;
                    foreach (char c in text)
                    {
                        // 偶发手误：打错一个相邻键再退格（3%）
                        if (Native.HumanLevel == 2 && rnd.NextDouble() < 0.03)
                        {
                            char wrong = (char)((int)c + rnd.Next(-1, 2));
                            SendUnicodeChar(wrong);
                            Thread.Sleep(60 + rnd.Next(90));
                            SendVk(0x08, false); Thread.Sleep(25); SendVk(0x08, true);
                            Thread.Sleep(40 + rnd.Next(70));
                            mistypes++;
                        }
                        SendUnicodeChar(c);
                        // 节奏：基础间隔 + 标点后停顿 + 随机抖动
                        int d;
                        if (Native.HumanLevel == 0) d = 2;
                        else
                        {
                            double mean = Native.HumanLevel == 2 ? 105 : 60;
                            d = (int)Math.Max(30, mean + (rnd.NextDouble() - 0.5) * mean);
                            if ("，。！？、；：,.!?;:".IndexOf(c) >= 0) d += Native.HumanLevel == 2 ? 260 : 140;
                            if (c == ' ') d += 20 + rnd.Next(40);
                        }
                        Thread.Sleep(d);
                    }
                    Console.WriteLine("typed " + text.Length + " chars"
                        + (mistypes > 0 ? (" (" + mistypes + " self-corrected)") : "")
                        + " -> " + Native.Describe(Native.GetForegroundWindow()));
                    break;
                }

                case "key":
                {
                    if (a.Length > 1) { Native.ForceForeground(Target(a[1], 0)); Thread.Sleep(80); }
                    SendKeyCombo(a[0]);
                    Console.WriteLine("key " + a[0]);
                    break;
                }

                case "shot":
                {
                    string path = a.Length > 0 ? a[0] : Path.Combine(Path.GetTempPath(), "rm_" + DateTime.Now.ToString("HHmmss") + ".png");
                    bool grid = Array.IndexOf(a, "--grid") >= 0;
                    int cell = 200; double scale = 1.0;
                    for (int i = 0; i < a.Length; i++)
                    {
                        if (a[i] == "--cell" && i + 1 < a.Length) cell = int.Parse(a[i + 1]);
                        if (a[i] == "--scale" && i + 1 < a.Length) scale = double.Parse(a[i + 1]);
                    }
                    Console.WriteLine(Shot(path, grid, cell, scale));
                    break;
                }

                case "crop":
                    Console.WriteLine(Crop(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]),
                        double.Parse(a[4]), a[5]));
                    break;

                case "ocr":
                    OcrService.ExecuteOcr(a, false);
                    break;

                case "ocrw":
                    OcrService.ExecuteOcr(a, true);
                    break;

                case "waitocr":
                {
                    string target = a[0];
                    int timeout = a.Length > 1 ? int.Parse(a[1]) : 5000;
                    OcrService.WaitOcr(target, timeout, true);
                    break;
                }

                case "waitpix":
                {
                    int px = int.Parse(a[0]), py = int.Parse(a[1]);
                    int timeout = a.Length > 2 ? int.Parse(a[2]) : 5000;
                    Color initColor = GetPixelColor(px, py);
                    var sww = Stopwatch.StartNew();
                    bool changed = false;
                    while (sww.ElapsedMilliseconds < timeout)
                    {
                        Thread.Sleep(40);
                        if (GetPixelColor(px, py) != initColor) { changed = true; break; }
                    }
                    Console.WriteLine("waitpix (" + px + "," + py + ") changed=" + changed + " in " + sww.ElapsedMilliseconds + "ms");
                    break;
                }

                case "locate":
                {
                    // locate <窗口标题> [关键词] [--type button|edit|text|hyperlink] [--limit N]
                    string kw = "", type = "";
                    int limit = 20;
                    for (int i = 1; i < a.Length; i++)
                    {
                        if (a[i] == "--type" && i + 1 < a.Length) { type = a[i + 1].ToLower(); continue; }
                        if (a[i] == "--limit" && i + 1 < a.Length) { limit = int.Parse(a[i + 1]); continue; }
                        if (!a[i].StartsWith("--") && kw == "") kw = a[i];
                    }
                    IntPtr h = Target(a[0], 0);
                    var root = AutomationElement.FromHandle(h);
                    if (root == null) { Console.WriteLine("no element tree"); break; }
                    var sw0 = Stopwatch.StartNew();
                    var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    int n = 0;
                    foreach (AutomationElement e in all)
                    {
                        AutomationElement.AutomationElementInformation cur;
                        try { cur = e.Current; } catch { continue; }
                        if (string.IsNullOrEmpty(cur.Name)) continue;
                        if (kw != "" && cur.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string ct = cur.ControlType.ProgrammaticName.Replace("ControlType.", "");
                        if (type != "" && ct.ToLower().IndexOf(type) < 0) continue;
                        System.Windows.Rect r;
                        try { r = cur.BoundingRectangle; } catch { continue; }
                        if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) continue;
                        Console.WriteLine("[{0}] {1} \"{2}\" center=({3},{4}) rect=({5},{6}) {7}x{8}",
                            n, ct, cur.Name, (int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2),
                            (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                        if (++n >= limit) break;
                    }
                    Console.WriteLine("# " + n + " matched / " + all.Count + " nodes in " + sw0.ElapsedMilliseconds + "ms");
                    break;
                }

                case "clickel":
                {
                    // clickel <窗口标题> <关键词>  —— 定位并点击第一个匹配元素
                    string kw = a.Length > 1 ? a[1] : "";
                    IntPtr h = Target(a[0], 0);
                    Native.ForceForeground(h);
                    Thread.Sleep(80);
                    var root = AutomationElement.FromHandle(h);
                    if (root == null) { Console.WriteLine("no element tree"); break; }
                    var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    bool hit = false;
                    foreach (AutomationElement e in all)
                    {
                        AutomationElement.AutomationElementInformation cur;
                        try { cur = e.Current; } catch { continue; }
                        if (string.IsNullOrEmpty(cur.Name)) continue;
                        if (cur.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        System.Windows.Rect r;
                        try { r = cur.BoundingRectangle; } catch { continue; }
                        if (r.IsEmpty || r.Width <= 0) continue;
                        int cx = (int)(r.X + r.Width / 2), cy = (int)(r.Y + r.Height / 2);
                        Native.Click(cx, cy, 100);
                        Console.WriteLine("clicked \"" + cur.Name + "\" @(" + cx + "," + cy + ") -> " + Native.Describe(Native.RootAt(cx, cy)));
                        hit = true;
                        break;
                    }
                    if (!hit) Console.WriteLine("NOT FOUND: " + kw);
                    break;
                }

                case "drag":
                {
                    // drag <x1> <y1> <x2> <y2>  —— 按住左键从起点拖到终点（选中文字/拖动对象）
                    int x1 = int.Parse(a[0]), y1 = int.Parse(a[1]), x2 = int.Parse(a[2]), y2 = int.Parse(a[3]);
                    Native.HumanMove(x1, y1, 90);
                    Thread.Sleep(60);
                    Native.LeftDown();
                    Thread.Sleep(90);
                    int steps = 14;
                    for (int k = 1; k <= steps; k++)
                    {
                        int cx = x1 + (x2 - x1) * k / steps;
                        int cy = y1 + (y2 - y1) * k / steps;
                        Native.MoveTo(cx, cy);
                        Thread.Sleep(12);
                    }
                    Thread.Sleep(70);
                    Native.LeftUp();
                    Thread.Sleep(120);
                    Console.WriteLine("drag (" + x1 + "," + y1 + ") -> (" + x2 + "," + y2 + ")");
                    break;
                }

                case "tclick":
                {
                    // tclick <x> <y>  —— 三击，Word/浏览器里用来选中整段或整格
                    int x = int.Parse(a[0]), y = int.Parse(a[1]);
                    Native.HumanMove(x, y, 90);
                    Thread.Sleep(60);
                    for (int k = 0; k < 3; k++)
                    {
                        Native.LeftDown();
                        Thread.Sleep(28);
                        Native.LeftUp();
                        Thread.Sleep(55);
                    }
                    Thread.Sleep(120);
                    Console.WriteLine("triple-click " + x + "," + y);
                    break;
                }

                case "findclick":
                {
                    // findclick "<描述>" [--type]  —— 一步到位：截图+视觉接地+点击
                    string desc = a[0];
                    bool alsoType = Array.Exists(a, x => x == "--type");
                    var sw = Stopwatch.StartNew();
                    int cx, cy; string box;
                    if (!Ground(desc, out cx, out cy, out box)) { Console.WriteLine("GROUND FAILED"); break; }
                    long ground = sw.ElapsedMilliseconds;
                    Console.WriteLine("ground: " + box + " -> physical (" + cx + "," + cy + ") in " + ground + "ms");
                    Console.WriteLine("target: " + Native.Describe(Native.RootAt(cx, cy)));
                    Native.Click(cx, cy, 100);
                    Thread.Sleep(250);
                    if (alsoType && a.Length > 1)
                    {
                        try { System.Windows.Forms.Clipboard.SetText(a[1]); Thread.Sleep(100); SendKeyCombo("ctrl+v"); Thread.Sleep(200); }
                        catch { }
                    }
                    Console.WriteLine("done in " + sw.ElapsedMilliseconds + "ms");
                    break;
                }

                case "ground":
                {
                    // ground "<描述>"  —— 只定位不点击
                    int cx, cy; string box;
                    var sw = Stopwatch.StartNew();
                    if (!Ground(a[0], out cx, out cy, out box)) { Console.WriteLine("GROUND FAILED"); break; }
                    Console.WriteLine("box=" + box + " physical=(" + cx + "," + cy + ") " + sw.ElapsedMilliseconds + "ms");
                    Console.WriteLine("under: " + Native.Describe(Native.RootAt(cx, cy)));
                    break;
                }

                case "grid":
                {
                    // grid <x> <y> <w> <h>  —— 自动检测区域内表格线，输出单元格矩形与中心
                    int gx = 0, gy = 0, gw = 0, gh = 0;
                    Bitmap srcBmp = null;
                    if (a.Length > 0 && a[0] == "--file")
                    {
                        srcBmp = new Bitmap(a[1]);
                        gw = srcBmp.Width; gh = srcBmp.Height;
                    }
                    else
                    {
                        gx = int.Parse(a[0]); gy = int.Parse(a[1]); gw = int.Parse(a[2]); gh = int.Parse(a[3]);
                        srcBmp = new Bitmap(gw, gh);
                        using (var g = Graphics.FromImage(srcBmp)) g.CopyFromScreen(gx, gy, 0, 0, new Size(gw, gh));
                    }
                    using (var bmp = srcBmp)
                    {
                        var bd = bmp.LockBits(new Rectangle(0, 0, gw, gh), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        int stride = bd.Stride;
                        var buf = new byte[stride * gh];
                        System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
                        bmp.UnlockBits(bd);

                        Func<int,int,int,bool> isDark = (px, py, thr) =>
                        {
                            int i = py * stride + px * 3;
                            return buf[i] < thr && buf[i+1] < thr && buf[i+2] < thr;
                        };

                        // 1) 找横线：某行深色像素 > 55%
                        var hl = new List<int>();
                        for (int yy = 0; yy < gh; yy++)
                        {
                            int dark = 0;
                            for (int xx = 0; xx < gw; xx++) if (isDark(xx, yy, 218)) dark++;
                            if (dark > gw * 0.45) hl.Add(yy);
                        }
                        // 2) 找竖线
                        var vl = new List<int>();
                        int vThr = (int)(gh * 0.45);
                        for (int xx = 0; xx < gw; xx++)
                        {
                            int dark = 0;
                            for (int yy = 0; yy < gh; yy++) if (isDark(xx, yy, 218)) dark++;
                            if (dark > vThr) vl.Add(xx);
                        }

                        // 3) 线段聚类（相邻像素合并成一条线）
                        Func<List<int>, List<int>> cluster = (src) =>
                        {
                            var r = new List<int>();
                            if (src.Count == 0) return r;
                            int start = src[0], prev = src[0];
                            for (int k = 1; k < src.Count; k++)
                            {
                                if (src[k] - prev <= 3) { prev = src[k]; continue; }
                                r.Add((start + prev) / 2);
                                start = src[k]; prev = src[k];
                            }
                            r.Add((start + prev) / 2);
                            return r;
                        };
                        var rows = cluster(hl);
                        var cols = cluster(vl);

                        Console.WriteLine("横线 y: " + string.Join(",", rows));
                        Console.WriteLine("竖线 x: " + string.Join(",", cols));
                        Console.WriteLine("检测到 " + (rows.Count - 1) + " 行 x " + (cols.Count - 1) + " 列单元格");
                        for (int r = 0; r + 1 < rows.Count; r++)
                        {
                            for (int cIdx = 0; cIdx + 1 < cols.Count; cIdx++)
                            {
                                int cx = gx + (cols[cIdx] + cols[cIdx + 1]) / 2;
                                int cy = gy + (rows[r] + rows[r + 1]) / 2;
                                Console.WriteLine(string.Format("  cell[{0},{1}] center=({2},{3})  rect=({4},{5})-({6},{7})",
                                    r, cIdx, cx, cy,
                                    gx + cols[cIdx], gy + rows[r],
                                    gx + cols[cIdx + 1], gy + rows[r + 1]));
                            }
                        }
                    }
                    break;
                }
                case "sleep":
                    Thread.Sleep(int.Parse(a[0]));
                    Console.WriteLine("slept " + a[0] + "ms");
                    break;

                case "wait":
                {
                    int to = a.Length > 1 ? int.Parse(a[1]) : 5000;
                    var sw = Stopwatch.StartNew();
                    IntPtr h = Native.WaitWindow(a[0], to);
                    Console.WriteLine(h != IntPtr.Zero ? ("found in " + sw.ElapsedMilliseconds + "ms: " + Native.Describe(h)) : ("timeout " + to + "ms"));
                    break;
                }

                case "human":
                {
                    int lv = a.Length > 0 ? int.Parse(a[0]) : Native.HumanLevel;
                    Native.HumanLevel = lv;
                    Console.WriteLine("human level = " + lv + (lv == 0 ? " (机械快速)" : lv == 1 ? " (标准)" : " (高拟真)"));
                    break;
                }

                case "verify":
                {
                    // verify <before.png> <after.png> [阈值%]  —— 判断操作后画面是否变化
                    string fa = a[0], fb = a[1];
                    double th = a.Length > 2 ? double.Parse(a[2]) : 0.15;
                    using (var A = new Bitmap(fa))
                    using (var B = new Bitmap(fb))
                    {
                        if (A.Width != B.Width || A.Height != B.Height)
                        { Console.WriteLine("尺寸不一致"); break; }
                        var da = A.LockBits(new Rectangle(0, 0, A.Width, A.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        var db = B.LockBits(new Rectangle(0, 0, B.Width, B.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                        int stride = da.Stride;
                        var bufA = new byte[stride * A.Height];
                        var bufB = new byte[stride * B.Height];
                        System.Runtime.InteropServices.Marshal.Copy(da.Scan0, bufA, 0, bufA.Length);
                        System.Runtime.InteropServices.Marshal.Copy(db.Scan0, bufB, 0, bufB.Length);
                        A.UnlockBits(da); B.UnlockBits(db);

                        long diff = 0, total = 0;
                        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                        for (int y = 0; y < A.Height; y += 3)
                        {
                            int row = y * stride;
                            for (int x = 0; x < A.Width; x += 3)
                            {
                                int i = row + x * 3;
                                total++;
                                int d = Math.Abs(bufA[i] - bufB[i]) + Math.Abs(bufA[i + 1] - bufB[i + 1]) + Math.Abs(bufA[i + 2] - bufB[i + 2]);
                                if (d > 24)
                                {
                                    diff++;
                                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                                }
                            }
                        }
                        double pct = 100.0 * diff / total;
                        Console.WriteLine("change = " + pct.ToString("F3") + "%  (sampled " + total + ")");
                        if (maxX >= 0)
                            Console.WriteLine("changed region = (" + minX + "," + minY + ")-(" + maxX + "," + maxY + ")");
                        Console.WriteLine(pct >= th ? "VERDICT: CHANGED" : "VERDICT: NO-CHANGE");
                    }
                    break;
                }

                case "findai":
                {
                    // findai <描述> [超时秒=60]  —— 用 Qwen3-VL 做视觉定位，返回物理坐标
                    string desc = a[0];
                    int to = a.Length > 1 ? int.Parse(a[1]) : 60;
                    string shotPath = Path.Combine(Path.GetTempPath(), "rm_ai_" + DateTime.Now.ToString("HHmmss") + ".png");
                    Shot(shotPath, false, 200, 0.445);   // 1280 宽，控制体积

                    string cfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bailian", "config.json");
                    string cfgText = File.ReadAllText(cfgPath);
                    var m = System.Text.RegularExpressions.Regex.Match(cfgText, "\"api_key\"\\s*:\\s*\"([^\"]+)\"");
                    if (!m.Success) { Console.WriteLine("找不到 api_key"); break; }
                    string key = m.Groups[1].Value;

                    string b64 = Convert.ToBase64String(File.ReadAllBytes(shotPath));
                    string prompt = "Locate this UI element: " + desc + "\\n"
                        + "Output ONLY the bounding box in normalized coordinates (0-1000) in this exact format:\\n"
                        + "<|box_start|>(x1,y1),(x2,y2)<|box_end|>";

                    var sb = new StringBuilder();
                    sb.Append("{\"model\":\"qwen3-vl-plus\",\"messages\":[{\"role\":\"user\",\"content\":[");
                    sb.Append("{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64,");
                    sb.Append(b64);
                    sb.Append("\"}},{\"type\":\"text\",\"text\":");
                    sb.Append(System.Web.HttpUtility.JavaScriptStringEncode(prompt).Replace("\"", "\\\""));
                    sb.Append("}]}]}");
                    // 手工拼 JSON 容易错，改用最小转义
                    var bodyObj = new
                    {
                        model = "qwen3-vl-plus",
                        messages = new object[] {
                            new { role = "user", content = new object[] {
                                new { type = "image_url", image_url = new { url = "data:image/png;base64," + b64 } },
                                new { type = "text", text = prompt }
                            } }
                        }
                    };
                    string body = MiniJson(bodyObj);

                    using (var wc = new System.Net.WebClient())
                    {
                        wc.Headers["Authorization"] = "Bearer " + key;
                        wc.Headers["Content-Type"] = "application/json";
                        var sw = Stopwatch.StartNew();
                        string resp = wc.UploadString("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "POST", body);
                        sw.Stop();
                        var mm = System.Text.RegularExpressions.Regex.Match(resp, "\\((\\d+),(\\d+)\\),\\((\\d+),(\\d+)\\)");
                        if (!mm.Success) { Console.WriteLine("未解析到 bbox: " + resp.Substring(0, Math.Min(400, resp.Length))); break; }
                        int x1 = int.Parse(mm.Groups[1].Value), y1 = int.Parse(mm.Groups[2].Value);
                        int x2 = int.Parse(mm.Groups[3].Value), y2 = int.Parse(mm.Groups[4].Value);
                        int W = Native.PhysW(), H = Native.PhysH();
                        int cx = (int)((x1 + x2) / 2.0 / 1000 * W);
                        int cy = (int)((y1 + y2) / 2.0 / 1000 * H);
                        Console.WriteLine("norm=(" + x1 + "," + y1 + ")-(" + x2 + "," + y2 + ")  -> physical center=(" + cx + "," + cy + ")");
                        Console.WriteLine("under-cursor: " + Native.Describe(Native.RootAt(cx, cy)));
                        Console.WriteLine("elapsed=" + sw.ElapsedMilliseconds + "ms");
                    }
                    break;
                }

                case "probe":
                {
                    // probe <x> <y>  —— 锚点校准：移动到目标点，回读实际坐标与命中窗口
                    int tx = int.Parse(a[0]), ty = int.Parse(a[1]);
                    Native.HumanMove(tx, ty, 100);
                    Thread.Sleep(60);
                    POINT ap = Native.Where();
                    Console.WriteLine("requested=(" + tx + "," + ty + ")  actual=(" + ap.X + "," + ap.Y + ")  offset=(" + (ap.X - tx) + "," + (ap.Y - ty) + ")");
                    Console.WriteLine("hit: " + Native.Describe(Native.RootAt(ap.X, ap.Y)));
                    break;
                }
                case "script":
                    RunScript(a[0]);
                    break;

                case "bench":
                {
                    var sw = Stopwatch.StartNew();
                    Native.PhysW();
                    Console.WriteLine("first-call: " + sw.ElapsedMilliseconds + "ms");
                    sw.Restart(); Native.Windows();
                    Console.WriteLine("windows(" + Native.Windows().Count + "): " + sw.ElapsedMilliseconds + "ms");
                    sw.Restart(); Native.HumanMove(800, 400, 120);
                    Console.WriteLine("move: " + sw.ElapsedMilliseconds + "ms");
                    break;
                }

                default:
                    Usage();
                    break;
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            int exitCode = 0;
            Thread t = new Thread(() =>
            {
                Native.AttachToDefaultDesktop();
                Native.MakeDpiAware();
                if (args.Length == 0) { Usage(); return; }
                try
                {
                    Exec(args[0].ToLower(), args.Length > 1 ? Sub(args) : new string[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERR: " + ex.Message);
                    exitCode = 1;
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            return exitCode;
        }

        static string MiniJson(object o)
        {
            if (o == null) return "null";
            if (o is string) { var s = (string)o; return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
            if (o is bool) return ((bool)o) ? "true" : "false";
            if (o is int || o is long || o is double) return o.ToString();
            if (o is System.Collections.IEnumerable)
            {
                var parts = new List<string>();
                foreach (var it in (System.Collections.IEnumerable)o) parts.Add(MiniJson(it));
                return "[" + string.Join(",", parts) + "]";
            }
            var fields = new List<string>();
            foreach (var pr in o.GetType().GetProperties())
                fields.Add("\"" + pr.Name + "\":" + MiniJson(pr.GetValue(o)));
            return "{" + string.Join(",", fields) + "}";
        }

        // ===== 视觉接地：截图 -> Qwen3-VL -> 物理坐标 =====
        static bool Ground(string desc, out int cx, out int cy, out string rawBox)
        {
            cx = cy = 0; rawBox = "";
            string shotPath = Path.Combine(Path.GetTempPath(), "rm_g_" + DateTime.Now.ToString("HHmmss") + ".png");
            Shot(shotPath, false, 200, 0.445);

            string cfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bailian", "config.json");
            if (!File.Exists(cfgPath)) { Console.WriteLine("找不到 ~/.bailian/config.json"); return false; }
            var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(cfgPath), "\"api_key\"\\s*:\\s*\"([^\"]+)\"");
            if (!m.Success) { Console.WriteLine("config.json 无 api_key"); return false; }
            string key = m.Groups[1].Value;

            string b64 = Convert.ToBase64String(File.ReadAllBytes(shotPath));
            string prompt = "Locate this UI element: " + desc + "\n"
                + "Output ONLY the bounding box in normalized coordinates (0-1000) using exactly:\n"
                + "<|box_start|>(x1,y1),(x2,y2)<|box_end|>";

            var bodyObj = new
            {
                model = "qwen3-vl-plus",
                messages = new object[] {
                    new { role = "user", content = new object[] {
                        new { type = "image_url", image_url = new { url = "data:image/png;base64," + b64 } },
                        new { type = "text", text = prompt }
                    } }
                }
            };
            string body = MiniJson(bodyObj);

            using (var wc = new System.Net.WebClient())
            {
                wc.Headers["Authorization"] = "Bearer " + key;
                wc.Headers["Content-Type"] = "application/json";
                string resp = wc.UploadString("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "POST", body);
                var mm = System.Text.RegularExpressions.Regex.Match(resp, "\\((\\d+),(\\d+)\\),\\((\\d+),(\\d+)\\)");
                if (!mm.Success) { Console.WriteLine("未解析到 bbox"); return false; }
                int x1 = int.Parse(mm.Groups[1].Value), y1 = int.Parse(mm.Groups[2].Value);
                int x2 = int.Parse(mm.Groups[3].Value), y2 = int.Parse(mm.Groups[4].Value);
                rawBox = "(" + x1 + "," + y1 + ")-(" + x2 + "," + y2 + ")";
                int W = Native.PhysW(), H = Native.PhysH();
                cx = (int)((x1 + x2) / 2.0 / 1000 * W);
                cy = (int)((y1 + y2) / 2.0 / 1000 * H);
                return true;
            }
        }

        static string[] Sub(string[] args)
        {
            var r = new string[args.Length - 1];
            Array.Copy(args, 1, r, 0, r.Length);
            return r;
        }
    }
}











