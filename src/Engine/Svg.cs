using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace RogHime
{
    /// 极简 SVG path 解析器：只支持 M/L/H/V/C/S/Q/Z（大小写），足够渲染 simple-icons 的单路径图标。
    internal static class Svg
    {
        public static string ExtractPathData(string svgText)
        {
            int i = svgText.IndexOf("<path", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            int j = svgText.IndexOf("d=\"", i, StringComparison.Ordinal);
            if (j < 0) return null;
            j += 3;
            int k = svgText.IndexOf('"', j);
            if (k < 0) return null;
            return svgText.Substring(j, k - j);
        }

        static void SkipSep(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
        }

        static double Num(string s, ref int i)
        {
            SkipSep(s, ref i);
            int start = i;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            bool dot = false;
            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9') { i++; continue; }
                if (c == '.' && !dot) { dot = true; i++; continue; }
                if ((c == 'e' || c == 'E') && i + 1 < s.Length)
                {
                    i++;
                    if (s[i] == '+' || s[i] == '-') i++;
                    continue;
                }
                break;
            }
            if (i == start) { i++; return 0; }
            double v;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            return v;
        }

        public static GraphicsPath ParsePath(string d)
        {
            GraphicsPath p = new GraphicsPath(FillMode.Winding);
            if (string.IsNullOrEmpty(d)) return p;
            int i = 0;
            char cmd = ' ';
            double x = 0, y = 0, sx = 0, sy = 0, cx = 0, cy = 0;
            char prev = ' ';
            while (i < d.Length)
            {
                SkipSep(d, ref i);
                if (i >= d.Length) break;
                char ch = d[i];
                if (char.IsLetter(ch)) { cmd = ch; i++; }
                else if (cmd == ' ') { i++; continue; }
                char u = char.ToUpperInvariant(cmd);
                bool rel = char.IsLower(cmd);
                switch (u)
                {
                    case 'M':
                        {
                            double nx = Num(d, ref i), ny = Num(d, ref i);
                            if (rel) { nx += x; ny += y; }
                            x = nx; y = ny; sx = x; sy = y;
                            p.StartFigure();
                            cmd = rel ? 'l' : 'L';
                            break;
                        }
                    case 'L':
                        {
                            double nx = Num(d, ref i), ny = Num(d, ref i);
                            if (rel) { nx += x; ny += y; }
                            p.AddLine((float)x, (float)y, (float)nx, (float)ny);
                            x = nx; y = ny;
                            break;
                        }
                    case 'H':
                        {
                            double nx = Num(d, ref i);
                            if (rel) nx += x;
                            p.AddLine((float)x, (float)y, (float)nx, (float)y);
                            x = nx;
                            break;
                        }
                    case 'V':
                        {
                            double ny = Num(d, ref i);
                            if (rel) ny += y;
                            p.AddLine((float)x, (float)y, (float)x, (float)ny);
                            y = ny;
                            break;
                        }
                    case 'C':
                        {
                            double x1 = Num(d, ref i), y1 = Num(d, ref i);
                            double x2 = Num(d, ref i), y2 = Num(d, ref i);
                            double nx = Num(d, ref i), ny = Num(d, ref i);
                            if (rel) { x1 += x; y1 += y; x2 += x; y2 += y; nx += x; ny += y; }
                            p.AddBezier((float)x, (float)y, (float)x1, (float)y1, (float)x2, (float)y2, (float)nx, (float)ny);
                            cx = x2; cy = y2; x = nx; y = ny;
                            break;
                        }
                    case 'S':
                        {
                            double x2 = Num(d, ref i), y2 = Num(d, ref i);
                            double nx = Num(d, ref i), ny = Num(d, ref i);
                            if (rel) { x2 += x; y2 += y; nx += x; ny += y; }
                            double x1 = x, y1 = y;
                            if (prev == 'C' || prev == 'S') { x1 = 2 * x - cx; y1 = 2 * y - cy; }
                            p.AddBezier((float)x, (float)y, (float)x1, (float)y1, (float)x2, (float)y2, (float)nx, (float)ny);
                            cx = x2; cy = y2; x = nx; y = ny;
                            break;
                        }
                    case 'Q':
                        {
                            double x1 = Num(d, ref i), y1 = Num(d, ref i);
                            double nx = Num(d, ref i), ny = Num(d, ref i);
                            if (rel) { x1 += x; y1 += y; nx += x; ny += y; }
                            p.AddBezier((float)x, (float)y, (float)x1, (float)y1, (float)x1, (float)y1, (float)nx, (float)ny);
                            cx = x1; cy = y1; x = nx; y = ny;
                            break;
                        }
                    case 'Z':
                        p.CloseFigure();
                        x = sx; y = sy;
                        break;
                    default:
                        i++;
                        break;
                }
                prev = u;
            }
            return p;
        }
    }
}
