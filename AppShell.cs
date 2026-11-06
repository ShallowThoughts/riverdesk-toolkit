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

    internal sealed class NavButton : Control
    {
        private readonly Bitmap normalIcon;
        private readonly Bitmap selectedIcon;
        private bool selected;
        private bool hovered;
        private bool spacePressed;

        internal NavButton(AppPage page, Color accent)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            ForeColor = FeatureTheme.Text;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 10F);
            normalIcon = CreateIcon(page, FeatureTheme.Muted, false);
            selectedIcon = CreateIcon(page, GlassTheme.Enabled ? FeatureTheme.AccentText : accent, true);
            SetSelected(false);
        }

        internal void SetSelected(bool selected)
        {
            this.selected = selected;
            BackColor = selected ? FeatureTheme.Selected : FeatureTheme.Navigation;
            ForeColor = selected ? FeatureTheme.AccentText : FeatureTheme.Text;
            AccessibleDescription = selected ? "当前页面" : "打开" + Text;
            Invalidate();
        }

        internal void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || keyData == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter)
            {
                PerformClick();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                spacePressed = true;
                e.Handled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode != Keys.Space || !spacePressed) return;
            spacePressed = false;
            PerformClick();
            e.Handled = true;
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

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            spacePressed = false;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (GlassTheme.DrawControlBackground(e, this)) return;
            using (var background = new SolidBrush(Parent == null ? FeatureTheme.Navigation : Parent.BackColor))
                e.Graphics.FillRectangle(background, e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            Color fill = hovered && !selected ? FeatureTheme.Hover : BackColor;
            Color ink = ForeColor;
            if (SystemInformation.HighContrast)
            {
                fill = selected || hovered ? SystemColors.Highlight : SystemColors.Control;
                ink = selected || hovered ? SystemColors.HighlightText : SystemColors.ControlText;
            }
            float scale = e.Graphics.DpiX / 96F;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (!GlassTheme.DrawSurface(e.Graphics, this, Height / 2F, fill, selected ? 95 : 125))
                using (var shape = FeatureTheme.RoundedRectangle(new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), 6F * scale))
                using (var surface = new SolidBrush(fill))
                    e.Graphics.FillPath(surface, shape);
            Bitmap icon = selected ? selectedIcon : normalIcon;
            e.Graphics.DrawImage(icon, new Rectangle((int)(2 * scale), (Height - (int)(24 * scale)) / 2,
                (int)(44 * scale), (int)(24 * scale)));
            var textArea = new Rectangle((int)(48 * scale), 0, Math.Max(0, Width - (int)(56 * scale)), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, textArea, ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
                | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
            if (Focused && ShowFocusCues)
                using (var focus = new Pen(ink) { DashStyle = DashStyle.Dot })
                    e.Graphics.DrawRectangle(focus, Rectangle.Inflate(ClientRectangle, -4, -4));
        }

        private static Bitmap CreateIcon(AppPage page, Color color, bool selected)
        {
            // The Windows icon family shares one em square across all four glyphs.
            string glyph = page == AppPage.Cleanup ? "\uE75C" : page == AppPage.Uninstaller ? "\uE71D"
                : page == AppPage.ForceDelete ? "\uE74D" : "\uE72C";
            using (var fluent = new Font("Segoe Fluent Icons", 18, GraphicsUnit.Pixel))
            using (var legacy = new Font("Segoe MDL2 Assets", 18, GraphicsUnit.Pixel))
            {
                var image = new Bitmap(44, 24);
                using (Graphics graphics = Graphics.FromImage(image))
                using (var ink = new SolidBrush(color))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    graphics.Clear(Color.Transparent);
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
        private readonly Panel content = new FeatureFrame();
        private readonly Dictionary<AppPage, Form> pages = new Dictionary<AppPage, Form>();
        private readonly Dictionary<AppPage, NavButton> navButtons = new Dictionary<AppPage, NavButton>();
        private AppPage? activePage;
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
            BackColor = FeatureTheme.Navigation;
            AutoScaleMode = AutoScaleMode.Dpi;

            var sidebar = new ScenePanel { Dock = DockStyle.Left, Width = 210, BackColor = FeatureTheme.Navigation };
            Controls.Add(sidebar);

            var brandMark = new PictureBox
            {
                Location = new Point(22, 25),
                Size = new Size(34, 34),
                BackColor = Color.Transparent,
                Image = brandImage,
                SizeMode = PictureBoxSizeMode.Zoom
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
                Text = "v" + RiverDesk.VersionInfo.Display,
                Font = new Font("Segoe UI", 9F),
                ForeColor = FeatureTheme.Muted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(24, 695),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom
            });
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

            var contentHost = new ScenePanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 12, 12),
                BackColor = FeatureTheme.Navigation
            };
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(8);
            content.BackColor = FeatureTheme.Background;
            contentHost.Controls.Add(content);
            Controls.Add(contentHost);
            contentHost.BringToFront();
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
            if (activePage == page) return;

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
            try
            {
                if (activePage.HasValue)
                {
                    pages[activePage.Value].Hide();
                    navButtons[activePage.Value].SetSelected(false);
                }
                activePage = page;
                navButtons[page].SetSelected(true);
                if (form.Parent == null) content.Controls.Add(form);
                form.Show();
                form.BringToFront();
            }
            finally { content.ResumeLayout(); }
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
