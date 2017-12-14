using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Cobalt.IO;

namespace Cobalt.Texture
{
    public static class TextureLoader
    {
        static readonly string[] validImageTypes = new string[] { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tiff", ".tif" };

        enum DxtFormat { DXT1, DXT3, DXT5 }

        public static Texture Load(string filename)
        {
            Bitmap bitmap = null;

            if (validImageTypes.Contains(Path.GetExtension(filename)))
            {
                bitmap = new Bitmap(filename);
            }
            else
            {
                using (EndianBinaryReader reader = new EndianBinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Endian.BigEndian))
                {
                    string fourCC = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    switch (fourCC)
                    {
                        case "DDS ": bitmap = LoadDDS(reader); break;
                    }
                }
            }

            //bitmap.Save(@"C:\temp\glsl\image-test-" + Path.GetFileNameWithoutExtension(filename) + ".png");

            return new Texture(bitmap);
        }

        // TODO: merge LoadCompressed & LoadDDS

        public static Texture LoadCompressed(string fourCC, int width, int height, byte[] data)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            byte[] pixelData;

            using (EndianBinaryReader reader = new EndianBinaryReader(new MemoryStream(data)))
            {
                if (fourCC.StartsWith("DXT"))
                {
                    DxtFormat dxtFormat;
                    switch (fourCC)
                    {
                        case "DXT1": dxtFormat = DxtFormat.DXT1; break;
                        case "DXT3": dxtFormat = DxtFormat.DXT3; break;
                        case "DXT5": dxtFormat = DxtFormat.DXT5; break;
                        default: throw new Exception("Unknown DXT format");
                    }
                    pixelData = DecompressDxt(reader, dxtFormat, bitmap.Width, bitmap.Height);
                }
                else
                    throw new Exception("Unknown FourCC");
            }

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);

