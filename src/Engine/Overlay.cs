using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RogHime
{
    /// 一个完全自绘、鼠标穿透、永不获取焦点的分层窗口。
    internal class OverlayWindow : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
        {
            public uint cbSize; public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
            public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int exStyle, string cls, string title, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref Native.BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        static ushort _atom;
        static WndProcDelegate _proc = new WndProcDelegate(StaticProc);

        static IntPtr StaticProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public IntPtr Handle;
        int _w, _h;

        public OverlayWindow()
        {
            if (_atom == 0)
            {
                WNDCLASSEX wc = new WNDCLASSEX();
                wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
                wc.style = 0;
                wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc);
                wc.hInstance = GetModuleHandle(null);
                wc.lpszClassName = "RogHimeOverlayWnd";
                _atom = RegisterClassEx(ref wc);
                if (_atom == 0) throw new Exception("RegisterClassEx failed");
            }
            int ex = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
            Handle = CreateWindowEx(ex, "RogHimeOverlayWnd", "RogHime", Native.WS_POPUP, 0, 0, 8, 8, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (Handle == IntPtr.Zero) throw new Exception("CreateWindowEx failed");
        }

        /// 上传一张新的位图内容并设置位置/大小
        public void SetContent(Bitmap bmp, int x, int y)
        {
            _w = bmp.Width; _h = bmp.Height;
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            Native.BITMAPINFO bmi = new Native.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = _w;
            bmi.bmiHeader.biHeight = -_h;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;
            IntPtr bits;
            IntPtr hBmp = CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
            IntPtr old = SelectObject(memDc, hBmp);
            using (Bitmap target = new Bitmap(_w, _h, _w * 4, PixelFormat.Format32bppPArgb, bits))
            {
                using (Graphics g = Graphics.FromImage(target))
                {
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    g.DrawImageUnscaled(bmp, 0, 0);
                }
            }
            Native.POINT dst = new Native.POINT(); dst.X = x; dst.Y = y;
            Native.SIZE size = new Native.SIZE(); size.cx = _w; size.cy = _h;
            Native.POINT src = new Native.POINT(); src.X = 0; src.Y = 0;
            Native.BLENDFUNCTION bf = new Native.BLENDFUNCTION();
            bf.BlendOp = Native.AC_SRC_OVER; bf.BlendFlags = 0; bf.SourceConstantAlpha = 255; bf.AlphaFormat = Native.AC_SRC_ALPHA;
            Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, Native.ULW_ALPHA);
            SelectObject(memDc, old);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }

        public void MoveTo(int x, int y)
        {
            Native.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }

        public void Show()
        {
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        }

        public void Hide()
        {
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_HIDEWINDOW);
        }

        const uint GW_HWNDPREV = 3;

        /// 把覆盖层插到目标窗口正上方（目标之上、目标上层窗口之下）
        public void StackAbove(IntPtr target)
        {
            IntPtr prev = Native.GetWindow(target, GW_HWNDPREV);
            IntPtr after = (prev == IntPtr.Zero) ? IntPtr.Zero : prev;
            Native.SetWindowPos(Handle, after, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) { DestroyWindow(Handle); Handle = IntPtr.Zero; }
        }
    }
}
