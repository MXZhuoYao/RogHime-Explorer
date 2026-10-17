using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RogHime
{
    internal static class Program
    {
        public static string BaseDir;
        public static string ConfigPath;
        public static ThemeConfig Cfg;
        public static Image BgArt;

        [STAThread]
        static void Main(string[] args)
        {
            // 防线：无参数启动（托盘模式）时补一个空参数，
            // 这样任何 args[0] 访问都不会越界 —— 这个坑踩过两次，此处一次根治
            if (args == null || args.Length == 0) args = new string[] { "" };
            BaseDir = AppDomain.CurrentDomain.BaseDirectory;
            ConfigPath = Path.Combine(BaseDir, "RogHime.ini");
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            if (args.Length > 0)
            {
                try
                {
                    StreamWriter w = new StreamWriter(Path.Combine(BaseDir, "report.txt"), false, new UTF8Encoding(false));
                    w.AutoFlush = true;
                    Console.SetOut(w);
                }
                catch { }
            }
            Cfg = ThemeConfig.Load(ConfigPath);
            Brand.Load(BaseDir);

            if (!File.Exists(ConfigPath)) { try { Cfg.Save(ConfigPath); } catch { } }
            if (args.Length >= 1 && string.Equals(args[0], "--write-default-config", StringComparison.OrdinalIgnoreCase))
            {
                try { Cfg.Save(ConfigPath); Console.WriteLine("config: " + ConfigPath); } catch { }
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase)) { Shot(args[1]); return; }
            if (args.Length >= 2 && string.Equals(args[0], "--glitchtest", StringComparison.OrdinalIgnoreCase)) { GlitchTest(args); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--list-windows", StringComparison.OrdinalIgnoreCase))
            {
                List<string> us = BgApply.CollectExplorerUrls();
                Console.WriteLine("list-windows: " + us.Count);
                foreach (string u in us) Console.WriteLine("  " + u);
                try { File.WriteAllText(Path.Combine(BaseDir, "windows.txt"), string.Join("\r\n", us.ToArray()), Encoding.UTF8); } catch { }
                return;
            }
            if (args.Length >= 1 && string.Equals(args[0], "--gui-check", StringComparison.OrdinalIgnoreCase))
            {
                Watcher.GuiCheck();
                return;
            }
            if (args.Length >= 1 && string.Equals(args[0], "--bg-reapply", StringComparison.OrdinalIgnoreCase))
            {
                // 按当前配置重建资源管理器背景图（安装/修复时用）
                string err;
                bool ok = BgApply.Apply(Cfg.BgIndex, Cfg.BgPosType, Cfg.BgAlpha, Cfg.BgMask, Cfg.BgCustomPath, Cfg.BgHomeIndex, null, out err);
                Console.WriteLine(ok ? ("background reapplied (index=" + Cfg.BgIndex + " home=" + Cfg.BgHomeIndex + ")") : ("failed: " + err));
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "--makeicon", StringComparison.OrdinalIgnoreCase)) { MakeIcon(args[1]); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--settings", StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int tabIdx = 0;
                if (args.Length >= 2) int.TryParse(args[1], out tabIdx);
                Application.Run(new SettingsForm(Cfg, delegate { Program.Cfg = ThemeConfig.Load(ConfigPath); Brand.Load(BaseDir); }, tabIdx));
                return;
            }
            if (args.Length >= 2 && string.Equals(args[0], "--shotwin", StringComparison.OrdinalIgnoreCase)) { ShotWindow(args[1]); return; }
            if (args.Length >= 2 && string.Equals(args[0], "--bgtest", StringComparison.OrdinalIgnoreCase)) { BgTest(args[1]); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--fit-windows", StringComparison.OrdinalIgnoreCase)) { FitWindows(); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase)) { SelfTest(); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase)) { Probe(); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--render-sample", StringComparison.OrdinalIgnoreCase)) { RenderSample(args); return; }
            if (args.Length >= 1 && string.Equals(args[0], "--render", StringComparison.OrdinalIgnoreCase)) { RenderOnly(args); return; }

            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Global\\RogHimeThemeEngine", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // 启动时若开启「跟随系统」，先按当前系统浅深色对齐主题
            try
            {
                if (Cfg.ThemeAuto == 1)
                {
                    string want = Themes.ForSystem();
                    if (!string.Equals(want, Cfg.Theme, StringComparison.OrdinalIgnoreCase))
                    {
                        Themes.Apply(Cfg, want);
                        if (want == Themes.Fubuki && Cfg.BgIndex == 2) Cfg.BgIndex = 4;
                        else if (want == Themes.RogHime && Cfg.BgIndex == 4) Cfg.BgIndex = 2;
                        Cfg.Save(ConfigPath);
                    }
                }
            }
            catch { }

            // 启动自愈：按配置把背景图重建一遍，避免上次「暂停/退出」留下的透明状态
            try
            {
                // 主文件夹的「压暗副本」是运行时生成的文件，可能丢失 —— 丢了会导致 XAML 失效、背景全无
                if (Cfg.BgHomeMask > 0)
                {
                    int hi = Cfg.BgHomeIndex > 0 ? Cfg.BgHomeIndex : Cfg.BgIndex;
                    string src = BgApply.ImageFor(hi, Cfg.BgCustomPath);
                    if (src != null) XamlBg.MaskedCopy(src, Cfg.BgHomeMask);
                }
                if (Cfg.BgIndex > 0 || Cfg.BgHomeIndex > 0)
                {
                    string bgErr;
                    BgApply.Apply(Cfg.BgIndex, Cfg.BgPosType,
                                  Cfg.BgAlpha > 0 ? Cfg.BgAlpha : 255,
                                  Cfg.BgMask, Cfg.BgCustomPath, Cfg.BgHomeIndex, null, out bgErr);
                }
            }
            catch { }

            try
            {
                Application.Run(new TrayContext());
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(BaseDir, "crash.txt"),
                        DateTime.Now.ToString("HH:mm:ss") + "\r\n" + ex.ToString());
                }
                catch { }
                throw;
            }
            }
        }

        public static void Log(string s)
        {
            try { File.AppendAllText(Path.Combine(BaseDir, "RogHime.log"), DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s + "\r\n", new UTF8Encoding(false)); }
            catch { }
        }


        // ---------------------------------------------------------------- probe
        static void Probe()
        {
            Console.WriteLine("RogHime probe (PerMonitorV2 aware)");
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);
            Console.WriteLine("explorer windows: " + wins.Count);
            foreach (IntPtr h in wins)
            {
                Native.RECT r; Native.GetWindowRect(h, out r);
                int dpi = (int)Native.GetDpiForWindow(h);
                Native.RECT cr; Extra.GetClientRect(h, out cr);
                Native.POINT co = new Native.POINT(); Extra.ClientToScreen(h, ref co);
                Console.WriteLine("--- hwnd=0x" + h.ToString("X") + " '" + Native.TextOf(h) + "' dpi=" + dpi);
                Console.WriteLine("    window  = " + r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + "  (" + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + ")");
                Console.WriteLine("    client  = origin " + co.X + "," + co.Y + " size " + cr.Right + "x" + cr.Bottom);
                Console.WriteLine("    inset   = L" + (co.X - r.Left) + " T" + (co.Y - r.Top) + " R" + (r.Right - (co.X + cr.Right)) + " B" + (r.Bottom - (co.Y + cr.Bottom)));
                Console.WriteLine("    zoomed  = " + Native.IsZoomed(h));
                Console.WriteLine("    SM_CYCAPTION=" + Native.GetSystemMetricsForDpi(Native.SM_CYCAPTION, dpi)
                    + " SM_CXSIZE=" + Native.GetSystemMetricsForDpi(Native.SM_CXSIZE, dpi)
                    + " SM_CYSIZE=" + Native.GetSystemMetricsForDpi(Native.SM_CYSIZE, dpi)
                    + " SM_CXSIZEFRAME=" + Native.GetSystemMetricsForDpi(Native.SM_CXSIZEFRAME, dpi)
                    + " SM_CXPADDEDBORDER=" + Native.GetSystemMetricsForDpi(Native.SM_CXPADDEDBORDER, dpi));
                Native.RECT cb;
                int hr = Native.DwmGetWindowAttribute(h, Native.DWMWA_CAPTION_BUTTON_BOUNDS, out cb, Marshal.SizeOf(typeof(Native.RECT)));
                Console.WriteLine("    CAPTION_BUTTON_BOUNDS hr=" + hr + "  " + cb.Left + "," + cb.Top + "," + cb.Right + "," + cb.Bottom
                    + "   (w=" + (cb.Right - cb.Left) + " h=" + (cb.Bottom - cb.Top) + ")");
                int thick;
                Native.DwmGetWindowAttribute(h, Native.DWMWA_VISIBLE_FRAME_BORDER_THICKNESS, out thick, 4);
                Console.WriteLine("    VISIBLE_FRAME_BORDER_THICKNESS = " + thick);
            }
        }

        public static FrameLayout MakeLayoutForWindow(IntPtr hwnd, Native.RECT r, int dpi, int hover)
        {
            float s = dpi / 96f;
            int margin = Math.Max(2, (int)Math.Round(Cfg.BorderThickness * s));
            int topExtra = Math.Max(0, (int)Math.Round(Cfg.TopBandExtra * s));
            int topMargin = margin + topExtra;
            int winW = r.Right - r.Left, winH = r.Bottom - r.Top;
            FrameLayout L = new FrameLayout();
            L.Dpi = dpi; L.Margin = margin; L.TopMargin = topMargin;
            L.WinX = margin; L.WinY = topMargin; L.WinW = winW; L.WinH = winH;
            L.Width = winW + margin * 2;
            L.Height = winH + topMargin + margin;
            Native.RECT cr; Native.POINT co = new Native.POINT();
            Extra.GetClientRect(hwnd, out cr); Extra.ClientToScreen(hwnd, ref co);
            int insetT = co.Y - r.Top; if (insetT < 0) insetT = 0;
            int insetR = r.Right - (co.X + cr.Right); if (insetR < 0) insetR = 0;
            L.FrameInset = insetR;
            L.CaptionH = Native.GetSystemMetricsForDpi(Native.SM_CYCAPTION, dpi);
            L.CornerRadius = Math.Max(4, (int)Math.Round(Cfg.CornerRadius * s));
            L.CornerCut = Math.Max(0, (int)Math.Round(Cfg.CornerCut * s));
            Rectangle cb = Rectangle.Empty;
            Native.RECT cbr;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CAPTION_BUTTON_BOUNDS, out cbr, Marshal.SizeOf(typeof(Native.RECT))) == 0)
            {
                int cw = cbr.Right - cbr.Left, chh = cbr.Bottom - cbr.Top;
                if (cw > 24 && chh > 8 && cbr.Right > winW / 2 && cbr.Left >= 0 && cbr.Bottom <= winH + 8)
                    cb = new Rectangle(cbr.Left, cbr.Top, cw, chh);
            }
            if (cb.IsEmpty)
            {
                int total = (int)Math.Round(146 * s);
                int rInset = (int)Math.Round(7 * s);
                int bot = (int)Math.Round(29 * s);
                cb = new Rectangle(winW - insetR - total, 0, total, bot);
            }
            FillButtons(L, cb, dpi);
            ApplyButtonOverrides(L, s);
            L.HoverButton = hover;
            return L;
        }

        public static void FillButtons(FrameLayout L, Rectangle cb, int dpi)
        {
            int pad = 0;
            Rectangle inner = cb;
            inner.Inflate(-pad, 0);
            if (inner.Width < 3) inner = cb;
            L.BtnY = L.WinY + inner.Top;
            L.BtnH = inner.Height;
            L.BtnW = (int)Math.Round(inner.Width / 3.0);
            L.MinX = L.WinX + inner.Left;
            L.MaxX = L.MinX + L.BtnW;
            L.CloseX = L.MaxX + L.BtnW;
            L.CaptionH = inner.Bottom;
        }

        static void ApplyButtonOverrides(FrameLayout L, float s)
        {
            // 自绘窗口按钮功能已整体移除（用户反馈不好看、不实用），这里保留空实现以兼容旧调用点
        }

        /// 实验：把一块纯色窗口放到资源管理器窗口「正下方」，并把窗口背景材质设为 None，
        /// 用来判断文件列表区域是否透明（能否把图片真正垫在文件下面）。
        static void BgTest(string outPath)
        {
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);
            if (wins.Count == 0) { Console.WriteLine("no explorer window"); return; }
            IntPtr hwnd = wins[0];
            Native.SetForegroundWindow(hwnd);
            System.Threading.Thread.Sleep(500);
            Native.RECT r; Native.GetWindowRect(hwnd, out r);
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            Console.WriteLine("target=0x" + hwnd.ToString("X") + " " + r.Left + "," + r.Top + " " + w + "x" + hh + " fg=" + (Native.GetForegroundWindow() == hwnd));
            OverlayWindow ov = null;
            int savedBackdrop = -999;
            try
            {
                ov = new OverlayWindow();
                Bitmap bmp = new Bitmap(w, hh, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.FromArgb(255, 0, 255, 0)); }
                ov.SetContent(bmp, r.Left, r.Top);
                bmp.Dispose();
                ov.Show();
                // 放到目标窗口正下方
                Native.SetWindowPos(ov.Handle, hwnd, r.Left, r.Top, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                Console.WriteLine("green window placed below target");

                int cur;
                Native.DwmGetWindowAttribute(hwnd, 38, out cur, 4);
                savedBackdrop = cur;
                Console.WriteLine("current backdrop type = " + cur);
                int none = 1;   // DWMSBT_NONE
                int hr = Native.DwmSetWindowAttribute(hwnd, 38, ref none, 4);
                Console.WriteLine("set backdrop NONE hr=" + hr);
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(1200);
                Shot2(outPath);
                int auto = 0;
                Native.DwmSetWindowAttribute(hwnd, 38, ref auto, 4);
                Console.WriteLine("restored backdrop to " + savedBackdrop);
            }
            catch (Exception ex) { Console.WriteLine("EXCEPTION: " + ex.Message); }
            finally { if (ov != null) ov.Dispose(); }
        }

        static void Shot2(string path)
        {
            int x = Native.GetSystemMetrics(76), y = Native.GetSystemMetrics(77);
            int cx = Native.GetSystemMetrics(78), cy = Native.GetSystemMetrics(79);
            Bitmap bmp = new Bitmap(cx, cy, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(x, y, 0, 0, new Size(cx, cy), CopyPixelOperation.SourceCopy);
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
            Console.WriteLine("saved " + path);
        }

        /// <summary>离线生成 glitch 对比图：上=干净 下=故障（方便肉眼验收）</summary>
        static void GlitchTest(string[] args)
        {
            string outPath = args[1];
            int area = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 0;
            int strength = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 85;
            int chroma = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 6;
            int barw = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 3;
            int w = 1100, h = 700, dpi = 144;

            FrameLayout L = MakeLayout(w, h, dpi, 0);
            Bitmap clean = RenderOverlay(L, 0);
            ThemeConfig keep = Cfg;
            Cfg.GlitchArea = area; Cfg.GlitchStrength = strength; Cfg.GlitchChroma = chroma; Cfg.GlitchBarW = barw;
            Bitmap glitched = new Bitmap(clean.Width, clean.Height, PixelFormat.Format32bppArgb);
            Art.Glitch(clean, glitched, strength, Cfg, new Random(20261005));
            Cfg = keep;

            Bitmap outBmp = new Bitmap(clean.Width, clean.Height * 2 + 8, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.Clear(Color.FromArgb(255, 60, 60, 66));
                g.DrawImageUnscaled(clean, 0, 0);
                g.DrawImageUnscaled(glitched, 0, clean.Height + 8);
            }
            outBmp.Save(outPath, ImageFormat.Png);
            clean.Dispose(); glitched.Dispose(); outBmp.Dispose();
            Console.WriteLine("glitchtest area=" + area + " str=" + strength + " -> " + outPath);
        }

        /// <summary>用 ROG 之眼矢量生成多尺寸 .ico（程序图标 / 托盘图标）</summary>
        static void MakeIcon(string outPath)
        {
            int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            List<byte[]> frames = new List<byte[]>();
            foreach (int sz in sizes)
            {
                byte[] data;
                using (Bitmap bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        float rad = sz * 0.24f;
                        using (GraphicsPath bgp = RoundRect(new RectangleF(0.5f, 0.5f, sz - 1f, sz - 1f), rad))
                        using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, sz, sz),
                                Color.FromArgb(255, 34, 34, 42), Color.FromArgb(255, 7, 7, 10), 115f))
                        {
                            g.FillPath(b, bgp);
                        }
                        if (Brand.Logo != null)
                        {
                            RectangleF lb = Brand.Logo.GetBounds();
                            float sc = (sz * 0.72f) / Math.Max(lb.Width, lb.Height);
                            using (Matrix m = new Matrix())
                            {
                                m.Translate(-lb.X, -lb.Y, MatrixOrder.Append);
                                m.Scale(sc, sc, MatrixOrder.Append);
                                m.Translate((sz - lb.Width * sc) / 2f, (sz - lb.Height * sc) / 2f, MatrixOrder.Append);
                                using (GraphicsPath p2 = (GraphicsPath)Brand.Logo.Clone())
                                {
                                    p2.Transform(m);
                                    using (SolidBrush rb = new SolidBrush(Color.FromArgb(255, 255, 0, 41))) g.FillPath(rb, p2);
                                    if (sz >= 48)
                                        using (Pen gp = new Pen(Color.FromArgb(110, 255, 70, 100), Math.Max(1f, sz * 0.018f))) g.DrawPath(gp, p2);
                                }
                            }
                        }
                    }
                    MemoryStream ms = new MemoryStream();
                    if (sz >= 64) bmp.Save(ms, ImageFormat.Png); else SaveBmpFrame(bmp, ms);
                    data = ms.ToArray();
                    ms.Dispose();
                }
                frames.Add(data);
            }
            using (FileStream fs = new FileStream(outPath, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)frames.Count);
                int offset = 6 + 16 * frames.Count;
                for (int i = 0; i < frames.Count; i++)
                {
                    int sz = sizes[i];
                    w.Write((byte)(sz >= 256 ? 0 : sz));
                    w.Write((byte)(sz >= 256 ? 0 : sz));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((ushort)1); w.Write((ushort)32);
                    w.Write(frames[i].Length); w.Write(offset);
                    offset += frames[i].Length;
                }
                for (int i = 0; i < frames.Count; i++) w.Write(frames[i]);
            }
            Console.WriteLine("icon: " + outPath + "  frames=" + frames.Count);
        }

        static void SaveBmpFrame(Bitmap bmp, Stream s)
        {
            int w = bmp.Width, h = bmp.Height;
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write(40); bw.Write(w); bw.Write(h * 2);
            bw.Write((ushort)1); bw.Write((ushort)32);
            bw.Write(0); bw.Write(w * h * 4);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            int stride = ((w + 31) / 32) * 4;
            byte[] row = new byte[stride];
            for (int y = h - 1; y >= 0; y--)
            {
                for (int i = 0; i < stride; i++) row[i] = 0;
                for (int x = 0; x < w; x++) { if (bmp.GetPixel(x, y).A < 128) row[x / 8] |= (byte)(0x80 >> (x % 8)); }
                bw.Write(row);
            }
            bw.Flush();
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(0.1f, rad * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// 抓取资源管理器窗口自身内容（即使被其他窗口遮挡），用于自检背景壁纸是否生效
        static void ShotWindow(string path)
        {
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h) && !Native.IsIconic(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);
            if (wins.Count == 0) { Console.WriteLine("no explorer window"); return; }
            IntPtr hwnd = wins[0];
            Native.RECT r; Native.GetWindowRect(hwnd, out r);
            int wid = r.Right - r.Left, hei = r.Bottom - r.Top;
            Console.WriteLine("window 0x" + hwnd.ToString("X") + " " + wid + "x" + hei);
            Bitmap bmp = new Bitmap(wid, hei, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                bool ok = Native.PrintWindow(hwnd, hdc, 2);
                g.ReleaseHdc(hdc);
                Console.WriteLine("PrintWindow = " + ok);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
            Console.WriteLine("saved " + path);
        }

        /// 以 PerMonitorV2 DPI 感知方式抓取整个物理桌面（用于效果自检）
        static void Shot(string path)
        {
            int x = Native.GetSystemMetrics(76);   // SM_XVIRTUALSCREEN
            int y = Native.GetSystemMetrics(77);
            int cx = Native.GetSystemMetrics(78);
            int cy = Native.GetSystemMetrics(79);
            Console.WriteLine("virtual screen " + x + "," + y + " " + cx + "x" + cy);
            try
            {
                Bitmap bmp = new Bitmap(cx, cy, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x, y, 0, 0, new Size(cx, cy), CopyPixelOperation.SourceCopy);
                }
                bmp.Save(path, ImageFormat.Png);
                bmp.Dispose();
                Console.WriteLine("saved " + path);
            }
            catch (Exception ex) { Console.WriteLine("EXCEPTION: " + ex.Message); }
        }

        /// 把跑到屏幕外的资源管理器窗口拉回显示器工作区（含任务栏区域修正）
        static void FitWindows()
        {
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h) && !Native.IsIconic(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);
            Console.WriteLine("explorer windows: " + wins.Count);
            foreach (IntPtr h in wins)
            {
                if (Native.IsZoomed(h)) { Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE); }
                Native.RECT r; Native.GetWindowRect(h, out r);
                IntPtr mon = Native.MonitorFromWindow(h, 2);
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!Native.GetMonitorInfo(mon, ref mi)) continue;
                Native.RECT w = mi.rcWork;
                int width = r.Right - r.Left, height = r.Bottom - r.Top;
                int x = r.Left, y = r.Top;
                if (width > w.Right - w.Left) width = w.Right - w.Left;
                if (height > w.Bottom - w.Top) height = w.Bottom - w.Top;
                if (x + width > w.Right) x = w.Right - width;
                if (y + height > w.Bottom) y = w.Bottom - height;
                if (x < w.Left) x = w.Left;
                if (y < w.Top) y = w.Top;
                if (x != r.Left || y != r.Top || width != (r.Right - r.Left) || height != (r.Bottom - r.Top))
                {
                    Console.WriteLine("move  hwnd=0x" + h.ToString("X") + "  " + r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top)
                        + "  ->  " + x + "," + y + " " + width + "x" + height);
                    Native.SetWindowPos(h, IntPtr.Zero, x, y, width, height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                }
                else Console.WriteLine("ok    hwnd=0x" + h.ToString("X") + "  " + r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + "  already inside");
            }
        }

        static void SelfTest()
        {
            Console.WriteLine("selftest start, art=" + (BgArt != null ? "yes" : "no"));
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);
            Console.WriteLine("windows=" + wins.Count);
            if (wins.Count == 0) return;
            IntPtr hwnd = wins[0];
            Native.RECT r; Native.GetWindowRect(hwnd, out r);
            int dpi = (int)Native.GetDpiForWindow(hwnd);
            Console.WriteLine("hwnd=0x" + hwnd.ToString("X") + " rect=" + r.Left + "," + r.Top + "," + r.Right + "," + r.Bottom + " dpi=" + dpi);
            try
            {
                OverlayWindow ov = new OverlayWindow();
                Console.WriteLine("overlay hwnd=0x" + ov.Handle.ToString("X"));
                FrameLayout L = MakeLayoutForWindow(hwnd, r, dpi, 0);
                Console.WriteLine("layout W=" + L.Width + " H=" + L.Height + " win=" + L.WinX + "," + L.WinY + "," + L.WinW + "," + L.WinH
                    + " margin=" + L.Margin + " top=" + L.TopMargin + " cap=" + L.CaptionH
                    + " minX=" + L.MinX + " maxX=" + L.MaxX + " closeX=" + L.CloseX + " btnY=" + L.BtnY + " btn=" + L.BtnW + "x" + L.BtnH);
                Bitmap bmp = RenderOverlay(L, 0);
                Console.WriteLine("rendered " + bmp.Width + "x" + bmp.Height);
                ov.SetContent(bmp, r.Left - L.Margin, r.Top - L.TopMargin);
                ov.Show();
                ov.StackAbove(hwnd);
                Console.WriteLine("shown at " + (r.Left - L.Margin) + "," + (r.Top - L.TopMargin));
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(4000);
                ov.Dispose();
                Console.WriteLine("selftest done OK");
            }
            catch (Exception ex) { Console.WriteLine("EXCEPTION: " + ex.ToString()); }
        }

        // --------------------------------------------------------- render tests
        public static FrameLayout MakeLayout(int winW, int winH, int dpi, int hover)
        {
            float s = dpi / 96f;
            int margin = Math.Max(2, (int)Math.Round(Cfg.BorderThickness * s));
            int topExtra = Math.Max(0, (int)Math.Round(Cfg.TopBandExtra * s));
            int topMargin = margin + topExtra;
            FrameLayout L = new FrameLayout();
            L.Dpi = dpi;
            L.Margin = margin;
            L.TopMargin = topMargin;
            L.WinX = margin; L.WinY = topMargin; L.WinW = winW; L.WinH = winH;
            L.Width = winW + margin * 2;
            L.Height = winH + topMargin + margin;
            L.CaptionH = Native.GetSystemMetricsForDpi(Native.SM_CYCAPTION, dpi);
            L.CornerRadius = Math.Max(4, (int)Math.Round(Cfg.CornerRadius * s));
            L.CornerCut = Math.Max(0, (int)Math.Round(Cfg.CornerCut * s));
            L.FrameInset = Native.GetSystemMetricsForDpi(Native.SM_CXSIZEFRAME, dpi) + 2;
            int total = (int)Math.Round(146 * s);
            int rInset = (int)Math.Round(7 * s);
            int bot = (int)Math.Round(29 * s);
            Rectangle cb = new Rectangle(winW - rInset - total, 0, total, bot);
            FillButtons(L, cb, dpi);
            ApplyButtonOverrides(L, s);
            L.HoverButton = hover;
            return L;
        }

        static void DrawFakeExplorer(Graphics g, int w, int h, int dpi)
        {
            float s = dpi / 96f;
            int cap = Native.GetSystemMetricsForDpi(Native.SM_CYCAPTION, dpi);
            g.Clear(Color.FromArgb(32, 32, 32));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(40, 40, 40))) g.FillRectangle(b, 0, 0, w, cap + (int)(22 * s));
            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 28, 28))) g.FillRectangle(b, 0, cap, w, h - cap);
            using (Font f = new Font("Segoe UI", 9f * s, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 235, 235)))
            {
                g.DrawString("工作区", f, b, 62 * s, cap + 4 * s);
                g.DrawString("此电脑  >  桌面  >  工作区", f, b, 150 * s, cap + 40 * s);
            }
            // native caption buttons (white glyphs on black) for alignment check
            using (Pen p = new Pen(Color.FromArgb(240, 240, 240), Math.Max(1f, 1.1f * s)))
            {
                int frameInset = Native.GetSystemMetricsForDpi(Native.SM_CXSIZEFRAME, dpi) + 2;
                int btnW = Native.GetSystemMetricsForDpi(Native.SM_CXSIZE, dpi);
                int btnH = Native.GetSystemMetricsForDpi(Native.SM_CYSIZE, dpi);
                int cellW = btnW;
                int y = (cap - btnH) / 2 + btnH / 2;
                int xClose = w - frameInset - cellW / 2;
                int xMax = xClose - cellW;
                int xMin = xMax - cellW;
                g.DrawLine(p, xMin - 5 * s, y, xMin + 5 * s, y);
                g.DrawRectangle(p, xMax - 5 * s, y - 5 * s, 10 * s, 10 * s);
                g.DrawLine(p, xClose - 5 * s, y - 5 * s, xClose + 5 * s, y + 5 * s);
                g.DrawLine(p, xClose + 5 * s, y - 5 * s, xClose - 5 * s, y + 5 * s);
            }
            // nav pane + file rows
            using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 34, 34))) g.FillRectangle(b, 0, cap + (int)(60 * s), (int)(170 * s), h - cap - (int)(60 * s));
            using (Font f = new Font("Segoe UI", 8.5f * s, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 210, 210)))
            {
                string[] rows = new string[] { "_probe", "BootAnimation", "AMD-AM5-CPU-主板-行情调研.md", "RO姬_注入提示词.md", "RO姬_调研报告.md", "ROG主题_RTX5090_装机配置单.md", "RTX5090_2026年10月行情调研.md" };
                float y = cap + 70 * s;
                for (int i = 0; i < rows.Length; i++)
                {
                    g.DrawString(rows[i], f, b, 190 * s, y);
                    y += 30 * s;
                }
            }
        }

        static void Composite(Bitmap overlayBmp, Bitmap winBmp, int winX, int winY, string outPath)
        {
            Bitmap final = new Bitmap(overlayBmp.Width, overlayBmp.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(final))
            {
                // 棋盘格桌面背景，便于观察透明区域
                int cell = 16;
                for (int y = 0; y < final.Height; y += cell)
                    for (int x = 0; x < final.Width; x += cell)
                    {
                        bool even = ((x / cell) + (y / cell)) % 2 == 0;
                        using (SolidBrush b = new SolidBrush(even ? Color.FromArgb(58, 58, 62) : Color.FromArgb(48, 48, 52)))
                            g.FillRectangle(b, x, y, cell, cell);
                    }
                g.DrawImageUnscaled(winBmp, winX, winY);
                g.DrawImageUnscaled(overlayBmp, 0, 0);
                // 关闭按钮位置标记
                FrameLayout L = (FrameLayout)overlayBmp.Tag;
                if (L != null)
                {
                    using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 0), 1))
                        g.DrawRectangle(p, L.CloseX, L.BtnY, L.BtnW - 1, L.BtnH - 1);
                    using (Pen p = new Pen(Color.FromArgb(80, 0, 212, 255), 1))
                        g.DrawRectangle(p, L.WinX, L.WinY, L.WinW - 1, L.WinH - 1);
                }
            }
            final.Save(outPath, ImageFormat.Png);
            final.Dispose();
        }

        public static Bitmap RenderOverlay(FrameLayout L, int hover) { return RenderOverlay(L, hover, false); }

        public static Bitmap RenderOverlay(FrameLayout L, int hover, bool quick)
        {
            L.HoverButton = hover;
            Bitmap bmp = new Bitmap(L.Width, L.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) Art.Draw(g, L, Cfg, quick);
            bmp.Tag = L;
            return bmp;
        }

        static void RenderSample(string[] args)
        {
            string outPath = args.Length > 1 ? args[1] : Path.Combine(BaseDir, "preview.png");
            int w = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1100;
            int h = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 700;
            int dpi = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 144;
            Console.WriteLine("brand diag: font=" + (Brand.Family != null) + " logo=" + (Brand.Logo != null) + " err=" + Brand.LoadError);   // brand diag
            FrameLayout L = MakeLayout(w, h, dpi, 0);
            Bitmap win = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(win)) DrawFakeExplorer(g, w, h, dpi);
            Bitmap ov = RenderOverlay(L, 0);
            Composite(ov, win, L.WinX, L.WinY, outPath);
            Console.WriteLine("saved " + outPath + " " + ov.Width + "x" + ov.Height);
            // 顺带输出一张悬停态
            Bitmap ov2 = RenderOverlay(MakeLayout(w, h, dpi, 3), 3);
            string hover = Path.ChangeExtension(outPath, null) + "_hover.png";
            Composite(ov2, win, L.WinX, L.WinY, hover);
            Console.WriteLine("saved " + hover);
            win.Dispose();
        }

        static void RenderOnly(string[] args)
        {
            string outPath = args.Length > 1 ? args[1] : Path.Combine(BaseDir, "overlay.png");
            int w = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1100;
            int h = args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 700;
            int dpi = args.Length > 4 ? int.Parse(args[4], CultureInfo.InvariantCulture) : 144;
            Bitmap ov = RenderOverlay(MakeLayout(w, h, dpi, 0), 0);
            ov.Save(outPath, ImageFormat.Png);
            Console.WriteLine("saved " + outPath);
        }

        public static Icon TrayIcon()
        {
            try
            {
                string p = Path.Combine(BaseDir, "assets\\RogHime.ico");
                if (File.Exists(p))
                {
                    Size sz = SystemInformation.SmallIconSize;
                    try { return new Icon(p, sz); } catch { return new Icon(p); }
                }
            }
            catch { }
            Bitmap b = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 10, 10, 12))) g.FillEllipse(bg, 1, 1, 30, 30);
                using (Pen p = new Pen(Cfg != null ? Cfg.ColGlow : Color.Red, 2.4f)) g.DrawEllipse(p, 6, 10, 20, 12);
                using (SolidBrush rb = new SolidBrush(Cfg != null ? Cfg.ColGlow : Color.Red)) g.FillEllipse(rb, 13, 13, 6, 6);
            }
            return Icon.FromHandle(b.GetHicon());
        }
    }

    internal static class Extra
    {
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Native.RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Native.POINT p);
    }

    // ------------------------------------------------------------------ tray
    internal class TrayContext : ApplicationContext
    {
        NotifyIcon _icon;
        Watcher _watcher;
        ToolStripMenuItem _pause;
        ToolStripMenuItem _reload;
        Timer _cfgWatch;
        DateTime _cfgTime;
        bool _cfgLoaded;

        public TrayContext()
        {
            _icon = new NotifyIcon();
            _icon.Icon = Program.TrayIcon();
            _icon.Text = "ROG 资源管理器主题";
            _icon.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem settings = new ToolStripMenuItem("设置…");
            settings.Font = new Font(settings.Font, FontStyle.Bold);
            settings.Click += delegate { OpenSettings(); };
            menu.Items.Add(settings);

            ToolStripMenuItem auto = new ToolStripMenuItem("开机自启");
            int curAuto = AutoStart.Detect();
            ToolStripMenuItem autoInfo = new ToolStripMenuItem("当前：" + AutoStart.Name(curAuto)) { Enabled = false };
            string[] autoNames = { "关闭", "注册表启动项（推荐）", "启动文件夹", "计划任务 · 登录时（最高权限）" };
            for (int m = 0; m <= 3; m++)
            {
                int mm = m;
                ToolStripMenuItem it = new ToolStripMenuItem(autoNames[m]);
                it.Checked = (curAuto == mm);
                it.Click += delegate
                {
                    string err = AutoStart.Apply(mm);
                    if (err.Length != 0)
                        System.Windows.Forms.MessageBox.Show("设置失败：" + err + "\n\n（「计划任务」方式需要管理员权限）", "ROG主题");
                    int now = AutoStart.Detect();                       // 回显实际生效的方式
                    for (int q = 0; q < auto.DropDownItems.Count && q < 4; q++)
                    {
                        ToolStripMenuItem o = auto.DropDownItems[q] as ToolStripMenuItem;
                        if (o != null) o.Checked = (q == now);
                    }
                    autoInfo.Text = "当前：" + AutoStart.Name(now);
                    try { Program.Cfg.AutoStart = mm; Program.Cfg.Save(Program.ConfigPath); } catch { }
                };
                auto.DropDownItems.Add(it);
            }
            auto.DropDownItems.Add(new ToolStripSeparator());
            auto.DropDownItems.Add(autoInfo);
            menu.Items.Add(auto);

            // 双击托盘图标 = 打开设置界面
            _icon.DoubleClick += delegate { OpenSettings(); };
            menu.Items.Add(new ToolStripSeparator());

            _pause = new ToolStripMenuItem("暂停美化");
            _pause.Click += delegate
            {
                _watcher.Enabled = !_watcher.Enabled;
                _pause.Checked = !_watcher.Enabled;
                _pause.Text = _watcher.Enabled ? "暂停美化" : "继续美化";
                if (!_watcher.Enabled)
                {
                    _watcher.HideAll();
                    RevertSystem();                      // 暂停 = 立刻恢复系统默认
                    _icon.ShowBalloonTip(2000, "ROG主题", "已暂停美化，并恢复系统默认配色与背景", ToolTipIcon.Info);
                }
                else
                {
                    ReapplySystem();                     // 继续 = 重新接管
                    _icon.ShowBalloonTip(2000, "ROG主题", "已恢复美化", ToolTipIcon.Info);
                }
            };
            menu.Items.Add(_pause);
            _reload = new ToolStripMenuItem("重新加载配置");
            _reload.Click += delegate
            {
                Program.Cfg = ThemeConfig.Load(Program.ConfigPath);
                Brand.Load(Program.BaseDir);
                _watcher.InvalidateAll();
                _icon.ShowBalloonTip(1500, "ROG主题", "配置已重新加载", ToolTipIcon.Info);
            };
            menu.Items.Add(_reload);
            ToolStripMenuItem openCfg = new ToolStripMenuItem("打开配置文件");
            openCfg.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Program.ConfigPath, null, null, 1); };
            menu.Items.Add(openCfg);
            ToolStripMenuItem openDir = new ToolStripMenuItem("打开主题文件夹");
            openDir.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Program.BaseDir, null, null, 1); };
            menu.Items.Add(openDir);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem about = new ToolStripMenuItem("关于 / 使用说明");
            about.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Path.Combine(Program.BaseDir, "使用手册.md"), null, null, 1); };
            menu.Items.Add(about);
            ToolStripMenuItem quit = new ToolStripMenuItem("退出（并恢复系统默认）");
            quit.Click += delegate
            {
                try { RevertSystem(); } catch { }
                ExitThread();
            };
            menu.Items.Add(quit);
            _icon.ContextMenuStrip = menu;

            _watcher = new Watcher();
            _watcher.Start();

            // 配置文件被外部修改（记事本 / 独立设置窗口）时自动重新加载
            _cfgWatch = new Timer();
            _cfgWatch.Interval = 1200;
            _cfgWatch.Tick += delegate
            {
                try
                {
                    DateTime t = File.GetLastWriteTime(Program.ConfigPath);
                    if (_cfgTime != t)
                    {
                        _cfgTime = t;
                        if (_cfgLoaded)   // 启动后第一次不重载，避免抖动
                        {
                            Program.Cfg = ThemeConfig.Load(Program.ConfigPath);
                            Brand.Load(Program.BaseDir);
                            _watcher.InvalidateAll();
                        }
                        _cfgLoaded = true;
                    }
                }
                catch { }
            };
            try { _cfgTime = File.GetLastWriteTime(Program.ConfigPath); } catch { }
            _cfgLoaded = true;   // 关键：启动即视为已加载，否则第一次变更会被吞掉
            _cfgWatch.Start();
        }

        /// <summary>恢复系统默认：强调色还原 + 清掉资源管理器背景（背景在用时重启资源管理器）</summary>
        void RevertSystem()
        {
            try
            {
                ThemeConfig cfg = Program.Cfg;
                if (cfg != null && cfg.EnforceAccent == 1) Watcher.RestoreAccentNow();
                bool hadBg = BgApply.HasImages();
                BgApply.HideBackground();          // 不删文件，只把不透明度设 0（避免扩展报错）
                if (hadBg) BgApply.RestartExplorer();
            }
            catch { }
        }

        /// <summary>重新接管：按配置恢复强调色与背景</summary>
        void ReapplySystem()
        {
            try
            {
                Program.Cfg = ThemeConfig.Load(Program.ConfigPath);
                ThemeConfig cfg = Program.Cfg;
                string err;
                BgApply.Apply(cfg.BgIndex, cfg.BgPosType, cfg.BgAlpha, cfg.BgMask, cfg.BgCustomPath, cfg.BgHomeIndex, null, out err);
                if (BgApply.HasImages()) BgApply.RestartExplorer();
            }
            catch { }
        }

        void OpenSettings()
        {
            try
            {
                if (_settingsOpen) return;          // 防止连点开出多个
                _settingsOpen = true;
                using (SettingsForm f = new SettingsForm(Program.Cfg, delegate
                {
                    Program.Cfg = ThemeConfig.Load(Program.ConfigPath);
                    Brand.Load(Program.BaseDir);
                    _watcher.InvalidateAll();
                }))
                {
                    f.ShowDialog();
                }
            }
            catch { }
            finally { _settingsOpen = false; }
        }

        bool _settingsOpen;

        protected override void Dispose(bool disposing)
        {
            if (_cfgWatch != null) { _cfgWatch.Stop(); _cfgWatch.Dispose(); }
            if (_watcher != null) _watcher.Dispose();
            if (_icon != null) { _icon.Visible = false; _icon.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ------------------------------------------------------ 虚拟桌面（Win10/11）
    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
    internal class VirtualDesktopManagerClass { }

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
    }

    // --------------------------------------------------------------- watcher
    internal class Target
    {
        public IntPtr Hwnd;
        public OverlayWindow Ov;
        public int X, Y, W, H;
        public bool HasLayout;
        public bool NeedRender;
        public long RenderDue;
        public long DwmDue;
        public int HoverButton;
        public int MinX, MaxX, CloseX, BtnY, BtnW, BtnH;
        public int FrameInset;   // 窗口自身的不可见缩放宽边框（顶部留缝要减去它）
        public int Dpi;
        public long QuickUntil;   // 这之前都走简化帧
        public Bitmap SrcBmp;     // 满画质的干净帧，glitch 时以它为源
        public bool Glitched;     // 当前是否处于故障帧
        public long GlitchFrameDue;
    }

    internal class Watcher : IDisposable
    {
        public bool Enabled = true;
        Dictionary<IntPtr, Target> _targets = new Dictionary<IntPtr, Target>();
        Timer _tick;
        Timer _list;
        Native.WinEventDelegate _cb;
        IntPtr _hook;
        bool _busy;

        public void Start()
        {
            _cb = new Native.WinEventDelegate(OnWinEvent);
            _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _cb, 0, 0,
                Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
            _tick = new Timer();
            _tick.Interval = Math.Max(15, Program.Cfg.PollMs);
            _tick.Tick += delegate { Tick(false); };
            _tick.Start();
            _list = new Timer();
            _list.Interval = 800;
            _list.Tick += delegate { Tick(true); };
            _list.Start();
            Tick(true);
        }

        void OnWinEvent(IntPtr hook, int ev, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            if (ev == Native.EVENT_OBJECT_LOCATIONCHANGE)
            {
                if (!_targets.ContainsKey(hwnd)) return;
            }
            else if (ev == Native.EVENT_SYSTEM_FOREGROUND)
            {
                Tick(true);
                return;
            }
            Tick(false);
        }

        void RefreshList()
        {
            List<IntPtr> wins = new List<IntPtr>();
            Native.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                string c = Native.ClassOf(h);
                if ((c == "CabinetWClass" || c == "ExploreWClass") && Native.IsWindowVisible(h) && !Native.IsIconic(h)) wins.Add(h);
                return true;
            }, IntPtr.Zero);

            foreach (IntPtr h in wins)
            {
                if (_targets.ContainsKey(h)) continue;
                try
                {
                    Target t = new Target();
                    t.Hwnd = h;
                    t.Ov = new OverlayWindow();
                    _targets[h] = t;
                }
                catch (Exception ex) { Program.Log("create overlay: " + ex.Message); }
            }

            List<IntPtr> dead = new List<IntPtr>();
            foreach (KeyValuePair<IntPtr, Target> kv in _targets)
            {
                if (!Native.IsWindow(kv.Key) || !wins.Contains(kv.Key)) dead.Add(kv.Key);
            }
            foreach (IntPtr h in dead)
            {
                Target t = _targets[h];
                try { if (t.SrcBmp != null) { t.SrcBmp.Dispose(); t.SrcBmp = null; } } catch { }
                try { t.Ov.Dispose(); } catch { }
                _targets.Remove(h);
            }
        }

        static long _themeChecked;   // 主题自动切换的节流

        public void InvalidateAll()
        {
            foreach (Target t in _targets.Values) { t.HasLayout = false; t.NeedRender = true; t.RenderDue = 0; }
        }

        public void HideAll()
        {
            foreach (Target t in _targets.Values) { try { t.Ov.Hide(); } catch { } }
        }

        void Tick(bool refresh)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (refresh) RefreshList();
                if (!Enabled) return;
                ThemeConfig cfg = Program.Cfg;
       
            // ---- 主题：跟随系统浅色/深色自动切换（每 2 秒检查）----
            if (cfg.ThemeAuto == 1 && Environment.TickCount - _themeChecked > 2000)
            {
                _themeChecked = Environment.TickCount;
                string wantTheme = Themes.ForSystem();
                if (!string.Equals(wantTheme, cfg.Theme, StringComparison.OrdinalIgnoreCase))
                {
                    Themes.Apply(cfg, wantTheme);
                    if (wantTheme == Themes.Fubuki && cfg.BgIndex == 2) cfg.BgIndex = 4;
                    else if (wantTheme == Themes.RogHime && cfg.BgIndex == 4) cfg.BgIndex = 2;
                    try { cfg.Save(Program.ConfigPath); } catch { }
                    string tbErr;
                    BgApply.Apply(cfg.BgIndex, cfg.BgPosType, cfg.BgAlpha, cfg.BgMask, cfg.BgCustomPath, 0, null, out tbErr);
                    Program.Log("theme -> " + wantTheme);
                    InvalidateAll();
                }
            }         EnforceAccent(cfg);
                IntPtr fg = Native.GetForegroundWindow();
                long now = Environment.TickCount;
                Native.POINT cur; Native.GetCursorPos(out cur);

                _glitchBudget = 6;
                foreach (Target t in _targets.Values)
                {
                    if (!Native.IsWindow(t.Hwnd)) continue;
                    bool show = (cfg.AllWindows == 1) || (t.Hwnd == fg);
                    if (cfg.HideWhenMaximized == 1 && Native.IsZoomed(t.Hwnd)) show = false;
                    // 切到别的虚拟桌面时，这里窗口的边框也要跟着消失
                    if (show && !OnCurrentDesktop(t.Hwnd)) show = false;
                    if (!show) { if (t.HasLayout) { t.Ov.Hide(); t.HasLayout = false; t.NeedRender = true; } continue; }

                    Native.RECT r; Native.GetWindowRect(t.Hwnd, out r);
                    int dpi = (int)Native.GetDpiForWindow(t.Hwnd);
                    if (dpi <= 0) dpi = 96;
                    float s = dpi / 96f;
                    int margin = Math.Max(2, (int)Math.Round(cfg.BorderThickness * s));
                    int topMargin = margin + Math.Max(0, (int)Math.Round(cfg.TopBandExtra * s));
                    int winW = r.Right - r.Left;
                    int winH = r.Bottom - r.Top;
                    // 顶部也要留出和左右下一样宽的缝：窗口有 FrameInset 宽的不可见缩放边框，
                    // 左右下靠它自然形成缝隙，顶部却没有 → 这里把边框整体上移 FrameInset 补上
                    int fi = t.FrameInset;
                    float sc = dpi / 96f;
                    int gH = (int)Math.Round(Program.Cfg.GapH * sc);   // 左右镂空
                    int gV = (int)Math.Round(Program.Cfg.GapV * sc);   // 上下镂空
                    int W = winW + margin * 2 + gH * 2;
                    int H = winH + topMargin + margin + fi + gV * 2;
                    int ox = r.Left - margin - gH;
                    int oy = r.Top - topMargin - fi - gV;

                    if (W != t.W || H != t.H)
                    {
                        // 尺寸一变就立刻重绘，不再延迟；这段时间先出简化帧，停手后自动补满画质
                        t.W = W; t.H = H;
                        t.NeedRender = true;
                        t.RenderDue = 0;
                        t.QuickUntil = now + 180;
                    }

                    if (t.NeedRender && now >= t.RenderDue)
                    {
                        try
                        {
                            // 边框整体上移了 t.FrameInset，窗口区域高度必须同步加长，
                            // 否则底部那条缝会被吃掉（用户实测 ✗）
                            Native.RECT rr2 = r;
                            rr2.Bottom = r.Bottom + t.FrameInset;
                            FrameLayout L = Program.MakeLayoutForWindow(t.Hwnd, rr2, dpi, t.HoverButton);
                            t.FrameInset = L.FrameInset;   // 记下来，下一帧顶部的留缝要用
                            // 用户设置的镂空：把窗口矩形在覆盖层内偏移，并放大覆盖层
                            float sc2 = dpi / 96f;
                            int gh2 = (int)Math.Round(Program.Cfg.GapH * sc2);
                            int gv2 = (int)Math.Round(Program.Cfg.GapV * sc2);
                            L.WinX += gh2; L.WinY += gv2;
                            L.Width += gh2 * 2; L.Height += gv2 * 2;
                            t.MinX = ox + L.MinX; t.MaxX = ox + L.MaxX; t.CloseX = ox + L.CloseX;
                            t.BtnY = oy + L.BtnY; t.BtnW = L.BtnW; t.BtnH = L.BtnH;
                            bool quick = now < t.QuickUntil;
                            Bitmap bmp = Program.RenderOverlay(L, t.HoverButton, quick);
                            t.Ov.SetContent(bmp, ox, oy);
                            if (!quick)
                            {
                                // 留一份干净帧给 glitch 用（拖动中的简化帧不缓存）
                                if (t.SrcBmp != null) { t.SrcBmp.Dispose(); t.SrcBmp = null; }
                                t.SrcBmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
                                using (Graphics gg = Graphics.FromImage(t.SrcBmp))
                                {
                                    gg.CompositingMode = CompositingMode.SourceCopy;
                                    gg.DrawImageUnscaled(bmp, 0, 0);
                                }
                                t.SrcBmp.Tag = L;   // glitch 需要这份布局来裁剪边框
                            }
                            bmp.Dispose();
                            t.Ov.Show();
                            t.HasLayout = true;
                            if (quick) { t.NeedRender = true; t.RenderDue = t.QuickUntil + 1; }   // 停手后补一帧满画质
                            else t.NeedRender = false;
                        }
                        catch (Exception ex) { Program.Log("render: " + ex.Message); t.NeedRender = false; }
                    }
                    else if (t.HasLayout)
                    {
                        t.Ov.MoveTo(ox, oy);
                    }

                    if (t.HasLayout)
                    {
                        t.Ov.StackAbove(t.Hwnd);

                        // ---- 随机 glitch 故障特效 ----
                        // 说明：不再要求「必须是前台窗口」，否则用户开着设置窗口时永远看不到故障；
                        //       所有可见窗口一起进入故障，任何一帧都不会留下卡死的半故障状态。
                        if (cfg.GlitchEffect == 1 && cfg.GlitchStrength > 0 && _glitchBudget > 0 && t.SrcBmp != null
                            && t.SrcBmp.Width == t.W && t.SrcBmp.Height == t.H
                            && now >= t.QuickUntil)
                        {
                            // 设置界面点「立即试一次」→ GlitchTrigger 变化 → 马上来一发
                            if (cfg.GlitchTrigger != _lastTrigger)
                            {
                                _lastTrigger = cfg.GlitchTrigger;
                                _glitchUntil = now + GlitchMs(cfg);
                                _glitchSeed = _rnd.Next(1, 1000000);
                            }

                            if (_nextGlitch == 0) _nextGlitch = now + RndGap(cfg) * 1000;

                            if (_glitchUntil == 0 && now >= _nextGlitch)
                            {
                                _glitchUntil = now + GlitchMs(cfg);
                                _glitchSeed = _rnd.Next(1, 1000000);
                            }

                            if (_glitchUntil != 0 && now < _glitchUntil)
                            {
                                if (now >= t.GlitchFrameDue && _glitchBudget > 0)
                                {
                                    _glitchBudget--;
                                    t.GlitchFrameDue = now + 45;   // 每帧约 22fps，够顺
                                    try
                                    {
                                        Random rr = new Random(_glitchSeed + (int)(now & 0x7FFF));
                                        int bw = t.SrcBmp.Width, bh = t.SrcBmp.Height;
                                        using (Bitmap gb = new Bitmap(bw, bh, PixelFormat.Format32bppArgb))
                                        {
                                            Art.Glitch(t.SrcBmp, gb, cfg.GlitchStrength, cfg, rr);
                                            t.Ov.SetContent(gb, ox, oy);
                                            t.Glitched = true;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            else
                            {
                                // 收尾：谁进入过故障谁就还原，避免残留半故障画面
                                if (_glitchUntil != 0)
                                {
                                    _glitchUntil = 0;
                                    _nextGlitch = now + RndGap(cfg) * 1000;
                                    _glitchRestoredAt = now;
                                }
                                if (t.Glitched)
                                {
                                    t.Glitched = false;
                                    try
                                    {
                                        int bw = t.SrcBmp.Width, bh = t.SrcBmp.Height;
                                        using (Bitmap cb = new Bitmap(bw, bh, PixelFormat.Format32bppArgb))
                                        {
                                            using (Graphics gg = Graphics.FromImage(cb))
                                            {
                                                gg.CompositingMode = CompositingMode.SourceCopy;
                                                gg.DrawImageUnscaled(t.SrcBmp, 0, 0);
                                            }
                                            t.Ov.SetContent(cb, ox, oy);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        int hb = ButtonAt(t, cur);
                        if (hb != t.HoverButton)
                        {
                            t.HoverButton = hb;
                            t.NeedRender = true;
                            t.RenderDue = now;
                        }
                    }

                    if (now >= t.DwmDue)
                    {
                        ApplyDwm(t.Hwnd, cfg);
                        t.DwmDue = now + 2500;
                    }
                }
            }
            finally { _busy = false; }
        }

        static bool InRect(Native.POINT p, int x, int y, int w, int h)
        {
            return p.X >= x && p.X < x + w && p.Y >= y && p.Y < y + h;
        }

        static int ButtonAt(Target t, Native.POINT p)
        {
            if (t.BtnW <= 0 || t.BtnH <= 0) return 0;
            if (InRect(p, t.CloseX, t.BtnY, t.BtnW, t.BtnH)) return 3;
            if (InRect(p, t.MaxX, t.BtnY, t.BtnW, t.BtnH)) return 2;
            if (InRect(p, t.MinX, t.BtnY, t.BtnW, t.BtnH)) return 1;
            return 0;
        }

        static IVirtualDesktopManager _vdm;
        static bool _vdmFailed;

        // ---- glitch 故障特效的调度状态 ----
        static Random _rnd = new Random();
        static long _nextGlitch;
        static long _glitchUntil;
        static long _glitchFrameDue;
        static int _glitchSeed;

        static long _glitchRestoredAt;
        static int _lastTrigger;
        static int _glitchBudget;   // 单帧最多同时处理几个窗口的故障帧

        /// <summary>把间隔/时长钳制到安全区间，避免用户填出诡异值</summary>
        // 时间参数统一在这里取，任何调用点都不可能拿到非法组合
        // 0 表示「上一次结束后立刻来下一次」= 连续故障（仍受单次持续时间约束）
        static int GapMin(ThemeConfig cfg) { return Math.Max(0, Math.Min(60, cfg.GlitchMinGap)); }
        static int GapMax(ThemeConfig cfg) { return Math.Max(GapMin(cfg), Math.Min(60, cfg.GlitchMaxGap)); }

        static int RndGap(ThemeConfig cfg)
        {
            int a = GapMin(cfg), b = GapMax(cfg);
            if (b <= 0) return 0;                    // 最短=最长=0 → 连续故障
            return a + _rnd.Next(0, b - a + 1);
        }

        static int GlitchMs(ThemeConfig cfg) { return Math.Max(120, Math.Min(2000, cfg.GlitchDuration));
        }

        /// <summary>目标窗口是否在当前虚拟桌面上（拿不到信息时按"是"处理，避免误隐藏）</summary>
        static bool OnCurrentDesktop(IntPtr hwnd)
        {
            if (_vdmFailed) return true;
            try
            {
                if (_vdm == null) _vdm = (IVirtualDesktopManager)new VirtualDesktopManagerClass();
                int on;
                if (_vdm.IsWindowOnCurrentVirtualDesktop(hwnd, out on) != 0) return true;
                return on != 0;
            }
            catch { _vdmFailed = true; return true; }
        }

        static long _accentDue;

        static bool _accentRestored;

        /// <summary>供托盘「暂停/退出」调用：把系统强调色还原成原样</summary>
        public static void RestoreAccentNow() { RestoreAccent(); }

        static string AccentBackupPath { get { return Path.Combine(Program.BaseDir, "state\\accent_orig.txt"); } }

        /// <summary>界面自检入口（QA 用）：遍历所有页签控件，找出重叠/越界</summary>
        public static void GuiCheck()
        {
            Application.EnableVisualStyles();
            using (SettingsForm f = new SettingsForm(Program.Cfg, null, 0))
            {
                f.CreateControl();
                int issues = 0;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                Walk(f, f, sb, ref issues, 0);
                sb.AppendLine(issues == 0 ? "GUI CHECK OK — 未发现重叠或越界" : ("GUI CHECK: " + issues + " 处问题"));
                File.WriteAllText(Path.Combine(Program.BaseDir, "gui-check.txt"), sb.ToString());
                Console.WriteLine(sb.ToString());
            }
        }

        /// <summary>递归检查控件重叠/越界</summary>
        static void Walk(Control parent, Form form, System.Text.StringBuilder sb, ref int issues, int depth)
        {
            List<Control> kids = new List<Control>();
            foreach (Control c in parent.Controls) kids.Add(c);
            for (int i = 0; i < kids.Count; i++)
            {
                Control a = kids[i];
                if (a is TabControl)
                {
                    foreach (TabPage tp in ((TabControl)a).TabPages)
                    {
                        sb.AppendLine("页签 [" + tp.Text + "]");
                        Walk(tp, form, sb, ref issues, depth + 1);
                    }
                    continue;
                }
                if (a is Label && ((Label)a).AutoSize && a.Text.Length == 0) continue;
                // 越界检查（父容器是面板时，允许滚动面板越界）
                if (!(parent is TabPage) && a.Right > parent.ClientSize.Width + 2 && !(parent is Panel && ((Panel)parent).AutoScroll))
                {
                    sb.AppendLine("  越界: " + a.GetType().Name + " \"" + Trim(a.Text) + "\" right=" + a.Right + " > 容器宽 " + parent.ClientSize.Width);
                    issues++;
                }
                // 重叠检查（同层兄弟，容差 4px）
                for (int j = i + 1; j < kids.Count; j++)
                {
                    Control b = kids[j];
                    if (a is Label || b is Label) continue;          // 标签经常作为说明文字，跳过
                    Rectangle ra = a.Bounds, rb = b.Bounds;
                    ra.Inflate(-4, -4); rb.Inflate(-4, -4);
                    if (ra.IntersectsWith(rb))
                    {
                        sb.AppendLine("  重叠: " + a.GetType().Name + " \"" + Trim(a.Text) + "\" 与 " + b.GetType().Name + " \"" + Trim(b.Text) + "\"");
                        issues++;
                    }
                }
                if (a.HasChildren) Walk(a, form, sb, ref issues, depth + 1);
            }
        }

        static string Trim(string t) { if (t == null) return ""; t = t.Replace("\r", " ").Replace("\n", " "); return t.Length > 18 ? t.Substring(0, 18) + "…" : t; }

        static void BackupAccent()
        {
            try
            {
                if (File.Exists(AccentBackupPath)) return;      // 只备份第一次
                int dwm = 0, menu = 0;
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM"))
                    if (k != null && k.GetValue("AccentColor") != null) dwm = Convert.ToInt32(k.GetValue("AccentColor"));
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent"))
                    if (k != null && k.GetValue("AccentColorMenu") != null) menu = Convert.ToInt32(k.GetValue("AccentColorMenu"));
                Directory.CreateDirectory(Path.GetDirectoryName(AccentBackupPath));
                File.WriteAllText(AccentBackupPath, dwm + " " + menu);
                Program.Log("accent backed up: " + dwm + " " + menu);
            }
            catch { }
        }

        /// <summary>把系统强调色还原成主题接管之前的样子</summary>
        static void RestoreAccent()
        {
            try
            {
                if (!File.Exists(AccentBackupPath)) return;
                string[] parts = File.ReadAllText(AccentBackupPath).Split(' ');
                if (parts.Length < 2) return;
                int dwm = int.Parse(parts[0]);
                int menu = int.Parse(parts[1]);
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM", true))
                    if (k != null) k.SetValue("AccentColor", dwm, Microsoft.Win32.RegistryValueKind.DWord);
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", true))
                    if (k != null) k.SetValue("AccentColorMenu", menu, Microsoft.Win32.RegistryValueKind.DWord);
                File.Delete(AccentBackupPath);
                Program.Log("accent restored");
            }
            catch { }
        }

        static void EnforceAccent(ThemeConfig cfg)
        {
            if (cfg.EnforceAccent != 1)
            {
                // 用户关掉了「强制强调色」→ 还原系统原色（只做一次）
                if (!_accentRestored) { _accentRestored = true; RestoreAccent(); }
                return;
            }
            long now = Environment.TickCount;
            if (now < _accentDue) return;
            _accentDue = now + 20000;
            try
            {
                BackupAccent();
                Color c = cfg.AccentColor;
                int want = unchecked((int)(0xFF000000u | ((uint)c.B << 16) | ((uint)c.G << 8) | (uint)c.R));
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM", true))
                {
                    if (k != null)
                    {
                        object o = k.GetValue("AccentColor");
                        int cur = (o == null) ? 0 : Convert.ToInt32(o);
                        if (cur != want) k.SetValue("AccentColor", want, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
                using (Microsoft.Win32.RegistryKey k2 = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", true))
                {
                    if (k2 != null) k2.SetValue("AccentColorMenu", want, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        static int ColorRef(Color c)
        {
            return (c.R << 0) | (c.G << 8) | (c.B << 16);
        }

        static void ApplyDwm(IntPtr h, ThemeConfig cfg)
        {
            if (cfg.ApplyDwm != 1) return;
            try
            {
                // 只开深色标题栏。注意：绝对不要设置 CAPTION_COLOR / TEXT_COLOR ——
                // 那会把 Windows「激活=深色 / 非激活=灰色」两套配色强行统一成同一色，
                // 导致窗口失去焦点后过几秒又变回深色（用户实测过的 bug）。
                // 深色标题栏要跟随主题：吹雪是浅色主题，必须设 0，否则资源管理器标题栏和
                // 右上角按钮会被强行按成深色，看起来"只浅了一半"
                bool lightTheme = (cfg.ColBase.R + cfg.ColBase.G + cfg.ColBase.B) > 384
                                  || string.Equals(cfg.Theme, Themes.Fubuki, StringComparison.OrdinalIgnoreCase);
                int v = lightTheme ? 0 : 1;
                Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4);
                int bor = ColorRef(cfg.ColGlow);
                Native.DwmSetWindowAttribute(h, Native.DWMWA_BORDER_COLOR, ref bor, 4);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero) { Native.UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
            if (_tick != null) { _tick.Stop(); _tick.Dispose(); }
            if (_list != null) { _list.Stop(); _list.Dispose(); }
            foreach (Target t in _targets.Values) { try { t.Ov.Dispose(); } catch { } }
            _targets.Clear();
        }
    }
}
