using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace RogHime
{
    /// <summary>开机自启的四种实现方式，用户可自选。</summary>
    internal static class AutoStart
    {
        public const int Off = 0;
        public const int RunKey = 1;          // HKCU\...\Run —— 最简单，无需管理员
        public const int StartupFolder = 2;   // 启动文件夹放一个 .cmd —— 无需管理员，用户可见可删
        public const int TaskLogon = 3;       // 计划任务·登录时·最高权限 —— 需要管理员，最可靠

        const string AppId = "RogHimeTheme";
        const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string ExePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        static string StartupDir
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.Startup); }
        }

        static string StartupCmd { get { return Path.Combine(StartupDir, AppId + ".cmd"); } }

        /// <summary>切换自启方式：先清掉其它方式，再设置目标方式。返回空串表示成功。</summary>
        public static string Apply(int mode)
        {
            Clear();                                  // 先全清，避免多种方式叠加
            try
            {
                switch (mode)
                {
                    case RunKey:
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunPath, true))
                            if (k != null) k.SetValue(AppId, "\"" + ExePath + "\"");
                        return "";
                    case StartupFolder:
                        Directory.CreateDirectory(StartupDir);
                        File.WriteAllText(StartupCmd,
                            "@echo off\r\nstart \"\" \"" + ExePath + "\"\r\n", System.Text.Encoding.Default);
                        return "";
                    case TaskLogon:
                        return RunSchtasks("/Create /SC ONLOGON /TN " + AppId + " /TR \"\"" + ExePath + "\"\" /RL HIGHEST /F");
                    default:
                        return "";
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>清除全部自启方式。</summary>
        public static void Clear()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunPath, true))
                    if (k != null && k.GetValue(AppId) != null) k.DeleteValue(AppId, false);
            }
            catch { }
            try { if (File.Exists(StartupCmd)) File.Delete(StartupCmd); } catch { }
            try { RunSchtasks("/Delete /TN " + AppId + " /F"); } catch { }
        }

        /// <summary>检测当前实际生效的方式（用于界面回显）。</summary>
        public static int Detect()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunPath, false))
                    if (k != null && k.GetValue(AppId) != null) return RunKey;
            }
            catch { }
            try { if (File.Exists(StartupCmd)) return StartupFolder; } catch { }
            if (TaskExists()) return TaskLogon;
            return Off;
        }

        static bool TaskExists()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks", "/Query /TN " + AppId);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(4000); return p.ExitCode == 0; }
            }
            catch { return false; }
        }

        static string RunSchtasks(string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo("schtasks", args);
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(8000);
                return p.ExitCode == 0 ? "" : o.Trim();
            }
        }

        public static string Name(int mode)
        {
            switch (mode)
            {
                case RunKey: return "注册表启动项";
                case StartupFolder: return "启动文件夹";
                case TaskLogon: return "计划任务（登录时 · 最高权限）";
                default: return "已关闭";
            }
        }
    }
}
