using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RogHime
{
    // 覆盖层内所有坐标都以覆盖层左上角为原点
    internal class FrameLayout
    {
        public int Width;
        public int Height;
        public int Margin;              // 通用边框厚度
        public int TopMargin;           // 顶部加厚（铭牌区）
        public int WinX, WinY, WinW, WinH;      // 目标窗口在覆盖层内的矩形
        public int CaptionH;            // 标题栏高度（窗口坐标）
        public int FrameInset;          // 窗口自身不可见缩放边框
        public int Dpi;
        public int CornerRadius;
        public int CornerCut;
        public int BtnY, BtnW, BtnH;    // 三个系统按钮
        public int MinX, MaxX, CloseX;
        public int HoverButton;         // 0=无 1=最小化 2=最大化 3=关闭

        public float S { get { return Dpi / 96f; } }
        public int Px(double v) { return (int)Math.Round(v * S); }
        public Rectangle Cell(int x) { return new Rectangle(x, BtnY, BtnW, BtnH); }
    }

    /// ROG「刀锋」边框：机加工底纹 + 长短交替的刃形切面 + 四角高光切角。
    /// 刻意不用整圈发光条纹，改用分层明暗与细高光做出金属刃口的质感。
    internal static class Art
    {
        public static void Draw(Graphics g, FrameLayout L, ThemeConfig t) { Draw(g, L, t, false); }

        /// <summary>quick=true 时只画简化边框（拖动/缩放过程中用，快 3-4 倍）</summary>
        public static void Draw(Graphics g, FrameLayout L, ThemeConfig t, bool quick)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            GraphicsPath ring = BuildRing(L);

            if (quick)
            {
                // 拖动中：平铺底色即可，省掉整面渐变 + 机加工纹 + 上百片刃
                using (SolidBrush sb = new SolidBrush(t.ColBase)) g.FillPath(sb, ring);
            }
            else
            {
                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(2, L.Width), Math.Max(2, L.Height)),
                    t.ColBaseHi, t.ColBase, 135f))
                {
                    g.FillPath(bg, ring);
                }
                Region keep = g.Clip;
                g.SetClip(ring, CombineMode.Replace);
                if (t.BorderStyle == "carbon")
                {
                    bool lightTex = (t.ColBase.R + t.ColBase.G + t.ColBase.B) > 384;
                    Image tex = GetTexture(lightTex, true);
                    if (tex != null)
                    {
                        using (TextureBrush tb = new TextureBrush(tex, WrapMode.Tile))
                        {
                            // 平铺尺寸：恢复成约 320px 一块（用户确认这个大小最好看）
                            float tsc = L.Px(21f) / 100f;
                            tb.ScaleTransform(tsc, tsc);
                            g.FillPath(tb, ring);
                        }
                    }
                    else { DrawBlades(g, L, t); }
                }
                else if (t.BorderStyle == "snow")        // 吹雪：只保留霜面斜纹（雪花已按用户要求移除）
                {
                    DrawFrost(g, L, t);
                }
                else if (t.BorderStyle != "none")   // "纯色（无花纹）"时跳过花纹
                {
                    DrawMachining(g, L, t);
                    DrawBlades(g, L, t);
                }
                g.Clip = keep;
            }

            DrawInnerEdge(g, L, t, quick);
            // ★ 铭牌裁剪到边框形状内：否则切角放大时，底板会溢出成一块"白色方框"（用户反馈）
            Region keepB = g.Clip;
            g.SetClip(ring, CombineMode.Replace);
            try { Brand.Draw(g, L, t); } catch (Exception ex) { Program.Log("brand EX: " + ex.Message); }
            g.Clip = keepB;
            // ★ 角落包围线放在铭牌之后画 → 处于最上层，不会被底板盖住（用户反馈"被压在下面"）
            DrawCorners(g, L, t);
            if (t.CaptionDecor == 1) DrawCaptionLine(g, L, t);
            // 自绘窗口按钮功能已移除（用户反馈不好看、不实用）——见 TabButtons 删除记录

            ring.Dispose();
        }

        /// <summary>一帧 glitch：边框切片错位 + 红青错色 + 扫描亮条；可选作用到窗口内部</summary>
        public static void Glitch(Bitmap src, Bitmap dst, int strength, ThemeConfig t, Random rnd)
        {
            int W = dst.Width, H = dst.Height;
            if (strength < 0) strength = 0;
            if (strength > 100) strength = 100;
            int area = (t.GlitchArea < 0 || t.GlitchArea > 2) ? 0 : t.GlitchArea;
            int barW = Math.Max(1, Math.Min(10, t.GlitchBarW));
            int chroma = Math.Max(0, Math.Min(20, t.GlitchChroma));
            int maxOff = 3 + Math.Max(barW, strength * 3 / 10);   // 切片最大错位（跟着条纹宽度走）
            int slices = 5 + strength / 7;                  // 切片数

            using (Graphics g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(src, 0, 0);
                g.CompositingMode = CompositingMode.SourceOver;

                FrameLayout fl = src.Tag as FrameLayout;
                GraphicsPath ring = (fl != null) ? BuildRing(fl) : null;

                // ---------------- A. 边框部分（仅边框 / 边框+内部） ----------------
                if (area != 2 && ring != null)
                {
                    g.SetClip(ring, CombineMode.Replace);

                    for (int i = 0; i < slices; i++)
                    {
                        int sy = rnd.Next(0, Math.Max(1, H - 6));
                        int sh = Math.Max(2, barW + rnd.Next(0, 4 + strength / 5));
                        if (sy + sh > H) sh = H - sy;
                        if (sh < 2) continue;
                        int dx = rnd.Next(-maxOff, maxOff + 1);
                        if (dx == 0) dx = 1;
                        g.DrawImage(src, new Rectangle(dx, sy, W, sh), new Rectangle(0, sy, W, sh), GraphicsUnit.Pixel);
                    }

                    if (chroma > 0)
                    {
                        ColorMatrix red = new ColorMatrix(new float[][] {
                            new float[] { 1f, 0f, 0f, 0f, 0f },
                            new float[] { 0f, 0f, 0f, 0f, 0f },
                            new float[] { 0f, 0f, 0f, 0f, 0f },
                            new float[] { 0f, 0f, 0f, 0.75f, 0f },
                            new float[] { 0f, 0f, 0f, 0f, 1f } });
                        ColorMatrix cyan = new ColorMatrix(new float[][] {
                            new float[] { 0f, 0f, 0f, 0f, 0f },
                            new float[] { 0f, 1f, 0f, 0f, 0f },
                            new float[] { 0f, 0f, 1f, 0f, 0f },
                            new float[] { 0f, 0f, 0f, 0.75f, 0f },
                            new float[] { 0f, 0f, 0f, 0f, 1f } });
                        using (ImageAttributes iar = new ImageAttributes())
                        using (ImageAttributes iac = new ImageAttributes())
                        {
                            iar.SetColorMatrix(red);
                            iac.SetColorMatrix(cyan);
                            int bands = 3 + strength / 20;
                            for (int i = 0; i < bands; i++)
                            {
                                int sy = rnd.Next(0, Math.Max(1, H - 8));
                                int sh = Math.Max(3, barW + rnd.Next(0, 6 + strength / 8));
                                if (sy + sh > H) sh = H - sy;
                                if (sh < 3) continue;
                                g.DrawImage(src, new Rectangle(-chroma, sy, W, sh), 0, sy, W, sh, GraphicsUnit.Pixel, iar);
                                g.DrawImage(src, new Rectangle(chroma, sy, W, sh), 0, sy, W, sh, GraphicsUnit.Pixel, iac);
                            }
                        }
                    }

                    int bars = 2 + strength / 25;
                    for (int i = 0; i < bars; i++)
                    {
                        int by = rnd.Next(0, Math.Max(1, H - 2));
                        int bh = Math.Max(1, barW + rnd.Next(0, barW + 1));
                        int alpha = 130 + rnd.Next(0, 100);
                        Color col = (rnd.Next(3) == 0) ? t.ColGlow : Color.FromArgb(255, 255, 255, 255);
                        int x0 = rnd.Next(0, Math.Max(1, W / 4));
                        int x1 = W - rnd.Next(0, Math.Max(1, W / 4));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Min(245, alpha), col)))
                            g.FillRectangle(b, x0, by, Math.Max(4, x1 - x0), bh);
                    }
                }

                // ---------------- B. 窗口内部（边框+内部 / 仅内部） ----------------
                // 内部本身是透明的（下面是文件列表），所以这里是「叠加上去的淡淡故障条纹」，
                // 透明度压得很低，保证文件名始终看得清。
                if (area != 0)
                {
                    // 内部 = 窗口矩形，用明确的矩形裁剪，避免 Exclude 的边界歧义
                    if (fl != null) g.SetClip(new Rectangle(fl.WinX, fl.WinY, fl.WinW, fl.WinH), CombineMode.Replace);
                    else g.ResetClip();

                    int ibars = 3 + strength / 18;
                    for (int i = 0; i < ibars; i++)
                    {
                        int by = rnd.Next(0, Math.Max(1, H - 2));
                        int bh = Math.Max(1, barW + rnd.Next(0, barW * 2 + 1));
                        int alpha = 16 + rnd.Next(0, 20 + strength / 5);
                        if (alpha > 90) alpha = 90;
                        Color col;
                        int pick = rnd.Next(3);
                        if (pick == 0) col = t.ColGlow;
                        else if (pick == 1) col = t.ColAccent;
                        else col = Color.FromArgb(255, 255, 255, 255);
                        int x0 = rnd.Next(0, Math.Max(1, W / 3));
                        int x1 = W - rnd.Next(0, Math.Max(1, W / 3));
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, col)))
                            g.FillRectangle(b, x0, by, Math.Max(4, x1 - x0), bh);
                    }

                    // 红青错位的两条（色差感）
                    int pairs = 1 + strength / 45;
                    for (int i = 0; i < pairs; i++)
                    {
                        int by = rnd.Next(0, Math.Max(1, H - 4));
                        int bh = Math.Max(1, barW);
                        using (SolidBrush rb = new SolidBrush(Color.FromArgb(46, 255, 0, 60)))
                            g.FillRectangle(rb, 0, by, W, bh);
                        using (SolidBrush cb = new SolidBrush(Color.FromArgb(38, 0, 200, 255)))
                            g.FillRectangle(cb, Math.Min(W - 1, chroma), by + bh + 1, W, bh);
                    }
                }

                if (ring != null) { g.ResetClip(); ring.Dispose(); }
            }
        }

        static GraphicsPath BuildRing(FrameLayout L)
        {
            GraphicsPath outer = ChamferPath(new Rectangle(0, 0, L.Width, L.Height), L.CornerCut);
            GraphicsPath inner = RoundPath(new Rectangle(L.WinX, L.WinY, L.WinW, L.WinH), L.CornerRadius);
            GraphicsPath ring = new GraphicsPath(FillMode.Alternate);
            ring.AddPath(outer, false);
            ring.AddPath(inner, false);
            outer.Dispose(); inner.Dispose();
            return ring;
        }

        static GraphicsPath ChamferPath(Rectangle r, int cut)
        {
            GraphicsPath p = new GraphicsPath();
            if (cut <= 0) { p.AddRectangle(r); return p; }
            PointF[] pts = new PointF[]
            {
                new PointF(r.Left + cut, r.Top),
                new PointF(r.Right - cut, r.Top),
                new PointF(r.Right, r.Top + cut),
                new PointF(r.Right, r.Bottom - cut),
                new PointF(r.Right - cut, r.Bottom),
                new PointF(r.Left + cut, r.Bottom),
                new PointF(r.Left, r.Bottom - cut),
                new PointF(r.Left, r.Top + cut)
            };
            p.AddPolygon(pts);
            return p;
        }

        static GraphicsPath RoundPath(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 1 || r.Width < 4 || r.Height < 4) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void DrawMachining(Graphics g, FrameLayout L, ThemeConfig t)
        {
            int step = Math.Max(3, L.Px(t.MachiningStep));
            using (Pen p = new Pen(Color.FromArgb(11, 255, 255, 255), 1f))
            {
                for (int x = -L.Height; x < L.Width + L.Height; x += step)
                    g.DrawLine(p, x, 0, x + (int)(L.Height * 0.4), L.Height);
            }
            using (Pen p = new Pen(Color.FromArgb(26, 0, 0, 0), 1f))
            {
                for (int x = -L.Height + step / 2; x < L.Width + L.Height; x += step)
                    g.DrawLine(p, x, 0, x + (int)(L.Height * 0.4), L.Height);
            }
        }

        /// <summary>吹雪：霜面渐变（浅色底 + 淡蓝斜纹）</summary>
        // 材质缓存：按「文件路径」分别缓存 —— 之前只写了两个槽位，
        // 导致星辰/碳纤维互相覆盖，切风格看不出区别 ✗
        static readonly System.Collections.Generic.Dictionary<string, Image> _texCache =
            new System.Collections.Generic.Dictionary<string, Image>();

        internal static Image GetTexture(bool light) { return GetTexture(light, false); }

        internal static Image GetTexture(bool light, bool carbon)
        {
            try
            {
                string name = carbon ? (light ? "carbon_light.png" : "carbon_red.png")
                                     : (light ? "carbon_light.png" : "carbon_red.png");
                string p = System.IO.Path.Combine(Program.BaseDir, "assets", "patterns", name);
                if (!System.IO.File.Exists(p)) return null;
                if (_texCache.ContainsKey(p)) return _texCache[p];
                Image im = Image.FromFile(p);
                _texCache[p] = im;
                return im;
            }
            catch { return null; }
        }


        static void DrawFrost(Graphics g, FrameLayout L, ThemeConfig t)
        {
            int step = Math.Max(8, L.Px(t.MachiningStep * 2));
            using (Pen p = new Pen(Color.FromArgb(38, t.ColBlade)))
            {
                p.Width = Math.Max(1f, L.Px(1));
                for (int i = -L.Height; i < L.Width; i += step) g.DrawLine(p, i, 0, i + L.Height, L.Height);
            }
        }

        /// <summary>吹雪：六芒雪花（程序化绘制，无需素材）</summary>

        /// <summary>画一朵六芒雪花（带分支）</summary>

        static void DrawBlades(Graphics g, FrameLayout L, ThemeConfig t)
        {
            int step = Math.Max(6, L.Px(t.BladeSpacing));
            int w = Math.Max(2, L.Px(t.BladeWidth));
            float slope = 0.4f;
            float over = L.Height + 8;
            int idx = 0;
            // 三种刃型的画刷/画笔只建一次，循环里复用（之前在循环内 new，拖窗口缩放时很慢）
            int gh = Math.Max(2, L.Height);
            Color[] c0s = new Color[3];
            Color[] c1s = new Color[3];
            Color[] egs = new Color[3];
            int[] eas = new int[3];
            c0s[1] = t.ColBlade;     c1s[1] = t.ColBladeDark; egs[1] = t.ColBladeEdge; eas[1] = 175;
            c0s[0] = t.ColBladeDim;  c1s[0] = t.ColBladeDark; egs[0] = t.ColBladeEdge; eas[0] = 85;
            // 第 3 种刃片原来是写死的近黑色，在浅色主题上显得又重又密 ✗
            // 改成跟随主题：深色主题保持原样，浅色主题用浅蓝灰
            bool lightBlade = (t.ColBase.R + t.ColBase.G + t.ColBase.B) > 384;
            c0s[2] = lightBlade ? Color.FromArgb(255, 206, 228, 240) : Color.FromArgb(255, 30, 30, 37);
            c1s[2] = lightBlade ? Color.FromArgb(255, 176, 208, 228) : Color.FromArgb(255, 12, 12, 16);
            egs[2] = t.ColBladeEdge;
            eas[2] = lightBlade ? 22 : 34;   // 浅色主题再降一点存在感

            LinearGradientBrush[] brs = new LinearGradientBrush[3];
            Pen[] eps = new Pen[3];
            for (int i = 0; i < 3; i++)
            {
                brs[i] = new LinearGradientBrush(new Rectangle(0, 0, 1, gh), c0s[i], c1s[i], 90f);
                eps[i] = new Pen(Color.FromArgb(eas[i], egs[i]), Math.Max(1f, L.Px(0.9f)));
            }
            Pen shadow = new Pen(Color.FromArgb(120, 0, 0, 0), 1f);
            PointF[] pts = new PointF[4];
            try
            {
                for (int x = -L.Height; x < L.Width + L.Height; x += step, idx++)
                {
                    // 绝大多数刃是「暗金属 + 亮刃口」，每隔几片才有一片深红刃，整体不再发红
                    int v = (t.BladeHotEvery > 0) ? (idx % t.BladeHotEvery) : 2;
                    if (v > 2 || v < 0) v = 2;

                    pts[0] = new PointF(x, -4);
                    pts[1] = new PointF(x + w, -4);
                    pts[2] = new PointF(x + w + slope * over, over);
                    pts[3] = new PointF(x + slope * over, over);

                    g.FillPolygon(brs[v], pts);
                    g.DrawLine(eps[v], pts[0], pts[3]);
                    g.DrawLine(shadow, pts[1], pts[2]);
                }
            }
            finally
            {
                for (int i = 0; i < 3; i++) { brs[i].Dispose(); eps[i].Dispose(); }
                shadow.Dispose();
            }
        }

        static void DrawInnerEdge(Graphics g, FrameLayout L, ThemeConfig t) { DrawInnerEdge(g, L, t, false); }

        static void DrawInnerEdge(Graphics g, FrameLayout L, ThemeConfig t, bool quick)
        {
            Rectangle inner = new Rectangle(L.WinX, L.WinY, L.WinW, L.WinH);
            using (GraphicsPath ip = RoundPath(inner, L.CornerRadius))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(inner.X - 1, inner.Y - 1, inner.Width + 2, inner.Height + 2),
                    Color.FromArgb(240, 255, 236, 240), Color.FromArgb(150, t.ColDeep), 62f))
                using (Pen p = new Pen(b, Math.Max(1f, L.Px(1.15f))))
                {
                    p.Alignment = PenAlignment.Inset;
                    g.DrawPath(p, ip);
                }
                if (t.GlowStrength > 0 && !quick)
                {
                    int st = Math.Max(10, Math.Min(160, t.GlowStrength));
                    int[] widths = new int[] { L.Px(7), L.Px(4), L.Px(2) };
                    int[] alphas = new int[] { 16, 34, 62 };
                    for (int i = 0; i < widths.Length; i++)
                    {
                        int a = alphas[i] * st / 100;
                        if (a <= 0) continue;
                        using (Pen gp = new Pen(Color.FromArgb(Math.Min(200, a), t.ColGlow), Math.Max(1f, widths[i])))
                        {
                            gp.LineJoin = LineJoin.Round;
                            g.DrawPath(gp, ip);
                        }
                    }
                }
                Rectangle in2 = Rectangle.Inflate(inner, -L.Px(2), -L.Px(2));
                using (GraphicsPath ip2 = RoundPath(in2, Math.Max(2, L.CornerRadius - 2)))
                using (Pen p = new Pen(Color.FromArgb(130, 0, 0, 0), 1f))
                    g.DrawPath(p, ip2);
            }
        }

        static void DrawCorners(Graphics g, FrameLayout L, ThemeConfig t)
        {
            int c = L.CornerCut;
            float inset = Math.Max(2f, L.Px(2.5f));
            float len = L.Px(20);
            float off = c * 0.62f;
            // 角落线的颜色要跟随主题：深色主题用亮色描边（红），
            // 浅色主题（吹雪）ColBladeEdge 是白色，在白边框上完全看不见 → 改用深蓝
            bool lightCorner = (t.ColBase.R + t.ColBase.G + t.ColBase.B) > 384;
            // 角落线和内边高光用同一种颜色，否则同一处会出现"几种颜色"（用户反馈）
            Color cornerCol = t.ColBladeEdge;
            float cw = lightCorner ? Math.Max(1.8f, L.Px(2.2f)) : Math.Max(1.4f, L.Px(1.7f));   // 加粗一档，更有存在感
            using (Pen p = new Pen(cornerCol, cw))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, inset + off, inset, inset + off + len, inset);
                g.DrawLine(p, inset, inset + off, inset, inset + off + len);
                g.DrawLine(p, L.Width - inset - off - len, inset, L.Width - inset - off, inset);
                g.DrawLine(p, L.Width - inset, inset + off, L.Width - inset, inset + off + len);
                g.DrawLine(p, inset, L.Height - inset - off - len, inset, L.Height - inset - off);
                g.DrawLine(p, inset + off, L.Height - inset, inset + off + len, L.Height - inset);
                g.DrawLine(p, L.Width - inset, L.Height - inset - off - len, L.Width - inset, L.Height - inset - off);
                g.DrawLine(p, L.Width - inset - off - len, L.Height - inset, L.Width - inset - off, L.Height - inset);
            }
            using (Pen p = new Pen(Color.FromArgb(235, t.ColBladeEdge), Math.Max(1.3f, L.Px(1.7f))))
            {
                // 45° 斜段：长度必须限制在"边框条带以内"，太长会越过切角伸进窗口里（穿模 ✗）
                float d = Math.Min(Math.Max(off, c * 0.5f), L.Margin * 0.8f);
                g.DrawLine(p, inset, inset + d, inset + d, inset);                                                    // 左上
                g.DrawLine(p, L.Width - inset - d, inset, L.Width - inset, inset + d);                                // 右上
                g.DrawLine(p, inset, L.Height - inset - d, inset + d, L.Height - inset);                              // 左下
                g.DrawLine(p, L.Width - inset - d, L.Height - inset, L.Width - inset, L.Height - inset - d);          // 右下
            }
            using (Pen p = new Pen(Color.FromArgb(200, t.ColAccent), Math.Max(1f, L.Px(1.5f))))
            {
                // 右上/左下那两条 8px 短线已按用户要求移除（只保留沿切角的长包围线）
            }
        }

        static void DrawCaptionLine(Graphics g, FrameLayout L, ThemeConfig t)
        {
            int y = L.WinY + L.CaptionH - L.Px(t.CaptionLineHeight);
            int x0 = L.WinX + L.CornerRadius;
            int x1 = L.MinX - L.Px(6);
            if (x1 < x0 + 10) x1 = L.WinX + L.WinW - L.CornerRadius;
            int h = Math.Max(1, L.Px(t.CaptionLineHeight));
            Rectangle r = new Rectangle(x0, y, Math.Max(1, x1 - x0), h);
            using (LinearGradientBrush lg = new LinearGradientBrush(
                new Rectangle(r.X - 1, r.Y, r.Width + 2, r.Height), Color.Transparent, t.ColGlow, LinearGradientMode.Horizontal))
            {
                ColorBlend cb = new ColorBlend(4);
                cb.Colors = new Color[] { Color.Transparent, t.ColGlow, t.ColAccent, Color.Transparent };
                cb.Positions = new float[] { 0f, 0.45f, 0.68f, 1f };
                lg.InterpolationColors = cb;
                g.FillRectangle(lg, r);
            }
        }


    }
}
