using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeepCleanC
{
    internal sealed class InstalledProgram
    {
        internal string DisplayName;
        internal string DisplayVersion;
        internal string Publisher;
        internal string InstallLocation;
        internal string DisplayIcon;
        internal string UninstallString;
        internal long EstimatedSizeKb;
        internal RegistryHive Hive;
        internal RegistryView View;
        internal string RegistryPath;
    }

    internal static class InstalledProgramCatalog
    {
        private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        internal static List<InstalledProgram> Load()
        {
            var programs = new List<InstalledProgram>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (RegistryView view in Environment.Is64BitOperatingSystem
                    ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
                    : new[] { RegistryView.Registry32 })
                    ReadHive(programs, seen, hive, view);

            return programs
                .OrderBy(program => program.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void ReadHive(List<InstalledProgram> programs, HashSet<string> seen, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey root = baseKey.OpenSubKey(UninstallRoot))
                {
                    if (root == null) return;
                    foreach (string subKeyName in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey key = root.OpenSubKey(subKeyName))
                            {
                                if (key == null) continue;
                                string name = ValueString(key, "DisplayName");
                                if (String.IsNullOrWhiteSpace(name)) continue;
                                if (ValueInt(key, "SystemComponent") == 1) continue;
                                if (!String.IsNullOrWhiteSpace(ValueString(key, "ParentKeyName"))) continue;

                                string uninstall = ValueString(key, "UninstallString");
                                string version = ValueString(key, "DisplayVersion");
                                string publisher = ValueString(key, "Publisher");
                                string identity = name + "|" + version + "|" + publisher + "|" + uninstall;
                                if (!seen.Add(identity)) continue;

                                programs.Add(new InstalledProgram
                                {
                                    DisplayName = name.Trim(),
                                    DisplayVersion = version,
                                    Publisher = publisher,
                                    InstallLocation = Environment.ExpandEnvironmentVariables(ValueString(key, "InstallLocation")),
                                    DisplayIcon = Environment.ExpandEnvironmentVariables(ValueString(key, "DisplayIcon")),
                                    UninstallString = Environment.ExpandEnvironmentVariables(uninstall),
                                    EstimatedSizeKb = ValueLong(key, "EstimatedSize"),
                                    Hive = hive,
                                    View = view,
                                    RegistryPath = UninstallRoot + "\\" + subKeyName
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        internal static bool RegistryEntryExists(InstalledProgram program)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(program.Hive, program.View))
                using (RegistryKey key = baseKey.OpenSubKey(program.RegistryPath))
                    return key != null;
            }
            catch { return false; }
        }

        private static string ValueString(RegistryKey key, string name)
        {
            object value = key.GetValue(name);
            return value == null ? "" : Convert.ToString(value) ?? "";
        }

        private static int ValueInt(RegistryKey key, string name)
        {
            object value = key.GetValue(name);
            int result;
            return value != null && Int32.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        private static long ValueLong(RegistryKey key, string name)
        {
            object value = key.GetValue(name);
            long result;
            return value != null && Int64.TryParse(Convert.ToString(value), out result) ? result : 0;
        }

        internal static bool SelfTest()
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "DeepClean-Uninstall-Test-" + Guid.NewGuid().ToString("N"));
            try
            {
                List<InstalledProgram> programs = Load();
                if (programs == null || programs.Any(program => String.IsNullOrWhiteSpace(program.DisplayName))) return false;

                Directory.CreateDirectory(testRoot);
                File.WriteAllText(Path.Combine(testRoot, "sample.bin"), "test");
                var testProgram = new InstalledProgram
                {
                    DisplayName = "RiverXILee Residual Test " + Guid.NewGuid().ToString("N"),
                    Publisher = "RiverXILee",
                    InstallLocation = testRoot,
                    RegistryPath = @"SOFTWARE\RiverXILee\NonexistentTest",
                    Hive = RegistryHive.CurrentUser,
                    View = RegistryView.Registry32
                };
                LeftoverItem explicitItem = LeftoverScanner.Scan(testProgram)
                    .FirstOrDefault(item => item.Location.Equals(testRoot, StringComparison.OrdinalIgnoreCase) && item.Selected);
                if (explicitItem == null) return false;
                string error = LeftoverRemover.Remove(explicitItem);
                return String.IsNullOrWhiteSpace(error) && !Directory.Exists(testRoot);
            }
            catch { return false; }
            finally
            {
                try { if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true); }
                catch { }
            }
        }
    }

    internal static class UninstallRunner
    {
        internal static string Run(InstalledProgram program)
        {
            if (String.IsNullOrWhiteSpace(program.UninstallString)) return "该软件没有注册标准卸载命令";
            try
            {
                string command = program.UninstallString.Trim();
                Match parts = Regex.Match(command, @"^(?:""([^""]+\.exe)""|([^""]+?\.exe))(?=\s|$)\s*(.*)$", RegexOptions.IgnoreCase);
                if (!parts.Success) return "卸载命令格式不受支持，请在 Windows 设置中尝试卸载";
                string executable = parts.Groups[1].Success ? parts.Groups[1].Value : parts.Groups[2].Value;
                string arguments = parts.Groups[3].Value;
                if (executable.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
                    executable = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
                if (!Path.IsPathRooted(executable) || executable.Length < 3 || executable[1] != ':' || executable[2] != '\\'
                    || !File.Exists(executable))
                    return "未找到本机卸载程序，请在 Windows 设置中尝试卸载";
                string executableName = Path.GetFileName(executable);
                if (new[] { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe" }
                    .Contains(executableName, StringComparer.OrdinalIgnoreCase))
                    return "此卸载命令依赖脚本解释器，请在 Windows 设置中卸载";
                if (executable.Equals(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), StringComparison.OrdinalIgnoreCase))
                    arguments = Regex.Replace(arguments, @"^/i(?=\s*\{)", "/x", RegexOptions.IgnoreCase);
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (process == null) return "无法启动卸载程序";
                    process.WaitForExit();
                    return process.ExitCode == 0 ? "" : "卸载程序退出代码 " + process.ExitCode;
                }
            }
            catch (Exception ex) { return ex.Message; }
        }
    }

    internal enum LeftoverKind
    {
        Folder,
        File,
        Shortcut,
        Registry
    }

    internal sealed class LeftoverItem
    {
        internal bool Selected;
        internal LeftoverKind Kind;
        internal string DisplayType;
        internal string Location;
        internal string Confidence;
        internal string Reason;
        internal RegistryHive Hive;
        internal RegistryView View;
        internal string RegistryPath;
    }

    internal static class LeftoverScanner
    {
        internal static List<LeftoverItem> Scan(InstalledProgram program)
        {
            var results = new List<LeftoverItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddExplicitInstallLocation(results, seen, program);
            AddIconDirectory(results, seen, program);
            AddMatchingDirectories(results, seen, program);
            AddShortcuts(results, seen, program);
            AddRegistryCandidates(results, seen, program);

            return results.OrderByDescending(item => item.Selected)
                .ThenBy(item => item.DisplayType)
                .ThenBy(item => item.Location, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddExplicitInstallLocation(List<LeftoverItem> results, HashSet<string> seen, InstalledProgram program)
        {
            if (String.IsNullOrWhiteSpace(program.InstallLocation)) return;
            try
            {
                string path = ForceDeleteSafety.Normalize(program.InstallLocation);
                if ((Directory.Exists(path) || File.Exists(path)) && ForceDeleteSafety.IsAllowed(path))
                    AddFileSystem(results, seen, path, "安装目录", "高", "卸载登记中明确记录的安装位置", true);
            }
            catch { }
        }

        private static void AddIconDirectory(List<LeftoverItem> results, HashSet<string> seen, InstalledProgram program)
        {
            string icon = ExtractIconPath(program.DisplayIcon);
            if (String.IsNullOrWhiteSpace(icon) || !File.Exists(icon)) return;
            try
            {
                string directory = Path.GetDirectoryName(icon);
                if (!String.IsNullOrWhiteSpace(directory) && ForceDeleteSafety.IsAllowed(directory) && Matches(program, new DirectoryInfo(directory).Name))
                    AddFileSystem(results, seen, directory, "图标所在目录", "中", "程序图标位置与软件名称匹配", false);
            }
            catch { }
        }

        private static void AddMatchingDirectories(List<LeftoverItem> results, HashSet<string> seen, InstalledProgram program)
        {
            foreach (string root in CandidateRoots())
            {
                foreach (string directory in SafeDirectories(root))
                {
                    try
                    {
                        var info = new DirectoryInfo(directory);
                        if (Matches(program, info.Name) && ForceDeleteSafety.IsAllowed(directory))
                            AddFileSystem(results, seen, directory, "名称匹配目录", "低", "目录名与软件名或发布者关键词相似，请人工确认", false);

                        if (!String.IsNullOrWhiteSpace(program.Publisher) && MatchesText(info.Name, program.Publisher))
                            foreach (string child in SafeDirectories(directory))
                                if (Matches(program, new DirectoryInfo(child).Name) && ForceDeleteSafety.IsAllowed(child))
                                    AddFileSystem(results, seen, child, "发布者目录中的候选", "中", "发布者目录下的软件名称匹配", false);
                    }
                    catch { }
                }
            }
        }

        private static void AddShortcuts(List<LeftoverItem> results, HashSet<string> seen, InstalledProgram program)
        {
            foreach (string root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            })
            {
                if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (string shortcut in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                        if (Matches(program, Path.GetFileNameWithoutExtension(shortcut)))
                            AddFileSystem(results, seen, shortcut, "快捷方式", "中", "快捷方式名称与软件匹配", false);
                }
                catch { }
            }
        }

        private static void AddRegistryCandidates(List<LeftoverItem> results, HashSet<string> seen, InstalledProgram program)
        {
            if (InstalledProgramCatalog.RegistryEntryExists(program))
                AddRegistry(results, seen, program.Hive, program.View, program.RegistryPath,
                    "卸载列表登记", "高", "该程序的卸载登记仍然存在", true);

        }

        private static void AddFileSystem(List<LeftoverItem> results, HashSet<string> seen, string path,
            string type, string confidence, string reason, bool selected)
        {
            string key = "F|" + path;
            if (!seen.Add(key)) return;
            LeftoverKind kind = Directory.Exists(path) ? LeftoverKind.Folder : (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? LeftoverKind.Shortcut : LeftoverKind.File);
            results.Add(new LeftoverItem
            {
                Selected = selected,
                Kind = kind,
                DisplayType = type,
                Location = path,
                Confidence = confidence,
                Reason = reason
            });
        }

        private static void AddRegistry(List<LeftoverItem> results, HashSet<string> seen, RegistryHive hive, RegistryView view,
            string path, string type, string confidence, string reason, bool selected)
        {
            string key = "R|" + hive + "|" + view + "|" + path;
            if (!seen.Add(key)) return;
            results.Add(new LeftoverItem
            {
                Selected = selected,
                Kind = LeftoverKind.Registry,
                DisplayType = type,
                Location = HiveName(hive) + "\\" + path,
                Confidence = confidence,
                Reason = reason,
                Hive = hive,
                View = view,
                RegistryPath = path
            });
        }

        private static IEnumerable<string> CandidateRoots()
        {
            return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            }.Where(path => !String.IsNullOrWhiteSpace(path) && Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SafeDirectories(string root)
        {
            try { return Directory.GetDirectories(root); }
            catch { return new string[0]; }
        }

        private static bool Matches(InstalledProgram program, string candidate)
        {
            return MatchesText(candidate, program.DisplayName)
                || (!String.IsNullOrWhiteSpace(program.Publisher) && MatchesText(candidate, program.Publisher)
                    && Tokens(program.DisplayName).Any(token => NormalizeText(candidate).Contains(token)));
        }

        private static bool MatchesText(string candidate, string reference)
        {
            string left = NormalizeText(candidate);
            string right = NormalizeText(reference);
            if (right.Length >= 4 && left.Contains(right)) return true;
            return Tokens(reference).Any(token => token.Length >= 4 && left.Contains(token));
        }

        private static IEnumerable<string> Tokens(string text)
        {
            var ignored = new HashSet<string>(new[] { "microsoft", "windows", "software", "company", "limited", "ltd", "inc", "tool", "tools", "setup" }, StringComparer.OrdinalIgnoreCase);
            return Regex.Split(NormalizeText(text), @"[^\p{L}\p{Nd}]+")
                .Where(token => token.Length >= 3 && !ignored.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string value)
        {
            return Regex.Replace((value ?? "").ToLowerInvariant(), @"\s+|[\-_().\[\]{}]", "");
        }

        private static string ExtractIconPath(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string path = value.Trim().Trim('"');
            int comma = path.LastIndexOf(',');
            if (comma > 1) path = path.Substring(0, comma).Trim().Trim('"');
            return path;
        }

        private static string HiveName(RegistryHive hive)
        {
            return hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
        }
    }

    internal static class LeftoverRemover
    {
        internal static string Remove(LeftoverItem item)
        {
            try
            {
                if (item.Kind == LeftoverKind.Registry)
                {
                    DeleteRegistry(item);
                    return "";
                }

                var target = new ForceDeleteTarget
                {
                    FullPath = item.Location,
                    Name = Path.GetFileName(item.Location),
                    Kind = item.Kind == LeftoverKind.Folder ? "文件夹" : "文件"
                };
                ForceDeleteResult result = ForceDeleteEngine.Delete(target);
                return result.Success ? "" : result.Message;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static void DeleteRegistry(LeftoverItem item)
        {
            string path = item.RegistryPath.Trim('\\');
            int separator = path.LastIndexOf('\\');
            if (separator <= 0) throw new InvalidOperationException("拒绝删除顶层注册表键");
            string parentPath = path.Substring(0, separator);
            string childName = path.Substring(separator + 1);
            if (!parentPath.Equals(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("仅允许移除软件自己的卸载登记");
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(item.Hive, item.View))
            using (RegistryKey parent = baseKey.OpenSubKey(parentPath, true))
            {
                if (parent != null) parent.DeleteSubKeyTree(childName, false);
            }
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(item.Hive, item.View))
            using (RegistryKey remaining = baseKey.OpenSubKey(path))
                if (remaining != null) throw new IOException("删除后卸载登记仍然存在");
        }
    }

    internal sealed class UninstallerForm : Form
    {
        private readonly bool smokeTest;
        private readonly List<InstalledProgram> programs = new List<InstalledProgram>();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox search = new TextBox();
        private readonly Label status = new Label();
        private readonly LiquidButton refresh = new LiquidButton();
        private readonly LiquidButton uninstall = new LiquidButton();
        private readonly LiquidButton forceRemove = new LiquidButton();
        private bool busy;
        internal bool IsBusy { get { return busy; } }

        internal UninstallerForm(bool smoke)
        {
            smokeTest = smoke;
            DoubleBuffered = true;
            BuildUi();
            Shown += OnShown;
            FormClosing += OnClosing;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { FeatureTheme.PaintBackground(this, e); }

        private void BuildUi()
        {
            Text = "软件卸载 · RiverDesk Toolkit";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 650);
            MinimumSize = new Size(800, 560);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = FeatureTheme.Background;

            var header = new Panel
            {
                Location = new Point(16, 10),
                Size = new Size(892, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Text = "软件卸载",
                Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 0),
                ForeColor = FeatureTheme.Text,
                BackColor = Color.Transparent
            });
            header.Controls.Add(new Label
            {
                Text = "先运行软件自己的卸载器，再检查明确关联的残留。",
                AutoSize = true,
                Location = new Point(6, 44),
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent
            });

            search.Location = new Point(20, 100);
            search.Size = new Size(400, 28);
            search.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            search.BackColor = FeatureTheme.Surface;
            search.ForeColor = FeatureTheme.Text;
            search.BorderStyle = BorderStyle.FixedSingle;
            search.TextChanged += (sender, args) => PopulateGrid();
            Controls.Add(search);

            var searchHint = new Label
            {
                Text = "输入软件名或发布者筛选",
                Location = new Point(428, 103),
                Size = new Size(220, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent
            };
            Controls.Add(searchHint);

            SetupButton(refresh, "刷新列表", new Point(788, 96), new Size(120, 34), (sender, args) => LoadPrograms());
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            grid.Location = new Point(20, 140);
            grid.Size = new Size(888, 390);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            FeatureTheme.StyleGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "软件", Width = 260 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "发布者", Width = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "版本", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "估算大小", Width = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "安装位置", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.SelectionChanged += (sender, args) => UpdateButtons();
            grid.CellDoubleClick += (sender, args) => { if (args.RowIndex >= 0) StartStandardUninstall(); };
            Controls.Add(grid);

            status.Location = new Point(20, 548);
            status.Size = new Size(560, 50);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.ForeColor = FeatureTheme.Muted;
            status.BackColor = Color.Transparent;
            Controls.Add(status);

            SetupButton(forceRemove, "扫描残留 / 强制移除", new Point(594, 550), new Size(180, 42), (sender, args) => OpenLeftovers(true));
            forceRemove.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            SetupButton(uninstall, "正常卸载", new Point(782, 550), new Size(126, 42), (sender, args) => StartStandardUninstall());
            uninstall.AccentColor = FeatureTheme.Sky;
            uninstall.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            UpdateButtons();
        }

        private void SetupButton(Button button, string text, Point location, Size size, EventHandler handler)
        {
            button.Text = text;
            button.Location = location;
            button.Size = size;
            button.Click += handler;
            Controls.Add(button);
        }

        private void OnShown(object sender, EventArgs e)
        {
            LoadPrograms();
        }

        private void LoadPrograms()
        {
            if (busy) return;
            SetBusy(true);
            status.Text = "正在读取已安装的软件……";
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => args.Result = InstalledProgramCatalog.Load();
            worker.RunWorkerCompleted += (sender, args) =>
            {
                SetBusy(false);
                if (args.Error != null)
                {
                    status.Text = "读取失败：" + args.Error.Message;
                    status.ForeColor = FeatureTheme.Danger;
                    return;
                }
                programs.Clear();
                programs.AddRange((List<InstalledProgram>)args.Result);
                PopulateGrid();
                status.Text = "共找到 " + programs.Count + " 个传统桌面软件。双击可运行标准卸载。";
                status.ForeColor = FeatureTheme.Muted;

                if (smokeTest)
                    BeginInvoke(new Action(CaptureAndClose));
            };
            worker.RunWorkerAsync();
        }

        private void CaptureAndClose()
        {
            string snapshot = Environment.GetEnvironmentVariable("DEEPCLEAN_UNINSTALLER_SNAPSHOT");
            if (!String.IsNullOrWhiteSpace(snapshot))
            {
                using (var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height))
                {
                    DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
                    bitmap.Save(snapshot, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            Close();
        }

        private void PopulateGrid()
        {
            string filter = search.Text.Trim();
            grid.Rows.Clear();
            IEnumerable<InstalledProgram> visible = programs;
            if (!String.IsNullOrWhiteSpace(filter))
                visible = visible.Where(program => Contains(program.DisplayName, filter) || Contains(program.Publisher, filter));

            foreach (InstalledProgram program in visible)
            {
                int row = grid.Rows.Add(program.DisplayName, program.Publisher, program.DisplayVersion,
                    program.EstimatedSizeKb > 0 ? FormatSize(program.EstimatedSizeKb * 1024L) : "—",
                    String.IsNullOrWhiteSpace(program.InstallLocation) ? "未登记" : program.InstallLocation);
                grid.Rows[row].Tag = program;
            }
            UpdateButtons();
        }

        private static bool Contains(string source, string value)
        {
            return !String.IsNullOrWhiteSpace(source) && source.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private InstalledProgram SelectedProgram()
        {
            return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as InstalledProgram;
        }

        private void StartStandardUninstall()
        {
            InstalledProgram program = SelectedProgram();
            if (program == null || busy) return;
            if (String.IsNullOrWhiteSpace(program.UninstallString))
            {
                MessageBox.Show(this, "该软件没有注册标准卸载命令，可以改用“扫描残留 / 强制移除”。", "没有卸载器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "将运行“" + program.DisplayName + "”自己的卸载程序。\r\n\r\n卸载结束后，本工具会扫描可能的残留。是否继续？",
                "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            SetBusy(true);
            status.Text = "卸载程序正在运行，请先在它的窗口中完成操作……";
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => args.Result = UninstallRunner.Run(program);
            worker.RunWorkerCompleted += (sender, args) =>
            {
                SetBusy(false);
                string error = args.Error != null ? args.Error.Message : Convert.ToString(args.Result);
                if (!String.IsNullOrWhiteSpace(error))
                {
                    status.Text = "卸载器返回提示：" + error;
                    status.ForeColor = FeatureTheme.Warning;
                    return;
                }
                else
                {
                    status.Text = "卸载器已结束，正在准备残留检查。";
                    status.ForeColor = FeatureTheme.Success;
                }
                OpenLeftovers(false, program);
            };
            worker.RunWorkerAsync();
        }

        private void OpenLeftovers(bool force)
        {
            OpenLeftovers(force, SelectedProgram());
        }

        private void OpenLeftovers(bool force, InstalledProgram program)
        {
            if (program == null || busy) return;
            string message = force
                ? "强制移除不会运行软件自己的卸载器，只会扫描并列出关联残留。所有项目仍需你确认勾选。\r\n\r\n继续扫描“" + program.DisplayName + "”吗？"
                : "现在扫描“" + program.DisplayName + "”可能残留的目录、快捷方式和注册表项。";
            if (force && MessageBox.Show(this, message, "确认强制移除扫描", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            using (var form = new LeftoversForm(program)) form.ShowDialog(this);
            LoadPrograms();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            refresh.Enabled = !value;
            search.Enabled = !value;
            grid.Enabled = !value;
            UseWaitCursor = value;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            InstalledProgram program = SelectedProgram();
            uninstall.Enabled = !busy && program != null && !String.IsNullOrWhiteSpace(program.UninstallString);
            forceRemove.Enabled = !busy && program != null;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "卸载或扫描仍在进行，请完成后再关闭。", "操作进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1073741824L) return (bytes / 1073741824D).ToString("N1") + " GB";
            if (bytes >= 1048576L) return (bytes / 1048576D).ToString("N1") + " MB";
            if (bytes >= 1024L) return (bytes / 1024D).ToString("N0") + " KB";
            return bytes + " B";
        }
    }

    internal sealed class LeftoversForm : Form
    {
        private readonly InstalledProgram program;
        private readonly List<LeftoverItem> items = new List<LeftoverItem>();
        private readonly DataGridView grid = new DataGridView();
        private readonly Label status = new Label();
        private readonly LiquidButton clear = new LiquidButton();
        private readonly LiquidButton remove = new LiquidButton();
        private bool busy;

        internal LeftoversForm(InstalledProgram selectedProgram)
        {
            program = selectedProgram;
            DoubleBuffered = true;
            BuildUi();
            Shown += (sender, args) => Scan();
            FormClosing += OnClosing;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { FeatureTheme.PaintBackground(this, e); }

        private void BuildUi()
        {
            Text = "残留扫描 · " + program.DisplayName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 640);
            MinimumSize = new Size(800, 540);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = FeatureTheme.Background;

            var header = new Panel
            {
                Location = new Point(16, 10),
                Size = new Size(892, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Text = "残留检查 · " + program.DisplayName,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 9),
                ForeColor = FeatureTheme.Text,
                BackColor = Color.Transparent
            });
            header.Controls.Add(new Label
            {
                Text = "清单默认全不选。请核对完整位置，确认残留属于该软件后再勾选。",
                AutoSize = true,
                Location = new Point(20, 46),
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent
            });

            grid.Location = new Point(20, 100);
            grid.Size = new Size(888, 410);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            FeatureTheme.StyleGrid(grid);
            grid.MultiSelect = false;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "删除", Width = 54 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "类型", Width = 150, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "位置", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "置信度", Width = 76, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "依据", Width = 220, ReadOnly = true });
            Controls.Add(grid);

            SetupButton(clear, "取消全选", new Point(20, 528), new Size(100, 36), (sender, args) => ClearSelection());
            clear.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            status.Location = new Point(20, 574);
            status.Size = new Size(640, 30);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.ForeColor = FeatureTheme.Muted;
            status.BackColor = Color.Transparent;
            Controls.Add(status);

            SetupButton(remove, "删除勾选残留", new Point(728, 540), new Size(180, 44), (sender, args) => RemoveSelected());
            remove.AccentColor = FeatureTheme.Danger;
            remove.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            SetBusy(true);
        }

        private void SetupButton(Button button, string text, Point location, Size size, EventHandler handler)
        {
            button.Text = text;
            button.Location = location;
            button.Size = size;
            button.Click += handler;
            Controls.Add(button);
        }

        private void Scan()
        {
            SetBusy(true);
            status.Text = "正在扫描明确关联的残留……";
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) => args.Result = LeftoverScanner.Scan(program);
            worker.RunWorkerCompleted += (sender, args) =>
            {
                SetBusy(false);
                if (args.Error != null)
                {
                    status.Text = "扫描失败：" + args.Error.Message;
                    status.ForeColor = FeatureTheme.Danger;
                    return;
                }
                items.Clear();
                items.AddRange((List<LeftoverItem>)args.Result);
                Populate();
                status.Text = items.Count == 0 ? "没有发现明确关联的残留。" : "发现 " + items.Count + " 个候选。请逐项核对后勾选。";
                status.ForeColor = items.Count == 0 ? FeatureTheme.Success : FeatureTheme.Muted;
            };
            worker.RunWorkerAsync();
        }

        private void Populate()
        {
            grid.Rows.Clear();
            foreach (LeftoverItem item in items)
            {
                int row = grid.Rows.Add(false, item.DisplayType, item.Location, item.Confidence, item.Reason);
                grid.Rows[row].Tag = item;
                if (item.Confidence == "低") grid.Rows[row].DefaultCellStyle.ForeColor = FeatureTheme.Warning;
            }
            remove.Enabled = !busy && items.Count > 0;
            clear.Enabled = !busy && items.Count > 0;
        }

        private void ClearSelection()
        {
            foreach (DataGridViewRow row in grid.Rows) row.Cells[0].Value = false;
        }

        private void RemoveSelected()
        {
            grid.EndEdit();
            var selected = grid.Rows.Cast<DataGridViewRow>()
                .Where(row => Convert.ToBoolean(row.Cells[0].Value))
                .Select(row => (LeftoverItem)row.Tag)
                .ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "请先勾选要删除的残留。", "没有选择项目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "将永久删除 " + selected.Count + " 个文件系统或注册表残留。\r\n\r\n请确认这些项目确实属于“" + program.DisplayName + "”。",
                "确认删除残留", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            SetBusy(true);
            status.Text = "正在删除勾选残留并验证……";
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                var failures = new Dictionary<LeftoverItem, string>();
                foreach (LeftoverItem item in selected)
                {
                    string error = LeftoverRemover.Remove(item);
                    if (!String.IsNullOrWhiteSpace(error)) failures[item] = error;
                }
                args.Result = failures;
            };
            worker.RunWorkerCompleted += (sender, args) =>
            {
                SetBusy(false);
                if (args.Error != null)
                {
                    status.Text = "删除失败：" + args.Error.Message;
                    status.ForeColor = FeatureTheme.Danger;
                    return;
                }
                var failures = (Dictionary<LeftoverItem, string>)args.Result;
                int succeeded = selected.Count - failures.Count;
                status.Text = failures.Count == 0
                    ? "已删除并验证 " + succeeded + " 个残留。"
                    : "成功 " + succeeded + " 项，失败 " + failures.Count + " 项。";
                status.ForeColor = failures.Count == 0 ? FeatureTheme.Success : FeatureTheme.Danger;
                items.RemoveAll(item => selected.Contains(item) && !failures.ContainsKey(item));
                Populate();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string error;
                    if (failures.TryGetValue((LeftoverItem)row.Tag, out error))
                    {
                        row.Cells[4].Value = error;
                        row.Cells[4].Style.ForeColor = FeatureTheme.Danger;
                    }
                }
            };
            worker.RunWorkerAsync();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            grid.Enabled = !value;
            clear.Enabled = !value && items.Count > 0;
            remove.Enabled = !value && items.Count > 0;
            UseWaitCursor = value;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "扫描或删除仍在进行，请完成后再关闭。", "操作进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
