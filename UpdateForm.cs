using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DeepCleanC;

namespace RiverDesk
{
    internal static class VersionInfo
    {
        internal const string Name = "RiverDesk Toolkit";
        internal const string Number = "1.3.1.0";
        internal const string Releases = "https://github.com/ShallowThoughts/riverdesk-toolkit/releases";
        internal static readonly Version Current = new Version(Number);
    }

    internal sealed class UpdateRelease
    {
        internal Version Version;
        internal string Hash;
        internal string Notes;

        internal static UpdateRelease Read(byte[] data, string publicKey)
        {
            string[] lines = Encoding.UTF8.GetString(data).TrimEnd('\r', '\n').Split('\n');
            if (lines.Length != 4 || !Regex.IsMatch(lines[0], @"\A[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}\z")
                || !Regex.IsMatch(lines[1], @"\A[0-9a-f]{64}\z"))
                throw new InvalidDataException("更新清单格式不正确。");
            using (var rsa = new RSACryptoServiceProvider(new CspParameters(24)))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(publicKey);
                byte[] payload = Encoding.UTF8.GetBytes(String.Join("\n", lines, 0, 3));
                if (!rsa.VerifyData(payload, CryptoConfig.MapNameToOID("SHA256"), Convert.FromBase64String(lines[3])))
                    throw new InvalidDataException("更新签名无效。");
            }
            return new UpdateRelease
            {
                Version = new Version(lines[0] + ".0"),
                Hash = lines[1],
                Notes = Encoding.UTF8.GetString(Convert.FromBase64String(lines[2]))
            };
        }

