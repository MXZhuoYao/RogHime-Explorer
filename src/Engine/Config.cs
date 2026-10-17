using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace RogHime
{
    internal class ThemeConfig
    {
        public int AutoStart = 1;              // 开机自启方式：0=关闭 1=注册表Run 2=启动文件夹 3=计划任务(登录时·最高权限)
        public int GapH = 0;                  // 边框与窗口的水平距离（左右镂空）
        public int GapV = 0;                  // 边框与窗口的垂直距离（上下镂空）
        public int BorderThickness = 9;
        public int TopBandExtra = 16;
        public int CornerCut = 12;
        public int CornerRadius = 9;
        // ---- 主题 ----
        public string Theme = "roghime";        // roghime = RO姬（深色）/ fubuki = 吹雪（浅色）
        public int ThemeAuto = 1;               // 1 = 跟随系统浅色/深色自动切换主题
        public int InstantApply = 1;            // 1 = 切换主题/背景后自动重开资源管理器窗口               // 1 = 跟随系统浅色/深色自动切换主题
        public string BorderStyle = "blade";
        public int MachiningStep = 6;
        public int BladeSpacing = 27;
        public int BladeWidth = 2;
        public int BladeHotEvery = 4;
        public int GlowStrength = 15;
        public int GlitchEffect = 1;          // 随机 glitch 故障特效（0=关闭）
        public int GlitchStrength = 85;        // 0-100 强度
        public int GlitchChroma = 5;           // 色差宽度（像素，0-200）
        public int GlitchBarW = 3;             // 故障条纹宽度（像素，1-80）
        public int GlitchArea = 0;             // 0=仅边框 1=边框+内部 2=仅内部
        public int GlitchMinGap = 4;           // 两次故障之间最短秒数（1-600）
        public int GlitchMaxGap = 10;          // 最长秒数（自动 >= 最短）
        public int GlitchDuration = 500;       // 单次持续毫秒（<120ms 几乎看不见）
        public int GlitchTrigger = 0;          // 设置界面「立即试一次」用：值一变就马上触发一次
        public int CaptionDecor = 0;
        public int CaptionLineHeight = 2;
        public int ShowBrand = 1;
        public string BrandText = "REPUBLIC OF GAMERS";
        public int BrandLogo = 1;
        public int BrandLogoScale = 110;
        public int BrandUnderline = 1;
        public int BgIndex = 2;              // 默认用「RO姬 立绘 + ROG 标志」那张                // 0=不使用 1/2/3=内置壁纸 4=自定义图片
        public string BgCustomPath = "";       // 自定义图片的源路径
        public int BgHomeIndex = 0;            // 主文件夹单独背景（0=与其它相同）
        public int BgHomeMask = 40;             // 主文件夹背景遮罩（0-100，压暗图片）—— 默认给一个能看清文字的浓度
        public int BgHomeOpacity = 100;        // 主文件夹图片不透明度（等效"遮罩/压暗"）
        public int BgHomeStretch = 0;          // 0=缩放并填充 1=缩放完整 2=拉伸 3=原始
        public int BgHomeAlign = 0;            // 0=居中 1=左上 2=上 3=左 4=右
        public int BgPosType = 6;              // 4=居中 5=缩放 6=缩放并填充
        public int BgMask = 45;                // 背景遮罩浓度 0-100 —— 默认 45%（深色主题的推荐值，和主题预设一致）
        public int BgAlpha = 255;              // 背景不透明度 0-255
        public int ApplyDwm = 1;
        public int EnforceAccent = 0;   // 默认关闭：不改系统强调色，避免整个窗口被染红
        public Color AccentColor = C("#800002");   // 默认暗红（可在设置里改；取消强制则会还原系统原色）
        public int AllWindows = 1;
        public int HideWhenMaximized = 0;
        public int PollMs = 90;
        public Color ColBase = C("#07070A");
        public Color ColBaseHi = C("#17171E");
        public Color ColBlade = C("#7E0010");
        public Color ColBladeDark = C("#170002");
        public Color ColBladeHot = C("#C4000E");
        public Color ColBladeHotDark = C("#2A0005");
        public Color ColBladeDim = C("#3A0006");
        public Color ColBladeEdge = C("#FF1929");
        public Color ColStripe = C("#FF1929");
        public Color ColGlow = C("#FF0029");
        public Color ColDeep = C("#CC000E");
        public Color ColClose = C("#FF0029");
        public Color ColCloseHover = C("#FF4C5F");
        public Color ColGlyph = C("#000000");
        public Color ColMetal = C("#4D4D4D");
        public Color ColAccent = C("#00D5FF");
        public Color ColText = C("#FFFFFF");

        public static Color C(string hex)
        {
            try { return ColorTranslator.FromHtml(hex); }
            catch { return Color.Magenta; }
        }

        public string ToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# ROG 资源管理器主题 配置文件");
            sb.AppendLine("# 修改后重启 RogHimeEngine.exe 生效（或在托盘菜单点『重新加载配置』）");
            sb.AppendLine();
            FieldInfo[] fields = typeof(ThemeConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
            {
                object v = f.GetValue(this);
                string s = v is Color ? ToHex((Color)v) : Convert.ToString(v, CultureInfo.InvariantCulture);
                sb.AppendLine(f.Name + "=" + s);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static ThemeConfig Load(string path)
        {
            ThemeConfig c = new ThemeConfig();
            if (!File.Exists(path)) return c;
            FieldInfo[] fields = typeof(ThemeConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
            string[] lines = File.ReadAllLines(path);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                foreach (FieldInfo f in fields)
                {
                    if (!string.Equals(f.Name, key, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        if (f.FieldType == typeof(int)) f.SetValue(c, int.Parse(val, CultureInfo.InvariantCulture));
                        else if (f.FieldType == typeof(Color)) f.SetValue(c, C(val));
                        else f.SetValue(c, val);
                    }
                    catch { }
                    break;
                }
            }
            return c;
        }
    }

    /// <summary>主题色板：RO姬（红黑·深色）/ 吹雪（白蓝·浅色）。
    /// 配色取自你提供的两张吹雪壁纸采样结果（#CBECF9 / #96E0F9 / #5E93B2）。</summary>
    internal static class Themes
    {
        public const string RogHime = "roghime";
        public const string Fubuki = "fubuki";

        static Color C(string s) { return ColorTranslator.FromHtml(s); }

        /// <summary>把主题色板写进配置（用户之后仍可逐项自定义）</summary>
        public static void Apply(ThemeConfig c, string theme)
        {
            if (string.Equals(theme, Fubuki, StringComparison.OrdinalIgnoreCase))
            {
                c.Theme = Fubuki;
                c.ColBase = C("#E8F5FB"); c.ColBaseHi = C("#FFFFFF");
                c.ColBlade = C("#7FC7E8"); c.ColBladeDark = C("#C3E2F0");
                c.ColBladeHot = C("#38A8DC"); c.ColBladeHotDark = C("#8FD3EE");
                c.ColBladeDim = C("#D6ECF6"); c.ColBladeEdge = C("#5FB6DE");   // 内边高光/边线：浅蓝（原来用纯白，在浅底上看不见 ✗）
                c.ColGlow = C("#6FC4E8"); c.ColDeep = C("#1E6E9C");   // 深蓝：浅色边框上的装饰线/文字用它
                c.ColClose = C("#38A8DC"); c.ColCloseHover = C("#1E7FB0");
                c.ColAccent = C("#4FA8D8");
                c.ColText = C("#2E6E96");       // 铭牌文字：冰蓝（在浅色条带上清晰）
                c.ColGlyph = C("#1E5F84");
                c.ColStripe = C("#BFE3F2"); c.ColMetal = C("#F4FBFE");
                if (string.IsNullOrEmpty(c.BorderStyle)) c.BorderStyle = "blade";  // 必须与设置界面下拉项一致（刀锋/碳纤维/纯色），否则界面与实际渲染不符 ✗
            }
            else
            {
                c.Theme = RogHime;
                c.ColBase = C("#07070A"); c.ColBaseHi = C("#17171E");
                c.ColBlade = C("#7E0010"); c.ColBladeDark = C("#170002");
                c.ColBladeHot = C("#C4000E"); c.ColBladeHotDark = C("#2A0005");
                c.ColBladeDim = C("#3A0006"); c.ColBladeEdge = C("#FF1929");
                c.ColGlow = C("#FF0029"); c.ColDeep = C("#5A000C");
                c.ColClose = C("#FF0029"); c.ColCloseHover = C("#FF4C66");
                c.ColAccent = C("#FF0029");
                c.ColText = C("#FFFFFF"); c.ColGlyph = C("#0A0A0C");
                c.ColStripe = C("#1A0004"); c.ColMetal = C("#2A2A33");
                if (string.IsNullOrEmpty(c.BorderStyle)) c.BorderStyle = "blade";  // 只在未设置时给默认值，不覆盖用户选择
            }
        }

        /// <summary>读系统是否浅色模式（AppsUseLightTheme）</summary>
        public static bool SystemIsLight()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object o = k.GetValue("AppsUseLightTheme");
                    return o != null && Convert.ToInt32(o) == 1;
                }
            }
            catch { return false; }
        }

        /// <summary>把系统和应用的浅色/深色模式切成与主题一致</summary>
        public static void ApplySystemMode(bool light)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", true))
                {
                    if (k == null) return;
                    int v = light ? 1 : 0;
                    k.SetValue("AppsUseLightTheme", v, Microsoft.Win32.RegistryValueKind.DWord);
                    k.SetValue("SystemUsesLightTheme", v, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        /// <summary>系统浅色 → 吹雪；深色 → RO姬</summary>
        public static string ForSystem() { return SystemIsLight() ? Fubuki : RogHime; }
    }
}
