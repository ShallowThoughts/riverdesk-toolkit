using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DeepCleanC;

internal static class UiRegressionCheck
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            CheckClippedPainting();
            CheckLogView();
            CheckNavigationActivation();
            CheckUpdateNotes();
            Console.WriteLine("UI regression checks passed");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckNavigationActivation()
    {
        using (var button = new NavButton(AppPage.Update, FeatureTheme.Sky))
        {
            int clicks = 0;
            button.Click += (sender, args) => clicks++;
            var down = typeof(NavButton).GetMethod("OnKeyDown", Hidden);
            var up = typeof(NavButton).GetMethod("OnKeyUp", Hidden);
            down.Invoke(button, new object[] { new KeyEventArgs(Keys.Space) });
            if (clicks != 0) throw new InvalidOperationException("Navigation activated before Space was released");
            up.Invoke(button, new object[] { new KeyEventArgs(Keys.Space) });
            down.Invoke(button, new object[] { new KeyEventArgs(Keys.Enter) });
            if (clicks != 2) throw new InvalidOperationException("Navigation keyboard activation failed");
            button.Enabled = false;
            button.PerformClick();
            if (clicks != 2) throw new InvalidOperationException("Disabled navigation activated");
        }
    }

    private static void CheckUpdateNotes()
    {
        using (var form = new RiverDesk.UpdateForm(() => true))
        {
            var notes = (Control)typeof(RiverDesk.UpdateForm).GetField("notes", Hidden).GetValue(form);
            if (notes is TextBoxBase || notes.CanSelect || notes.TabStop)
                throw new InvalidOperationException("Update notes became a selectable text input");
        }
    }

    private static void CheckClippedPainting()
    {
        var background = typeof(NavButton).GetMethod("OnPaintBackground", Hidden);
        var paint = typeof(NavButton).GetMethod("OnPaint", Hidden);
        foreach (float dpi in new[] { 96F, 144F })
        foreach (AppPage page in Enum.GetValues(typeof(AppPage)))
        using (var button = new NavButton(page, FeatureTheme.Sky)
        {
            Text = "导航文字", Size = new Size(178, 42)
        })
        using (var bitmap = new Bitmap(button.Width, button.Height))
        {
            bitmap.SetResolution(dpi, dpi);
            foreach (Rectangle clip in new[]
            {
                new Rectangle(0, 0, 4, 42),
                new Rectangle(70, 10, 12, 20),
                new Rectangle(174, 0, 4, 42)
            })
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Magenta);
                graphics.SetClip(clip);
                var args = new PaintEventArgs(graphics, clip);
                background.Invoke(button, new object[] { args });
                paint.Invoke(button, new object[] { args });
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                        if (!clip.Contains(x, y) && bitmap.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb())
                            throw new InvalidOperationException("Navigation painting escaped the invalidated region");
            }
        }
    }

    private static void CheckLogView()
    {
        using (var form = new Form { ClientSize = new Size(420, 160), ShowInTaskbar = false })
        using (var log = new LogView { Dock = DockStyle.Fill })
        {
            form.Controls.Add(log);
            form.Show();
            for (int i = 0; i < 50; i++) log.AppendText("清理记录 " + i + " & 跳过原因\r\n");
            Application.DoEvents();
            var text = (Label)log.Controls[0];
            if (log.TabStop || log.CanSelect || text.CanSelect || text.UseMnemonic)
                throw new InvalidOperationException("Log output became selectable or interpreted as a mnemonic");
            if (text.Text != log.Text || !log.VerticalScroll.Visible)
                throw new InvalidOperationException("Log text or scrolling is unavailable");
            log.AutoScrollPosition = Point.Empty;
            typeof(ScrollableControl).GetMethod("OnMouseWheel", Hidden).Invoke(log,
                new object[] { new MouseEventArgs(MouseButtons.None, 0, 10, 10, -120) });
            if (log.AutoScrollPosition.Y >= 0)
                throw new InvalidOperationException("Log output did not scroll");
            form.ClientSize = new Size(220, 120);
            Application.DoEvents();
            if (text.Width > log.ClientSize.Width || log.HorizontalScroll.Visible)
                throw new InvalidOperationException("Log output did not wrap to the available width");
            log.Clear();
            Application.DoEvents();
            if (log.Text.Length != 0 || text.Text.Length != 0 || log.AutoScrollPosition.Y != 0)
                throw new InvalidOperationException("Log output did not reset");
        }
    }
}
