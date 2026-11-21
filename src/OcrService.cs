using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RealMouse
{
    public class OcrHit
    {
        public string Text;
        public int X, Y, W, H;
        public int CX { get { return X + W / 2; } }
        public int CY { get { return Y + H / 2; } }
        public string Kind; // "line" or "word"
    }

    public static class OcrService
    {
        static bool _inited = false;
        static readonly object _initLock = new object();

        static Type _ocrEngineType;
        static Type _decoderType;
        static Type _softwareBitmapType;
        static Type _ocrResultType;
        static MethodInfo _asRandomAccessStream;
        static MethodInfo _createAsync;
        static MethodInfo _asTaskDecoder;
        static MethodInfo _asTaskSbm;
        static MethodInfo _asTaskOcr;
        static MethodInfo _getSbmMethod;
        static MethodInfo _recMethod;
        static MethodInfo _tryCreateMethod;

        public static void EnsureInitialized()
        {
            if (_inited) return;
            lock (_initLock)
            {
                if (_inited) return;
                _ocrEngineType = Type.GetType("Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime");
                _decoderType = Type.GetType("Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime");
                _softwareBitmapType = Type.GetType("Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType=WindowsRuntime");
                _ocrResultType = Type.GetType("Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType=WindowsRuntime");

                Type streamExtType = typeof(WindowsRuntimeStreamExtensions);
                _asRandomAccessStream = streamExtType.GetMethod("AsRandomAccessStream", new[] { typeof(Stream) });

                foreach (var m in _decoderType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "CreateAsync" && m.GetParameters().Length == 1)
                    {
                        _createAsync = m;
                        break;
                    }
                }

                MethodInfo asTaskGeneric = null;
                foreach (var m in typeof(System.WindowsRuntimeSystemExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "AsTask" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                    {
                        asTaskGeneric = m;
                        break;
                    }
                }

                _asTaskDecoder = asTaskGeneric.MakeGenericMethod(_decoderType);
                _asTaskSbm = asTaskGeneric.MakeGenericMethod(_softwareBitmapType);
                _asTaskOcr = asTaskGeneric.MakeGenericMethod(_ocrResultType);

                _getSbmMethod = _decoderType.GetMethod("GetSoftwareBitmapAsync", Type.EmptyTypes);
                _recMethod = _ocrEngineType.GetMethod("RecognizeAsync", new[] { _softwareBitmapType });
                _tryCreateMethod = _ocrEngineType.GetMethod("TryCreateFromUserProfileLanguages", Type.EmptyTypes);

                _inited = true;
            }
        }

        public static List<OcrHit> Recognize(Bitmap bmp, int offsetX = 0, int offsetY = 0)
        {
            EnsureInitialized();
            var list = new List<OcrHit>();

            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Bmp);
                ms.Position = 0;

                object randomAccessStream = _asRandomAccessStream.Invoke(null, new object[] { ms });
                object createOp = _createAsync.Invoke(null, new object[] { randomAccessStream });
                var task1 = (Task)_asTaskDecoder.Invoke(null, new object[] { createOp });
                task1.Wait();
                object decoder = task1.GetType().GetProperty("Result").GetValue(task1, null);

                object sbmOp = _getSbmMethod.Invoke(decoder, null);
                var task2 = (Task)_asTaskSbm.Invoke(null, new object[] { sbmOp });
                task2.Wait();
                object sbm = task2.GetType().GetProperty("Result").GetValue(task2, null);

                object engine = _tryCreateMethod.Invoke(null, null);
                object recOp = _recMethod.Invoke(engine, new object[] { sbm });
                var task3 = (Task)_asTaskOcr.Invoke(null, new object[] { recOp });
                task3.Wait();
                dynamic ocrResult = task3.GetType().GetProperty("Result").GetValue(task3, null);

                foreach (var line in ocrResult.Lines)
                {
                    double minX = double.MaxValue, minY = double.MaxValue, maxX = 0, maxY = 0;
                    foreach (var w in line.Words)
                    {
                        dynamic r = w.BoundingRect;
                        if (r.X < minX) minX = r.X;
                        if (r.Y < minY) minY = r.Y;
                        if (r.X + r.Width > maxX) maxX = r.X + r.Width;
                        if (r.Y + r.Height > maxY) maxY = r.Y + r.Height;
                    }
                    if (maxX > 0)
                    {
                        list.Add(new OcrHit
                        {
                            Text = line.Text,
                            X = (int)minX + offsetX,
                            Y = (int)minY + offsetY,
                            W = (int)(maxX - minX),
                            H = (int)(maxY - minY),
                            Kind = "line"
                        });
                    }
                }
            }
            return list;
        }

        public static List<OcrHit> RecognizeParallel(Bitmap fullBmp, int slices = 2)
        {
            EnsureInitialized();
            int w = fullBmp.Width;
            int h = fullBmp.Height;
            int sliceH = h / slices;
            int overlap = 80;

            var tasks = new Task<List<OcrHit>>[slices];
            var bitmapsToDispose = new List<Bitmap>();

            for (int i = 0; i < slices; i++)
            {
                int y = Math.Max(0, i * sliceH - (i > 0 ? overlap : 0));
                int curH = (i == slices - 1) ? (h - y) : (sliceH + (i > 0 ? overlap : 0) + overlap);
                if (y + curH > h) curH = h - y;

                Bitmap sliceBmp = new Bitmap(w, curH);
                using (Graphics g = Graphics.FromImage(sliceBmp))
                {
                    g.DrawImage(fullBmp, new Rectangle(0, 0, w, curH), new Rectangle(0, y, w, curH), GraphicsUnit.Pixel);
                }
                bitmapsToDispose.Add(sliceBmp);

                int offsetY = y;
                tasks[i] = Task.Run(() => Recognize(sliceBmp, 0, offsetY));
            }

            Task.WaitAll(tasks);
            foreach (var b in bitmapsToDispose) b.Dispose();

            var merged = new List<OcrHit>();
            foreach (var t in tasks)
            {
                merged.AddRange(t.Result);
            }
            return merged;
        }

        static string NormalizeChinese(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(" ", "")
                    .Replace("亻乍", "作")
                    .Replace("纟吉", "结")
                    .Replace("幺士", "结")
                    .Replace("关踺", "关键")
                    .Replace("傾空", "填空")
                    .Replace("诜顿", "选题")
                    .Replace("些亚", "毕业")
                    .Replace("老立", "文献")
                    .ToLowerInvariant();
        }

        static bool IsSubsequenceMatch(string pattern, string text)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(text)) return false;
            int pi = 0;
            for (int ti = 0; ti < text.Length; ti++)
            {
                if (char.ToLowerInvariant(text[ti]) == char.ToLowerInvariant(pattern[pi]))
                {
                    pi++;
                    if (pi == pattern.Length) return true;
                }
            }
            return false;
        }

        public static List<OcrHit> FindHits(List<OcrHit> hits, string pattern)
        {
            var results = new List<OcrHit>();
            string normPattern = NormalizeChinese(pattern);

            // 1. Exact substring match after radical & space normalization
            foreach (var h in hits)
            {
                string normText = NormalizeChinese(h.Text);
                if (normText.Contains(normPattern))
                {
                    results.Add(h);
                }
            }

            // 2. Subsequence match (characters in order, robust against broken radicals)
            if (results.Count == 0 && normPattern.Length >= 2)
            {
                foreach (var h in hits)
                {
                    string normText = NormalizeChinese(h.Text);
                    if (IsSubsequenceMatch(normPattern, normText))
                    {
                        results.Add(h);
                    }
                }
            }

            // 3. Fallback to raw contains
            if (results.Count == 0)
            {
                string rawPattern = pattern.ToLowerInvariant();
                foreach (var h in hits)
                {
                    if (h.Text.ToLowerInvariant().Contains(rawPattern))
                    {
                        results.Add(h);
                    }
                }
            }
            return results;
        }

        public static bool ExecuteOcr(string[] a, bool roiOnly)
        {
            if (a.Length == 0)
            {
                Console.WriteLine("Usage: rm.exe ocr/ocrw <text> [--click] [--toright [d]] [--toleft [d]] [--below [d]] [--type <t>] [--enter] [--tab] [--window <w>] [--index <n>] [--parallel]");
                return false;
            }

            string text = a[0];
            bool click = false, dclick = false, rclick = false, tclick = false;
            bool toRight = false, toLeft = false, below = false;
            int offset = 0, offsetX = 0, offsetY = 0;
            string typeText = null;
            bool pressEnter = false, pressTab = false;
            string windowSub = null;
            int index = 1;
            bool parallel = false;

            for (int i = 1; i < a.Length; i++)
            {
                string arg = a[i];
                if (arg.Equals("--click", StringComparison.OrdinalIgnoreCase)) click = true;
                else if (arg.Equals("--dclick", StringComparison.OrdinalIgnoreCase)) { click = true; dclick = true; }
                else if (arg.Equals("--rclick", StringComparison.OrdinalIgnoreCase)) { click = true; rclick = true; }
                else if (arg.Equals("--tclick", StringComparison.OrdinalIgnoreCase)) { click = true; tclick = true; }
                else if (arg.Equals("--parallel", StringComparison.OrdinalIgnoreCase)) parallel = true;
                else if (arg.Equals("--toright", StringComparison.OrdinalIgnoreCase))
                {
                    toRight = true;
                    if (i + 1 < a.Length && int.TryParse(a[i + 1], out offset)) i++;
                }
                else if (arg.Equals("--toleft", StringComparison.OrdinalIgnoreCase))
                {
                    toLeft = true;
                    if (i + 1 < a.Length && int.TryParse(a[i + 1], out offset)) i++;
                }
                else if (arg.Equals("--below", StringComparison.OrdinalIgnoreCase))
                {
                    below = true;
                    if (i + 1 < a.Length && int.TryParse(a[i + 1], out offset)) i++;
                }
                else if (arg.Equals("--offsetx", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                {
                    offsetX = int.Parse(a[++i]);
                }
                else if (arg.Equals("--offsety", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                {
                    offsetY = int.Parse(a[++i]);
                }
                else if (arg.Equals("--type", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                {
                    click = true;
                    typeText = a[++i];
                }
                else if (arg.Equals("--enter", StringComparison.OrdinalIgnoreCase)) pressEnter = true;
                else if (arg.Equals("--tab", StringComparison.OrdinalIgnoreCase)) pressTab = true;
                else if (arg.Equals("--window", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                {
                    windowSub = a[++i];
                }
                else if (arg.Equals("--index", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                {
                    index = int.Parse(a[++i]);
                }
            }

            if (!string.IsNullOrEmpty(windowSub))
            {
                IntPtr hw = Native.FindByTitle(windowSub);
                if (hw != IntPtr.Zero)
                {
                    Native.ForceForeground(hw);
                    Thread.Sleep(80);
                }
            }

            var sw = Stopwatch.StartNew();
            List<OcrHit> allHits;

            if (roiOnly)
            {
                IntPtr fg = Native.GetForegroundWindow();
                RECT r;
                Native.GetWindowRect(fg, out r);
                int rx = Math.Max(0, r.Left);
                int ry = Math.Max(0, r.Top);
                int rw = Math.Min(Native.PhysW() - rx, Math.Max(50, r.Right - rx));
                int rh = Math.Min(Native.PhysH() - ry, Math.Max(50, r.Bottom - ry));
                using (var bmp = new Bitmap(rw, rh))
                {
                    using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(rx, ry, 0, 0, new Size(rw, rh));
                    allHits = Recognize(bmp, rx, ry);
                }
            }
            else
            {
                int W = Native.PhysW(), H = Native.PhysH();
                using (var bmp = new Bitmap(W, H))
                {
                    using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, new Size(W, H));
                    if (parallel) allHits = RecognizeParallel(bmp, 2);
                    else allHits = Recognize(bmp, 0, 0);
                }
            }

            var matched = FindHits(allHits, text);
            if (matched.Count == 0)
            {
                Console.WriteLine("NOT FOUND: \"" + text + "\" (scanned " + allHits.Count + " elements in " + sw.ElapsedMilliseconds + "ms)");
                Console.WriteLine("--- Visible Lines (" + Math.Min(35, allHits.Count) + "/" + allHits.Count + ") ---");
                for (int i = 0; i < Math.Min(35, allHits.Count); i++)
                {
                    Console.WriteLine("  [" + (i + 1) + "] " + allHits[i].Text + "  @(" + allHits[i].CX + "," + allHits[i].CY + ")");
                }
                return false;
            }

            if (index < 1 || index > matched.Count)
            {
                Console.WriteLine("ERR: Index " + index + " out of bounds (found " + matched.Count + " matches)");
                return false;
            }

            var target = matched[index - 1];
            int tx = target.CX;
            int ty = target.CY;
            string offsetDesc = "center";

            if (toRight)
            {
                int d = offset > 0 ? offset : 55;
                tx = target.X + target.W + d + offsetX;
                ty = target.CY + offsetY;
                offsetDesc = "toright(+" + d + "px)";
            }
            else if (toLeft)
            {
                int d = offset > 0 ? offset : 30;
                tx = target.X - d + offsetX;
                ty = target.CY + offsetY;
                offsetDesc = "toleft(-" + d + "px)";
            }
            else if (below)
            {
                int d = offset > 0 ? offset : 35;
                tx = target.CX + offsetX;
                ty = target.Y + target.H + d + offsetY;
                offsetDesc = "below(+" + d + "px)";
            }
            else
            {
                tx += offsetX;
                ty += offsetY;
            }

            Console.WriteLine("# Match [" + index + "/" + matched.Count + "] \"" + target.Text + "\" " + offsetDesc + " -> (" + tx + "," + ty + ") in " + sw.ElapsedMilliseconds + "ms");

            if (click || typeText != null)
            {
                if (dclick) Native.DoubleClick(tx, ty, 60);
                else if (rclick) Native.RightClick(tx, ty, 60);
                else if (tclick)
                {
                    Native.Click(tx, ty, 50);
                    Thread.Sleep(30);
                    Native.Click(tx, ty, 50);
                    Thread.Sleep(30);
                    Native.Click(tx, ty, 50);
                }
                else Native.Click(tx, ty, 70);

                if (typeText != null)
                {
                    Thread.Sleep(60);
                    Native.TypeText(typeText);
                }

                if (pressTab)
                {
                    Thread.Sleep(40);
                    Native.SendKeyCombo("tab");
                }
                if (pressEnter)
                {
                    Thread.Sleep(40);
                    Native.SendKeyCombo("enter");
                }
            }

            return true;
        }

        public static bool WaitOcr(string pattern, int timeoutMs = 5000, bool roiOnly = true)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                List<OcrHit> hits;
                if (roiOnly)
                {
                    IntPtr fg = Native.GetForegroundWindow();
                    RECT r;
                    Native.GetWindowRect(fg, out r);
                    int rw = Math.Max(50, r.Right - r.Left);
                    int rh = Math.Max(50, r.Bottom - r.Top);
                    using (var bmp = new Bitmap(rw, rh))
                    {
                        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(rw, rh));
                        hits = Recognize(bmp, r.Left, r.Top);
                    }
                }
                else
                {
                    int W = Native.PhysW(), H = Native.PhysH();
                    using (var bmp = new Bitmap(W, H))
                    {
                        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, new Size(W, H));
                        hits = Recognize(bmp, 0, 0);
                    }
                }

                var matched = FindHits(hits, pattern);
                if (matched.Count > 0)
                {
                    Console.WriteLine("waitocr: \"" + pattern + "\" appeared after " + sw.ElapsedMilliseconds + "ms");
                    return true;
                }
                Thread.Sleep(100);
            }
            Console.WriteLine("waitocr: timeout (" + timeoutMs + "ms) waiting for \"" + pattern + "\"");
            return false;
        }
    }
}
