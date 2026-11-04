using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Drawing.Text;

namespace DeepCleanC
{
    internal enum AppPage
    {
        Cleanup,
        Uninstaller,
        ForceDelete,
        Update
    }

    internal sealed class NavButton : Button
    {
        private readonly Bitmap normalIcon;
        private readonly Bitmap selectedIcon;

        internal NavButton(AppPage page, Color accent)
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            ForeColor = FeatureTheme.Text;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10F);
            Padding = new Padding(2, 0, 8, 0);
            ImageAlign = ContentAlignment.MiddleLeft;
            TextAlign = ContentAlignment.MiddleLeft;
            TextImageRelation = TextImageRelation.ImageBeforeText;
            normalIcon = CreateIcon(page, FeatureTheme.Muted, false);
            selectedIcon = CreateIcon(page, accent, true);
            SetSelected(false);
        }

        internal void SetSelected(bool selected)
        {
            BackColor = selected ? Color.FromArgb(226, 235, 250) : FeatureTheme.Navigation;
            FlatAppearance.MouseOverBackColor = selected ? BackColor : Color.FromArgb(230, 235, 243);
            FlatAppearance.MouseDownBackColor = FeatureTheme.Grid;
            Image = selected ? selectedIcon : normalIcon;
        }

        private static Bitmap CreateIcon(AppPage page, Color color, bool selected)
        {
            // The Windows icon family shares one em square across all four glyphs.
            string glyph = page == AppPage.Cleanup ? "\uEDA2" : page == AppPage.Uninstaller ? "\uE773"
                : page == AppPage.ForceDelete ? "\uE74D" : "\uE777";
            using (var fluent = new Font("Segoe Fluent Icons", 18, GraphicsUnit.Pixel))
            using (var legacy = new Font("Segoe MDL2 Assets", 18, GraphicsUnit.Pixel))
            {
                var image = new Bitmap(44, 24);
                using (Graphics graphics = Graphics.FromImage(image))
                using (var ink = new SolidBrush(color))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.DrawString(glyph, fluent.Name == "Segoe Fluent Icons" ? fluent : legacy, ink,
                        new RectangleF(10, 0, 24, 24), format);
                    if (selected)
                        using (var pen = new Pen(color, 3F) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            graphics.DrawLine(pen, 2, 5, 2, 19);
                }
                return image;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Image = null;
                normalIcon.Dispose();
                selectedIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class AppShellForm : Form
    {
        private readonly AppPage initialPage;
        private readonly bool smokeTest;
        private readonly string[] initialPaths;
        private readonly Panel content = new Panel();
        private readonly Dictionary<AppPage, Form> pages = new Dictionary<AppPage, Form>();
        private readonly Dictionary<AppPage, NavButton> navButtons = new Dictionary<AppPage, NavButton>();
        private Icon brandIcon;
        private Bitmap brandImage;

        internal AppShellForm(AppPage page, bool smoke, string[] paths)
        {
            initialPage = page;
            smokeTest = smoke;
            initialPaths = paths ?? new string[0];
            DoubleBuffered = true;
            BuildUi();
            Shown += OnShown;
        }

        private void BuildUi()
        {
            Text = RiverDesk.VersionInfo.Name;
            brandIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (brandIcon != null)
            {
                Icon = brandIcon;
                brandImage = brandIcon.ToBitmap();
            }
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1160, 760);
            MinimumSize = new Size(1040, 680);
            Font = new Font("Segoe UI", 10F);
            BackColor = FeatureTheme.Background;
            AutoScaleMode = AutoScaleMode.Dpi;

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = FeatureTheme.Navigation };
            Controls.Add(sidebar);

            var brandMark = new PictureBox
            {
                Location = new Point(22, 25),
                Size = new Size(34, 34),
                BackColor = Color.Transparent,
                Image = brandImage,
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            sidebar.Controls.Add(brandMark);

            sidebar.Controls.Add(new Label
            {
                Text = "RiverDesk",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = FeatureTheme.Text,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(66, 24)
            });
            sidebar.Controls.Add(new Label
            {
                Text = "Toolkit",
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(68, 51)
            });

            AddNavButton(sidebar, AppPage.Cleanup, "空间清理", 112, FeatureTheme.Sky);
            AddNavButton(sidebar, AppPage.Uninstaller, "软件卸载", 160, FeatureTheme.Sky);
            AddNavButton(sidebar, AppPage.ForceDelete, "强制删除", 208, FeatureTheme.Sky);
            AddNavButton(sidebar, AppPage.Update, "软件更新", 256, FeatureTheme.Sky);

            sidebar.Controls.Add(new Label
            {
                Text = "仅在本机运行",
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(24, 720),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            });

            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(8, 8, 8, 8);
            content.BackColor = FeatureTheme.Background;
            Controls.Add(content);
            content.BringToFront();
        }

        private void AddNavButton(Control parent, AppPage page, string text, int top, Color accent)
        {
            var button = new NavButton(page, accent)
            {
                Text = text,
                Location = new Point(16, top),
                Size = new Size(178, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            button.Click += (sender, args) => ShowPage(page);
            navButtons[page] = button;
            parent.Controls.Add(button);
        }

        private void OnShown(object sender, EventArgs e)
        {
            navButtons[initialPage].PerformClick();
            if (!smokeTest) return;

            var timer = new Timer();
            timer.Interval = initialPage == AppPage.Uninstaller ? 4200 : 900;
            timer.Tick += (timerSender, timerArgs) =>
            {
                timer.Dispose();
                string variable = initialPage == AppPage.Update ? "RIVERDESK_UPDATE_SNAPSHOT"
                    : initialPage == AppPage.Cleanup ? "DEEPCLEAN_SNAPSHOT"
                    : (initialPage == AppPage.Uninstaller ? "DEEPCLEAN_UNINSTALLER_SNAPSHOT" : "DEEPCLEAN_FORCE_DELETE_SNAPSHOT");
                string snapshot = Environment.GetEnvironmentVariable(variable);
                if (!String.IsNullOrWhiteSpace(snapshot))
                {
                    using (var bitmap = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
                        bitmap.Save(snapshot, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                Close();
            };
            timer.Start();
        }

        private void ShowPage(AppPage page)
        {
            foreach (KeyValuePair<AppPage, NavButton> pair in navButtons)
                pair.Value.SetSelected(pair.Key == page);

            Form form;
            if (!pages.TryGetValue(page, out form))
            {
                if (page == AppPage.Cleanup) form = new CleanerForm(smokeTest);
                else if (page == AppPage.Uninstaller) form = new UninstallerForm(false);
                else if (page == AppPage.Update) form = new RiverDesk.UpdateForm(CanClose);
                else form = new ForceDeleteForm(false, initialPaths);

                form.TopLevel = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.MinimumSize = Size.Empty;
                form.Dock = DockStyle.Fill;
                pages[page] = form;
            }

            content.SuspendLayout();
            content.Controls.Clear();
            content.Controls.Add(form);
            form.Show();
            form.BringToFront();
            content.ResumeLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!CanClose())
            {
                e.Cancel = true;
                if (!smokeTest) MessageBox.Show(this, "请等待当前任务结束后再关闭。", "操作进行中");
            }
            base.OnFormClosing(e);
        }

        private bool CanClose()
        {
            foreach (Form page in pages.Values)
            {
                var cleanup = page as CleanerForm;
                var uninstall = page as UninstallerForm;
                var forceDelete = page as ForceDeleteForm;
                var update = page as RiverDesk.UpdateForm;
                if ((cleanup != null && cleanup.IsBusy) || (uninstall != null && uninstall.IsBusy)
                    || (forceDelete != null && forceDelete.IsBusy) || (update != null && update.IsBusy))
                    return false;
            }
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (Form page in pages.Values) page.Dispose();
            base.Dispose(disposing);
            if (disposing)
            {
                if (brandImage != null) brandImage.Dispose();
                if (brandIcon != null) brandIcon.Dispose();
            }
        }
    }
}
