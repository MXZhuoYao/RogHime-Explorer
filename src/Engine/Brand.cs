using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace RogHime
{
    /// 顶部铭牌：AsusRog 字体写的 REPUBLIC OF GAMERS + 红色 ROG 之眼。
    /// 文字用 GraphicsPath.AddString 取真实墨迹包围盒，避免 Calligraphr 字体过宽的字宽导致排版间距异常。
    internal static class Brand
    {
        public static FontFamily Family;
        public static GraphicsPath Logo;
        public static string LoadError = "";

        public static void Load(string baseDir)
        {
            try
            {
                string fp = Path.Combine(baseDir, "assets\\fonts\\AsusRog-Regular.ttf");
                if (File.Exists(fp))
                {
                    PrivateFontCollection pfc = new PrivateFontCollection();
                    pfc.AddFontFile(fp);
                    if (pfc.Families.Length > 0) Family = pfc.Families[0];
                }
                else LoadError += "font missing; ";
            }
            catch (Exception ex) { LoadError += "font:" + ex.Message + "; "; }
            try
            {
                string sp = Path.Combine(baseDir, "assets\\logo\\rog-eye.svg");
                if (File.Exists(sp))
                {
                    string d = Svg.ExtractPathData(File.ReadAllText(sp));
                    Logo = Svg.ParsePath(d);
                }
                else LoadError += "logo missing; ";
            }
            catch (Exception ex) { LoadError += "logo:" + ex.Message + "; "; }
        }

        static GraphicsPath BrandRound(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 1 || r.Width < 4 || r.Height < 4) { p.AddRectangle(r); return p; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static bool Draw(Graphics g, FrameLayout L, ThemeConfig t)
        {
            if (t.ShowBrand != 1) return false;
            int band = L.TopMargin;
            if (band < L.Px(12)) return false;

            float pad = L.Px(9);
            float gap = L.Px(8);
            float cy = band * 0.5f;

            Font f = MakeFont(L.Px(12));
            GraphicsPath tp = TextPath(t.BrandText, f);
            if (tp == null) { f.Dispose(); return false; }
            RectangleF tb = tp.GetBounds();

            float logoH = L.Px(14f) * (t.BrandLogoScale / 100f);
            float logoW = 0f;
            RectangleF lb = RectangleF.Empty;
            if (Logo != null && t.BrandLogo != 0)
            {
                lb = Logo.GetBounds();
                if (lb.Height > 0.01f) logoW = lb.Width * (logoH / lb.Height);
            }

            float totalW = tb.Width + (logoW > 0 ? gap + logoW : 0);   // 文字左、logo 右上，互不重叠
            float avail = L.Width - pad - L.Px(12);
            float shrink = (totalW > avail && totalW > 0) ? (avail / totalW) : 1f;
            if (shrink < 1f)
            {
                f.Dispose(); tp.Dispose();
                f = MakeFont(Math.Max(6f, L.Px(12) * shrink));
                tp = TextPath(t.BrandText, f);
                if (tp == null) { f.Dispose(); return false; }
                tb = tp.GetBounds();
                logoH *= shrink; logoW *= shrink;
                totalW = tb.Width + (logoW > 0 ? gap + logoW : 0);
            }

            float x = pad;
            // 深色底衬
            // 底板四周都留出边距，别顶到边框条带的边缘 ——
            // 否则圆角会伸出条带、露出一个灰色的圆弧（用户反馈）
            float pm = Math.Max(3f, L.Px(4));
            float pw = Math.Min(totalW + L.Px(9), Math.Max(L.Px(20), L.Width - x - L.Px(8)));
            float phh = Math.Max(6f, band - pm * 2);
            // 左上角要盖满：往左、往上都扩到边框条带内缘，避免窗口自身的圆角从切角处露出来
            float leftEdge = Math.Max(1f, Math.Min(x - pm, L.WinX - L.Px(1)));
            float topEdge = Math.Max(1f, pm - L.Px(2));
            float extraW = Math.Max(0f, (x - pm) - leftEdge);
            Rectangle plate = Rectangle.Round(new RectangleF(leftEdge, topEdge, pw + extraW, phh + L.Px(2)));
            try
            {
                // 底板颜色跟随主题明暗：深色主题用深底，浅色主题（吹雪）用白底，
                // 否则浅色边框上会出现一块灰斑
                bool lightTheme = (t.ColBase.R + t.ColBase.G + t.ColBase.B) > 384;
                Color plateCol = lightTheme ? Color.FromArgb(150, 255, 255, 255) : Color.FromArgb(150, 4, 4, 6);
                using (GraphicsPath bp = BrandRound(plate, L.Px(3)))
                using (SolidBrush bb = new SolidBrush(plateCol)) g.FillPath(bb, bp);
            }
            catch { }

            // 文字（按墨迹盒垂直居中）
            using (Matrix m = new Matrix())
            {
                m.Translate(-tb.X, -tb.Y, MatrixOrder.Append);
                m.Translate(x, cy - tb.Height * 0.5f, MatrixOrder.Append);
                tp.Transform(m);
            }
            using (SolidBrush wb = new SolidBrush(t.ColText)) g.FillPath(wb, tp);

            // 红色下划线
            if (t.BrandUnderline == 1)
            {
                float uy = cy + tb.Height * 0.5f + L.Px(1.4f);
                using (Pen up = new Pen(Color.FromArgb(215, t.ColGlow), Math.Max(1f, L.Px(1.6f))))
                    g.DrawLine(up, x, uy, x + tb.Width, uy);
            }

            // 红色 ROG 之眼
            if (Logo != null && t.BrandLogo != 0 && logoW > 1f)
            {
                float lx = L.Width - pad - logoW - L.Px(5);   // 右上角对齐，留出切角余量
                float ly = cy - logoH * 0.5f;
                float sc = logoH / lb.Height;
                using (Matrix m = new Matrix())
                {
                    m.Translate(-lb.X, -lb.Y, MatrixOrder.Append);
                    m.Scale(sc, sc, MatrixOrder.Append);
                    m.Translate(lx, ly, MatrixOrder.Append);
                    using (GraphicsPath p2 = (GraphicsPath)Logo.Clone())
                    {
                        p2.Transform(m);
                        using (SolidBrush rb = new SolidBrush(t.ColGlow)) g.FillPath(rb, p2);
                        using (Pen gp = new Pen(Color.FromArgb(70, t.ColGlow), Math.Max(1f, L.Px(1.8f))))
                        {
                            gp.LineJoin = LineJoin.Round;
                            g.DrawPath(gp, p2);
                        }
                    }
                }
            }
            tp.Dispose();
            f.Dispose();
            return true;
        }

        static Font MakeFont(float px)
        {
            try
            {
                if (Family != null) return new Font(Family, px, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch { }
            return new Font("Segoe UI", px * 0.82f, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        static GraphicsPath TextPath(string text, Font f)
        {
            try
            {
                GraphicsPath p = new GraphicsPath();
                p.AddString(text, f.FontFamily, (int)f.Style, f.Size, new PointF(0, 0), StringFormat.GenericTypographic);
                return p;
            }
            catch { return null; }
        }
    }
}
