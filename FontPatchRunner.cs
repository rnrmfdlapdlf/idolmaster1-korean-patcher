using System;
using System.Collections.Generic;
using System.IO;

namespace ImasKoreanPatcher
{
    internal static class FontPatchRunner
    {
        private const int CellWidth = 27;
        private const int CellHeight = 27;
        private const int FontSize = 24;
        private const int CellGuardClear = 1;

        public static void PatchExtractedRoot(string extractedRoot, string assetRoot, string workRoot, Action<int, string> progress)
        {
            string initialTempBna = Path.Combine(extractedRoot, Path.Combine("root", Path.Combine("initialTemp", "initialTemp.bna")));
            string initialFixBna = Path.Combine(extractedRoot, Path.Combine("root", Path.Combine("initialFix", "initialFix.bna")));
            string remapPath = Path.Combine(assetRoot, Path.Combine("Remap", "xbox_hangul_remap.json"));
            string fontPath = Path.Combine(assetRoot, Path.Combine("Fonts", "title_Medium.ttf"));

            RequireFile(initialTempBna, "initialTemp.bna");
            RequireFile(initialFixBna, "initialFix.bna");
            RequireFile(remapPath, "xbox_hangul_remap.json");
            RequireFile(fontPath, "title_Medium.ttf");

            Report(progress, 73, "\ud3f0\ud2b8 \ud328\uce58 \uc900\ube44 \uc911...");
            BnaArchive initialTemp = BnaArchive.Load(initialTempBna);
            BnaArchive initialFix = BnaArchive.Load(initialFixBna);
            byte[] nutData = initialTemp.ReadEntry("root/font/font27.nut");
            byte[] nfhData = initialFix.ReadEntry("root/initialFix/font27.nfh");

            Report(progress, 76, "\ud55c\uae00 \uae00\ub9ac\ud504 \ub80c\ub354\ub9c1 \uc911...");
            byte[] patchedNut = PatchFontNut(nutData, nfhData, remapPath, fontPath);

            Report(progress, 84, "\ud328\uce58\ub41c \ud3f0\ud2b8\ub97c BNA\uc5d0 \uc801\uc6a9 \uc911...");
            initialTemp.ReplaceEntry("root/font/font27.nut", patchedNut);
            initialTemp.Save(initialTempBna);
            Report(progress, 86, "\ud3f0\ud2b8 \ud328\uce58 \uc644\ub8cc");
        }

