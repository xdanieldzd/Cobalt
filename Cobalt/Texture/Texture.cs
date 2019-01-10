using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Imaging = System.Drawing.Imaging;

using OpenTK.Graphics.OpenGL;

namespace Cobalt.Texture
{
    internal class PixelFormatMapping
    {
        public PixelInternalFormat PixelInternalFormat { get; private set; }
        public PixelFormat PixelFormat { get; private set; }
        public PixelType PixelType { get; private set; }

        public PixelFormatMapping(PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
        {
            PixelInternalFormat = internalFormat;
            PixelFormat = format;
            PixelType = type;
        }
    }

    public class Texture : IDisposable
    {
        static int lastTextureBound = -1;
        static TextureUnit lastTextureUnitUsed = (TextureUnit)0;

        internal static Dictionary<Imaging.PixelFormat, PixelFormatMapping> pixelFormatMap = new Dictionary<Imaging.PixelFormat, PixelFormatMapping>()
        {
            { Imaging.PixelFormat.Format32bppArgb, new PixelFormatMapping(PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedByte) },
            { Imaging.PixelFormat.Format24bppRgb, new PixelFormatMapping(PixelInternalFormat.Rgb, PixelFormat.Bgr, PixelType.UnsignedByte) },
            { Imaging.PixelFormat.Format16bppArgb1555, new PixelFormatMapping(PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedShort1555Reversed) },
            { Imaging.PixelFormat.Format16bppRgb565, new PixelFormatMapping(PixelInternalFormat.Rgb, PixelFormat.Bgr, PixelType.UnsignedShort565Reversed) } /* TODO: test me! */
            /* TODO: add more! */
        };

        Bitmap sourceBitmap;
        int width, height, textureHandle;

        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public int Handle { get { return textureHandle; } }

        bool disposed = false;

        Texture()
        {
            width = height = 0;
            textureHandle = -1;
        }

        public Texture(Bitmap bitmap) : this(bitmap, TextureWrapMode.Repeat, TextureWrapMode.Repeat, TextureMinFilter.Linear, TextureMagFilter.Linear) { }

        public Texture(Bitmap bitmap, TextureWrapMode wrapS, TextureWrapMode wrapT, TextureMinFilter minFilter, TextureMagFilter magFilter)
        {
            width = bitmap.Width;
            height = bitmap.Height;

            if (bitmap.PixelFormat == Imaging.PixelFormat.Format1bppIndexed ||
                bitmap.PixelFormat == Imaging.PixelFormat.Format4bppIndexed ||
                bitmap.PixelFormat == Imaging.PixelFormat.Format8bppIndexed)
            {
                Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height, Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(newBitmap))
                {
                    g.DrawImageUnscaled(bitmap, Point.Empty);
                }
                bitmap = newBitmap;
            }

            if (!pixelFormatMap.ContainsKey(bitmap.PixelFormat))
                throw new ArgumentException(string.Format("Unhandled bitmap pixel format {0}", bitmap.PixelFormat));

            textureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureHandle);

            Imaging.BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            GL.TexImage2D(TextureTarget.Texture2D, 0,
                pixelFormatMap[bitmap.PixelFormat].PixelInternalFormat, bitmap.Width, bitmap.Height, 0,
                pixelFormatMap[bitmap.PixelFormat].PixelFormat, pixelFormatMap[bitmap.PixelFormat].PixelType,
                bmpData.Scan0);
            bitmap.UnlockBits(bmpData);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapS);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapT);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);

            sourceBitmap = bitmap;
        }

        public void Activate()
        {
            Activate(TextureUnit.Texture0);
        }

        public void Activate(TextureUnit textureUnit)
        {
            if (lastTextureBound != textureHandle || lastTextureUnitUsed != textureUnit)
            {
                if (textureHandle == -1) throw new InvalidOperationException("Invalid texture handle");
                if (lastTextureUnitUsed != textureUnit)
                {
                    GL.ActiveTexture(textureUnit);
                    lastTextureUnitUsed = textureUnit;
                }
                GL.BindTexture(TextureTarget.Texture2D, textureHandle);
                lastTextureBound = textureHandle;
            }
        }

        public void Save(string path)
        {
            sourceBitmap.Save(path);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (GL.IsTexture(textureHandle))
                    GL.DeleteTexture(textureHandle);

                sourceBitmap.Dispose();

                disposed = true;
            }
        }
    }
}
