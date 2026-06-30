using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ImasKoreanPatcher
{
    internal sealed class GlyphAlphaRenderer : IDisposable
    {
        private readonly PrivateFontCollection fontCollection;
        private readonly FontFamily fontFamily;
        private readonly float fontSize;

        public GlyphAlphaRenderer(string fontPath, float fontSize)
        {
            fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(fontPath);
            if (fontCollection.Families.Length == 0)
            {
                throw new InvalidOperationException("Font file did not expose any font families.");
            }

            fontFamily = fontCollection.Families[0];
            this.fontSize = fontSize;
        }

        public byte[] RenderCell(char ch, int width, int height, int xAdjust, int yAdjust)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
                format.FormatFlags |= StringFormatFlags.NoClip;
                FontStyle style = fontFamily.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                path.AddString(ch.ToString(), fontFamily, (int)style, fontSize, new PointF(0, 0), format);

                RectangleF bounds = path.GetBounds();
                Matrix matrix = new Matrix();
                float x = (float)Math.Floor((width - bounds.Width) / 2.0f) - bounds.Left + xAdjust;
                float y = (float)Math.Floor((height - bounds.Height) / 2.0f) - bounds.Top + yAdjust;
                matrix.Translate(x, y);
                path.Transform(matrix);

                using (Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.FillPath(Brushes.White, path);
                    return BitmapToAlpha(bitmap);
                }
            }
        }

        public void Dispose()
        {
            fontCollection.Dispose();
        }

        private static byte[] BitmapToAlpha(Bitmap bitmap)
        {
            byte[] alpha = new byte[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    alpha[y * bitmap.Width + x] = bitmap.GetPixel(x, y).A;
                }
            }

            return alpha;
        }
    }
}