        private static byte[] PatchFontNut(byte[] nutBytes, byte[] nfhBytes, string remapPath, string fontPath)
        {
            byte[] output = new byte[nutBytes.Length];
            Buffer.BlockCopy(nutBytes, 0, output, 0, nutBytes.Length);

            NfhFont nfh = NfhFont.Parse(nfhBytes);
            NutTexture texture = NutTexture.Parse(output);
            HangulFontRemap remap = HangulFontRemap.Load(remapPath);
            Dictionary<int, byte[]> alphaPages = new Dictionary<int, byte[]>();
            Dictionary<int, HashSet<int>> dirtyBlocks = new Dictionary<int, HashSet<int>>();
            List<PendingGlyphDraw> pendingDraws = new List<PendingGlyphDraw>();
            List<GuardRect> guardRects = new List<GuardRect>();

            using (GlyphAlphaRenderer renderer = new GlyphAlphaRenderer(fontPath, FontSize))
            {
                foreach (HangulFontRemapEntry entry in remap.Entries)
                {
                    if (entry.DonorRecordIndex < 0 || entry.DonorRecordIndex >= nfh.GlyphCount)
                    {
                        continue;
                    }

                    NfhGlyph glyph = nfh.GetGlyph(entry.DonorRecordIndex);
                    NutTexturePage page = texture.GetPage(glyph.PageIndex);
                    if (page == null)
                    {
                        continue;
                    }

                    int x = glyph.X;
                    int y = glyph.Y;
                    if (x < 0 || y < 0 || x + CellWidth > page.Width || y + CellHeight > page.Height)
                    {
                        continue;
                    }

                    byte[] alpha = GetAlphaPage(output, page, alphaPages);
                    byte[] donorCell = AlphaBitmap.CropFromPage(alpha, page.Width, x, y, CellWidth, CellHeight);
                    AlphaBounds donorBounds = AlphaBitmap.GetBounds(donorCell, CellWidth, CellHeight);
                    byte[] rendered = renderer.RenderCell(entry.Render, CellWidth, CellHeight, entry.RenderXAdjust, entry.RenderYAdjust);
                    byte[] aligned = AlphaBitmap.AlignToDonorBounds(rendered, CellWidth, CellHeight, donorBounds, entry.PlacementXAdjust, entry.PlacementYAdjust);
                    pendingDraws.Add(new PendingGlyphDraw(page, x, y, aligned));
                    guardRects.Add(new GuardRect(page, x - CellGuardClear, y - CellGuardClear, x + CellWidth + CellGuardClear, y + CellHeight + CellGuardClear));
                    AddDirtyBlocks(GetDirtyBlocks(page.Index, dirtyBlocks), page, x - CellGuardClear, y - CellGuardClear, CellWidth + CellGuardClear * 2, CellHeight + CellGuardClear * 2);
                    AlphaBitmap.PasteToPage(alpha, page.Width, x, y, CellWidth, CellHeight, aligned);
                }
            }

            if (pendingDraws.Count == 0)
            {
                throw new InvalidOperationException("No Korean glyphs were patched into font27.nut.");
            }

            for (int index = 0; index < guardRects.Count; index++)
            {
                GuardRect rect = guardRects[index];
                byte[] alpha = GetAlphaPage(output, rect.Page, alphaPages);
                AlphaBitmap.ClearRect(alpha, rect.Page.Width, rect.Page.Height, rect.Left, rect.Top, rect.Right, rect.Bottom);
            }

            for (int index = 0; index < pendingDraws.Count; index++)
            {
                PendingGlyphDraw draw = pendingDraws[index];
                byte[] alpha = GetAlphaPage(output, draw.Page, alphaPages);
                AlphaBitmap.PasteToPage(alpha, draw.Page.Width, draw.X, draw.Y, CellWidth, CellHeight, draw.Alpha);
            }

            foreach (KeyValuePair<int, byte[]> pageAlpha in alphaPages)
            {
                NutTexturePage page = texture.GetPage(pageAlpha.Key);
                Bc3Dxt3Codec.EncodeDxt3Alpha(output, page.DataOffset, page.Width, page.Height, pageAlpha.Value, GetDirtyBlocks(page.Index, dirtyBlocks), true);
            }

            return output;
        }

        private static byte[] GetAlphaPage(byte[] nutData, NutTexturePage page, Dictionary<int, byte[]> alphaPages)
        {
            byte[] alpha;
            if (!alphaPages.TryGetValue(page.Index, out alpha))
            {
                alpha = Bc3Dxt3Codec.DecodeDxt3Alpha(nutData, page.DataOffset, page.Width, page.Height);
                alphaPages[page.Index] = alpha;
            }

            return alpha;
        }

        private static HashSet<int> GetDirtyBlocks(int pageIndex, Dictionary<int, HashSet<int>> dirtyBlocks)
        {
            HashSet<int> blocks;
            if (!dirtyBlocks.TryGetValue(pageIndex, out blocks))
            {
                blocks = new HashSet<int>();
                dirtyBlocks[pageIndex] = blocks;
            }

            return blocks;
        }

        private static void AddDirtyBlocks(HashSet<int> dirtyBlocks, NutTexturePage page, int x, int y, int width, int height)
        {
            int left = Math.Max(0, x);
            int top = Math.Max(0, y);
            int right = Math.Min(page.Width - 1, x + width - 1);
            int bottom = Math.Min(page.Height - 1, y + height - 1);
            int blocksWide = page.Width / 4;
            for (int blockY = top / 4; blockY <= bottom / 4; blockY++)
            {
                for (int blockX = left / 4; blockX <= right / 4; blockX++)
                {
                    dirtyBlocks.Add(blockY * blocksWide + blockX);
                }
            }
        }

        private static void RequireFile(string path, string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(label + " was not found.", path);
            }
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }

        private sealed class PendingGlyphDraw
        {
            public readonly NutTexturePage Page;
            public readonly int X;
            public readonly int Y;
            public readonly byte[] Alpha;

            public PendingGlyphDraw(NutTexturePage page, int x, int y, byte[] alpha)
            {
                Page = page;
                X = x;
                Y = y;
                Alpha = alpha;
            }
        }

        private sealed class GuardRect
        {
            public readonly NutTexturePage Page;
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;

            public GuardRect(NutTexturePage page, int left, int top, int right, int bottom)
            {
                Page = page;
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }
    }
}
