using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DeepCleanC
{
    internal static class GlassTheme
    {
        private const int SceneWidth = 960;
        private const int SceneHeight = 640;
        private static readonly Lazy<Backdrop> Scene = new Lazy<Backdrop>(CreateBackdrop);
        private static readonly ConditionalWeakTable<Control, SurfaceCache> Surfaces =
            new ConditionalWeakTable<Control, SurfaceCache>();

        internal static bool Enabled
        {
            get { return RiverDesk.VersionInfo.IsLiquidGlass && !SystemInformation.HighContrast; }
        }

        internal static Color Choose(Color standard, Color glass)
        {
            return Enabled ? glass : standard;
        }

        internal static void DrawBackdrop(Graphics graphics, Control control)
        {
            Size rootSize;
            Point origin = GetOrigin(control, out rootSize);
            var source = new RectangleF(origin.X * SceneWidth / (float)rootSize.Width,
                origin.Y * SceneHeight / (float)rootSize.Height,
                control.Width * SceneWidth / (float)rootSize.Width,
                control.Height * SceneHeight / (float)rootSize.Height);
            graphics.DrawImage(Scene.Value.Image, control.ClientRectangle, source, GraphicsUnit.Pixel);
        }

        internal static bool DrawControlBackground(PaintEventArgs e, Control control)
        {
            if (!Enabled) return false;
            DrawBackdrop(e.Graphics, control);
            if (!(control.Parent is ScenePanel))
                using (var veil = new SolidBrush(Color.FromArgb(155, FeatureTheme.Background)))
                    e.Graphics.FillRectangle(veil, e.ClipRectangle);
            return true;
        }

        internal static bool DrawSurface(Graphics graphics, Control control, float radius, Color tint, int opacity)
        {
            if (!Enabled || control.Width < 2 || control.Height < 2) return false;
            Size rootSize;
            Point origin = GetOrigin(control, out rootSize);
            var cache = Surfaces.GetValue(control, key =>
            {
                var created = new SurfaceCache();
                key.Disposed += (sender, args) => created.Dispose();
                return created;
            });
            if (cache.Image == null || cache.Size != control.Size || cache.Origin != origin
                || cache.RootSize != rootSize || cache.Radius != radius)
            {
                cache.Dispose();
                cache.Image = Refract(control.Size, rootSize, origin, radius);
                cache.Size = control.Size;
                cache.RootSize = rootSize;
                cache.Origin = origin;
                cache.Radius = radius;
            }
            graphics.DrawImage(cache.Image, control.ClientRectangle);
            using (var shape = FeatureTheme.RoundedRectangle(
                new RectangleF(0.5F, 0.5F, control.Width - 1F, control.Height - 1F), radius))
            using (var veil = new SolidBrush(Color.FromArgb(opacity, tint)))
            using (var sheen = new LinearGradientBrush(control.ClientRectangle,
                Color.FromArgb(115, 222, 247, 255), Color.FromArgb(20, 170, 190, 250), 90F))
            using (var rim = new Pen(sheen))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillPath(veil, shape);
                graphics.DrawPath(rim, shape);
            }
            return true;
        }

        private static Point GetOrigin(Control control, out Size rootSize)
        {
            var origin = Point.Empty;
            Control root = control;
            while (root.Parent != null)
            {
                origin.Offset(root.Location);
                root = root.Parent;
            }
            rootSize = new Size(Math.Max(1, root.ClientSize.Width), Math.Max(1, root.ClientSize.Height));
            return origin;
        }

        private static Backdrop CreateBackdrop()
        {
            var pixels = new byte[SceneWidth * SceneHeight * 4];
            for (int y = 0; y < SceneHeight; y++)
            for (int x = 0; x < SceneWidth; x++)
            {
                double nx = x / (double)SceneWidth;
                double ny = y / (double)SceneHeight;
                double blue = Math.Exp(-((nx - 0.16) * (nx - 0.16) / 0.08 + (ny - 0.2) * (ny - 0.2) / 0.22));
                double violet = Math.Exp(-((nx - 0.88) * (nx - 0.88) / 0.11 + (ny - 0.7) * (ny - 0.7) / 0.17));
                double teal = Math.Exp(-((nx - 0.5) * (nx - 0.5) / 0.12 + (ny - 0.96) * (ny - 0.96) / 0.06));
                double wave = ny - (0.36 + 0.16 * Math.Sin(nx * 5.2));
                double ribbon = Math.Exp(-wave * wave / 0.002);
                double threadDistance = ny - (0.18 + 0.05 * Math.Sin(nx * 8 + 0.3));
                double thread = Math.Exp(-threadDistance * threadDistance / 0.00004);
                int index = (y * SceneWidth + x) * 4;
                pixels[index] = (byte)(28 + 85 * blue + 88 * violet + 55 * teal + 38 * ribbon + 30 * thread);
                pixels[index + 1] = (byte)(17 + 48 * blue + 22 * violet + 61 * teal + 23 * ribbon + 18 * thread);
                pixels[index + 2] = (byte)(10 + 12 * blue + 56 * violet + 10 * teal + 16 * ribbon + 8 * thread);
                pixels[index + 3] = 255;
            }
            return new Backdrop { Pixels = pixels, Image = MakeBitmap(SceneWidth, SceneHeight, pixels) };
        }

        private static Bitmap Refract(Size size, Size rootSize, Point origin, float radius)
        {
            int step = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(size.Width * (double)size.Height / 150000D)));
            int width = Math.Max(1, (size.Width + step - 1) / step);
            int height = Math.Max(1, (size.Height + step - 1) / step);
            var pixels = new byte[width * height * 4];
            float halfWidth = size.Width / 2F;
            float halfHeight = size.Height / 2F;
            radius = Math.Min(radius, Math.Min(halfWidth, halfHeight));
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5F) * step;
                float py = (y + 0.5F) * step;
                float qx = Math.Abs(px - halfWidth) - (halfWidth - radius);
                float qy = Math.Abs(py - halfHeight) - (halfHeight - radius);
                float ox = Math.Max(qx, 0);
                float oy = Math.Max(qy, 0);
                float distance = (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - radius;
                float coverage = Math.Max(0, Math.Min(1, 0.5F - distance / step));
                if (coverage <= 0) continue;
                float nx = (px - halfWidth) / halfWidth;
                float ny = (py - halfHeight) / halfHeight;
                float length = (float)Math.Sqrt(nx * nx + ny * ny) + 0.001F;
                nx /= length;
                ny /= length;
                float edge = (float)Math.Exp(-Math.Abs(distance) / 8F);
                float shift = 17F * edge;
                float sx = (origin.X + px + nx * shift) * SceneWidth / rootSize.Width;
                float sy = (origin.Y + py + ny * shift) * SceneHeight / rootSize.Height;
                float split = edge * 1.8F;
                int index = (y * width + x) * 4;
                pixels[index] = (byte)Math.Min(255, BlurChannel(sx - nx * split, sy - ny * split, 0) * 1.12F);
                pixels[index + 1] = (byte)Math.Min(255, BlurChannel(sx, sy, 1) * 1.12F);
                pixels[index + 2] = (byte)Math.Min(255, BlurChannel(sx + nx * split, sy + ny * split, 2) * 1.12F);
                pixels[index + 3] = (byte)(coverage * 255);
            }
            return MakeBitmap(width, height, pixels);
        }

        private static int BlurChannel(float x, float y, int channel)
        {
            return (Sample(x, y, channel) * 4 + Sample(x - 5, y, channel) + Sample(x + 5, y, channel)
                + Sample(x, y - 5, channel) + Sample(x, y + 5, channel)) / 8;
        }

        private static int Sample(float x, float y, int channel)
        {
            int sx = Math.Max(0, Math.Min(SceneWidth - 1, (int)x));
            int sy = Math.Max(0, Math.Min(SceneHeight - 1, (int)y));
            return Scene.Value.Pixels[(sy * SceneWidth + sx) * 4 + channel];
        }

        private static Bitmap MakeBitmap(int width, int height, byte[] pixels)
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }

        private sealed class Backdrop
        {
            internal Bitmap Image;
            internal byte[] Pixels;
        }

        private sealed class SurfaceCache : IDisposable
        {
            internal Bitmap Image;
            internal Size Size;
            internal Size RootSize;
            internal Point Origin;
            internal float Radius;

            public void Dispose()
            {
                if (Image != null) Image.Dispose();
                Image = null;
            }
        }
    }

    internal sealed class ScenePanel : Panel
    {
        internal ScenePanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (GlassTheme.Enabled) GlassTheme.DrawBackdrop(e.Graphics, this);
            else base.OnPaintBackground(e);
        }
    }
}
