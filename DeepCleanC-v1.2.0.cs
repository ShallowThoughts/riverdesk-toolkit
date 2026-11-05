using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Windows.Forms;

namespace DeepCleanC
{
    public static class ToolkitApplication
    {
        internal static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static void Run(string[] args)
        {
            if (args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Cleaner.SelfTest()
                    && CleanerForm.ConfirmationSelfTest()
                    && ForceDeleteEngine.SelfTest()
                    && InstalledProgramCatalog.SelfTest() ? 0 : 1;
                return;
            }

            bool smokeTest = args.Any(a => a.Equals("--ui-smoke", StringComparison.OrdinalIgnoreCase));
            bool forceDeleteSmoke = args.Any(a => a.Equals("--force-delete-smoke", StringComparison.OrdinalIgnoreCase));
            bool uninstallerSmoke = args.Any(a => a.Equals("--uninstaller-smoke", StringComparison.OrdinalIgnoreCase));
            bool updateSmoke = args.Any(a => a.Equals("--update-smoke", StringComparison.OrdinalIgnoreCase));
            string[] droppedPaths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
            if (!smokeTest && !forceDeleteSmoke && !uninstallerSmoke && !updateSmoke && !IsAdministrator())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Arguments = String.Join(" ", args.Select(a => "\"" + a.Replace("\"", "\\\"") + "\"")),
                        Verb = "runas",
                        UseShellExecute = true
                    });
                    return;
                }
                catch (Win32Exception)
                {
                    // 用户取消 UAC 后仍可进入只具备有限权限的界面。
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppPage page = forceDeleteSmoke || droppedPaths.Length > 0 ? AppPage.ForceDelete
                : uninstallerSmoke ? AppPage.Uninstaller : updateSmoke ? AppPage.Update : AppPage.Cleanup;
            Application.Run(new AppShellForm(page, smokeTest || forceDeleteSmoke || uninstallerSmoke || updateSmoke, droppedPaths));
        }
    }

    internal sealed class Category
    {
        internal string Id;
        internal string Name;
        internal string Description;
        internal bool DefaultSelected;
        internal bool RequiresAdmin;
        internal bool DeleteTargets;
        internal Func<IEnumerable<string>> GetTargets;
    }

    internal sealed class CleanResult
    {
        internal string Id;
        internal long Before;
        internal long After;
        internal long Freed;
        internal int Failures;
        internal string Note;
    }

    internal sealed class WorkRequest
    {
        internal bool Clean;
        internal List<Category> Categories;
    }

    internal static class Cleaner
    {
        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        internal static List<Category> CreateCategories()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            return new List<Category>
            {
                Cat("user_temp", "用户临时文件", true, false, false,
                    "当前账户的临时文件；正在使用的文件会自动跳过。",
                    () => new[] { Path.GetTempPath(), Path.Combine(local, "Temp") }),
                Cat("windows_temp", "Windows 临时文件", true, true, false,
                    "Windows 临时目录中的可删除内容。",
                    () => new[] { Path.Combine(windows, "Temp") }),
                Cat("update_cache", "Windows 更新下载缓存", true, true, false,
                    "已下载的更新安装包缓存；需要时系统会重新下载。",
                    () => new[] { Path.Combine(windows, "SoftwareDistribution", "Download") }),
                Cat("delivery_cache", "传递优化缓存", true, true, false,
                    "Windows 更新的局域网/互联网分发缓存。",
                    () => new[] { Path.Combine(windows, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache") }),
                Cat("browser_cache", "浏览器缓存", true, false, false,
                    "Chrome、Edge、Firefox 网页缓存；不删除书签、密码、Cookie 或历史记录。",
                    () => BrowserCaches(local, roaming)),
                Cat("error_reports", "错误报告与崩溃转储", true, false, false,
                    "Windows 错误报告和当前账户的小型崩溃转储。",
                    () => new[]
                    {
                        Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                        Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
                        Path.Combine(local, "CrashDumps")
                    }),
                Cat("shader_cache", "显卡着色器缓存", false, false, false,
                    "DirectX/NVIDIA 可再生成缓存；清理后首次进游戏可能短暂卡顿。",
                    () => new[]
                    {
                        Path.Combine(local, "D3DSCache"), Path.Combine(local, "NVIDIA", "DXCache"),
                        Path.Combine(local, "NVIDIA", "GLCache"), Path.Combine(programData, "NVIDIA Corporation", "NV_Cache")
                    }),
                Cat("thumbnails", "缩略图缓存", false, false, true,
                    "资源管理器图片/视频缩略图；系统会自动重建。",
                    () => MatchingFiles(Path.Combine(local, "Microsoft", "Windows", "Explorer"), new[] { "thumbcache_*.db", "iconcache_*.db" })),
                Cat("recycle_bin", "回收站", false, false, false,
                    "永久清空 C 盘回收站，清空后无法从回收站恢复。", () => new string[0]),
                Cat("memory_dumps", "系统内存转储", false, true, true,
                    "蓝屏诊断文件；确认近期不需要排查蓝屏再清理。",
                    () => MemoryDumps(windows)),
                Cat("dev_caches", "开发工具缓存", false, false, false,
                    "NuGet、npm、pip、Gradle 缓存；后续构建可能需要重新下载。",
                    () => new[]
                    {
                        Path.Combine(roaming, "npm-cache"), Path.Combine(local, "npm-cache"),
                        Path.Combine(local, "pip", "Cache"), Path.Combine(profile, ".cache", "pip"),
                        Path.Combine(profile, ".nuget", "packages"), Path.Combine(profile, ".gradle", "caches")
                    }),
                Cat("component_store", "系统组件存储维护", false, true, false,
                    "运行微软 DISM 组件清理；不直接删除 WinSxS。", () => new string[0]),
                Cat("hibernation", "关闭休眠并释放空间", false, true, true,
                    "关闭休眠/快速启动并删除 hiberfil.sys；可用 powercfg /hibernate on 恢复。",
                    () => new[] { @"C:\hiberfil.sys" })
            };
        }

        private static Category Cat(string id, string name, bool selected, bool admin, bool deleteTargets,
            string description, Func<IEnumerable<string>> targets)
        {
            return new Category
            {
                Id = id,
                Name = name,
                DefaultSelected = selected,
                RequiresAdmin = admin,
                DeleteTargets = deleteTargets,
                Description = description,
                GetTargets = targets
            };
        }

        private static IEnumerable<string> BrowserCaches(string local, string roaming)
        {
            var result = new List<string>();
            AddProfileCaches(result, Path.Combine(local, "Google", "Chrome", "User Data"));
            AddProfileCaches(result, Path.Combine(local, "Microsoft", "Edge", "User Data"));
            string firefox = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            foreach (string profile in SafeDirectories(firefox))
            {
                result.Add(Path.Combine(profile, "cache2"));
                result.Add(Path.Combine(profile, "startupCache"));
            }
            return result;
        }

        private static void AddProfileCaches(List<string> result, string userData)
        {
            foreach (string profile in SafeDirectories(userData))
            {
                result.Add(Path.Combine(profile, "Cache"));
                result.Add(Path.Combine(profile, "Code Cache"));
                result.Add(Path.Combine(profile, "GPUCache"));
            }
        }

        private static IEnumerable<string> MemoryDumps(string windows)
        {
            var result = new List<string> { Path.Combine(windows, "MEMORY.DMP") };
            result.AddRange(MatchingFiles(Path.Combine(windows, "Minidump"), new[] { "*" }));
            return result;
        }

        private static IEnumerable<string> MatchingFiles(string directory, IEnumerable<string> patterns)
        {
            var result = new List<string>();
            if (!Directory.Exists(directory)) return result;
            foreach (string pattern in patterns)
            {
                try { result.AddRange(Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly)); }
                catch { }
            }
            return result;
        }

        private static IEnumerable<string> SafeDirectories(string directory)
        {
            try { return Directory.Exists(directory) ? Directory.GetDirectories(directory) : new string[0]; }
            catch { return new string[0]; }
        }

        private static IEnumerable<string> ExistingTargets(Category category)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in category.GetTargets() ?? new string[0])
            {
                if (String.IsNullOrWhiteSpace(raw)) continue;
                string path;
                try { path = Path.GetFullPath(raw); }
                catch { continue; }
                if (!IsOnCDrive(path) || (!File.Exists(path) && !Directory.Exists(path))) continue;
                if (seen.Add(path.TrimEnd(Path.DirectorySeparatorChar))) yield return path;
            }
        }

        private static bool IsOnCDrive(string path)
        {
            try { return String.Equals(Path.GetPathRoot(path), @"C:\", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        internal static long ScanBytes(Category category)
        {
            if (category.Id == "recycle_bin") return ItemBytes(@"C:\$Recycle.Bin");
            if (category.Id == "component_store") return 0;

            long total = 0;
            foreach (string target in ExistingTargets(category))
            {
                if (category.DeleteTargets) total += ItemBytes(target);
                else if (Directory.Exists(target))
                {
                    foreach (string child in SafeEntries(target)) total += ItemBytes(child);
                }
                else total += ItemBytes(target);
            }
            return total;
        }

        private static long ItemBytes(string path)
        {
            try
            {
                FileAttributes rootAttributes = File.GetAttributes(path);
                if ((rootAttributes & FileAttributes.Directory) == 0) return new FileInfo(path).Length;

                long total = 0;
                var stack = new Stack<string>();
                stack.Push(path);
                while (stack.Count > 0)
                {
                    foreach (string child in SafeEntries(stack.Pop()))
                    {
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(child);
                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                if ((attributes & FileAttributes.ReparsePoint) == 0) stack.Push(child);
                            }
                            else total += new FileInfo(child).Length;
                        }
                        catch { }
                    }
                }
                return total;
            }
            catch { return 0; }
        }

        private static IEnumerable<string> SafeEntries(string directory)
        {
            try { return Directory.GetFileSystemEntries(directory); }
            catch { return new string[0]; }
        }

        internal static CleanResult Scan(Category category)
        {
            long bytes = ScanBytes(category);
            return new CleanResult
            {
                Id = category.Id,
                Before = bytes,
                After = bytes,
                Freed = 0,
                Note = category.Id == "component_store" ? "实际可释放空间由 DISM 计算" : ""
            };
        }

        internal static CleanResult Clean(Category category)
        {
            var result = new CleanResult { Id = category.Id, Note = "" };
            result.Before = ScanBytes(category);

            if (category.RequiresAdmin && !ToolkitApplication.IsAdministrator())
            {
                result.After = result.Before;
                result.Failures = 1;
                result.Note = "需要管理员权限";
                return result;
            }

            if (category.Id == "recycle_bin")
            {
                int code = SHEmptyRecycleBin(IntPtr.Zero, @"C:\", SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (code != 0) { result.Failures++; result.Note = "回收站清理返回代码 " + code; }
            }
            else if (category.Id == "component_store")
            {
                int code = RunProcess("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup");
                if (code != 0) { result.Failures++; result.Note = "DISM 退出代码 " + code; }
                else result.Note = "DISM 组件清理已完成";
            }
            else if (category.Id == "hibernation")
            {
                int code = RunProcess("powercfg.exe", "/hibernate off");
                if (code != 0) { result.Failures++; result.Note = "powercfg 退出代码 " + code; }
                else result.Note = "休眠与快速启动已关闭";
            }
            else if (category.Id == "update_cache")
            {
                List<string> running = StopUpdateServices(ref result.Failures);
                try { ClearStandard(category, ref result.Failures); }
                finally { RestartServices(running, ref result.Failures); }
            }
            else ClearStandard(category, ref result.Failures);

            result.After = ScanBytes(category);
            result.Freed = Math.Max(0, result.Before - result.After);
            return result;
        }

        private static void ClearStandard(Category category, ref int failures)
        {
            foreach (string target in ExistingTargets(category))
            {
                if (category.DeleteTargets) DeleteSafe(target, ref failures);
                else if (Directory.Exists(target))
                    foreach (string child in SafeEntries(target)) DeleteSafe(child, ref failures);
            }
        }

        private static void DeleteSafe(string path, ref int failures)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                bool directory = (attributes & FileAttributes.Directory) != 0;
                bool reparse = (attributes & FileAttributes.ReparsePoint) != 0;
                if (directory && !reparse)
                    foreach (string child in SafeEntries(path)) DeleteSafe(child, ref failures);

                if (directory) Directory.Delete(path, false);
                else
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch { failures++; }
        }

        private static List<string> StopUpdateServices(ref int failures)
        {
            var running = new List<string>();
            foreach (string name in new[] { "wuauserv", "bits" })
            {
                try
                {
                    using (var service = new ServiceController(name))
                    {
                        if (service.Status != ServiceControllerStatus.Running) continue;
                        running.Add(name);
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
                catch { failures++; }
            }
            return running;
        }

        private static void RestartServices(IEnumerable<string> services, ref int failures)
        {
            foreach (string name in services)
            {
                try
                {
                    using (var service = new ServiceController(name)) service.Start();
                }
                catch { failures++; }
            }
        }

        private static int RunProcess(string fileName, string arguments)
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            catch { return -1; }
        }

        internal static bool SelfTest()
        {
            string root = Path.Combine(Path.GetTempPath(), "DeepCleanC-Test-" + Guid.NewGuid().ToString("N"));
            try
            {
                string nested = Path.Combine(root, "nested");
                Directory.CreateDirectory(nested);
                File.WriteAllBytes(Path.Combine(nested, "sample.bin"), new byte[4096]);
                var test = Cat("test", "test", false, false, false, "", () => new[] { root });
                if (ScanBytes(test) != 4096) return false;
                CleanResult result = Clean(test);
                return result.Freed == 4096 && !File.Exists(Path.Combine(nested, "sample.bin"));
            }
            catch { return false; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch { }
            }
        }
    }

    internal class LiquidButton : Button
    {
        private Color accentColor;
        private bool hovered;
        private bool pressed;

        internal Color AccentColor
        {
            set
            {
                accentColor = value;
                ApplyColors();
            }
        }

        internal LiquidButton()
        {
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.ResizeRedraw, true);
            ApplyColors();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) pressed = true;
            Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) pressed = false;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) pressed = false;
            ApplyColors();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? FeatureTheme.Background : Parent.BackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            Color fill = BackColor;
            if (Enabled && pressed) fill = FlatAppearance.MouseDownBackColor;
            else if (Enabled && hovered) fill = FlatAppearance.MouseOverBackColor;
            Color ink = ForeColor;
            Color border = FlatAppearance.BorderColor;
            if (SystemInformation.HighContrast)
            {
                fill = Enabled && (hovered || pressed) ? SystemColors.Highlight : SystemColors.Control;
                ink = !Enabled ? SystemColors.GrayText
                    : hovered || pressed ? SystemColors.HighlightText : SystemColors.ControlText;
                border = SystemColors.ControlText;
            }
            float scale = e.Graphics.DpiX / 96F;
            using (var shape = FeatureTheme.RoundedRectangle(new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), 6F * scale))
            using (var surface = new SolidBrush(fill))
            using (var outline = new Pen(border))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(surface, shape);
                if (FlatAppearance.BorderSize > 0) e.Graphics.DrawPath(outline, shape);
            }

            var textArea = Rectangle.Inflate(ClientRectangle, -(int)(8 * scale), -2);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (Image != null)
            {
                int imageWidth = (int)(Image.Width * scale);
                int imageHeight = (int)(Image.Height * scale);
                e.Graphics.DrawImage(Image, new Rectangle((int)(2 * scale), (Height - imageHeight) / 2, imageWidth, imageHeight));
                textArea.X = (int)(48 * scale);
                textArea.Width = Math.Max(0, Width - textArea.X - (int)(8 * scale));
                flags |= TextFormatFlags.Left;
            }
            else flags |= TextFormatFlags.HorizontalCenter;
            if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(e.Graphics, Text, Font, textArea, ink, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ink, fill);
        }

        private void ApplyColors()
        {
            bool primary = Enabled && !accentColor.IsEmpty;
            BackColor = primary ? accentColor : Enabled ? Color.White : FeatureTheme.Header;
            ForeColor = primary ? Color.White : Enabled ? FeatureTheme.Text : FeatureTheme.Muted;
            FlatAppearance.BorderColor = primary ? accentColor : Enabled ? FeatureTheme.ControlBorder : FeatureTheme.Grid;
            FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Dark(accentColor, 0.08F) : FeatureTheme.Hover;
            FlatAppearance.MouseDownBackColor = primary ? ControlPaint.Dark(accentColor, 0.16F) : FeatureTheme.Selected;
            Invalidate();
        }
    }

    internal sealed class CleanerForm : Form
    {
        private readonly List<Category> categories;
        private readonly DataGridView grid = new FeatureGrid();
        private readonly LiquidButton scanButton = new LiquidButton();
        private readonly LiquidButton recommendedButton = new LiquidButton();
        private readonly LiquidButton clearButton = new LiquidButton();
        private readonly LiquidButton cleanButton = new LiquidButton();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Label summary = new Label();
        private readonly TextBox log = new TextBox();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly bool smokeTest;
        private bool busy;
        internal bool IsBusy { get { return busy; } }

        internal CleanerForm(bool smoke)
        {
            smokeTest = smoke;
            categories = Cleaner.CreateCategories();
            DoubleBuffered = true;
            BuildUi();
            worker.WorkerReportsProgress = true;
            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += (sender, e) =>
            {
                progress.Value = e.ProgressPercentage;
                summary.Text = (string)e.UserState;
            };
            worker.RunWorkerCompleted += WorkerCompleted;
            Shown += OnShown;
            FormClosing += OnClosing;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FeatureTheme.PaintBackground(this, e);
        }

        private void BuildUi()
        {
            Text = "C盘清理工具";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(924, 660);
            MinimumSize = new Size(780, 560);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = FeatureTheme.Background;

            var header = new Panel
            {
                Location = new Point(16, 10),
                Size = new Size(892, 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            Controls.Add(header);

            var title = new Label
            {
                Text = "空间清理",
                Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 0),
                ForeColor = FeatureTheme.Text,
                BackColor = Color.Transparent
            };
            header.Controls.Add(title);

            bool isAdmin = ToolkitApplication.IsAdministrator();
            var admin = new Label
            {
                AutoSize = true,
                Location = new Point(6, 44),
                ForeColor = isAdmin ? FeatureTheme.Success : FeatureTheme.Warning,
                BackColor = Color.Transparent,
                Text = isAdmin ? "● 已使用管理员权限" : "● 当前不是管理员，部分项目只能扫描"
            };
            header.Controls.Add(admin);

            grid.Location = new Point(20, 82);
            grid.Size = new Size(884, 378);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            FeatureTheme.StyleGrid(grid);
            grid.MultiSelect = false;
            grid.RowTemplate.Height = 31;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "清理", Width = 50 });
            grid.Columns[0].HeaderCell.Style.Padding = new Padding(2, 0, 2, 0);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "项目", Width = 180, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "可清理", Width = 100, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "说明", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", Width = 115, ReadOnly = true });
            foreach (Category category in categories)
            {
                int index = grid.Rows.Add(category.DefaultSelected, category.Name, "未扫描", category.Description,
                    category.RequiresAdmin && !isAdmin ? "需管理员权限" : "");
                grid.Rows[index].Tag = category;
            }
            Controls.Add(grid);

            SetupButton(scanButton, "扫描全部", new Point(20, 472), new Size(100, 34), ScanAll);
            SetupButton(recommendedButton, "选择推荐项", new Point(128, 472), new Size(110, 34), SelectRecommended);
            SetupButton(clearButton, "取消全选", new Point(246, 472), new Size(90, 34), ClearSelection);
            SetupButton(cleanButton, "清理勾选项", new Point(784, 472), new Size(120, 34), CleanSelected);
            cleanButton.AccentColor = FeatureTheme.Sky;
            cleanButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            progress.Location = new Point(20, 516);
            progress.Size = new Size(884, 6);
            progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progress.Style = ProgressBarStyle.Continuous;
            progress.Visible = false;
            Controls.Add(progress);

            summary.Text = "先扫描，再决定清理什么。不会扫描或删除桌面、下载、文档、照片。";
            summary.Location = new Point(20, 532);
            summary.Size = new Size(884, 22);
            summary.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            summary.ForeColor = FeatureTheme.Text;
            summary.BackColor = Color.Transparent;
            Controls.Add(summary);

            var logFrame = new FeatureFrame
            {
                Location = new Point(20, 558), Size = new Size(884, 88),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                HighlightFocus = true
            };
            log.Dock = DockStyle.Fill;
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            log.BorderStyle = BorderStyle.None;
            log.BackColor = FeatureTheme.Surface;
            log.ForeColor = FeatureTheme.Text;
            log.Text = "清理完成后，释放空间和跳过原因将显示在这里。" + Environment.NewLine;
            logFrame.Controls.Add(log);
            Controls.Add(logFrame);
        }

        private void SetupButton(Button button, string text, Point location, Size size, EventHandler handler)
        {
            button.Text = text;
            button.Location = location;
            button.Size = size;
            button.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            button.Click += handler;
            Controls.Add(button);
        }

        private void OnShown(object sender, EventArgs e)
        {
            Activate();
            if (smokeTest)
                return;
            ScanAll(sender, e);
        }

        private void ScanAll(object sender, EventArgs e)
        {
            StartWork(false, categories);
        }

        private void SelectRecommended(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in grid.Rows)
                row.Cells[0].Value = ((Category)row.Tag).DefaultSelected;
        }

        private void ClearSelection(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in grid.Rows) row.Cells[0].Value = false;
        }

        private void CleanSelected(object sender, EventArgs e)
        {
            grid.EndEdit();
            var selected = new List<Category>();
            foreach (DataGridViewRow row in grid.Rows)
                if (Convert.ToBoolean(row.Cells[0].Value)) selected.Add((Category)row.Tag);

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "请先勾选至少一个清理项目。", "没有选择项目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = "将清理 " + selected.Count + " 个项目。正在使用的文件会跳过，个人文件不会被处理。";
            if (MessageBox.Show(this, message, "确认清理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            var dangerWarnings = selected.Select(DangerWarning).Where(w => w != null).ToList();
            if (dangerWarnings.Count > 0)
            {
                string warning = "以下项目具有不可恢复或功能变化风险：\r\n\r\n• "
                    + String.Join("\r\n• ", dangerWarnings)
                    + "\r\n\r\n确定仍要继续吗？";
                if (MessageBox.Show(this, warning, "高风险项目二次确认", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            }
            log.Clear();
            StartWork(true, selected);
        }

        internal static string DangerWarning(Category category)
        {
            switch (category.Id)
            {
                case "recycle_bin": return "回收站：文件清空后无法从回收站恢复";
                case "memory_dumps": return "系统内存转储：会删除近期蓝屏诊断材料";
                case "dev_caches": return "开发工具缓存：后续构建可能需要重新下载依赖";
                case "component_store": return "系统组件存储维护：DISM 会清理旧组件，过程可能较久";
                case "hibernation": return "关闭休眠：将同时关闭休眠和 Windows 快速启动";
                default: return null;
            }
        }

        internal static bool ConfirmationSelfTest()
        {
            string[] dangerous = { "recycle_bin", "memory_dumps", "dev_caches", "component_store", "hibernation" };
            return dangerous.All(id => DangerWarning(new Category { Id = id }) != null)
                && DangerWarning(new Category { Id = "browser_cache" }) == null;
        }

        private void StartWork(bool clean, IEnumerable<Category> selected)
        {
            progress.Value = 0;
            SetBusy(true);
            summary.Text = clean ? "正在清理，请勿关闭窗口…" : "正在扫描，请稍候…";
            worker.RunWorkerAsync(new WorkRequest { Clean = clean, Categories = selected.ToList() });
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            var request = (WorkRequest)e.Argument;
            var results = new List<CleanResult>();
            int count = request.Categories.Count;
            string action = request.Clean ? "清理" : "扫描";
            foreach (Category category in request.Categories)
            {
                worker.ReportProgress(results.Count * 100 / count,
                    String.Format("正在{0} {1}/{2} · {3}", action, results.Count + 1, count, category.Name));
                results.Add(request.Clean ? Cleaner.Clean(category) : Cleaner.Scan(category));
            }
            e.Result = new object[] { request.Clean, results };
        }

        private void WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null) throw e.Error;
                var payload = (object[])e.Result;
                bool cleaned = (bool)payload[0];
                var results = (List<CleanResult>)payload[1];
                bool isAdmin = ToolkitApplication.IsAdministrator();
                long total = 0;
                foreach (CleanResult result in results)
                {
                    DataGridViewRow row = grid.Rows.Cast<DataGridViewRow>().First(r => ((Category)r.Tag).Id == result.Id);
                    row.Cells[2].Value = result.Id == "component_store" ? "系统计算" : FormatBytes(result.After);
                    if (cleaned)
                    {
                        total += result.Freed;
                        row.Cells[4].Value = result.Failures > 0 ? "跳过 " + result.Failures + " 项" : "已完成";
                        log.AppendText("[" + ((Category)row.Tag).Name + "] 释放 " + FormatBytes(result.Freed)
                            + (String.IsNullOrEmpty(result.Note) ? "" : "；" + result.Note) + Environment.NewLine);
                    }
                    else
                    {
                        total += result.Before;
                        row.Cells[4].Value = ((Category)row.Tag).RequiresAdmin && !isAdmin ? "需管理员权限" : "已扫描";
                    }
                }
                summary.Text = cleaned
                    ? "清理完成：本次实际释放 " + FormatBytes(total) + "。被占用或无权限的项目已安全跳过。"
                    : "扫描完成：以上项目共占用 " + FormatBytes(total) + "。请确认勾选后清理。";
            }
            catch (Exception ex)
            {
                summary.Text = "操作失败，详情已写入日志。";
                log.AppendText(ex.Message + Environment.NewLine);
            }
            finally { SetBusy(false); }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            scanButton.Enabled = !value;
            recommendedButton.Enabled = !value;
            clearButton.Enabled = !value;
            cleanButton.Enabled = !value;
            grid.Enabled = !value;
            progress.Visible = value;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "扫描或清理仍在进行，请完成后再关闭。", "操作进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1099511627776L) return (bytes / 1099511627776D).ToString("N2") + " TB";
            if (bytes >= 1073741824L) return (bytes / 1073741824D).ToString("N2") + " GB";
            if (bytes >= 1048576L) return (bytes / 1048576D).ToString("N1") + " MB";
            if (bytes >= 1024L) return (bytes / 1024D).ToString("N1") + " KB";
            return bytes + " B";
        }
    }
}
