using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace DeepCleanC
{
    internal sealed class ForceDeleteTarget
    {
        internal string FullPath;
        internal string Name;
        internal string Kind;
        internal string Status;
    }

    internal sealed class ForceDeleteResult
    {
        internal ForceDeleteTarget Target;
        internal bool Success;
        internal string Message;
    }

    internal static class FeatureTheme
    {
        internal static readonly Color Background = GlassTheme.Choose(Color.White, Color.FromArgb(12, 22, 39));
        internal static readonly Color Navigation = GlassTheme.Choose(Color.FromArgb(244, 247, 251), Color.FromArgb(12, 23, 42));
        internal static readonly Color Surface = GlassTheme.Choose(Color.White, Color.FromArgb(17, 31, 49));
        internal static readonly Color SurfaceAlt = GlassTheme.Choose(Color.FromArgb(248, 250, 253), Color.FromArgb(20, 37, 57));
        internal static readonly Color Header = GlassTheme.Choose(Color.FromArgb(240, 244, 249), Color.FromArgb(28, 49, 71));
        internal static readonly Color Hover = GlassTheme.Choose(Color.FromArgb(231, 238, 248), Color.FromArgb(32, 65, 91));
        internal static readonly Color Selected = GlassTheme.Choose(Color.FromArgb(218, 234, 251), Color.FromArgb(30, 72, 100));
        internal static readonly Color Text = GlassTheme.Choose(Color.FromArgb(27, 42, 64), Color.FromArgb(228, 241, 253));
        internal static readonly Color Muted = GlassTheme.Choose(Color.FromArgb(76, 94, 119), Color.FromArgb(164, 188, 213));
        internal static readonly Color Success = GlassTheme.Choose(Color.FromArgb(24, 116, 84), Color.FromArgb(111, 224, 174));
        internal static readonly Color Warning = GlassTheme.Choose(Color.FromArgb(143, 88, 17), Color.FromArgb(255, 210, 135));
        internal static readonly Color Danger = Color.FromArgb(184, 53, 73);
        internal static readonly Color Grid = GlassTheme.Choose(Color.FromArgb(226, 233, 242), Color.FromArgb(48, 73, 96));
        internal static readonly Color ControlBorder = GlassTheme.Choose(Color.FromArgb(133, 150, 173), Color.FromArgb(110, 149, 180));
        internal static readonly Color Indigo = Color.FromArgb(87, 78, 185);
        internal static readonly Color Sky = Color.FromArgb(33, 100, 157);
        internal static readonly Color AccentText = GlassTheme.Choose(Sky, Color.FromArgb(151, 225, 255));

        internal static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void PaintBackground(Control control, PaintEventArgs e)
        {
            if (GlassTheme.Enabled)
            {
                GlassTheme.DrawBackdrop(e.Graphics, control);
                using (var veil = new SolidBrush(Color.FromArgb(155, Background)))
                    e.Graphics.FillRectangle(veil, e.ClipRectangle);
            }
            else
                using (var background = new SolidBrush(Background))
                    e.Graphics.FillRectangle(background, e.ClipRectangle);
        }

        internal static void StyleGrid(DataGridView grid)
        {
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Grid;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Header,
                ForeColor = Text,
                SelectionForeColor = Text,
                SelectionBackColor = Header,
                Padding = new Padding(8, 0, 8, 0),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = Text,
                SelectionBackColor = Selected,
                SelectionForeColor = Text,
                Padding = new Padding(8, 4, 8, 4)
            };
            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = SurfaceAlt };
            grid.RowTemplate.Height = 36;
        }
    }

    internal sealed class FeatureFrame : Panel
    {
        internal bool HighlightFocus { get; set; }

        internal FeatureFrame()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            BackColor = FeatureTheme.Surface;
            Padding = new Padding(10);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            if (HighlightFocus) Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            if (HighlightFocus) Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!GlassTheme.DrawControlBackground(e, this))
                using (var background = new SolidBrush(Parent == null ? FeatureTheme.Background : Parent.BackColor))
                    e.Graphics.FillRectangle(background, e.ClipRectangle);
            if (Width < 2 || Height < 2) return;
            float radius = Height < 64 ? Height / 2F : 20F;
            if (GlassTheme.DrawSurface(e.Graphics, this, radius, BackColor, 140)) return;
            Color border = HighlightFocus && ContainsFocus ? FeatureTheme.Sky : FeatureTheme.Grid;
            Color fill = BackColor;
            if (SystemInformation.HighContrast)
            {
                border = SystemColors.WindowText;
                fill = SystemColors.Window;
            }
            using (var shape = FeatureTheme.RoundedRectangle(new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), 8F * e.Graphics.DpiX / 96F))
            using (var surface = new SolidBrush(fill))
            using (var outline = new Pen(border))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(surface, shape);
                e.Graphics.DrawPath(outline, shape);
            }
        }
    }

    internal sealed class FeatureGrid : DataGridView
    {
        internal FeatureGrid()
        {
            DoubleBuffered = true;
        }
    }

    internal sealed class LogView : Panel
    {
        private readonly Label content = new Label
        {
            AutoSize = true, UseMnemonic = false, TabStop = false,
            Location = Point.Empty, BackColor = Color.Transparent
        };

        internal LogView()
        {
            AutoScroll = true;
            TabStop = false;
            Cursor = Cursors.Default;
            AccessibleName = "清理记录";
            Controls.Add(content);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            content.Text = Text;
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            content.MaximumSize = new Size(Math.Max(1, ClientSize.Width - 1), 0);
        }

        internal void AppendText(string value)
        {
            Text += value;
            AutoScrollPosition = new Point(0, content.Height);
        }

        internal void Clear()
        {
            Text = String.Empty;
            AutoScrollPosition = Point.Empty;
        }
    }

    internal static class ForceDeleteSafety
    {
        internal static string Normalize(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("路径为空");
            string input = value.Trim().Trim('"');
            if (input.Length < 3 || input[1] != ':' || (input[2] != '\\' && input[2] != '/')
                || input.IndexOf(':', 2) >= 0)
                throw new InvalidOperationException("请选择本机磁盘上的完整路径");
            string full = Path.GetFullPath(input);
            string root = Path.GetPathRoot(full);
            if (!String.IsNullOrEmpty(root) && full.Length > root.Length)
                full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full;
        }

        internal static void AssertAllowed(string fullPath)
        {
            string root = Normalize(Path.GetPathRoot(fullPath));
            if (Same(fullPath, root)) throw new InvalidOperationException("拒绝删除磁盘根目录");
            if (fullPath.IndexOf('~') >= 0)
                throw new InvalidOperationException("请选择完整名称，不使用短路径别名");
            var device = new System.Text.StringBuilder(2048);
            if (QueryDosDevice(root.TrimEnd('\\'), device, device.Capacity) == 0
                || device.ToString().StartsWith(@"\??\", StringComparison.Ordinal)
                || new DriveInfo(root).DriveType == DriveType.Network)
                throw new InvalidOperationException("拒绝通过映射或替代驱动器删除");

            string windows = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (SameOrChild(fullPath, windows)) throw new InvalidOperationException("拒绝删除 Windows 目录或其中的系统文件");

            foreach (string container in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            })
            {
                if (!String.IsNullOrWhiteSpace(container) && SameOrChild(Normalize(container), fullPath))
                    throw new InvalidOperationException("拒绝删除受保护的顶层目录");
            }

            foreach (string name in new[] { "bootmgr", "Boot", "EFI", "Recovery", "pagefile.sys", "hiberfil.sys", "swapfile.sys", "System Volume Information", "$Recycle.Bin" })
            {
                string critical = Normalize(Path.Combine(Path.GetPathRoot(fullPath), name));
                if (SameOrChild(fullPath, critical)) throw new InvalidOperationException("拒绝删除关键系统目标");
            }

            string executable = Normalize(Application.ExecutablePath);
            string library = Normalize(typeof(ToolkitApplication).Assembly.Location);
            if (SameOrChild(executable, fullPath) || SameOrChild(library, fullPath))
                throw new InvalidOperationException("拒绝删除当前工具或其所在目录");

            for (string parent = Path.GetDirectoryName(fullPath); !Same(parent, root); parent = Path.GetDirectoryName(parent))
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("拒绝通过目录链接删除其中的内容");
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern uint QueryDosDevice(string name, System.Text.StringBuilder target, int capacity);

        internal static bool IsAllowed(string fullPath)
        {
            try { AssertAllowed(Normalize(fullPath)); return true; }
            catch { return false; }
        }

        private static bool Same(string first, string second)
        {
            return String.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameOrChild(string candidate, string basePath)
        {
            return Same(candidate, basePath) || candidate.StartsWith(basePath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ForceDeleteEngine
    {
        internal static ForceDeleteResult Delete(ForceDeleteTarget target)
        {
            var result = new ForceDeleteResult { Target = target };
            try
            {
                string path = ForceDeleteSafety.Normalize(target.FullPath);
                ForceDeleteSafety.AssertAllowed(path);
                DeleteEntry(path, ToolkitApplication.IsAdministrator());
                try
                {
                    File.GetAttributes(path);
                    throw new IOException("删除后目标仍然存在");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                result.Success = true;
                result.Message = "已删除并验证";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = FriendlyError(ex);
            }
            return result;
        }

        private static void GrantAccess(string path)
        {
            Run("takeown.exe", "/F " + Quote(path));
            using (var identity = WindowsIdentity.GetCurrent())
                Run("icacls.exe", Quote(path) + " /L /grant *" + identity.User.Value + ":F /Q");
        }

        private static void DeleteEntry(string path, bool administrator)
        {
            FileAttributes attributes = File.GetAttributes(path);
            bool directory = (attributes & FileAttributes.Directory) != 0;
            bool reparse = (attributes & FileAttributes.ReparsePoint) != 0;
            if (reparse)
            {
                if (directory) Directory.Delete(path, false);
                else File.Delete(path);
                return;
            }

            string[] children = null;
            try
            {
                if (directory) children = Directory.GetFileSystemEntries(path);
                ClearRestrictiveAttributes(path, attributes);
                if (!directory) File.Delete(path);
            }
            catch (UnauthorizedAccessException)
            {
                if (!administrator) throw;
                GrantAccess(path);
                DeleteEntry(path, false);
                return;
            }
            if (!directory) return;

            foreach (string child in children) DeleteEntry(child, administrator);
            try { Directory.Delete(path, false); }
            catch (UnauthorizedAccessException)
            {
                if (!administrator) throw;
                GrantAccess(path);
                Directory.Delete(path, false);
            }
        }

        private static void ClearRestrictiveAttributes(string path, FileAttributes attributes)
        {
            FileAttributes mask = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;
            if ((attributes & mask) != 0) File.SetAttributes(path, attributes & ~mask);
        }

        private static void Run(string fileName, string arguments)
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, fileName),
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (process != null) process.WaitForExit();
                }
            }
            catch { }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "访问被拒绝；请确认管理员权限并关闭占用程序";
            if (ex is IOException) return "目标正在被其他程序使用，或目录仍包含被占用项目";
            return ex.Message;
        }

        internal static bool SelfTest()
        {
            string root = Path.Combine(Path.GetTempPath(), "ForceDelete-Test-" + Guid.NewGuid().ToString("N"));
            string outside = root + "-outside";
            try
            {
                Directory.CreateDirectory(root);
                string file = Path.Combine(root, "sample.tmp");
                File.WriteAllText(file, "test");
                File.SetAttributes(file, FileAttributes.ReadOnly);
                var result = Delete(new ForceDeleteTarget { FullPath = file, Name = "sample.tmp", Kind = "文件" });
                if (!result.Success || File.Exists(file)) return false;

                string tree = Path.Combine(root, "tree");
                string junction = Path.Combine(tree, "external-link");
                string sentinel = Path.Combine(outside, "keep.txt");
                Directory.CreateDirectory(tree);
                Directory.CreateDirectory(outside);
                File.WriteAllText(sentinel, "keep");
                bool junctionCreated = CreateJunction(junction, outside);
                var treeResult = Delete(new ForceDeleteTarget { FullPath = tree, Name = "tree", Kind = "文件夹" });
                if (!treeResult.Success || Directory.Exists(tree)) return false;
                if (junctionCreated && !File.Exists(sentinel)) return false;

                return !ForceDeleteSafety.IsAllowed(Path.GetPathRoot(root))
                    && !ForceDeleteSafety.IsAllowed(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                    && !ForceDeleteSafety.IsAllowed(Application.ExecutablePath)
                    && !ForceDeleteSafety.IsAllowed(typeof(ToolkitApplication).Assembly.Location);
            }
            catch { return false; }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch { }
                try { if (Directory.Exists(outside)) Directory.Delete(outside, true); }
                catch { }
            }
        }

        private static bool CreateJunction(string junction, string target)
        {
            try
            {
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/d /c mklink /J " + Quote(junction) + " " + Quote(target),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (process == null) return false;
                    process.WaitForExit();
                    return process.ExitCode == 0 && Directory.Exists(junction);
                }
            }
            catch { return false; }
        }
    }

    internal sealed class ForceDeleteForm : Form
    {
        private readonly List<ForceDeleteTarget> targets = new List<ForceDeleteTarget>();
        private readonly HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string[] initialPaths;
        private readonly bool smokeTest;
        private readonly DataGridView grid = new FeatureGrid();
        private readonly Label status = new Label();
        private readonly Label count = new Label();
        private readonly LiquidButton addFiles = new LiquidButton();
        private readonly LiquidButton addFolder = new LiquidButton();
        private readonly LiquidButton remove = new LiquidButton();
        private readonly LiquidButton clear = new LiquidButton();
        private readonly LiquidButton delete = new LiquidButton();
        private bool busy;
        internal bool IsBusy { get { return busy; } }

        internal ForceDeleteForm(bool smoke, string[] pathsToAdd)
        {
            smokeTest = smoke;
            initialPaths = pathsToAdd ?? new string[0];
            DoubleBuffered = true;
            BuildUi();
            Shown += OnShown;
            FormClosing += OnClosing;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FeatureTheme.PaintBackground(this, e);
        }

        private void BuildUi()
        {
            Text = "强制删除 · RiverDesk Toolkit";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(760, 520);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = FeatureTheme.Background;
            AllowDrop = true;

            var header = new Panel
            {
                Location = new Point(16, 10),
                Size = new Size(852, 76),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            Controls.Add(header);

            var title = new Label
            {
                Text = "强制删除",
                Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 0),
                ForeColor = FeatureTheme.Text,
                BackColor = Color.Transparent
            };
            header.Controls.Add(title);

            var subtitle = new Label
            {
                Text = "永久删除明确选择的文件或文件夹；不会进入回收站。",
                AutoSize = true,
                Location = new Point(6, 44),
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent
            };
            header.Controls.Add(subtitle);

            var admin = new Label
            {
                Text = ToolkitApplication.IsAdministrator() ? "● 管理员权限" : "● 缺少管理员权限",
                AutoSize = true,
                Location = new Point(700, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = ToolkitApplication.IsAdministrator() ? FeatureTheme.Success : FeatureTheme.Warning,
                BackColor = Color.Transparent
            };
            header.Controls.Add(admin);

            SetupButton(addFiles, "添加文件", new Point(20, 98), new Size(100, 34), AddFilesClicked);
            SetupButton(addFolder, "添加文件夹", new Point(128, 98), new Size(112, 34), AddFolderClicked);
            SetupButton(remove, "移除选中", new Point(248, 98), new Size(104, 34), RemoveClicked);
            SetupButton(clear, "清空清单", new Point(360, 98), new Size(100, 34), ClearClicked);

            var dropHint = new Label
            {
                Text = "可将目标拖到软件的 EXE 上",
                Location = new Point(478, 105),
                Size = new Size(390, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent
            };
            Controls.Add(dropHint);

            grid.Location = new Point(20, 142);
            grid.Size = new Size(848, 340);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.ReadOnly = true;
            FeatureTheme.StyleGrid(grid);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "类型", Width = 72 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", Width = 185 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "完整路径", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", Width = 116 });
            grid.SelectionChanged += (sender, args) => remove.Enabled = !busy && grid.SelectedRows.Count > 0;
            grid.KeyDown += (sender, args) =>
            {
                if (!busy && args.KeyCode == Keys.Delete) { RemoveSelected(); args.Handled = true; }
            };
            Controls.Add(grid);

            grid.Paint += (sender, args) =>
            {
                if (grid.Rows.Count != 0) return;
                var area = new Rectangle(0, grid.ColumnHeadersHeight, grid.ClientSize.Width,
                    grid.ClientSize.Height - grid.ColumnHeadersHeight);
                TextRenderer.DrawText(args.Graphics, "清单中还没有文件\r\n点击「添加文件」或「添加文件夹」开始",
                    grid.Font, area, FeatureTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };

            count.Location = new Point(20, 500);
            count.Size = new Size(500, 24);
            count.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            count.ForeColor = FeatureTheme.Text;
            count.BackColor = Color.Transparent;
            count.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            Controls.Add(count);

            status.Location = new Point(20, 530);
            status.Size = new Size(620, 38);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.ForeColor = FeatureTheme.Muted;
            status.BackColor = Color.Transparent;
            status.Text = "核对清单后，再确认永久删除。";
            Controls.Add(status);

            SetupButton(delete, "永久删除", new Point(688, 518), new Size(180, 42), DeleteClicked);
            delete.AccentColor = FeatureTheme.Danger;
            delete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            DragEnter += DragEntered;
            DragLeave += (sender, args) => status.Text = "删除结果会在这里显示。";
            DragDrop += DragDropped;
            UpdateState();
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
            if (initialPaths.Length > 0) AddTargets(initialPaths);
            if (!smokeTest) return;
            BeginInvoke(new Action(() =>
            {
                string snapshot = Environment.GetEnvironmentVariable("DEEPCLEAN_FORCE_DELETE_SNAPSHOT");
                if (!String.IsNullOrWhiteSpace(snapshot))
                {
                    using (var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height))
                    {
                        DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
                        bitmap.Save(snapshot, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                Close();
            }));
        }

        private void DragEntered(object sender, DragEventArgs e)
        {
            if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                status.Text = "松开鼠标即可加入清单。";
            }
            else e.Effect = DragDropEffects.None;
        }

        private void DragDropped(object sender, DragEventArgs e)
        {
            if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop))
                AddTargets((string[])e.Data.GetData(DataFormats.FileDrop));
        }

        private void AddFilesClicked(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Multiselect = true, Title = "选择要永久删除的文件", Filter = "所有文件 (*.*)|*.*" })
                if (dialog.ShowDialog(this) == DialogResult.OK) AddTargets(dialog.FileNames);
        }

        private void AddFolderClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "选择要永久删除的文件夹", ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) AddTargets(new[] { dialog.SelectedPath });
        }

        private void AddTargets(IEnumerable<string> sourcePaths)
        {
            var rejected = new List<string>();
            int added = 0;
            foreach (string raw in sourcePaths)
            {
                try
                {
                    string path = ForceDeleteSafety.Normalize(raw);
                    ForceDeleteSafety.AssertAllowed(path);
                    bool directory = Directory.Exists(path);
                    if (!directory && !File.Exists(path)) throw new FileNotFoundException("目标不存在");
                    if (!paths.Add(path)) continue;
                    var target = new ForceDeleteTarget
                    {
                        FullPath = path,
                        Name = directory ? new DirectoryInfo(path).Name : Path.GetFileName(path),
                        Kind = directory ? "文件夹" : "文件",
                        Status = "等待删除"
                    };
                    targets.Add(target);
                    int row = grid.Rows.Add(target.Kind, target.Name, target.FullPath, target.Status);
                    grid.Rows[row].Tag = target;
                    added++;
                }
                catch (Exception ex) { rejected.Add((raw ?? "") + "：" + ex.Message); }
            }
            UpdateState();
            if (added > 0) status.Text = "已加入 " + added + " 个目标，请核对完整路径。";
            if (rejected.Count > 0)
                MessageBox.Show(this, "以下目标未加入：\r\n\r\n" + String.Join("\r\n", rejected.Take(8)), "无法加入目标", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void RemoveClicked(object sender, EventArgs e) { RemoveSelected(); }

        private void RemoveSelected()
        {
            foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(row => row.Index).ToList())
            {
                var target = row.Tag as ForceDeleteTarget;
                if (target != null) { targets.Remove(target); paths.Remove(target.FullPath); }
                grid.Rows.RemoveAt(row.Index);
            }
            status.Text = "已从清单移除选中项；磁盘内容没有改变。";
            UpdateState();
        }

        private void ClearClicked(object sender, EventArgs e)
        {
            targets.Clear();
            paths.Clear();
            grid.Rows.Clear();
            status.Text = "清单已清空；磁盘内容没有改变。";
            UpdateState();
        }

        private void DeleteClicked(object sender, EventArgs e)
        {
            if (!ToolkitApplication.IsAdministrator())
            {
                MessageBox.Show(this, "请关闭软件后重新以管理员身份运行。", "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var pending = targets.Where(target => File.Exists(target.FullPath) || Directory.Exists(target.FullPath)).ToList();
            if (pending.Count == 0) return;
            if (MessageBox.Show(this, "将永久删除 " + pending.Count + " 个目标，不会进入回收站，也无法撤销。\r\n\r\n是否继续？",
                "确认永久删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            SetBusy(true);
            status.Text = "正在接管权限、删除并验证……";
            var worker = new BackgroundWorker();
            worker.DoWork += (workerSender, args) => args.Result = pending.Select(ForceDeleteEngine.Delete).ToList();
            worker.RunWorkerCompleted += DeleteCompleted;
            worker.RunWorkerAsync();
        }

        private void DeleteCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            if (e.Error != null)
            {
                status.Text = "操作失败：" + e.Error.Message;
                status.ForeColor = FeatureTheme.Danger;
                return;
            }

            var results = (List<ForceDeleteResult>)e.Result;
            foreach (ForceDeleteResult result in results)
            {
                result.Target.Status = result.Success ? "已删除" : "删除失败";
                foreach (DataGridViewRow row in grid.Rows)
                    if (ReferenceEquals(row.Tag, result.Target))
                    {
                        row.Cells[3].Value = result.Target.Status;
                        row.Cells[3].Style.ForeColor = result.Success ? FeatureTheme.Success : FeatureTheme.Danger;
                        row.Cells[3].ToolTipText = result.Message;
                    }
            }

            int successCount = results.Count(result => result.Success);
            int failureCount = results.Count - successCount;
            status.Text = failureCount == 0
                ? "已删除并验证 " + successCount + " 个目标。"
                : "成功 " + successCount + " 项，失败 " + failureCount + " 项；把鼠标停在状态上查看原因。";
            status.ForeColor = failureCount == 0 ? FeatureTheme.Success : FeatureTheme.Danger;
            UpdateState();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            addFiles.Enabled = !value;
            addFolder.Enabled = !value;
            remove.Enabled = !value && grid.SelectedRows.Count > 0;
            clear.Enabled = !value && targets.Count > 0;
            delete.Enabled = !value && targets.Any(target => File.Exists(target.FullPath) || Directory.Exists(target.FullPath));
            grid.Enabled = !value;
            UseWaitCursor = value;
        }

        private void UpdateState()
        {
            int pending = targets.Count(target => File.Exists(target.FullPath) || Directory.Exists(target.FullPath));
            count.Text = targets.Count == 0 ? "清单为空" : "清单中有 " + targets.Count + " 项 · 待处理 " + pending + " 项";
            remove.Enabled = !busy && grid.SelectedRows.Count > 0;
            clear.Enabled = !busy && targets.Count > 0;
            delete.Enabled = !busy && pending > 0;
            delete.Text = pending > 0 ? "永久删除  " + pending + " 项" : "永久删除";
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (!busy) return;
            e.Cancel = true;
            MessageBox.Show(this, "删除仍在进行，请完成后再关闭。", "操作进行中", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