            return new Texture(bitmap);
        }

        private static Bitmap LoadDDS(EndianBinaryReader reader)
        {
            DDSHeader header = new DDSHeader(reader);
            Bitmap bitmap = new Bitmap((int)header.Width, (int)header.Height);

            DxtFormat dxtFormat = DxtFormat.DXT1;
            switch (header.PixelFormat.FourCC)
            {
                case "DXT1": dxtFormat = DxtFormat.DXT1; break;
                case "DXT3": dxtFormat = DxtFormat.DXT3; break;
                case "DXT5": dxtFormat = DxtFormat.DXT5; break;
            }

            byte[] pixelData = DecompressDxt(reader, dxtFormat, (int)header.Width, (int)header.Height);

            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        private static byte[] DecompressDxt(EndianBinaryReader reader, DxtFormat format, int width, int height)
        {
            byte[] pixelData = new byte[width * height * 4];
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    byte[] decompressedBlock = DecompressDxtBlock(reader, format);
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int ix = (x + px);
                            int iy = (y + py);
                            if (ix >= width || iy >= height) continue;
                            for (int c = 0; c < 4; c++)
                                pixelData[(((iy * width) + ix) * 4) + c] = decompressedBlock[(((py * 4) + px) * 4) + c];
                        }
                    }
                }
            }
            return pixelData;
        }

        private static byte[] DecompressDxtBlock(EndianBinaryReader reader, DxtFormat format)
        {
            byte[] outputData = new byte[(4 * 4) * 4];
            byte[] colorData = null, alphaData = null;

            if (format != DxtFormat.DXT1)
                alphaData = DecompressDxtAlpha(reader, format);

            colorData = DecompressDxtColor(reader, format);

            for (int i = 0; i < colorData.Length; i += 4)
            {
                outputData[i] = colorData[i];
                outputData[i + 1] = colorData[i + 1];
                outputData[i + 2] = colorData[i + 2];
                outputData[i + 3] = (alphaData != null ? alphaData[i + 3] : colorData[i + 3]);
            }

            return outputData;
        }

        private static byte[] DecompressDxtColor(EndianBinaryReader reader, DxtFormat format)
        {
            byte[] colorOut = new byte[(4 * 4) * 4];

            ushort color0 = reader.ReadUInt16(Endian.LittleEndian);
            ushort color1 = reader.ReadUInt16(Endian.LittleEndian);
            uint bits = reader.ReadUInt32(Endian.LittleEndian);

            byte c0r, c0g, c0b, c1r, c1g, c1b;

            UnpackRgb565(color0, out c0r, out c0g, out c0b);
            UnpackRgb565(color1, out c1r, out c1g, out c1b);

            byte[] bitsExt = new byte[16];
            for (int i = 0; i < bitsExt.Length; i++)
                bitsExt[i] = (byte)((bits >> (i * 2)) & 0x3);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    byte code = bitsExt[(y * 4) + x];
                    int destOffset = ((y * 4) + x) * 4;

                    if (format == DxtFormat.DXT1)
                    {
                        colorOut[destOffset + 3] = (byte)((color0 <= color1 && code == 3) ? 0 : 0xFF);
                    }

                    if (format == DxtFormat.DXT1 && color0 <= color1)
                    {
                        switch (code)
                        {
                            case 0x00:
                                colorOut[destOffset + 0] = c0b;
                                colorOut[destOffset + 1] = c0g;
                                colorOut[destOffset + 2] = c0r;
                                break;

                            case 0x01:
                                colorOut[destOffset + 0] = c1b;
                                colorOut[destOffset + 1] = c1g;
                                colorOut[destOffset + 2] = c1r;
                                break;

                            case 0x02:
                                colorOut[destOffset + 0] = (byte)((c0b + c1b) / 2);
                                colorOut[destOffset + 1] = (byte)((c0g + c1g) / 2);
                                colorOut[destOffset + 2] = (byte)((c0r + c1r) / 2);
                                break;

                            case 0x03:
                                colorOut[destOffset + 0] = 0;
                                colorOut[destOffset + 1] = 0;
                                colorOut[destOffset + 2] = 0;
                                break;
                        }
                    }
                    else
                    {
                        switch (code)
                        {
                            case 0x00:
                                colorOut[destOffset + 0] = c0b;
                                colorOut[destOffset + 1] = c0g;
                                colorOut[destOffset + 2] = c0r;
                                break;

                            case 0x01:
                                colorOut[destOffset + 0] = c1b;
                                colorOut[destOffset + 1] = c1g;
                                colorOut[destOffset + 2] = c1r;
                                break;

                            case 0x02:
                                colorOut[destOffset + 0] = (byte)((2 * c0b + c1b) / 3);
                                colorOut[destOffset + 1] = (byte)((2 * c0g + c1g) / 3);
                                colorOut[destOffset + 2] = (byte)((2 * c0r + c1r) / 3);
                                break;

                            case 0x03:
                                colorOut[destOffset + 0] = (byte)((c0b + 2 * c1b) / 3);
                                colorOut[destOffset + 1] = (byte)((c0g + 2 * c1g) / 3);
                                colorOut[destOffset + 2] = (byte)((c0r + 2 * c1r) / 3);
                                break;
                        }
                    }
                }
            }

            return colorOut;
        }

        private static void UnpackRgb565(ushort rgb565, out byte r, out byte g, out byte b)
        {
            r = (byte)((rgb565 & 0xF800) >> 11);
            r = (byte)((r << 3) | (r >> 2));
            g = (byte)((rgb565 & 0x07E0) >> 5);
            g = (byte)((g << 2) | (g >> 4));
            b = (byte)(rgb565 & 0x1F);
            b = (byte)((b << 3) | (b >> 2));
        }

        private static byte[] DecompressDxtAlpha(EndianBinaryReader reader, DxtFormat format)
        {
            byte[] alphaOut = new byte[(4 * 4) * 4];
            switch (format)
            {
                case DxtFormat.DXT3:
                    {
                        ulong alpha = reader.ReadUInt64(Endian.LittleEndian);
                        for (int i = 0; i < alphaOut.Length; i += 4)
                        {
                            alphaOut[i + 3] = (byte)(((alpha & 0xF) << 4) | (alpha & 0xF));
                            alpha >>= 4;
                        }
                    }
                    break;

                case DxtFormat.DXT5:
                    {
                        byte alpha0 = reader.ReadByte();
                        byte alpha1 = reader.ReadByte();
                        byte bits_5 = reader.ReadByte();
                        byte bits_4 = reader.ReadByte();
                        byte bits_3 = reader.ReadByte();
                        byte bits_2 = reader.ReadByte();
                        byte bits_1 = reader.ReadByte();
                        byte bits_0 = reader.ReadByte();

                        ulong bits = (ulong)(((ulong)bits_0 << 40) | ((ulong)bits_1 << 32) | ((ulong)bits_2 << 24) | ((ulong)bits_3 << 16) | ((ulong)bits_4 << 8) | (ulong)bits_5);

                        byte[] bitsExt = new byte[16];
                        for (int i = 0; i < bitsExt.Length; i++)
                            bitsExt[i] = (byte)((bits >> (i * 3)) & 0x7);

                        for (int y = 0; y < 4; y++)
                        {
                            for (int x = 0; x < 4; x++)
                            {
                                byte code = bitsExt[(y * 4) + x];
                                int destOffset = (((y * 4) + x) * 4) + 3;

                                if (alpha0 > alpha1)
                                {
                                    switch (code)
                                    {
                                        case 0x00: alphaOut[destOffset] = alpha0; break;
                                        case 0x01: alphaOut[destOffset] = alpha1; break;
                                        case 0x02: alphaOut[destOffset] = (byte)((6 * alpha0 + 1 * alpha1) / 7); break;
                                        case 0x03: alphaOut[destOffset] = (byte)((5 * alpha0 + 2 * alpha1) / 7); break;
                                        case 0x04: alphaOut[destOffset] = (byte)((4 * alpha0 + 3 * alpha1) / 7); break;
                                        case 0x05: alphaOut[destOffset] = (byte)((3 * alpha0 + 4 * alpha1) / 7); break;
                                        case 0x06: alphaOut[destOffset] = (byte)((2 * alpha0 + 5 * alpha1) / 7); break;
                                        case 0x07: alphaOut[destOffset] = (byte)((1 * alpha0 + 6 * alpha1) / 7); break;
                                    }
                                }
                                else
                                {
                                    switch (code)
                                    {
                                        case 0x00: alphaOut[destOffset] = alpha0; break;
                                        case 0x01: alphaOut[destOffset] = alpha1; break;
                                        case 0x02: alphaOut[destOffset] = (byte)((4 * alpha0 + 1 * alpha1) / 5); break;
                                        case 0x03: alphaOut[destOffset] = (byte)((3 * alpha0 + 2 * alpha1) / 5); break;
                                        case 0x04: alphaOut[destOffset] = (byte)((2 * alpha0 + 3 * alpha1) / 5); break;
                                        case 0x05: alphaOut[destOffset] = (byte)((1 * alpha0 + 4 * alpha1) / 5); break;
                                        case 0x06: alphaOut[destOffset] = 0x00; break;
                                        case 0x07: alphaOut[destOffset] = 0xFF; break;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }

            return alphaOut;
        }
    }

    [Flags]
    internal enum DDSD
    {
        Caps = 0x1,
        Height = 0x2,
        Width = 0x4,
        Pitch = 0x8,
        PixelFormat = 0x1000,
        MipMapCount = 0x20000,
        LinearSize = 0x80000,
        Depth = 0x800000
    }

    [Flags]
    internal enum DDSCaps
    {
        Complex = 0x8,
        MipMap = 0x400000,
        Texture = 0x1000
    }

    [Flags]
    internal enum DDSCaps2
    {
        CubeMap = 0x200,
        CubeMap_PositiveX = 0x400,
        CubeMap_NegativeX = 0x800,
        CubeMap_PositiveY = 0x1000,
        CubeMap_NegativeY = 0x2000,
        CubeMap_PositiveZ = 0x4000,
        CubeMap_NegativeZ = 0x8000,
        Volume = 0x200000
    }

    internal class DDSHeader
    {
        public uint Size { get; set; }
        public DDSD Flags { get; set; }
        public uint Height { get; set; }
        public uint Width { get; set; }
        public uint PitchOrLinearSize { get; set; }
        public uint Depth { get; set; }
        public uint MipMapCount { get; set; }
        public uint[] Reserved1 { get; set; }
        public DDSPixelFormat PixelFormat { get; set; }
        public DDSCaps Caps { get; set; }
        public DDSCaps2 Caps2 { get; set; }
        public uint Caps3 { get; set; }
        public uint Caps4 { get; set; }
        public uint Reserved2 { get; set; }

        public DDSHeader(EndianBinaryReader reader)
        {
            Size = reader.ReadUInt32(Endian.LittleEndian);
            Flags = (DDSD)reader.ReadUInt32(Endian.LittleEndian);
            Height = reader.ReadUInt32(Endian.LittleEndian);
            Width = reader.ReadUInt32(Endian.LittleEndian);
            PitchOrLinearSize = reader.ReadUInt32(Endian.LittleEndian);
            Depth = reader.ReadUInt32(Endian.LittleEndian);
            MipMapCount = reader.ReadUInt32(Endian.LittleEndian);
            Reserved1 = new uint[11];
            for (int i = 0; i < Reserved1.Length; i++) Reserved1[i] = reader.ReadUInt32(Endian.LittleEndian);
            PixelFormat = new DDSPixelFormat(reader);
            Caps = (DDSCaps)reader.ReadUInt32(Endian.LittleEndian);
            Caps2 = (DDSCaps2)reader.ReadUInt32(Endian.LittleEndian);
            Caps3 = reader.ReadUInt32(Endian.LittleEndian);
            Caps4 = reader.ReadUInt32(Endian.LittleEndian);
            Reserved2 = reader.ReadUInt32(Endian.LittleEndian);
        }
    }

    [Flags]
    internal enum DDPF
    {
        AlphaPixels = 0x1,
        Alpha = 0x2,
        FourCC = 0x4,
        RGB = 0x40,
        YUV = 0x200,
        Luminance = 0x20000
    }

    internal class DDSPixelFormat
    {
        public uint Size { get; set; }
        public DDPF Flags { get; set; }
        public string FourCC { get; set; }
        public uint RGBBitCount { get; set; }
        public uint RBitMask { get; set; }
        public uint GBitMask { get; set; }
        public uint BBitMask { get; set; }
        public uint ABitMask { get; set; }

        public DDSPixelFormat(EndianBinaryReader reader)
        {
            Size = reader.ReadUInt32(Endian.LittleEndian);
            Flags = (DDPF)reader.ReadUInt32(Endian.LittleEndian);
            FourCC = Encoding.ASCII.GetString(reader.ReadBytes(4));
            RGBBitCount = reader.ReadUInt32(Endian.LittleEndian);
            RBitMask = reader.ReadUInt32(Endian.LittleEndian);
            GBitMask = reader.ReadUInt32(Endian.LittleEndian);
            BBitMask = reader.ReadUInt32(Endian.LittleEndian);
            ABitMask = reader.ReadUInt32(Endian.LittleEndian);
        }
    }
}
