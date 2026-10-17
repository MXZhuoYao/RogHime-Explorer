﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RogHime
{
    /// <summary>背景壁纸的读写（操作 ExplorerBg 外壳扩展的图片与 config.ini）</summary>
    internal static class BgApply
    {
        public const string Clsid = "{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}";

        public static string Dir { get { return Path.Combine(Program.BaseDir, "ExplorerBg"); } }
        public static string IniPath { get { return Path.Combine(Dir, "config.ini"); } }
        public static string ImgDir { get { return Path.Combine(Dir, "Image"); } }
        public static string WallpaperDir { get { return Path.Combine(Program.BaseDir, "assets\\wallpapers"); } }

        public static string RegPath()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + Clsid + @"\InprocServer32"))
                {
                    if (k == null) return null;
                    return k.GetValue(null) as string;
                }
            }
            catch { return null; }
        }

        public static bool Registered() { return RegPath() != null; }

        /// <summary>图片目录里现在有没有背景图（用于判断"是否需要重启资源管理器"）</summary>
        public static bool HasImages()
        {
            try { return Directory.Exists(ImgDir) && Directory.GetFiles(ImgDir).Length > 0; }
            catch { return false; }
        }

        /// <summary>把背景"藏起来"：不透明度设为 0。
        /// 注意：不要删图片文件！ExplorerBgTool 在图片目录为空时会弹「缺少图片文件」报错框。</summary>
        public static void HideBackground()
        {
            SetIni("imgAlpha", "0");
        }

        /// <summary>清掉背景图（仅卸载时使用；正常暂停请用 HideBackground）</summary>

        /// <summary>重启资源管理器（让外壳扩展重新读取配置/图片）</summary>
        /// <summary>只关掉"当前那个文件夹窗口"并重新打开它 —— 不杀 explorer 进程（其它窗口不受影响）。
        /// 新窗口会重新读取背景图，所以背景立刻生效。</summary>

        /// <summary>收集当前所有文件夹窗口的路径</summary>
        public static List<string> CollectExplorerUrls()
        {
            // 注意：在 WinForms 消息循环里直接调 Shell.Application 会拿到 0 个窗口，
            // 放到独立的 STA 线程里就正常 —— 这里用 STA 线程包一层。
            List<string> res = new List<string>();
            System.Threading.Thread th = new System.Threading.Thread(delegate()
            {
                try
                {
                    Type t = Type.GetTypeFromProgID("Shell.Application");
                    if (t == null) { Program.Log("reopen: 拿不到 Shell.Application 类型"); return; }
                    dynamic shell = Activator.CreateInstance(t);
                    dynamic wins = shell.Windows();
                    int cnt = (int)wins.Count;
                    for (int i = 0; i < cnt; i++)
                    {
                        dynamic w = wins.Item(i);
                        if (w == null) continue;
                        string u = (string)w.LocationURL;
                        if (!string.IsNullOrEmpty(u) && !res.Contains(u)) res.Add(u);
                    }
                }
                catch (Exception ex) { Program.Log("reopen 收集失败: " + ex.GetType().Name + " - " + ex.Message); }
            });
            try
            {
                th.SetApartmentState(System.Threading.ApartmentState.STA);
                th.IsBackground = true;
                th.Start();
                th.Join(4000);          // 最多等 4 秒
            }
            catch { }

            // 兜底：进程内 COM 偶尔拿不到窗口（返回 0），改用 powershell 子进程收集 —— 实测最可靠
            if (res.Count == 0)
            {
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("powershell",
                        "-NoProfile -Command \"(New-Object -ComObject Shell.Application).Windows() | ForEach-Object { $_.LocationURL }\"");
                    psi.RedirectStandardOutput = true;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (System.Diagnostics.Process pp = System.Diagnostics.Process.Start(psi))
                    {
                        string o = pp.StandardOutput.ReadToEnd();
                        pp.WaitForExit(5000);
                        foreach (string line in o.Split('\n'))
                        {
                            string tt = line.Trim();
                            if (tt.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && !res.Contains(tt)) res.Add(tt);
                        }
                    }
                    Program.Log("reopen: PowerShell 兜底收集到 " + res.Count + " 个");
                }
                catch (Exception ex) { Program.Log("reopen 兜底失败: " + ex.Message); }
            }
            return res;
        }



        /// <summary>重启资源管理器，并把原来看着的窗口路径重新打开（最多 8 个，避免刷屏）</summary>
        public static void RestartExplorerAndReopen()
        {
            List<string> urls = CollectExplorerUrls();
            Program.Log("reopen: 收集到 " + urls.Count + " 个 -> " + string.Join(" | ", urls.ToArray()));
            System.Threading.Thread.Sleep(1000);          // 等背景图写盘

            // 用「独立 cmd 进程」延时重开 —— 不依赖本进程是否还活着，时间也给足
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("/c timeout /t 8 /nobreak >nul");
            int n = 0;
            foreach (string u in urls)
            {
                if (n++ >= 8) break;
                string arg = u;
                if (u.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                {
                    string local = Uri.UnescapeDataString(u.Substring(8)).Replace('/', '\\');
                    if (local.Length > 1 && local[1] == ':') arg = local;
                }
                arg = arg.Replace("\"", "");
                sb.Append(" & start \"\" explorer.exe \"" + arg + "\"");
                sb.Append(" & timeout /t 1 /nobreak >nul");
            }
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", sb.ToString());
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(psi);
                Program.Log("reopen: 已启动独立重开进程，共 " + urls.Count + " 个路径");
            }
            catch (Exception ex) { Program.Log("reopen 启动失败: " + ex.Message); }

            RestartExplorer();
        }


        /// <summary>用 explorer.exe 打开一个路径或 shell: URL（比 Process.Start 可靠）</summary>

        public static void RestartExplorer()
        {
            try
            {
                // 关键：等图片文件彻底写盘再重启，否则资源管理器可能读到"空目录"
                // （Apply 会先清空 Image 目录再写新图，重启太快就会吞掉背景）
                System.Threading.Thread.Sleep(900);
                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(); } catch { }
                }
                System.Threading.Thread.Sleep(1800);
                if (System.Diagnostics.Process.GetProcessesByName("explorer").Length == 0)
                    System.Diagnostics.Process.Start("explorer.exe");
            }
            catch { }
        }


        public static void SetIni(string key, string val)
        {
            try
            {
                string text = File.Exists(IniPath) ? File.ReadAllText(IniPath) : "";
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                bool done = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + val;
                        done = true;
                    }
                }
                List<string> outp = new List<string>(lines);
                if (!done) outp.Add(key + "=" + val);
                File.WriteAllText(IniPath, string.Join("\r\n", outp.ToArray()));
            }
            catch { }
        }

        /// <summary>Win11「主文件夹」在不同版本下可能是这几个标识之一，全部写一遍</summary>
        public static readonly string[] HomePaths = new string[]
        {
            "::" + "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}",   // Win11 Home（24H2+）
            "::" + "{679F85CB-0220-4080-B29B-5540CC05AAB6}",   // 快速访问 / 主文件夹（22H2 起沿用）
            "shell:Home"
        };

        /// <summary>写/更新某个路径的专属背景段（ExplorerBgTool 的 custom 段）</summary>
        public static void WriteSection(string path, string imgFile)
        {
            try
            {
                if (!File.Exists(IniPath)) return;
                string text = File.ReadAllText(IniPath).Replace("\r\n", "\n");
                List<string> keep = new List<string>();
                string[] lines = text.Split('\n');
                bool skip = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        skip = (t.Substring(1, t.Length - 2).Trim() == path);
                    }
                    if (!skip) keep.Add(lines[i]);
                }
                if (imgFile == null) { File.WriteAllText(IniPath, string.Join("\r\n", keep.ToArray())); return; }
                keep.Add("");
                keep.Add("# ROG主题：主文件夹专属背景");
                keep.Add("[" + path + "]");
                keep.Add("img=" + imgFile);
                File.WriteAllText(IniPath, string.Join("\r\n", keep.ToArray()));
            }
            catch { }
        }

        /// <summary>把索引解析成实际图片完整路径（0 = 不使用）</summary>
        public static string ImageFor(int index, string customPath)
        {
            if (index >= 1 && index <= 4) return Path.Combine(Program.BaseDir, "assets\\wallpapers\\bg" + index + ".jpg");
            if (index == 5 && !string.IsNullOrEmpty(customPath) && File.Exists(customPath)) return customPath;
            return null;
        }

        /// <summary>index: 0=关闭 1/2/3=内置 4=自定义图片；homeIndex 同上，0=与其它文件夹相同</summary>
        public static bool Apply(int index, int posType, int alpha, int mask, string customPath, int homeIndex, string homeCustom, out string err)
        {
            err = null;
            try
            {
                Directory.CreateDirectory(ImgDir);
                foreach (string f in Directory.GetFiles(ImgDir)) { try { File.Delete(f); } catch { } }

                if (index > 0)
                {
                    string src;
                    string dst;
                    if (index == 5)
                    {
                        src = customPath;
                        if (string.IsNullOrEmpty(src) || !File.Exists(src)) { err = "自定义图片不存在，请重新选择"; return false; }
                        string ext = Path.GetExtension(src);
                        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                        dst = Path.Combine(ImgDir, "custom" + ext);
                    }
                    else
                    {
                        src = Path.Combine(WallpaperDir, "bg" + index + ".jpg");   // 1~4
                        if (!File.Exists(src)) { err = "找不到内置壁纸：" + src; return false; }
                        dst = Path.Combine(ImgDir, "bg" + index + ".jpg");
                    }
                    if (mask > 0 && dst.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) SaveMasked(src, dst, mask);
                    else File.Copy(src, dst, true);
                }

                // 主文件夹单独背景
                foreach (string hp in HomePaths) WriteSection(hp, null);
                if (homeIndex > 0)
                {
                    string hSrc = null, hName = null;
                    if (homeIndex == 5) { hSrc = homeCustom; hName = "home_custom" + (Path.GetExtension(homeCustom) ?? ".jpg"); }
                    else { hSrc = Path.Combine(WallpaperDir, "bg" + homeIndex + ".jpg"); hName = "home_bg" + homeIndex + ".jpg"; }
                    if (hSrc != null && File.Exists(hSrc))
                    {
                        string hDst = Path.Combine(ImgDir, hName);
                        File.Copy(hSrc, hDst, true);
                        SetIni("custom", "true");
                        foreach (string hp in HomePaths) WriteSection(hp, hName);
                    }
                }
                else SetIni("custom", "false");

                SetIni("random", "false");
                SetIni("folder", "");
                SetIni("posType", posType.ToString());
                SetIni("imgAlpha", alpha.ToString());
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }

        /// <summary>尝试让已打开的窗口就地重画（对 XAML 层有效，对外壳扩展层无效——它在内存里缓存了位图）</summary>

        /// <summary>遮罩颜色跟随主题：深色主题用黑（压暗，白字清晰）；
        /// 浅色主题（吹雪）用白（提亮，深色文件名清晰）。之前写死黑色，导致浅色主题下文件名看不清。</summary>
        internal static Color MaskColor()
        {
            ThemeConfig c = Program.Cfg;
            bool light = c != null && ((c.ColBase.R + c.ColBase.G + c.ColBase.B) > 384
                          || string.Equals(c.Theme, Themes.Fubuki, StringComparison.OrdinalIgnoreCase));
            return light ? Color.White : Color.Black;
        }

        static void SaveMasked(string src, string dst, int mask)
        {
            using (Image img = Image.FromFile(src))
            {
                int w = img.Width, h = img.Height;
                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(img, 0, 0, w, h);
                        int a = Math.Max(0, Math.Min(255, mask * 255 / 100));
                Color mc = MaskColor();
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(a, mc)))
                            g.FillRectangle(b, 0, 0, w, h);
                    }
                    ImageCodecInfo jpg = null;
                    foreach (ImageCodecInfo ci in ImageCodecInfo.GetImageEncoders())
                        if (ci.FormatID == ImageFormat.Jpeg.Guid) jpg = ci;
                    if (jpg != null)
                    {
                        using (EncoderParameters ep = new EncoderParameters(1))
                        {
                            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                            bmp.Save(dst, jpg, ep);
                        }
                    }
                    else bmp.Save(dst, ImageFormat.Jpeg);
                }
            }
        }
    }

    /// <summary>托盘「设置…」打开的配置界面（深色，跟随主题配色）</summary>
    internal class SettingsForm : Form
    {
        // ---- 主题配色 ----
        static readonly Color C_BG = Color.FromArgb(12, 12, 15);
        static readonly Color C_PANEL = Color.FromArgb(19, 19, 24);
        static readonly Color C_INPUT = Color.FromArgb(30, 30, 37);
        static readonly Color C_LINE = Color.FromArgb(52, 52, 62);
        static readonly Color C_TEXT = Color.FromArgb(236, 236, 242);
        static readonly Color C_DIM = Color.FromArgb(148, 148, 160);
        static readonly Color C_RED = Color.FromArgb(255, 0, 41);
        static readonly Color C_RED_DARK = Color.FromArgb(120, 0, 22);

        ThemeConfig _cfg;
        Action _onApply;
        List<Action> _save = new List<Action>();
        TabControl _tabs;
        Label _status;
        Timer _flash;
        static float _k = 1f;
        static readonly string VER = "2.2.0";
        Font _fTab, _fTabSel;

        PictureBox _bgPreview;
        bool _livePreview = true;
        CheckBox _themeAutoChk;
        string _pendingTheme;
        bool _themeApplied;    // 本次「应用」是否真的换了主题（决定要不要重启）   // 下拉里选的主题，只有点「应用」才真正生效   // 改控件即时预览到资源管理器
        Control _bgSel, _bgPos, _bgHome;
        int _bgPosIdx, _bgHomeIdx;
        CheckBox _bgHomeChk;
        RedSlider _bgMask, _bgAlpha;
        Label _bgMaskVal, _bgAlphaVal, _bgHint;
        int _bgIndex;

        static int S(int v) { return (int)Math.Round(v * _k); }

        public SettingsForm(ThemeConfig cfg, Action onApply) : this(cfg, onApply, 0) { }

        public SettingsForm(ThemeConfig cfg, Action onApply, int initialTab)
        {
            _cfg = cfg;
            _onApply = onApply;
            _bgIndex = Math.Max(0, Math.Min(5, cfg.BgIndex));

            try { using (Graphics gg = Graphics.FromHwnd(IntPtr.Zero)) { _k = gg.DpiX / 96f; } } catch { _k = 1f; }
            if (_k < 1f) _k = 1f;

            Font = new Font("Microsoft YaHei UI", 9.5f);
            AutoScaleMode = AutoScaleMode.None;
            Text = "ROG 资源管理器主题 — 设置";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(28, 0, 6);   // 深红：任何缝隙都不会露白
            ForeColor = C_TEXT;
            FormBorderStyle = FormBorderStyle.None;   // 自绘标题栏，去掉系统那圈白顶
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize = new Size(S(680), S(500));
            ClientSize = new Size(S(900), S(600));   // 保持紧凑，内容填满（页签内容超出时用滚动条）

            _fTab = new Font("Microsoft YaHei UI", 9.5f);
            _fTabSel = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(S(18), S(5));
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(S(112), S(30));
            // 七个页签平铺满整行，右侧不留黑边（随窗口宽度重算）
            _tabs.Resize += delegate { FitTabs(); };
            _tabs.HandleCreated += delegate { FitTabs(); };
            Shown += delegate { FitTabs(); };   // 布局完成后确保页签铺满
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.DrawItem += TabDraw;
            _tabs.Paint += delegate(object s3, PaintEventArgs e3)   // 盖掉系统画的页签框白线（四周全包）
            {
                Rectangle d = _tabs.DisplayRectangle;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 0, 6)))
                {
                    if (d.Bottom < _tabs.ClientSize.Height)
                        e3.Graphics.FillRectangle(b, 0, d.Bottom, _tabs.ClientSize.Width, _tabs.ClientSize.Height - d.Bottom);
                    if (d.Top > 0) e3.Graphics.FillRectangle(b, 0, 0, _tabs.ClientSize.Width, d.Top);
                    if (d.Left > 0) e3.Graphics.FillRectangle(b, 0, d.Top, d.Left, d.Height);
                    if (d.Right < _tabs.ClientSize.Width)
                        e3.Graphics.FillRectangle(b, d.Right, d.Top, _tabs.ClientSize.Width - d.Right, d.Height);
                }
            };
            Controls.Add(_tabs);

            _tabs.TabPages.Add(TabBackground());
            _tabs.TabPages.Add(TabBorder());
            _tabs.TabPages.Add(TabGlitch());
            _tabs.TabPages.Add(TabBrand());
            _tabs.TabPages.Add(TabColors());
            _tabs.TabPages.Add(TabAdvanced());
            _tabs.TabPages.Add(TabAbout());
            if (initialTab > 0 && initialTab < _tabs.TabPages.Count) _tabs.SelectedIndex = initialTab;

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = S(58);
            bottom.BackColor = C_PANEL;
            Controls.Add(bottom);
            Panel titleBar = BuildTitleBar();
            Controls.Add(titleBar);
            _tabs.BringToFront();   // 关键：Fill 控件放最前，才不会被标题栏/底栏盖住
            bottom.BringToFront();

            Button ok = FlatButton("保存并应用", S(120), S(32), C_RED, Color.White);
            ok.Location = new Point(bottom.Width - S(358), S(13));
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Click += delegate { Apply(); };
            bottom.Controls.Add(ok);

            Button cancel = FlatButton("取消", S(92), S(32), C_INPUT, C_TEXT);
            cancel.Location = new Point(bottom.Width - S(230), S(13));
            cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            cancel.Click += delegate { Close(); };
            bottom.Controls.Add(cancel);

            Button def = FlatButton("恢复默认", S(112), S(32), C_INPUT, C_TEXT);
            def.Location = new Point(bottom.Width - S(132), S(13));
            def.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            def.Click += delegate
            {
                if (MessageBox.Show("把所有外观设置恢复为默认值？", "ROG主题", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    ThemeConfig d = new ThemeConfig();
                    CopyInto(d, _cfg);
                    _cfg.Save(Program.ConfigPath);
                    if (_onApply != null) _onApply();
                    Flash("已恢复默认（重新打开设置可看到默认值）");
                }
            };
            bottom.Controls.Add(def);

            _status = new Label();
            _status.AutoSize = true;
            _status.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            _status.ForeColor = Color.FromArgb(80, 220, 140);
            _status.BackColor = Color.Transparent;
            _status.Location = new Point(S(16), S(21));
            bottom.Controls.Add(_status);

            _flash = new Timer();
            _flash.Interval = 2800;
            _flash.Tick += delegate { _flash.Stop(); _status.Text = ""; };
        }

        /// <summary>自绘黑红标题栏：图标 + 标题 + 最小化/关闭 + 可拖动</summary>
        Panel BuildTitleBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = S(38);
            bar.BackColor = C_BG;
            bar.Paint += delegate(object s1, PaintEventArgs e1)
            {
                using (Pen pe = new Pen(C_RED, S(2)))
                    e1.Graphics.DrawLine(pe, 0, bar.Height - S(2), bar.Width, bar.Height - S(2));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, 255, 0, 41)))
                    e1.Graphics.FillRectangle(b, 0, 0, bar.Width, S(1));
            };

            PictureBox ico = new PictureBox();
            ico.SizeMode = PictureBoxSizeMode.Zoom;
            ico.Size = new Size(S(20), S(20));
            ico.Location = new Point(S(12), S(9));
            ico.BackColor = Color.Transparent;
            try { ico.Image = Icon.ToBitmap(); } catch { }
            bar.Controls.Add(ico);

            Label t = new Label();
            t.AutoSize = true;
            t.Text = "ROG 资源管理器主题 — 设置";
            t.ForeColor = C_TEXT;
            t.BackColor = Color.Transparent;
            t.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            t.Location = new Point(S(40), S(9));
            t.MouseDown += TitleDrag; bar.MouseDown += TitleDrag; ico.MouseDown += TitleDrag;
            t.MouseMove += TitleDragMove; bar.MouseMove += TitleDragMove;
            t.MouseUp += TitleDragEnd; bar.MouseUp += TitleDragEnd;
            bar.Controls.Add(t);

            Label hint = new Label();
            hint.AutoSize = true;
            hint.Text = "拖动此处移动窗口";
            hint.ForeColor = C_DIM;
            hint.BackColor = Color.Transparent;
            hint.Location = new Point(S(250), S(12));
            hint.MouseDown += TitleDrag; hint.MouseMove += TitleDragMove; hint.MouseUp += TitleDragEnd;
            bar.Controls.Add(hint);

            // 最小化 / 关闭：直接画在标题栏上，避免子控件定位问题
            bar.MouseMove += delegate(object s4, MouseEventArgs e4)
            {
                if (_dragging) return;
                bool overClose = e4.X >= bar.ClientSize.Width - S(44);
                bool overMin = e4.X >= bar.ClientSize.Width - S(88) && !overClose;
                bar.Cursor = (overClose || overMin) ? Cursors.Hand : Cursors.Default;
            };
            bar.MouseClick += delegate(object s5, MouseEventArgs e5)
            {
                int w = bar.ClientSize.Width;
                if (e5.X >= w - S(44)) { Close(); return; }
                if (e5.X >= w - S(88)) { WindowState = FormWindowState.Minimized; return; }
            };
            bar.Paint += delegate(object s6, PaintEventArgs e6)
            {
                int w = bar.ClientSize.Width;
                bool hotClose = w > 0 && bar.ClientRectangle.Contains(bar.PointToClient(Cursor.Position)) && Cursor.Position.X >= bar.PointToScreen(new Point(w - S(44), 0)).X;
                if (hotClose)
                    using (SolidBrush hb = new SolidBrush(C_RED))
                        e6.Graphics.FillRectangle(hb, w - S(44), 0, S(44), bar.Height - S(2));
                using (Font fb = new Font("Microsoft YaHei UI", 10f))
                using (SolidBrush tb2 = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    e6.Graphics.DrawString("—", fb, tb2, new RectangleF(w - S(88), 0, S(44), bar.Height - S(2)), sf);
                    e6.Graphics.DrawString("✕", fb, tb2, new RectangleF(w - S(44), 0, S(44), bar.Height - S(2)), sf);
                }
            };
            return bar;
        }

        Button TitleButton(string glyph, bool isMin)
        {
            Button b = new Button();
            b.Text = glyph;
            b.AutoSize = false;
            b.Size = new Size(S(44), S(30));
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = isMin ? C_INPUT : C_RED;
            b.BackColor = C_BG;
            b.ForeColor = C_TEXT;
            b.Font = new Font("Microsoft YaHei UI", isMin ? 10f : 11f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.Tag = isMin ? "min" : "close";
            b.Location = new Point(ClientSize.Width - (isMin ? S(96) : S(52)), S(4));
            return b;
        }

        bool _dragging;
        Point _dragStart;
        void TitleDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true; _dragStart = e.Location;
        }
        void TitleDragMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point sp = PointToScreen(e.Location);
            Location = new Point(sp.X - _dragStart.X, sp.Y - _dragStart.Y);
        }
        void TitleDragEnd(object sender, MouseEventArgs e) { _dragging = false; }

        bool _fittingTabs;   // 重入锁：设 ItemSize 会反过来触发 Resize，不加锁会无限递归 → 栈溢出 ✗

        /// <summary>按控件宽度均分七个页签（右侧不留空隙）</summary>
        void FitTabs()
        {
            if (_fittingTabs) return;
            _fittingTabs = true;
            try
            {
                int n = Math.Max(1, _tabs.TabPages.Count);
                int w = Math.Max(S(60), (_tabs.ClientSize.Width - S(6)) / n);
                if (_tabs.ItemSize.Width != w) _tabs.ItemSize = new Size(w, _tabs.ItemSize.Height);
            }
            finally { _fittingTabs = false; }
        }

        void TabDraw(object sender, DrawItemEventArgs e)
        {
            bool sel = (_tabs.SelectedIndex == e.Index);
            Rectangle b = e.Bounds;
            Color strip = Color.FromArgb(28, 0, 6);          // 页签条底色（深红）
            Color stripDim = Color.FromArgb(36, 0, 8);

            // 补上系统用白底画的空白区（第一个页签左边 / 最后一个页签右边）
            using (SolidBrush sb = new SolidBrush(strip))
            {
                if (e.Index == 0 && b.Left > 0) e.Graphics.FillRectangle(sb, 0, b.Top, b.Left, b.Height);
                if (e.Index == _tabs.TabPages.Count - 1 && b.Right < _tabs.ClientSize.Width)
                    e.Graphics.FillRectangle(sb, b.Right, b.Top, _tabs.ClientSize.Width - b.Right, b.Height);
            }

            using (SolidBrush bb = new SolidBrush(sel ? C_RED : stripDim)) e.Graphics.FillRectangle(bb, b);
            if (sel)
                using (SolidBrush ub = new SolidBrush(Color.FromArgb(255, 90, 110)))
                    e.Graphics.FillRectangle(ub, b.Left, b.Bottom - S(2), b.Width, S(2));
            using (SolidBrush tb = new SolidBrush(sel ? Color.White : Color.FromArgb(196, 150, 158)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString(_tabs.TabPages[e.Index].Text, sel ? _fTabSel : _fTab, tb, b, sf);
            }
        }

        static void CopyInto(ThemeConfig from, ThemeConfig to)
        {
            foreach (System.Reflection.FieldInfo f in typeof(ThemeConfig).GetFields())
            {
                if (f.IsStatic) continue;
                f.SetValue(to, f.GetValue(from));
            }
        }

        void Flash(string text)
        {
            _status.Text = text;
            _flash.Stop();
            _flash.Start();
        }

        void Apply()
        {
            try
            {
                // 时间参数：先纠正非法组合，避免存下自相矛盾的值
                if (_cfg.GlitchMinGap > _cfg.GlitchMaxGap) _cfg.GlitchMaxGap = _cfg.GlitchMinGap;
                _cfg.GlitchMinGap = Math.Max(0, Math.Min(60, _cfg.GlitchMinGap));
                _cfg.GlitchMaxGap = Math.Max(_cfg.GlitchMinGap, Math.Min(60, _cfg.GlitchMaxGap));

                // 记下应用前的背景状态：主题没变但壁纸/遮罩变了，同样要重启 explorer，
                // 否则它继续用内存里缓存的旧图，表现就是"壁纸没跟着换" ✗
                int oldBgIndex = _cfg.BgIndex;
                int oldBgMask = _cfg.BgMask;
                string oldBgCustom = _cfg.BgCustomPath;

                string bgErr;
                if (!ApplyBackgroundNow(out bgErr))
                    MessageBox.Show("背景应用失败：" + bgErr, "ROG主题", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                bool bgChanged = (oldBgIndex != _bgIndex) || (oldBgMask != _cfg.BgMask)
                                 || !string.Equals(oldBgCustom ?? "", _cfg.BgCustomPath ?? "");
                bool needReopen = (_cfg.InstantApply == 1) && (_themeApplied || bgChanged);   // 主题或壁纸真的变了才重启
                _cfg.BgIndex = _bgIndex;
                _cfg.BgHomeIndex = (_bgHomeChk != null && _bgHomeChk.Checked) ? _bgHomeIdx + 1 : 0;
                _cfg.BgPosType = _bgPosIdx + 4;
                _cfg.BgMask = _bgMask.Value;
                _cfg.BgAlpha = _bgAlpha.Value;

                _cfg.Save(Program.ConfigPath);
                if (_onApply != null) _onApply();
                Flash("已保存并应用");
                if (needReopen) BgApply.RestartExplorerAndReopen();   // 重启 explorer 并恢复原窗口路径
            }
            catch (Exception ex) { MessageBox.Show("保存失败：" + ex.Message, "ROG主题", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>统一的背景应用：有 Windhawk mod 走 XAML（能管主文件夹），否则回退 ExplorerBgTool</summary>
        /// <summary>把下拉里暂存的主题真正落实（配色 + 系统浅深色 + 默认壁纸 + 遮罩强度）</summary>
        void ApplyPendingTheme()
        {
            _themeApplied = false;
            if (string.IsNullOrEmpty(_pendingTheme)) return;
            // 关键：即使主题名相同也要"无条件重新应用一遍"。
            // 因为配置可能处于不一致状态（颜色是 A 主题的、主题名却是 B），
            // 之前的"名字相同就跳过"会让用户点了没反应 ✗
            bool nameSame = string.Equals(_pendingTheme, _cfg.Theme, StringComparison.OrdinalIgnoreCase);
            Color expectBase = string.Equals(_pendingTheme, Themes.Fubuki, StringComparison.OrdinalIgnoreCase)
                ? ColorTranslator.FromHtml("#E8F5FB") : ColorTranslator.FromHtml("#07070A");
            bool paletteSame = (_cfg.ColBase.ToArgb() == expectBase.ToArgb());
            bool light = string.Equals(_pendingTheme, Themes.Fubuki, StringComparison.OrdinalIgnoreCase);
            Themes.Apply(_cfg, _pendingTheme);
            Themes.ApplySystemMode(light);
            _cfg.ThemeAuto = 0;
            if (_themeAutoChk != null) _themeAutoChk.Checked = false;
            _bgIndex = light ? 4 : 2;
            _cfg.BgIndex = _bgIndex;
            _cfg.BgMask = light ? 60 : 45;
            if (_bgMask != null) _bgMask.Value = _cfg.BgMask;
            // 主文件夹遮罩也跟随主题（与列表遮罩对称）；滑轨的显示值要重开窗口才刷新，
            // 但写进配置的是正确值，背景会按新值生成 ✓
            _cfg.BgHomeMask = light ? 50 : 40;
            try { SetDropText(_bgSel, new string[] { "不使用背景", "内置壁纸 1 · RO姬 · 机械红战姬", "内置壁纸 2 · RO姬 · 立绘 + ROG 标志", "内置壁纸 3 · 吹雪 · 冰蓝雪景", "内置壁纸 4 · 吹雪 · 雪武战姬", "自定义图片…" }[_bgIndex]); } catch { }
            UpdatePreview();
            _themeApplied = !(nameSame && paletteSame);   // 名字和配色都一致才算"没换主题"
            _pendingTheme = null;
        }

        bool ApplyBackgroundNow(out string err)
        {
            ApplyPendingTheme();   // 先把暂存的主题落实，遮罩颜色才会对
            Program.Cfg = _cfg;   // 保证 MaskColor() 拿到的是当前主题（浅色→白遮罩）
            string listImg = BgApply.ImageFor(_bgIndex, _cfg.BgCustomPath);
            // 关键：任何指向不存在的图片都会让 XAML 解析失败、整套背景样式失效 ✗ 这里逐级兜底
            if (listImg != null && !File.Exists(listImg)) listImg = null;
            string homeImg = null;
            if (_bgHomeChk != null && _bgHomeChk.Checked && _bgHomeIdx < 4)
            {
                string raw = BgApply.ImageFor(_bgHomeIdx + 1, _cfg.BgCustomPath);
                if (raw != null)
                {
                    homeImg = XamlBg.MaskedCopy(raw, _cfg.BgHomeMask);
                    if (homeImg == null || !File.Exists(homeImg)) homeImg = raw;    // 兜底：用原图
                }
            }
            if (homeImg == null) homeImg = listImg;
            if (XamlBg.Available())
            {
                // 两层都配好：XAML 画在 Grid 的 Background 上会盖住下层，
                // 一旦 XAML 失效（比如图片丢失），下层外壳扩展仍然兜底显示，不会「完全没背景」。
                string bgWarn;
                BgApply.Apply(_bgIndex, _bgPosIdx + 4, _bgAlphaSafe(), _bgMaskSafe(), _cfg.BgCustomPath, 0, null, out bgWarn);
                return XamlBg.Write(listImg ?? "", homeImg ?? "", _bgAlphaSafe() * 100 / 255,
                                    _cfg.BgHomeOpacity, _cfg.BgHomeStretch, _cfg.BgHomeAlign, out err);
            }
            return BgApply.Apply(_bgIndex, _bgPosIdx + 4, _bgAlphaSafe(), _bgMaskSafe(), _cfg.BgCustomPath, 0, null, out err);
        }

        TabPage TabAbout()
        {
            TabPage tab = new TabPage("关于");
            tab.BackColor = C_PANEL;
            Panel p = Page("关于");
            tab.Controls.Add(p);
            int y = 14, left = 10;

            // 标题区
            PictureBox ico = new PictureBox();
            ico.SizeMode = PictureBoxSizeMode.Zoom;
            ico.Size = new Size(S(56), S(56));
            ico.Location = new Point(S(left), S(y));
            ico.BackColor = Color.Transparent;
            try { ico.Image = Icon.ToBitmap(); } catch { }
            p.Controls.Add(ico);

            Label name = new Label();
            name.AutoSize = true;
            name.Text = "ROG 资源管理器主题";
            name.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            name.ForeColor = C_RED;
            name.BackColor = Color.Transparent;
            name.Location = new Point(S(left + 70), S(y + 2));
            p.Controls.Add(name);

            Label sub = new Label();
            sub.AutoSize = true;
            sub.Text = "Windows 11 资源管理器红黑主题美化  ·  版本 " + VER;
            sub.ForeColor = C_DIM;
            sub.BackColor = Color.Transparent;
            sub.Location = new Point(S(left + 72), S(y + 34));
            p.Controls.Add(sub);
            y += 70;

            p.Controls.Add(Section("作者", left, y)); y += 30;
            p.Controls.Add(Caption("Zhuo Yao", left + 12, y, false)); y += 26;
            p.Controls.Add(Caption("QQ            83015102", left + 12, y, false)); y += 26;

            Label gh = new Label();
            gh.AutoSize = true;
            gh.Text = "GitHub      https://github.com/MXZhuoYao";
            gh.ForeColor = C_RED;
            gh.BackColor = Color.Transparent;
            gh.Cursor = Cursors.Hand;
            gh.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Underline);
            gh.Location = new Point(S(left + 12), S(y));
            gh.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", "https://github.com/MXZhuoYao", null, null, 1); };
            p.Controls.Add(gh);
            y += 34;

            Button openGh = FlatButton("打开 GitHub 主页", S(150), S(28), C_RED, Color.White);
            openGh.Location = new Point(S(left + 12), S(y));
            openGh.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", "https://github.com/MXZhuoYao", null, null, 1); };
            p.Controls.Add(openGh);
            Button openDir = FlatButton("打开程序文件夹", S(140), S(28), C_INPUT, C_TEXT);
            openDir.Location = new Point(S(left + 172), S(y));
            openDir.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Program.BaseDir, null, null, 1); };
            p.Controls.Add(openDir);
            y += 44;

            p.Controls.Add(Section("第三方组件", left, y)); y += 30;
            string[] credits = new string[]
            {
                "ExplorerBgTool（Maplespe）· MIT License — 文件列表背景",
                "Windows 11 File Explorer Styler（Windhawk mod，可选）— 主文件夹背景",
                "ROG 之眼图标（simple-icons · CC0）· AsusRog 字体（版权归华硕）· .NET Framework"
            };
            foreach (string c in credits) { p.Controls.Add(Caption("· " + c, left + 12, y, true)); y += 20; }
            y += 10;

            p.Controls.Add(Section("声明", left, y)); y += 30;
            p.Controls.Add(Caption("· 个人爱好者作品，与华硕 / ROG 官方无任何关联；字体与图标版权归各自所有者。", left + 12, y, true)); y += 22;
            p.Controls.Add(Caption("· 使用本主题造成的任何问题由使用者自行承担。", left + 12, y, true)); y += 22;

            EnsureBottom(p, y);
            return tab;
        }


        // ---------------------------------------------------------- 深色控件工厂
        static Panel Page(string title)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.AutoScroll = true;
            p.BackColor = C_PANEL;
            p.Padding = new Padding(28, 20, 28, 18);   // 内容离边缘留出余量，不再贴着边框
            return p;
        }

        static Label Section(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(S(x), S(y));
            l.AutoSize = true;
            l.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            l.ForeColor = C_RED;
            l.BackColor = Color.Transparent;
            return l;
        }

        static Label Caption(string text, int x, int y, bool dim)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(S(x), S(y));
            l.AutoSize = true;
            l.ForeColor = dim ? C_DIM : C_TEXT;
            l.BackColor = Color.Transparent;
            return l;
        }

        static Button FlatButton(string text, int w, int h, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(w, h);
            b.BackColor = back;
            b.ForeColor = fore;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = C_LINE;
            b.FlatAppearance.BorderSize = 1;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        NumericUpDown Num(int val, int min, int max, int x, int y, int w, Action<int> set)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, val));
            n.Location = new Point(S(x), S(y));
            n.Size = new Size(S(w), S(26));
            n.BackColor = C_INPUT;
            n.ForeColor = C_TEXT;
            n.BorderStyle = BorderStyle.FixedSingle;
            if (set != null) _save.Add(delegate { set((int)n.Value); });
            return n;
        }

        CheckBox Chk(string text, bool val, int x, int y, Action<bool> set)
        {
            CheckBox c = new CheckBox();
            c.Text = text; c.Checked = val; c.AutoSize = true;
            c.Location = new Point(S(x), S(y));
            c.ForeColor = C_TEXT;
            c.BackColor = C_PANEL;          // 不能用 Transparent，否则勾画不出来
            c.FlatStyle = FlatStyle.Flat;
            if (set != null)
            {
                c.CheckedChanged += delegate { set(c.Checked); if (_livePreview && _onApply != null) _onApply(); };   // 勾选立刻生效并刷新画面
                _save.Add(delegate { set(c.Checked); });
            }
            return c;
        }

        ComboBox Combo(string[] items, int sel, int x, int y, int w, Action<int> set)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = C_INPUT;
            c.ForeColor = C_TEXT;
            c.Items.AddRange(items);
            c.SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, sel));
            c.Location = new Point(S(x), S(y));
            c.Size = new Size(S(w), S(26));
            if (set != null) _save.Add(delegate { set(c.SelectedIndex); });
            return c;
        }

        Control Txt(string val, int x, int y, int w, Action<string> set)
        {
            Panel wrap = new Panel();   // 用面板当边框，避免系统白边
            wrap.Location = new Point(S(x), S(y));
            wrap.Size = new Size(S(w), S(28));
            wrap.BackColor = C_INPUT;
            TextBox t = new TextBox();
            t.Text = val;
            t.Location = new Point(S(6), S(5));
            t.Size = new Size(S(w - 12), S(20));
            t.BackColor = C_INPUT;
            t.ForeColor = C_TEXT;
            t.BorderStyle = BorderStyle.None;   // 有边框会出现一条白线
            wrap.Controls.Add(t);
            if (set != null)
            {
                EventHandler push = delegate { set(t.Text); if (_onApply != null) _onApply(); };
                t.LostFocus += push;                                  // 点走就生效
                t.KeyDown += delegate(object ks, KeyEventArgs ke)     // 回车立即生效
                {
                    if (ke.KeyCode == Keys.Enter) { set(t.Text); if (_onApply != null) _onApply(); }
                };
                _save.Add(delegate { set(t.Text); });
            }
            return wrap;
        }

        Button ColorPick(Color val, int x, int y, Action<Color> set)
        {
            Button b = new Button();
            b.BackColor = val;
            b.Location = new Point(S(x), S(y));
            b.Size = new Size(S(64), S(26));
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = C_LINE;
            b.Text = "";
            b.Click += delegate
            {
                using (ColorDialog d = new ColorDialog())
                {
                    d.Color = b.BackColor;
                    d.FullOpen = true;
                    if (d.ShowDialog() == DialogResult.OK) b.BackColor = d.Color;
                }
            };
            Label hex = new Label();
            hex.AutoSize = true;
            hex.Location = new Point(S(x + 72), S(y + 4));
            hex.ForeColor = C_DIM;
            hex.BackColor = Color.Transparent;
            hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}", val.R, val.G, val.B);
            b.BackColorChanged += delegate
            {
                hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}", b.BackColor.R, b.BackColor.G, b.BackColor.B);
            };
            if (set != null) _save.Add(delegate { set(b.BackColor); });
            b.Tag = hex;
            return b;
        }

        /// <summary>深色下拉框：自己画，避免系统那圈白底</summary>
        Control Dropdown(string[] items, int sel, int x, int y, int w, Action<int> set)
        {
            Panel box = new Panel();
            box.Location = new Point(S(x), S(y));
            box.Size = new Size(S(w), S(28));
            box.BackColor = C_INPUT;
            box.Cursor = Cursors.Hand;

            Label arrow = new Label();
            arrow.Text = "▼";
            arrow.AutoSize = false;
            arrow.Dock = DockStyle.Right;
            arrow.Width = S(26);
            arrow.TextAlign = ContentAlignment.MiddleCenter;
            arrow.ForeColor = C_RED;
            arrow.BackColor = C_INPUT;
            box.Controls.Add(arrow);

            Label txt = new Label();
            txt.AutoSize = false;
            txt.Dock = DockStyle.Fill;
            txt.TextAlign = ContentAlignment.MiddleLeft;
            txt.Padding = new Padding(S(8), 0, 0, 0);
            txt.ForeColor = C_TEXT;
            txt.BackColor = C_INPUT;
            int cur = Math.Max(0, Math.Min(items.Length - 1, sel));
            txt.Text = items[cur];
            box.Controls.Add(txt);
            arrow.BringToFront();

            EventHandler open = delegate
            {
                ContextMenuStrip m = new ContextMenuStrip();
                m.BackColor = C_INPUT;
                m.ForeColor = C_TEXT;
                m.Renderer = new DarkMenuRenderer();
                m.ShowImageMargin = false;
                for (int i = 0; i < items.Length; i++)
                {
                    int idx = i;
                    ToolStripMenuItem it = new ToolStripMenuItem(items[i]);
                    it.ForeColor = C_TEXT;
                    if (i == cur) it.Font = new Font(it.Font, FontStyle.Bold);
                    it.Click += delegate
                    {
                        cur = idx;
                        txt.Text = items[idx];
                        if (set != null) set(idx);
                        if (_livePreview && _onApply != null) _onApply();
                    };
                    m.Items.Add(it);
                }
                m.Show(box, 0, box.Height);
            };
            box.Click += open;
            txt.Click += open;
            arrow.Click += open;
            box.Tag = txt;
            if (set != null) _save.Add(delegate { set(cur); });
            return box;
        }

        /// <summary>一行滑轨：标题 + 滑轨 + 当前值 + (最小~最大)</summary>
        Panel SliderRow(string label, int value, int min, int max, Action<int> set, string unit)
        {
            Panel row = new Panel();
            row.Size = new Size(S(640), S(34));   // 够放下标签+滑轨+数值即可；太宽会出横向滚动条，反而挤掉垂直空间
            row.BackColor = Color.Transparent;

            // 数值直接拼在标题里，任何缩放下都一定看得见，拖动时实时刷新
            Label l = Caption(label + "  (" + min + "~" + max + ") = " + value + unit, 0, 8, false);
            row.Controls.Add(l);

            RedSlider tb = new RedSlider();
            tb.Minimum = min; tb.Maximum = max;
            // 关键：按标签的【实际渲染宽度】定位滑轨，否则长标签会被滑轨盖住 ✗
            // （Caption 是 AutoSize，宽度要等字体应用后才准）
            int sx = S(250);
            try
            {
                l.AutoSize = true;
                int need = l.PreferredWidth + S(16);
                if (need > sx) sx = Math.Min(need, S(360));   // 上限 360，保证滑轨和数值还有位置
            }
            catch { }
            tb.Location = new Point(sx, S(6));
            tb.Size = new Size(Math.Max(S(140), S(470) - sx), S(26));
            tb.BackColor = C_PANEL;
            tb.Value = Math.Max(min, Math.Min(max, value));
            row.Controls.Add(tb);

            Label val = new Label();
            val.AutoSize = false;
            val.TextAlign = ContentAlignment.MiddleLeft;
            val.Location = new Point(S(478), S(4));
            val.Size = new Size(S(120), S(24));
            val.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            val.ForeColor = C_RED;
            val.BackColor = Color.Transparent;
            val.Text = tb.Value + unit;
            row.Controls.Add(val);

            tb.ValueChanged += delegate
            {
                val.Text = tb.Value + unit;
                l.Text = label + "  (" + min + "~" + max + ") = " + tb.Value + unit;
                if (set != null) set(tb.Value);                       // 拖动即写入内存
                if (_livePreview && _onApply != null) _onApply();     // 立即刷新资源管理器边框
            };
            if (set != null) _save.Add(delegate { set(tb.Value); });
            row.Tag = tb;
            return row;
        }

        /// <summary>在内容底部放一个撑高控件。AutoScroll 只按子控件边界计算滚动范围，
        /// 最后一行贴底时滚不到，必须靠它把范围撑开。</summary>
        void EnsureBottom(Panel p, int y)
        {
            Panel sp = new Panel();
            sp.Size = new Size(S(2), S(2));
            sp.Location = new Point(0, S(y + 40));
            sp.BackColor = C_PANEL;
            sp.TabStop = false;
            p.Controls.Add(sp);
        }

        int AddSlider(Panel p, int y, string label, int val, int min, int max, Action<int> set, string unit)
        {
            Panel row = SliderRow(label, val, min, max, set, unit);
            row.Location = new Point(0, S(y));
            p.Controls.Add(row);
            return y + 36;   // 行高 34 → 行距 36，绝不留叠加
        }

        int AddDropdown(Panel p, int y, string label, string[] items, int sel, Action<int> set)
        {
            p.Controls.Add(Caption(label, 4, y + 7, false));
            p.Controls.Add(Dropdown(items, sel, 250, y, 300, set));
            return y + 40;
        }

        static void SetDropText(Control dd, string text)
        {
            if (dd != null && dd.Tag is Label) ((Label)dd.Tag).Text = text;
        }


        // ---------------------------------------------------------- 背景
        TabPage TabBackground()
        {
            TabPage tab = new TabPage("背景和主题");
            tab.BackColor = C_PANEL;
            Panel p = Page("背景");
            tab.Controls.Add(p);
            int y = 8;

            p.Controls.Add(Section("主题", 0, y)); y += 32;
            p.Controls.Add(Caption("主题 = 花纹 + 配色 + 默认背景", 0, y + 5, true));
            p.Controls.Add(Dropdown(new string[] { "RO姬（红黑 · 深色）", "吹雪（白蓝 · 浅色 ❄）" },
                                    string.Equals(_cfg.Theme, Themes.Fubuki, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                                    300, y, 260, delegate(int v)
            {
                // 只记住选择 —— 不点「应用」什么都不变（用户指定行为）
                _pendingTheme = v == 1 ? Themes.Fubuki : Themes.RogHime;
                Flash(v == 1 ? "已选择「吹雪」—— 点「应用背景 / 保存并应用」后生效" : "已选择「RO姬」—— 点「应用背景 / 保存并应用」后生效");
            }));
            y += 40;
            p.Controls.Add(Chk("立即生效（切换主题/背景后自动重开资源管理器窗口）", _cfg.InstantApply == 1, 4, y, delegate(bool v)
            {
                _cfg.InstantApply = v ? 1 : 0;
            }));
            y += 34;
            _themeAutoChk = Chk("跟随系统浅色/深色自动切换主题", _cfg.ThemeAuto == 1, 4, y, delegate(bool v)
            {
                _cfg.ThemeAuto = v ? 1 : 0;
                if (v)
                {
                    Themes.Apply(_cfg, Themes.ForSystem());
                    try { _cfg.Save(Program.ConfigPath); } catch { }
                    if (_onApply != null) _onApply();
                    Flash("已开启自动跟随（系统浅色 → 吹雪，深色 → RO姬）");
                }
            });
            p.Controls.Add(_themeAutoChk);
            y += 40;

            p.Controls.Add(Section("背景选择", 0, y)); y += 32;
            p.Controls.Add(Caption("选择资源管理器文件区的背景图", 0, y + 5, true));

            string[] bgItems = new string[] { "不使用背景", "内置壁纸 1 · RO姬 · 机械红战姬", "内置壁纸 2 · RO姬 · 立绘 + ROG 标志", "内置壁纸 3 · 吹雪 · 冰蓝雪景", "内置壁纸 4 · 吹雪 · 雪武战姬", "自定义图片…" };
            _bgSel = Dropdown(bgItems, _bgIndex, 300, y, 290, delegate(int v)
            {
                _bgIndex = v;
                if (v == 5) PickCustom();
                UpdatePreview();
            });
            p.Controls.Add(_bgSel);
            y += 40;

            p.Controls.Add(Section("显示方式", 0, y)); y += 32;
            p.Controls.Add(Caption("图片如何适应窗口大小", 0, y + 5, true));
            _bgPosIdx = Math.Max(0, Math.Min(2, _cfg.BgPosType - 4));
            _bgPos = Dropdown(new string[] { "居中（原尺寸）", "缩放（完整显示）", "缩放并填充（推荐，不变形）" },
                              _bgPosIdx, 300, y, 290, delegate(int v) { _bgPosIdx = v; });
            p.Controls.Add(_bgPos);
            y += 44;

            p.Controls.Add(Section("背景遮罩 / 不透明度", 0, y)); y += 34;

            Panel m = SliderRow("遮罩浓度（压暗图片）", _cfg.BgMask, 0, 100, delegate(int v) { _cfg.BgMask = v; }, "%");
            m.Location = new Point(0, S(y)); p.Controls.Add(m); y += 36;

            Panel al = SliderRow("不透明度", _cfg.BgAlpha, 0, 255, delegate(int v) { _cfg.BgAlpha = v; }, "");
            al.Location = new Point(0, S(y)); p.Controls.Add(al); y += 40;

            Button applyBg = FlatButton("应用背景", S(120), S(30), C_RED, Color.White);
            applyBg.Location = new Point(S(238), S(y));
            applyBg.Click += delegate
            {
                string err;
                int pos = _bgPosIdx + 4;
                bool ok = ApplyBackgroundNow(out err);
                if (!ok) { MessageBox.Show("应用失败：" + err, "ROG主题", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                _cfg.BgIndex = _bgIndex; _cfg.BgPosType = pos; _cfg.BgMask = _bgMaskSafe(); _cfg.BgAlpha = _bgAlphaSafe();
                _cfg.BgHomeIndex = _bgHomeChk.Checked ? _bgHomeIdx + 1 : 0;
                try { _cfg.Save(Program.ConfigPath); } catch { }
                // 手动应用背景 = 手动优先：关掉主题自动跟随，否则系统浅深色一变就把背景换走，
                // 看起来就像"背景被吞了"
                _cfg.ThemeAuto = 0;
                if (_themeAutoChk != null) _themeAutoChk.Checked = false;
                Flash(_bgIndex == 0 ? "已关闭背景" : "背景已更新（已关闭主题自动跟随，防止背景被切走）");
                if (_cfg.InstantApply == 1 && _themeApplied) BgApply.RestartExplorerAndReopen();   // 只有主题真的变了才重启
                UpdateHint();
            };
            p.Controls.Add(applyBg);

            Button restartEx = FlatButton("重启资源管理器", S(140), S(30), C_INPUT, C_TEXT);
            restartEx.Location = new Point(S(500), S(y));
            restartEx.Click += delegate
            {
                if (MessageBox.Show("重启资源管理器会让所有文件夹窗口关闭（其它程序不受影响）。\n背景图改动有时需要这样才能刷新，继续吗？",
                                    "ROG主题", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    BgApply.RestartExplorer();
                    Flash("资源管理器已重启");
                }
            };
            p.Controls.Add(restartEx);

            Button openImg = FlatButton("打开图片文件夹", S(130), S(30), C_INPUT, C_TEXT);
            openImg.Location = new Point(S(370), S(y));
            openImg.Click += delegate
            {
                try { Directory.CreateDirectory(BgApply.ImgDir); } catch { }
                Native.ShellExecuteW(IntPtr.Zero, "open", BgApply.ImgDir, null, null, 1);
            };
            p.Controls.Add(openImg);
            y += 42;

            _bgHint = Caption("", 0, y, true);
            p.Controls.Add(_bgHint);
            UpdateHint();
            y += 28;   // 关键：把 y 推过去，否则下面的复选框会和提示文字重叠、点不到

            _bgPreview = new PictureBox();
            _bgPreview.Location = new Point(S(620), S(40));
            _bgPreview.Size = new Size(S(280), S(158));
            _bgPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _bgPreview.BackColor = C_INPUT;
            _bgPreview.BorderStyle = BorderStyle.FixedSingle;
            p.Controls.Add(_bgPreview);

            // 滑轨值单独同步（不用 _save 里的委托，避免重复）
            _bgMask = (RedSlider)m.Controls[1];
            _bgAlpha = (RedSlider)al.Controls[1];

            // ---- 主文件夹背景（走 Windhawk XAML，唯一能生效的途径）----
            bool xamlOk = XamlBg.Available() && XamlBg.ModEnabled();
            _bgHomeChk = Chk("主文件夹使用不同背景", _cfg.BgHomeIndex > 0, 4, y, delegate(bool v)
            {
                _bgHome.Enabled = v && xamlOk;
                if (!v) _cfg.BgHomeIndex = 0;
            });
            _bgHomeChk.Enabled = xamlOk;
            p.Controls.Add(_bgHomeChk); y += 34;

            _bgHomeIdx = Math.Max(0, Math.Min(4, _cfg.BgHomeIndex > 0 ? _cfg.BgHomeIndex - 1 : 0));
            _bgHome = Dropdown(new string[] { "内置壁纸 1 · RO姬 · 机械红战姬", "内置壁纸 2 · RO姬 · 立绘", "内置壁纸 3 · 吹雪 · 冰蓝雪景", "内置壁纸 4 · 吹雪 · 雪武战姬", "跟随通用背景" },
                               _bgHomeIdx, 300, y, 290, delegate(int v) { _bgHomeIdx = v; });
            _bgHome.Enabled = _cfg.BgHomeIndex > 0 && xamlOk;
            p.Controls.Add(_bgHome); y += 36;

            y += 4;
            Panel hmask = SliderRow("主文件夹背景遮罩（压暗图片）", _cfg.BgHomeMask, 0, 100,
                                    delegate(int v) { _cfg.BgHomeMask = v; }, "%");
            hmask.Location = new Point(0, S(y)); p.Controls.Add(hmask); y += 37;
            Panel hop = SliderRow("主文件夹背景透明度", _cfg.BgHomeOpacity, 0, 100,
                                  delegate(int v) { _cfg.BgHomeOpacity = v; }, "%");
            hop.Location = new Point(0, S(y)); p.Controls.Add(hop); y += 37;
            y = AddDropdown(p, y, "主文件夹缩放方式",
                            new string[] { "缩放并填充（不变形）", "缩放（完整显示）", "拉伸（变形）", "原始大小" },
                            Math.Max(0, Math.Min(3, _cfg.BgHomeStretch)), delegate(int v) { _cfg.BgHomeStretch = v; });
            y = AddDropdown(p, y, "主文件夹对齐方式",
                            new string[] { "居中", "左对齐", "右对齐", "顶部", "底部" },
                            Math.Max(0, Math.Min(4, _cfg.BgHomeAlign)), delegate(int v) { _cfg.BgHomeAlign = v; });
            Label st = Caption(XamlBg.StatusText(), 4, y, true);
            st.ForeColor = xamlOk ? C_DIM : C_RED;
            p.Controls.Add(st);
            Button guide = FlatButton("组件安装说明", S(120), S(26), C_INPUT, C_TEXT);
            guide.Location = new Point(S(600), S(y - 4));
            guide.Click += delegate
            {
                string msg =
                    "主文件夹背景需要两个组件：\n\n" +
                    "1) Windhawk（免费开源，windhawk.net）\n" +
                    "2) 它的 mod：Windows 11 File Explorer Styler\n\n" +
                    "安装步骤：\n" +
                    "① 装好 Windhawk 并让它保持运行\n" +
                    "② 打开 Windhawk → 搜索 Windows 11 File Explorer Styler\n" +
                    "③ 点「安装」→ 等编译完成\n" +
                    "④ 回到本页面，主文件夹设置就会自动变为可用\n\n" +
                    "现在要打开 Windhawk 的 mod 页面吗？";
                if (MessageBox.Show(msg, "主文件夹背景 · 组件说明", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Native.ShellExecuteW(IntPtr.Zero, "open", "https://windhawk.net/mods/windows-11-file-explorer-styler", null, null, 1);
            };
            p.Controls.Add(guide);
            y += 30;

            UpdatePreview();
            EnsureBottom(p, y);
            return tab;
        }

        int _bgAlphaSafe() { return _bgAlpha != null ? _bgAlpha.Value : _cfg.BgAlpha; }
        int _bgMaskSafe() { return _bgMask != null ? _bgMask.Value : _cfg.BgMask; }

        void UpdateHint()
        {
            if (_bgHint == null) return;
            if (BgApply.Registered())
                _bgHint.Text = "背景组件已注册 · 切换背景后重开一个资源管理器窗口即可看到";
            else
                _bgHint.Text = "背景组件尚未注册：请先运行一次 资源管理器背景.bat（管理员）";
            _bgHint.ForeColor = BgApply.Registered() ? C_DIM : Color.FromArgb(255, 170, 60);
        }

        void PickCustom()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "选择背景图片";
                d.Filter = "图片 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    _cfg.BgCustomPath = d.FileName;
                    UpdatePreview();
                    Flash("已选择自定义图片，点「应用背景」生效");
                }
                else
                {
                    _bgIndex = Math.Max(0, Math.Min(3, _cfg.BgIndex));
                    SetDropText(_bgSel, new string[] { "不使用背景", "内置壁纸 1 · RO姬 · 机械红战姬", "内置壁纸 2 · RO姬 立绘 + ROG 标志", "内置壁纸 3 · ROG 霓虹字标", "自定义图片…" }[_bgIndex]);
                }
            }
        }

        void UpdatePreview()
        {
            try
            {
                if (_bgPreview == null) return;
                if (_bgPreview.Image != null) { Image old = _bgPreview.Image; _bgPreview.Image = null; old.Dispose(); }
                string f = null;
                if (_bgIndex >= 1 && _bgIndex <= 4) f = Path.Combine(BgApply.WallpaperDir, "bg" + _bgIndex + ".jpg");
                else if (_bgIndex == 5) f = _cfg.BgCustomPath;
                if (f == null || !File.Exists(f)) return;
                using (Image src = Image.FromFile(f))
                    _bgPreview.Image = new Bitmap(src, new Size(S(276), S(154)));
            }
            catch { }
        }

        // ---------------------------------------------------------- 边框
        TabPage TabBorder()
        {
            TabPage tab = new TabPage("边框");
            tab.BackColor = C_PANEL;
            Panel p = Page("边框");
            tab.Controls.Add(p);
            int y = 8, left = 4;
            _save.Clear();

            p.Controls.Add(Section("整体", left, y)); y += 32;
            y = AddSlider(p, y, "左右镂空", _cfg.GapH, 0, 40, delegate(int v) { _cfg.GapH = v; }, " px");
            y = AddSlider(p, y, "上下镂空", _cfg.GapV, 0, 40, delegate(int v) { _cfg.GapV = v; }, " px");
            y = AddSlider(p, y, "边框厚度（像素）", _cfg.BorderThickness, 2, 40, delegate(int v) { _cfg.BorderThickness = v; }, "");
            y = AddSlider(p, y, "顶部条带额外高度（铭牌区）", _cfg.TopBandExtra, 0, 60, delegate(int v) { _cfg.TopBandExtra = v; }, "");
            y = AddSlider(p, y, "四角 45° 切角", _cfg.CornerCut, 0, 40, delegate(int v) { _cfg.CornerCut = v; }, "");
            y = AddSlider(p, y, "圆角", _cfg.CornerRadius, 0, 30, delegate(int v) { _cfg.CornerRadius = v; }, "");
            y = AddDropdown(p, y, "边框风格", new string[] { "刀锋", "碳纤维", "纯色" },
                            _cfg.BorderStyle == "carbon" ? 1 : (_cfg.BorderStyle == "none" ? 2 : 0),
                            delegate(int v) { _cfg.BorderStyle = v == 1 ? "carbon" : (v == 2 ? "none" : "blade"); });
            y += 6;

            p.Controls.Add(Section("刃片花纹", left, y)); y += 34;
            y = AddSlider(p, y, "刃片间距", _cfg.BladeSpacing, 8, 80, delegate(int v) { _cfg.BladeSpacing = v; }, "");
            y = AddSlider(p, y, "刃片宽度", _cfg.BladeWidth, 1, 12, delegate(int v) { _cfg.BladeWidth = v; }, "");
            y = AddSlider(p, y, "每隔几片出现一片亮红", _cfg.BladeHotEvery, 0, 12, delegate(int v) { _cfg.BladeHotEvery = v; }, "");
            y = AddSlider(p, y, "机加工纹路步长（0=关闭）", _cfg.MachiningStep, 0, 30, delegate(int v) { _cfg.MachiningStep = v; }, "");
            y = AddSlider(p, y, "边框发光强度", _cfg.GlowStrength, 0, 100, delegate(int v) { _cfg.GlowStrength = v; }, "");
            y += 6;

            p.Controls.Add(Section("标题栏", left, y)); y += 34;
            y = AddSlider(p, y, "标题栏装饰线（0=关闭）", _cfg.CaptionDecor, 0, 2, delegate(int v) { _cfg.CaptionDecor = v; }, "");
            y = AddSlider(p, y, "装饰线高度", _cfg.CaptionLineHeight, 1, 8, delegate(int v) { _cfg.CaptionLineHeight = v; }, "");
            p.Controls.Add(Chk("最大化时隐藏边框", _cfg.HideWhenMaximized != 0, left, y, delegate(bool v) { _cfg.HideWhenMaximized = v ? 1 : 0; }));
            y += 44;

            EnsureBottom(p, y);
            return tab;
        }



        TabPage TabGlitch()
        {
            TabPage tab = new TabPage("特效");
            tab.BackColor = C_PANEL;
            Panel p = Page("特效");
            tab.Controls.Add(p);
            int y = 8, left = 4;
            _save.Clear();

            p.Controls.Add(Section("随机 glitch 故障特效", left, y)); y += 34;
            p.Controls.Add(Chk("启用随机故障特效", _cfg.GlitchEffect != 0, left, y, delegate(bool v) { _cfg.GlitchEffect = v ? 1 : 0; }));
            Button tryG = FlatButton("立即试一次", S(120), S(28), C_RED, Color.White);
            tryG.Location = new Point(S(300), S(y - 4));
            tryG.Click += delegate
            {
                _cfg.GlitchTrigger = _cfg.GlitchTrigger + 1;
                try { _cfg.Save(Program.ConfigPath); } catch { }
                if (_onApply != null) _onApply();
                Flash("已触发一次故障 —— 去看资源管理器边框");
            };
            p.Controls.Add(tryG);
            y += 40;

            y = AddDropdown(p, y, "作用范围", new string[] { "仅边框（推荐）", "边框 + 窗口内部", "仅窗口内部" },
                            _cfg.GlitchArea, delegate(int v) { _cfg.GlitchArea = v; });
            y += 4;
            y = AddSlider(p, y, "故障强度", _cfg.GlitchStrength, 0, 100, delegate(int v) { _cfg.GlitchStrength = v; }, "");
            y = AddSlider(p, y, "色差宽度（像素）", _cfg.GlitchChroma, 0, 20, delegate(int v) { _cfg.GlitchChroma = v; }, " px");
            y = AddSlider(p, y, "故障条纹宽度（像素）", _cfg.GlitchBarW, 1, 10, delegate(int v) { _cfg.GlitchBarW = v; }, " px");

            Panel slMin = SliderRow("最短间隔（秒，0=连续）", Math.Max(0, Math.Min(60, _cfg.GlitchMinGap)), 0, 60, null, " s");
            slMin.Location = new Point(0, S(y)); p.Controls.Add(slMin); y += 37;
            Panel slMax = SliderRow("最长间隔（秒，0=连续）", Math.Max(0, Math.Min(60, _cfg.GlitchMaxGap)), 0, 60, null, " s");
            slMax.Location = new Point(0, S(y)); p.Controls.Add(slMax); y += 37;
            y = AddSlider(p, y, "单次持续时间（毫秒）", Math.Max(120, Math.Min(2000, _cfg.GlitchDuration)), 120, 2000, delegate(int v) { _cfg.GlitchDuration = v; }, " ms");

            RedSlider tbMin = (RedSlider)slMin.Tag;
            RedSlider tbMax = (RedSlider)slMax.Tag;
            bool fixing = false;
            tbMin.ValueChanged += delegate
            {
                if (fixing) return;
                fixing = true;
                if (tbMin.Value > tbMax.Value) tbMax.Value = tbMin.Value;
                _cfg.GlitchMinGap = tbMin.Value;
                _cfg.GlitchMaxGap = tbMax.Value;
                fixing = false;
            };
            tbMax.ValueChanged += delegate
            {
                if (fixing) return;
                fixing = true;
                if (tbMax.Value < tbMin.Value) tbMin.Value = tbMax.Value;
                _cfg.GlitchMinGap = tbMin.Value;
                _cfg.GlitchMaxGap = tbMax.Value;
                fixing = false;
            };
            _save.Add(delegate { _cfg.GlitchMinGap = tbMin.Value; _cfg.GlitchMaxGap = tbMax.Value; });

            EnsureBottom(p, y);
            return tab;
        }

        // ---------------------------------------------------------- 铭牌
        TabPage TabBrand()
        {
            TabPage tab = new TabPage("铭牌");
            tab.BackColor = C_PANEL;
            Panel p = Page("铭牌");
            tab.Controls.Add(p);
            int y = 8, left = 4;

            p.Controls.Add(Section("左上角文字", left, y)); y += 34;
            p.Controls.Add(Chk("显示铭牌", _cfg.ShowBrand != 0, left, y, delegate(bool v) { _cfg.ShowBrand = v ? 1 : 0; })); y += 36;
            p.Controls.Add(Caption("文字内容", left, y + 7, false));
            p.Controls.Add(Txt(_cfg.BrandText, 300, y, 320, delegate(string v) { _cfg.BrandText = v; })); y += 40;
            p.Controls.Add(Caption("提示：铭牌字体是「大写专用」，输入小写字母会被系统字体替代（笔画偏细）。", 4, y - 4, true));
            p.Controls.Add(Caption("建议用大写英文或数字，效果和示例一致。", 4, y + 18, true));
            y += 46;
            p.Controls.Add(Chk("文字下划线", _cfg.BrandUnderline != 0, left, y, delegate(bool v) { _cfg.BrandUnderline = v ? 1 : 0; })); y += 44;

            p.Controls.Add(Section("右上角 ROG 之眼", left, y)); y += 34;
            p.Controls.Add(Chk("显示 LOGO", _cfg.BrandLogo != 0, left, y, delegate(bool v) { _cfg.BrandLogo = v ? 1 : 0; })); y += 36;
            y = AddSlider(p, y, "LOGO 大小（%）", _cfg.BrandLogoScale, 30, 300, delegate(int v) { _cfg.BrandLogoScale = v; }, " %");
            y += 8;
            EnsureBottom(p, y);
            return tab;
        }



        // ---------------------------------------------------------- 按钮



        // ---------------------------------------------------------- 配色
        TabPage TabColors()
        {
            TabPage tab = new TabPage("配色");
            tab.BackColor = C_PANEL;
            Panel p = Page("配色");
            tab.Controls.Add(p);
            int y = 8, left = 4, right = 250, rh = 30;

            p.Controls.Add(Section("红黑主题配色", left, y)); y += 34;
            string[] names = { "ColBase 底色", "ColBaseHi 底色高光", "ColBlade 刃片", "ColBladeDark 刃片暗部",
                               "ColBladeHot 亮刃", "ColBladeHotDark 亮刃暗部", "ColBladeDim 暗刃", "ColBladeEdge 刃口高光",
                               "ColGlow 发光", "ColDeep 深红", "ColClose 关闭键", "ColCloseHover 关闭键悬停",
                               "ColAccent 强调色", "ColText 符号/文字", "ColGlyph 悬停符号", "ColStripe 条纹", "ColMetal 金属" };
            for (int i = 0; i < names.Length; i++)
            {
                string fn = names[i].Split(' ')[0];
                System.Reflection.FieldInfo f = typeof(ThemeConfig).GetField(fn);
                p.Controls.Add(Caption(names[i], left, y + 4, false));
                Color cur = (Color)f.GetValue(_cfg);
                Button b = ColorPick(cur, right, y + 2, MakeColorSetter(f));
                p.Controls.Add(b);
                if (b.Tag is Label) p.Controls.Add((Label)b.Tag);
                y += rh;
            }
            EnsureBottom(p, y);
            return tab;
        }

        Action<Color> MakeColorSetter(System.Reflection.FieldInfo f)
        {
            return delegate(Color c) { f.SetValue(_cfg, c); };
        }

        // ---------------------------------------------------------- 高级
        TabPage TabAdvanced()
        {
            TabPage tab = new TabPage("高级");
            tab.BackColor = C_PANEL;
            Panel p = Page("高级");
            tab.Controls.Add(p);
            int y = 8, left = 4;

            p.Controls.Add(Section("行为", left, y)); y += 30;
            p.Controls.Add(Chk("对所有资源管理器窗口生效", _cfg.AllWindows != 0, left, y, delegate(bool v) { _cfg.AllWindows = v ? 1 : 0; })); y += 30;
            p.Controls.Add(Chk("使用 DWM 圆角 / 材质 API", _cfg.ApplyDwm != 0, left, y, delegate(bool v) { _cfg.ApplyDwm = v ? 1 : 0; })); y += 30;
            p.Controls.Add(Chk("强制把系统强调色改成 ROG 红", _cfg.EnforceAccent != 0, left, y, delegate(bool v) { _cfg.EnforceAccent = v ? 1 : 0; })); y += 28;
            p.Controls.Add(Caption("会影响所有普通窗口（标题栏/开关/选中框）；关掉即自动还原原色。", left + 26, y, true)); y += 26;

            p.Controls.Add(Section("开机自启", left, y)); y += 32;
            p.Controls.Add(Caption("启动方式", left, y + 5, false));
            p.Controls.Add(Dropdown(new string[] { "关闭", "注册表启动项（推荐 · 免管理员）", "启动文件夹（可直接看到/删除）", "计划任务 · 登录时（最高权限 · 需管理员）" },
                                    Math.Max(0, Math.Min(3, _cfg.AutoStart)), 300, y, 380, delegate(int v)
            {
                _cfg.AutoStart = v;
                string err = AutoStart.Apply(v);
                Flash(err.Length == 0 ? ("开机自启 → " + AutoStart.Name(v)) : ("设置失败：" + err));
            })); y += 40;
            p.Controls.Add(Caption("前两种免管理员；计划任务更稳但需授权一次。", left + 26, y, true)); y += 26;

            p.Controls.Add(Caption("强调色", left, y + 7, false));
            Button ab = ColorPick(_cfg.AccentColor, 300, y, delegate(Color c) { _cfg.AccentColor = c; });
            p.Controls.Add(ab);
            if (ab.Tag is Label) p.Controls.Add((Label)ab.Tag);
            y += 42;

            y = AddSlider(p, y, "轮询间隔（毫秒，越小越跟手）", _cfg.PollMs, 30, 500, delegate(int v) { _cfg.PollMs = v; }, " ms");
            y += 8;

            p.Controls.Add(Section("快捷入口", left, y)); y += 34;
            Button openCfg = FlatButton("打开 RogHime.ini", S(210), S(30), C_INPUT, C_TEXT);
            openCfg.Location = new Point(S(left), S(y));
            openCfg.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Program.ConfigPath, null, null, 1); };
            p.Controls.Add(openCfg); y += 38;

            Button openBgDir = FlatButton("打开背景组件文件夹", S(210), S(30), C_INPUT, C_TEXT);
            openBgDir.Location = new Point(S(left), S(y));
            openBgDir.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", BgApply.Dir, null, null, 1); };
            p.Controls.Add(openBgDir); y += 38;

            Button openDir = FlatButton("打开主题文件夹", S(210), S(30), C_INPUT, C_TEXT);
            openDir.Location = new Point(S(left), S(y));
            openDir.Click += delegate { Native.ShellExecuteW(IntPtr.Zero, "open", Program.BaseDir, null, null, 1); };
            p.Controls.Add(openDir);
            EnsureBottom(p, y);
            return tab;
        }
    }


    /// <summary>
    /// 通过 Windhawk 的「Windows 11 File Explorer Styler」mod 设置 XAML 背景。
    /// 这是唯一能改「主文件夹」背景的办法（ExplorerBgTool 够不到那个页面）。
    /// 设置存在 HKLM，需要提权写入。
    /// </summary>
    internal static class XamlBg
    {
        public const string ModName = "windows-11-file-explorer-styler";
        public const string ListTarget = "Grid#DetailsViewControlRootGrid";
        public const string HomeTarget = "Grid#HomeViewRootGrid";

        static string SettingsSub
        {
            get { return @"SOFTWARE\Windhawk\Engine\Mods\" + ModName + @"\Settings"; }
        }

        /// <summary>Windhawk 本体是否安装</summary>
        public static bool WindhawkInstalled()
        {
            try { using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Windhawk")) return k != null; }
            catch { return false; }
        }

        /// <summary>需要的 mod 是否已安装</summary>
        public static bool Available()
        {
            try { using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(SettingsSub)) return k != null; }
            catch { return false; }
        }

        /// <summary>mod 是否处于启用状态</summary>
        public static bool ModEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Windhawk\Engine\Mods\" + ModName))
                {
                    if (k == null) return false;
                    object o = k.GetValue("Disabled");
                    return o == null || Convert.ToInt32(o) == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>综合状态说明（给界面用）</summary>
        public static string StatusText()
        {
            if (!WindhawkInstalled()) return "未检测到 Windhawk —— 主文件夹背景需要它，点右边按钮查看安装方法";
            if (!Available()) return "已装 Windhawk，但缺少「Windows 11 File Explorer Styler」mod —— 点右边按钮查看安装方法";
            if (!ModEnabled()) return "mod 已安装但处于停用状态 —— 请到 Windhawk 里启用它";
            return "就绪：可通过本页设置主文件夹背景（保存时会请求管理员权限）";
        }

        static readonly string[] StretchNames = { "UniformToFill", "Uniform", "Fill", "None" };
        static readonly string[] AlignNames = { "Center", "Left", "Right", "Top", "Bottom" };

        static string Brush(string img, int opacityPercent) { return Brush(img, opacityPercent, 0, 0); }

        static string Brush(string img, int opacityPercent, int stretch, int align)
        {
            double op = Math.Max(0, Math.Min(100, opacityPercent)) / 100.0;
            string o = op.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            string st = StretchNames[Math.Max(0, Math.Min(StretchNames.Length - 1, stretch))];
            string ax = AlignNames[Math.Max(0, Math.Min(AlignNames.Length - 1, align))];
            string ay = (ax == "Top" || ax == "Bottom") ? "Center" : "Center";
            if (ax == "Top" || ax == "Bottom") { ay = ax; ax = "Center"; }
            string esc = img;   // 直写注册表用原始路径；写 .reg 文件时才需要转义
            return "Background:=<ImageBrush Stretch=\"" + st + "\" AlignmentX=\"" + ax + "\" AlignmentY=\"" + ay + "\" Opacity=\"" + o +
                   "\" ImageSource=\"" + esc + "\" />";
        }

        /// <summary>listImg / homeImg 为 null 表示该项不设置（保持原样）；空串表示清除背景</summary>
        /// <summary>把遮罩（黑色半透明）烘焙进图片，返回新图片路径（遮罩=0 时原样返回）</summary>
        public static string MaskedCopy(string src, int maskPercent)
        {
            if (maskPercent <= 0 || string.IsNullOrEmpty(src) || !File.Exists(src)) return src;
            try
            {
                string dst = Path.Combine(Program.BaseDir, "state", "home_masked.jpg");
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                using (Image img = Image.FromFile(src))
                using (Bitmap b = new Bitmap(img.Width, img.Height))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.DrawImage(img, 0, 0, img.Width, img.Height);
                        int a = Math.Max(0, Math.Min(255, maskPercent * 255 / 100));
                        Color mc = BgApply.MaskColor();   // 浅色主题 → 白遮罩，深色主题 → 黑遮罩
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(a, mc)))
                            g.FillRectangle(br, 0, 0, img.Width, img.Height);
                    }
                    b.Save(dst, System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                return File.Exists(dst) ? dst : src;   // 生成失败就退回原图
            }
            catch { return src; }
        }

        public static bool Write(string listImg, string homeImg, int opacityPercent, int homeOpacity, int homeStretch, int homeAlign, out string err)
        {
            err = null;
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();
                sb.AppendLine("[HKEY_LOCAL_MACHINE\\" + SettingsSub.Replace("\\", "\\\\") + "]");
                if (listImg != null)
                {
                    sb.AppendLine("\"controlStyles[0].target\"=\"" + ListTarget + "\"");
                    sb.AppendLine("\"controlStyles[0].styles[0]\"=\"" + (listImg.Length == 0 ? "" : Brush(listImg, opacityPercent).Replace("\\", "\\\\")) + "\"");
                }
                if (homeImg != null)
                {
                    sb.AppendLine("\"controlStyles[1].target\"=\"" + HomeTarget + "\"");
                    sb.AppendLine("\"controlStyles[1].styles[0]\"=\"" + (homeImg.Length == 0 ? "" : Brush(homeImg, homeOpacity, homeStretch, homeAlign).Replace("\\", "\\\\")) + "\"");
                }
                // 先试直接写（进程有管理员权限时无需弹 UAC）
                try
                {
                    using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(SettingsSub, true))
                    {
                        if (k != null)
                        {
                            if (listImg != null) { k.SetValue("controlStyles[0].target", ListTarget); k.SetValue("controlStyles[0].styles[0]", listImg.Length == 0 ? "" : Brush(listImg, opacityPercent)); }
                            if (homeImg != null) { k.SetValue("controlStyles[1].target", HomeTarget); k.SetValue("controlStyles[1].styles[0]", homeImg.Length == 0 ? "" : Brush(homeImg, homeOpacity, homeStretch, homeAlign)); }
                            try
                            {
                                using (Microsoft.Win32.RegistryKey m = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Windhawk\Engine\Mods\" + ModName, true))
                                    if (m != null) m.SetValue("SettingsChangeTime", (int)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds, Microsoft.Win32.RegistryValueKind.DWord);
                            }
                            catch { }
                            return true;
                        }
                    }
                }
                catch { }

                string regPath = Path.Combine(Program.BaseDir, "state", "xamlbg.reg");
                Directory.CreateDirectory(Path.GetDirectoryName(regPath));
                File.WriteAllText(regPath, sb.ToString(), new System.Text.UnicodeEncoding(false, true));
                if (!Native.ShellExecuteW(IntPtr.Zero, "runas", "reg.exe", "import \"" + regPath + "\"", null, 0))
                { err = "需要管理员权限（提权被取消）"; return false; }
                // 触发 Windhawk 重载
                try
                {
                    using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Windhawk\Engine\Mods\" + ModName, true))
                    {
                        if (k != null) k.SetValue("SettingsChangeTime", Environment.TickCount, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
                catch { }
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }
    }

    /// <summary>自绘滑轨：红色轨道 + 红色滑块（系统 TrackBar 的白色凹槽改不掉）</summary>
    internal class RedSlider : Control
    {
        public int Minimum = 0;
        public int Maximum = 100;
        int _value;
        bool _drag;

        public event EventHandler ValueChanged;

        public RedSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 26;
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(19, 19, 24);
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        int TrackLeft { get { return 8; } }
        int TrackRight { get { return Width - 8; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int cy = Height / 2;
            int x0 = TrackLeft, x1 = TrackRight;
            if (x1 <= x0) return;
            double frac = (Maximum > Minimum) ? (double)(_value - Minimum) / (Maximum - Minimum) : 0;
            int tx = x0 + (int)Math.Round((x1 - x0) * frac);

            // 轨道（深红底）
            using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 0, 8)))
                g.FillRectangle(b, x0, cy - 2, x1 - x0, 4);
            // 已选部分（亮红）
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 0, 41)))
                g.FillRectangle(b, x0, cy - 2, Math.Max(0, tx - x0), 4);
            // 刻度
            if (Maximum > Minimum)
            {
                using (Pen tp = new Pen(Color.FromArgb(90, 255, 0, 41)))
                {
                    int ticks = 10;
                    for (int i = 0; i <= ticks; i++)
                    {
                        int x = x0 + (x1 - x0) * i / ticks;
                        g.DrawLine(tp, x, cy + 6, x, cy + (i % 5 == 0 ? 10 : 8));
                    }
                }
            }
            // 滑块
            Rectangle thumb = new Rectangle(tx - 6, cy - 10, 12, 20);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 0, 41)))
                g.FillRectangle(b, thumb);
            using (Pen bp = new Pen(Color.FromArgb(255, 120, 140), 1f))
                g.DrawRectangle(bp, thumb);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                g.FillRectangle(b, thumb.X + 3, thumb.Y + 2, 2, thumb.Height - 4);
        }

        void SetFromMouse(int mx)
        {
            int x0 = TrackLeft, x1 = TrackRight;
            if (x1 <= x0) return;
            double frac = (double)(mx - x0) / (x1 - x0);
            if (frac < 0) frac = 0;
            if (frac > 1) frac = 1;
            Value = Minimum + (int)Math.Round((Maximum - Minimum) * frac);
        }

        protected override void OnMouseDown(MouseEventArgs e) { _drag = true; SetFromMouse(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (_drag) SetFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _drag = false; base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { Value = _value + (e.Delta > 0 ? 1 : -1) * Math.Max(1, (Maximum - Minimum) / 50); base.OnMouseWheel(e); }
    }

    /// <summary>深色右键菜单配色</summary>
    internal class DarkColorTable : ProfessionalColorTable
    {
        static readonly Color BG = Color.FromArgb(30, 30, 37);
        static readonly Color RED = Color.FromArgb(255, 0, 41);
        public override Color ToolStripDropDownBackground { get { return BG; } }
        public override Color MenuItemSelected { get { return RED; } }
        public override Color MenuItemSelectedGradientBegin { get { return RED; } }
        public override Color MenuItemSelectedGradientEnd { get { return RED; } }
        public override Color MenuItemBorder { get { return RED; } }
        public override Color MenuBorder { get { return Color.FromArgb(60, 60, 72); } }
        public override Color ImageMarginGradientBegin { get { return BG; } }
        public override Color ImageMarginGradientMiddle { get { return BG; } }
        public override Color ImageMarginGradientEnd { get { return BG; } }
        public override Color SeparatorDark { get { return Color.FromArgb(60, 60, 72); } }
        public override Color SeparatorLight { get { return Color.FromArgb(60, 60, 72); } }
    }

    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(236, 236, 242);
            base.OnRenderItemText(e);
        }
    }
}