        internal static UpdateRelease Check()
        {
            using (var key = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("RiverDesk.UpdatePublicKey")))
            using (var buffer = new MemoryStream())
            {
                Download(VersionInfo.Releases + "/latest/download/update.txt", buffer, 65536, null);
                return Read(buffer.ToArray(), key.ReadToEnd());
            }
        }

        internal static void Download(string url, Stream target, long limit, IProgress<int> progress)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "RiverDesk-Toolkit/" + VersionInfo.Current.ToString(3);
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.ResponseUri.Scheme != Uri.UriSchemeHttps || response.ContentLength > limit)
                    throw new InvalidDataException("下载来源或文件大小不符合要求。");
                using (var stream = response.GetResponseStream())
                {
                    byte[] buffer = new byte[32768];
                    int count;
                    long total = 0;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += count;
                        if (total > limit) throw new InvalidDataException("下载文件过大。");
                        target.Write(buffer, 0, count);
                        if (progress != null && response.ContentLength > 0)
                            progress.Report((int)Math.Min(99, 100 * total / response.ContentLength));
                    }
                }
            }
        }

        internal bool Matches(Stream file)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() == Hash;
        }
    }

    internal sealed class UpdateForm : Form
    {
        private readonly Func<bool> canClose;
        private readonly LiquidButton check = new LiquidButton { Text = "检查更新", Width = 126, Height = 38 };
        private readonly LiquidButton install = new LiquidButton { Text = "下载并安装", Width = 126, Height = 38, Enabled = false };
        private readonly Label status = new Label { AutoSize = true, ForeColor = FeatureTheme.Muted };
        private readonly ProgressBar progress = new ProgressBar { Height = 12, Dock = DockStyle.Fill, Visible = false };
        private readonly TextBox notes = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = FeatureTheme.Surface, ForeColor = FeatureTheme.Text, BorderStyle = BorderStyle.FixedSingle
        };
        private UpdateRelease available;
        internal bool IsBusy { get; private set; }

        internal UpdateForm(Func<bool> canClose)
        {
            this.canClose = canClose;
            Font = new Font("Microsoft YaHei UI", 10F);
            BackColor = FeatureTheme.Background;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 24), ColumnCount = 1, RowCount = 7 };
            for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "软件更新", AutoSize = true, ForeColor = FeatureTheme.Text,
                Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold) }, 0, 0);
            layout.Controls.Add(new Label { Text = VersionInfo.Name + "  ·  当前版本 " + VersionInfo.Current.ToString(3),
                AutoSize = true, ForeColor = FeatureTheme.Muted, Margin = new Padding(4, 8, 0, 18) }, 0, 1);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
            check.AccentColor = FeatureTheme.Sky;
            install.AccentColor = FeatureTheme.Sky;
            actions.Controls.Add(check);
            actions.Controls.Add(install);
            var releases = new LinkLabel { Text = "GitHub 发布记录", AutoSize = true, Margin = new Padding(12, 13, 0, 0) };
            releases.LinkClicked += (sender, args) => Process.Start(new ProcessStartInfo(VersionInfo.Releases) { UseShellExecute = true });
            actions.Controls.Add(releases);
            layout.Controls.Add(actions, 0, 2);
            status.Margin = new Padding(4, 12, 0, 10);
            status.Text = "点击「检查更新」查询最新正式版。";
            layout.Controls.Add(status, 0, 3);
            layout.Controls.Add(progress, 0, 4);
            layout.Controls.Add(new Label { Text = "更新说明", AutoSize = true, ForeColor = FeatureTheme.Text,
                Margin = new Padding(4, 20, 0, 8) }, 0, 5);
            layout.Controls.Add(notes, 0, 6);
            notes.Text = "检查后将在这里显示最新版本的说明。";
            Controls.Add(layout);
            check.Click += async (sender, args) => await CheckAsync();
            install.Click += async (sender, args) => await InstallAsync();
        }

        private void SetBusy(bool value)
        {
            IsBusy = value;
            check.Enabled = !value;
            install.Enabled = !value && available != null;
        }

        private async Task CheckAsync()
        {
            available = null;
            SetBusy(true);
            status.Text = "正在检查更新…";
            try
            {
                UpdateRelease latest = await Task.Run(() => UpdateRelease.Check());
                notes.Text = latest.Notes;
                if (latest.Version > VersionInfo.Current)
                {
                    available = latest;
                    status.Text = "发现新版本 " + latest.Version.ToString(3) + "。";
                }
                else status.Text = "已是最新版本。";
            }
            catch (Exception ex)
            {
                status.Text = "检查更新失败。请重试或查看 GitHub 发布记录。";
                notes.Text = ex.Message;
            }
            finally { SetBusy(false); }
        }

        private async Task InstallAsync()
        {
            UpdateRelease release = available;
            if (release == null || !canClose()) return;
            SetBusy(true);
            progress.Value = 0;
            progress.Visible = true;
            string path = null;
            bool launched = false;
            try
            {
                string installedDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), VersionInfo.Name);
                if (!Program.IsAdministrator() || !String.Equals(Application.StartupPath, installedDirectory, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(Path.Combine(installedDirectory, "Uninstall.exe")))
                    throw new InvalidOperationException("请从开始菜单以管理员权限打开已安装的 RiverDesk Toolkit。");
                string directory = Path.Combine(installedDirectory, "Updates");
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, "RiverDeskToolkit-" + Guid.NewGuid().ToString("N") + ".exe");
                var reporter = new Progress<int>(percent =>
                {
                    progress.Value = percent;
                    status.Text = "正在下载安装包… " + percent + "%";
                });
                string version = release.Version.ToString(3);
                await Task.Run(() =>
                {
                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        UpdateRelease.Download(VersionInfo.Releases + "/download/v" + version + "/RiverDeskToolkit-Setup.exe",
                            file, 32 * 1024 * 1024, reporter);
                });
                status.Text = "正在校验安装包…";
                SetBusy(false);
                if (!canClose()) throw new InvalidOperationException("请等待当前任务完成，再重试安装。");
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (!release.Matches(file)) throw new InvalidDataException("安装包校验失败。");
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    launched = true;
                }
                var shell = ParentForm;
                while (shell != null && shell.ParentForm != null) shell = shell.ParentForm;
                if (shell != null) shell.Close();
            }
            catch (Exception ex)
            {
                status.Text = "升级未启动。";
                notes.Text = ex.Message;
            }
            finally
            {
                if (!launched && path != null)
                    try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (!IsDisposed)
                {
                    progress.Visible = false;
                    SetBusy(false);
                }
            }
        }
    }
}
