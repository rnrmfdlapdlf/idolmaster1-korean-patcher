using System;

namespace ImasKoreanPatcher
{
    internal static class AlphaBitmap
    {
        public static AlphaBounds GetBounds(byte[] alpha, int width, int height)
        {
            int left = width;
            int top = height;
            int right = -1;
            int bottom = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (alpha[y * width + x] == 0)
                    {
                        continue;
                    }

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }

            if (right < left || bottom < top)
            {
                return AlphaBounds.Empty;
            }

            return new AlphaBounds(left, top, right + 1, bottom + 1);
        }

        public static byte[] CropFromPage(byte[] alpha, int pageWidth, int x, int y, int width, int height)
        {
            byte[] output = new byte[width * height];
            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(alpha, (y + row) * pageWidth + x, output, row * width, width);
            }

            return output;
        }

        public static void PasteToPage(byte[] pageAlpha, int pageWidth, int x, int y, int width, int height, byte[] cellAlpha)
        {
            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(cellAlpha, row * width, pageAlpha, (y + row) * pageWidth + x, width);
            }
        }

        public static void ClearRect(byte[] alpha, int pageWidth, int pageHeight, int x0, int y0, int x1, int y1)
        {
            int left = Math.Max(0, x0);
            int top = Math.Max(0, y0);
            int right = Math.Min(pageWidth, x1);
            int bottom = Math.Min(pageHeight, y1);
            for (int y = top; y < bottom; y++)
            {
                Array.Clear(alpha, y * pageWidth + left, Math.Max(0, right - left));
            }
        }

        public static byte[] AlignToDonorBounds(byte[] rendered, int width, int height, AlphaBounds donorBounds, int placementXAdjust, int placementYAdjust)
        {
            if (donorBounds.IsEmpty)
            {
                return rendered;
            }

            AlphaBounds sourceBounds = GetBounds(rendered, width, height);
            if (sourceBounds.IsEmpty)
            {
                return new byte[width * height];
            }

            int targetWidth = donorBounds.Width;
            int targetHeight = donorBounds.Height;
            int cropWidth = Math.Min(sourceBounds.Width, targetWidth);
            int cropHeight = Math.Min(sourceBounds.Height, targetHeight);
            int sourceX = sourceBounds.Left + Math.Max(0, (sourceBounds.Width - cropWidth) / 2);
            int sourceY = sourceBounds.Top + Math.Max(0, (sourceBounds.Height - cropHeight) / 2);
            int pasteX = donorBounds.Left + (targetWidth - cropWidth) / 2 + placementXAdjust;
            int pasteY = donorBounds.Top + (targetHeight - cropHeight) / 2 + placementYAdjust;
            byte[] output = new byte[width * height];

            for (int y = 0; y < cropHeight; y++)
            {
                int targetY = pasteY + y;
                if (targetY < 0 || targetY >= height)
                {
                    continue;
                }

                for (int x = 0; x < cropWidth; x++)
                {
                    int targetX = pasteX + x;
                    if (targetX < 0 || targetX >= width)
                    {
                        continue;
                    }

                    output[targetY * width + targetX] = rendered[(sourceY + y) * width + sourceX + x];
                }
            }

            return output;
        }
    }

    internal struct AlphaBounds
    {
        public static readonly AlphaBounds Empty = new AlphaBounds(0, 0, 0, 0, true);

        private readonly bool empty;

        public AlphaBounds(int left, int top, int right, int bottom)
            : this(left, top, right, bottom, false)
        {
        }

        private AlphaBounds(int left, int top, int right, int bottom, bool empty)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            this.empty = empty;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public bool IsEmpty { get { return empty; } }
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }
}
