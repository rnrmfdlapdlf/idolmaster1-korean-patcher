using System;
using System.Collections.Generic;

namespace ImasKoreanPatcher
{
    internal static class Bc3Dxt3Codec
    {
        public static byte[] DecodeDxt3Alpha(byte[] data, int offset, int width, int height)
        {
            byte[] alpha = new byte[width * height];
            byte[] block = new byte[16];
            int source = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    Buffer.BlockCopy(data, source, block, 0, 16);
                    source += 16;
                    AlphaSwap16(block);

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int alphaIndex = py * 4 + px;
                            int packed = block[alphaIndex / 2];
                            int nibble = (alphaIndex % 2) == 0 ? (packed & 0x0F) : (packed >> 4);
                            int x = blockX * 4 + px;
                            int y = blockY * 4 + py;
                            alpha[y * width + x] = (byte)(nibble * 17);
                        }
                    }
                }
            }

            return alpha;
        }

        public static void EncodeDxt3Alpha(byte[] data, int offset, int width, int height, byte[] alpha, HashSet<int> dirtyBlocks, bool patchColor)
        {
            byte[] block = new byte[16];
            int destination = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    int blockId = blockY * blocksWide + blockX;
                    if (dirtyBlocks == null || dirtyBlocks.Contains(blockId))
                    {
                        Buffer.BlockCopy(data, destination, block, 0, 16);
                        AlphaSwap16(block);
                        EncodeDxt3AlphaBlock(block, alpha, width, blockX, blockY);
                        if (patchColor)
                        {
                            WriteOriginalWhiteColorBlock(block);
                        }

                        AlphaSwap16(block);
                        Buffer.BlockCopy(block, 0, data, destination, 16);
                    }

                    destination += 16;
                }
            }
        }

        private static void EncodeDxt3AlphaBlock(byte[] block, byte[] alpha, int width, int blockX, int blockY)
        {
            for (int index = 0; index < 8; index++)
            {
                block[index] = 0;
            }

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int alphaIndex = py * 4 + px;
                    int x = blockX * 4 + px;
                    int y = blockY * 4 + py;
                    int value = alpha[y * width + x];
                    int nibble = (int)Math.Round(Math.Max(0, Math.Min(255, value)) * 15.0 / 255.0);
                    if ((alphaIndex % 2) == 0)
                    {
                        block[alphaIndex / 2] = (byte)(block[alphaIndex / 2] | nibble);
                    }
                    else
                    {
                        block[alphaIndex / 2] = (byte)(block[alphaIndex / 2] | (nibble << 4));
                    }
                }
            }
        }

        private static void WriteOriginalWhiteColorBlock(byte[] block)
        {
            block[8] = 0xFF;
            block[9] = 0xFF;
            block[10] = 0x00;
            block[11] = 0x00;
            block[12] = 0x00;
            block[13] = 0x00;
            block[14] = 0x00;
            block[15] = 0x00;
        }

        private static void AlphaSwap16(byte[] block)
        {
            for (int index = 0; index < 8; index += 2)
            {
                byte temp = block[index];
                block[index] = block[index + 1];
                block[index + 1] = temp;
            }
        }
    }
}
