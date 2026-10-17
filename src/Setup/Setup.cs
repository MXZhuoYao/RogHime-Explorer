using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace RogHimeSetup
{
    /// <summary>
    /// ROG主题 安装向导 —— 经典分页式（欢迎 / 安装位置 / 附加任务 / 正在安装 / 完成）。
    /// 布局参考 Inno Setup 与 NSIS Modern UI：左侧品牌栏 + 右侧内容区 + 底部按钮栏。
    /// </summary>
    internal static class Program
    {
        // ── 配色（与主题一致的红黑）
        static readonly Color BG     = Color.FromArgb(16, 16, 20);
        static readonly Color CARD   = Color.FromArgb(22, 22, 27);
        static readonly Color SIDE   = Color.FromArgb(10, 10, 13);
        static readonly Color RED    = Color.FromArgb(255, 0, 41);
        static readonly Color REDDK  = Color.FromArgb(120, 0, 20);
        static readonly Color TXT    = Color.FromArgb(238, 238, 244);
        static readonly Color DIM    = Color.FromArgb(150, 150, 164);
        static readonly Color LINE   = Color.FromArgb(44, 44, 52);

        static Form _form;
        static Panel _content;         // 右侧内容区（分页承载）
        static Panel _side;            // 左侧品牌栏
        static Label _pageTitle, _pageDesc;
        static Button _btnNext, _btnBack, _btnCancel;
        static ProgressBar _bar;
        static TextBox _log;

        static int _page;              // 0..4
        static string _installDir;
        static TextBox _dirBox;
        static int _autostart = 1;     // 0关闭 1注册表 2启动文件夹 3计划任务
        static bool _regExt = true;

        static readonly string[] Titles =
        {
            "欢迎使用 ROG 资源管理器主题 安装向导",
            "选择安装位置",
            "选择附加任务",
            "正在安装，请稍候",
            "ROG 资源管理器主题 安装完成"
        };
        static readonly string[] Descs =
        {
            "这个向导会引导你把红黑 ROG 主题安装到电脑上，大约需要几秒钟。",
            "选择安装位置。默认即可，想换就点「浏览」。",
            "这些之后都能在设置里改。",
            "正在复制文件并注册资源管理器扩展。",
            "一切就绪，资源管理器窗口已经换上主题了。"
        };

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr value);   // Win10 1607+

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RogHimeTheme");

            _form = new Form();
            _form.Text = "ROG 资源管理器主题 安装向导";
            _form.FormBorderStyle = FormBorderStyle.None;
            _form.StartPosition = FormStartPosition.CenterScreen;
            // 标准 WinForms 做法：按设计基准 96 DPI 自动缩放全部子控件
            _form.AutoScaleDimensions = new SizeF(96f, 96f);
            _form.AutoScaleMode = AutoScaleMode.Dpi;
            _form.ClientSize = new Size(640, 420);
            _form.BackColor = BG;
            _form.ForeColor = TXT;
            try { _form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildTitleBar();
            BuildSideBar();
            BuildContent();
            BuildButtons();
            ShowPage(0);

            Application.Run(_form);
        }

        // ────────────────────────────────────────────── 标题栏
        static void BuildTitleBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = SIDE };
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Font f = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(DIM))
                    e.Graphics.DrawString("ROG 资源管理器主题  ·  安装向导", f, b, 14, 10);
                using (Pen p = new Pen(LINE)) e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };
            Label x = new Label
            {
                Text = "✕", AutoSize = false, Size = new Size(46, 30), TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DIM, Cursor = Cursors.Hand, BackColor = SIDE
            };
            x.Click += delegate { Application.Exit(); };
            x.MouseEnter += delegate { x.BackColor = RED; x.ForeColor = Color.White; };
            x.MouseLeave += delegate { x.BackColor = SIDE; x.ForeColor = DIM; };
            bar.Controls.Add(x);
            bar.Resize += delegate { x.Location = new Point(bar.ClientSize.Width - 50, 4); };
            bool drag = false; Point pt = Point.Empty;
            bar.MouseDown += delegate(object s, MouseEventArgs e) { drag = true; pt = e.Location; };
            bar.MouseMove += delegate(object s, MouseEventArgs e) { if (drag) _form.Location = new Point(_form.Left + e.X - pt.X, _form.Top + e.Y - pt.Y); };
            bar.MouseUp += delegate { drag = false; };
            _form.Controls.Add(bar);
        }

        // ────────────────────────────────────────────── 左侧品牌栏
        static void BuildSideBar()
        {
            _side = new Panel { Dock = DockStyle.Left, Width = 176, BackColor = SIDE };
            _side.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // 斜向红纹（呼应主题）
                using (Pen p = new Pen(Color.FromArgb(26, RED), 3))
                    for (int i = -_side.Height; i < _side.Width + 40; i += 16)
                        g.DrawLine(p, i, _side.Height, i + _side.Height, 0);
                // 品牌文字
                using (Font f = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold))
                using (SolidBrush b = new SolidBrush(RED))
                    g.DrawString("ROG", f, b, 18, 34);
                using (Font f = new Font("Microsoft YaHei UI", 9f))
                using (SolidBrush b = new SolidBrush(DIM))
                {
                    g.DrawString("资源管理器主题", f, b, 20, 72);
                    g.DrawString("红黑 · ROG 风格", f, b, 20, 92);
                }
                using (Pen p = new Pen(RED, 2)) g.DrawLine(p, 18, 122, 62, 122);
                using (Font f = new Font("Consolas", 9f))
                using (SolidBrush b = new SolidBrush(DIM))
                    g.DrawString("v2.2.0", f, b, 20, 136);
                // 右侧分隔线
                using (Pen p = new Pen(LINE)) g.DrawLine(p, _side.Width - 1, 0, _side.Width - 1, _side.Height);
            };

            // 步骤指示器
            Label steps = new Label
            {
                AutoSize = false, Size = new Size(150, 150), Location = new Point(18, 200),
                ForeColor = DIM, BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 9f)
            };
            _side.Controls.Add(steps);
            _side.Tag = steps;
            _form.Controls.Add(_side);
        }

        static void UpdateSteps()
        {
            Label steps = _side.Tag as Label;
            if (steps == null) return;
            string[] names = { "欢迎", "安装位置", "附加任务", "正在安装", "完成" };
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Length; i++)
                sb.Append(i == _page ? "●  " : (i < _page ? "✓  " : "○  ")).Append(names[i]).Append("\r\n\r\n");
            steps.Text = sb.ToString();
            steps.ForeColor = DIM;
        }

        // ────────────────────────────────────────────── 右侧内容区
        static void BuildContent()
        {
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = BG };
            _form.Controls.Add(right);
            right.BringToFront();

            _pageTitle = new Label
            {
                AutoSize = false, Location = new Point(26, 26), Size = new Size(410, 28),
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold), ForeColor = TXT, BackColor = Color.Transparent
            };
            _pageDesc = new Label
            {
                AutoSize = false, Location = new Point(27, 60), Size = new Size(410, 40),
                Font = new Font("Microsoft YaHei UI", 9f), ForeColor = DIM, BackColor = Color.Transparent
            };
            right.Controls.Add(_pageTitle);
            right.Controls.Add(_pageDesc);

            _content = new Panel { Location = new Point(24, 108), Size = new Size(416, 210), BackColor = BG };
            right.Controls.Add(_content);

            // 进度条与日志（第 4 页用）
            _bar = new ProgressBar { Location = new Point(0, 0), Size = new Size(416, 16), Style = ProgressBarStyle.Continuous, Value = 0 };
            _log = new TextBox
            {
                Location = new Point(0, 26), Size = new Size(416, 178), Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Vertical, BackColor = CARD, ForeColor = DIM, BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9f)
            };
        }

        // ────────────────────────────────────────────── 底部按钮栏
        static void BuildButtons()
        {
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = CARD };
            bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(LINE)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
            };
            _btnCancel = MakeBtn("取消", 92, false);
            _btnBack   = MakeBtn("上一步", 92, false);
            _btnNext   = MakeBtn("下一步", 104, true);
            _btnNext.Location = new Point(bottom.Width - 118, 12);
            _btnBack.Location = new Point(bottom.Width - 218, 12);
            _btnCancel.Location = new Point(bottom.Width - 318, 12);
            _btnNext.Click   += delegate { NextPage(); };
            _btnBack.Click   += delegate { ShowPage(_page - 1); };
            _btnCancel.Click += delegate { Application.Exit(); };
            bottom.Controls.Add(_btnNext);
            bottom.Controls.Add(_btnBack);
            bottom.Controls.Add(_btnCancel);
            bottom.Resize += delegate
            {
                _btnNext.Location = new Point(bottom.ClientSize.Width - 118, 12);
                _btnBack.Location = new Point(bottom.ClientSize.Width - 218, 12);
                _btnCancel.Location = new Point(bottom.ClientSize.Width - 318, 12);
            };
            _form.Controls.Add(bottom);
        }

        static Button MakeBtn(string text, int w, bool primary)
        {
            Button b = new Button
            {
                Text = text, AutoSize = false, Size = new Size(w, 34), FlatStyle = FlatStyle.Flat,
                BackColor = primary ? RED : BG, ForeColor = primary ? Color.White : TXT,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = LINE;
            return b;
        }

        // ────────────────────────────────────────────── 分页
        static void ShowPage(int p)
        {
            _page = Math.Max(0, Math.Min(4, p));
            _pageTitle.Text = Titles[_page];
            _pageDesc.Text = Descs[_page];
            _content.Controls.Clear();
            switch (_page)
            {
                case 0: BuildWelcomePage(); break;
                case 1: BuildDirPage(); break;
                case 2: BuildTaskPage(); break;
                case 3: _content.Controls.Add(_bar); _content.Controls.Add(_log); break;
            }
            _btnBack.Enabled = (_page >= 1 && _page <= 2);
            _btnBack.Visible = (_page <= 2);
            _btnNext.Text = _page == 4 ? "完成" : (_page == 0 ? "开始安装" : "下一步");
            _btnNext.Visible = (_page != 3);
            _btnCancel.Visible = (_page <= 2);
            UpdateSteps();
        }

        static void BuildWelcomePage()
        {
            // 首页保持干净：只留一句引导 ✓
            Label tip = new Label
            {
                Text = "点击「开始安装」继续。",
                Location = new Point(0, 40), AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 10f), ForeColor = TXT
            };
            _content.Controls.Add(tip);
        }

        static void BuildDirPage()
        {
            Label l = new Label { Text = "目标文件夹", Location = new Point(0, 6), AutoSize = true, ForeColor = DIM };
            _content.Controls.Add(l);

            _dirBox = new TextBox
            {
                Text = _installDir, Location = new Point(0, 32), Width = 316, BackColor = CARD, ForeColor = TXT,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 9.5f)
            };
            _dirBox.TextChanged += delegate { _installDir = _dirBox.Text; };
            _content.Controls.Add(_dirBox);

            Button b = MakeBtn("浏览…", 88, false);
            b.Location = new Point(326, 31);
            b.Click += delegate
            {
                using (FolderBrowserDialog fb = new FolderBrowserDialog())
                {
                    fb.Description = "选择安装位置";
                    fb.SelectedPath = _installDir;
                    if (fb.ShowDialog() == DialogResult.OK) _dirBox.Text = fb.SelectedPath;
                }
            };
            _content.Controls.Add(b);

            // 说明从简：默认位置已经是最优解，不需要长篇解释 ✓
        }

        static void BuildTaskPage()
        {
            Label l = new Label { Text = "附加任务", Location = new Point(0, 0), AutoSize = true, ForeColor = DIM };
            _content.Controls.Add(l);

            CheckBox c1 = new CheckBox
            {
                Text = "开机自动启动（推荐）", Location = new Point(4, 30), AutoSize = true, ForeColor = TXT,
                Checked = _autostart != 0, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9.5f)
            };
            c1.CheckedChanged += delegate { _autostart = c1.Checked ? 1 : 0; };
            _content.Controls.Add(c1);

            Label l2 = new Label { Text = "启动方式", Location = new Point(4, 66), AutoSize = true, ForeColor = DIM };
            _content.Controls.Add(l2);

            ComboBox cb = new ComboBox
            {
                Location = new Point(4, 88), Width = 380, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CARD, ForeColor = TXT, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9.5f)
            };
            cb.Items.AddRange(new object[]
            {
                "注册表启动项（推荐 · 免管理员）",
                "启动文件夹（用户可见可删）",
                "计划任务 · 登录时（最高权限）"
            });
            cb.SelectedIndex = Math.Max(0, Math.Min(2, _autostart - 1));
            cb.SelectedIndexChanged += delegate { if (_autostart != 0) _autostart = cb.SelectedIndex + 1; };
            cb.Enabled = _autostart != 0;
            c1.CheckedChanged += delegate { cb.Enabled = _autostart != 0; };
            _content.Controls.Add(cb);

            CheckBox c2 = new CheckBox
            {
                Text = "注册资源管理器背景扩展（文件列表背景图）", Location = new Point(4, 132), AutoSize = true,
                ForeColor = TXT, Checked = true, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9.5f)
            };
            c2.CheckedChanged += delegate { _regExt = c2.Checked; };
            _content.Controls.Add(c2);
        }

        static void NextPage()
        {
            if (_page == 0) { ShowPage(1); return; }
            if (_page == 1)
            {
                if (string.IsNullOrWhiteSpace(_installDir)) { MessageBox.Show("请先选择安装位置。", "ROG主题"); return; }
                ShowPage(2); return;
            }
            if (_page == 2) { ShowPage(3); DoInstall(); return; }
            if (_page == 4) { Application.Exit(); return; }
            ShowPage(_page + 1);
        }

        // ────────────────────────────────────────────── 安装
        static void DoInstall()
        {
            try
            {
                Log("准备安装包 ...");
                string zip = PayloadZip();
                if (zip == null) { Log("✗ 安装包损坏：找不到内嵌数据"); return; }

                string tmp = Path.Combine(Path.GetTempPath(), "RogHimeSetup_" + DateTime.Now.Ticks);
                Directory.CreateDirectory(tmp);
                Log("解压到临时目录 ...");
                ZipFile.ExtractToDirectory(zip, tmp);
                File.Delete(zip);
                Progress(15);

                string ps = Path.Combine(tmp, "install.ps1");
                if (!File.Exists(ps)) { Log("✗ 安装包里没有 install.ps1"); return; }

                Log("安装位置：" + _installDir);
                Log("启动方式：" + (_autostart == 0 ? "关闭" : new[] { "", "注册表启动项", "启动文件夹", "计划任务" }[_autostart]));
                Progress(35);

                System.Text.StringBuilder args = new System.Text.StringBuilder();
                args.Append("-NoProfile -ExecutionPolicy Bypass -File \"").Append(ps).Append("\"");
                args.Append(" -InstallDir \"").Append(_installDir).Append("\"");
                if (_autostart == 0) args.Append(" -NoAutostart");
                if (!_regExt) args.Append(" -NoShellExt");

                Log("正在复制文件并注册扩展（可能需要几秒）...");
                ProcessStartInfo psi = new ProcessStartInfo("powershell", args.ToString());
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    string e = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    foreach (string line in o.Split('\n'))
                        if (line.Trim().Length > 0) Log("  " + line.Trim());
                    if (e.Trim().Length > 0) Log("  [stderr] " + e.Trim());
                    Progress(100);
                    if (p.ExitCode == 0)
                    {
                        Log("");
                        Log("✓ 安装完成");
                        ShowPage(4);
                    }
                    else
                    {
                        Log("");
                        Log("✗ 安装脚本退出码：" + p.ExitCode);
                        MessageBox.Show("安装过程中出现问题，退出码 " + p.ExitCode + "。\r\n详情见上方日志。", "ROG主题",
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex) { Log("✗ " + ex.Message); }
        }

        static void Log(string t)
        {
            _log.AppendText(t + "\r\n");
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            Application.DoEvents();
        }

        static void Progress(int v)
        {
            _bar.Value = Math.Max(0, Math.Min(100, v));
            Application.DoEvents();
        }

        static string PayloadZip()
        {
            Assembly a = Assembly.GetExecutingAssembly();
            foreach (string n in a.GetManifestResourceNames())
            {
                if (n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "RogHimeSetup_" + DateTime.Now.Ticks + ".zip");
                    using (Stream s = a.GetManifestResourceStream(n))
                    using (FileStream fs = new FileStream(tmp, FileMode.Create))
                        s.CopyTo(fs);
                    return tmp;
                }
            }
            return null;
        }
    }
}
