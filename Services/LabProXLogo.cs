using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LabReportApp.Services
{
    /// <summary>
    /// Official Lab ProX mark, drawn once and reused on every PDF footer.
    /// </summary>
    public static class LabProXLogo
    {
        private static byte[]? _png;

        public static byte[] GetPng()
        {
            if (_png != null)
                return _png;
            _png = Render();
            return _png;
        }

        private static byte[] Render()
        {
            const int size = 128;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromRgb(20, 87, 184)),
                    null,
                    new Rect(4, 4, 120, 120),
                    28, 28);

                var flask = Geometry.Parse("M9,1 H15 V7 L19.2,15.2 A4.6,4.6 0 0 1 15,22 H9 A4.6,4.6 0 0 1 4.8,15.2 L9,7 Z");
                flask.Transform = new MatrixTransform(4.2, 0, 0, 4.2, 18, 16);
                dc.DrawGeometry(Brushes.White, null, flask);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(21, 128, 61)), null, new Point(96, 96), 12, 12);
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}
