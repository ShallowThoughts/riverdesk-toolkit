using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace RiverDesk
{
    internal static class Launcher
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                RunToolkit(args);
            }
            catch (Exception error)
            {
                if (!(error is FileNotFoundException) && !(error is FileLoadException)
                    && !(error is BadImageFormatException)) throw;
                Environment.ExitCode = 1;
                if (Array.Exists(args, arg => arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase)
                    || arg.EndsWith("-smoke", StringComparison.OrdinalIgnoreCase))) return;
                MessageBox.Show("程序文件不完整或无法加载。请使用安装包重新安装 RiverDesk Toolkit。",
                    VersionInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Keep assembly loading inside Main's error handler.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunToolkit(string[] args)
        {
            if (typeof(DeepCleanC.ToolkitApplication).Assembly.GetName().Version != VersionInfo.Current)
                throw new FileLoadException("The application and toolkit versions do not match.");
            DeepCleanC.ToolkitApplication.Run(args);
        }
    }
}
