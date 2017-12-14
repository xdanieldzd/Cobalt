using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Cobalt
{
    [System.ComponentModel.DesignTimeVisible(true), System.ComponentModel.ToolboxItem(true)]
    public class RenderControl : GLControl, System.ComponentModel.IComponent
    {
        public event EventHandler<EventArgs> Render;

        public RenderControl()
            : base(new OpenTK.Graphics.GraphicsMode(
                OpenTK.Graphics.GraphicsMode.Default.ColorFormat,
                OpenTK.Graphics.GraphicsMode.Default.Depth,
                OpenTK.Graphics.GraphicsMode.Default.Stencil,
                Cobalt.Core.GetMaxAASamples()))
        {
            if (!Core.IsRuntime) return;

            Application.Idle += ((s, e) =>
            {
                if (Core.IsReady) this.Invalidate();
            });
        }

        public new Bitmap GrabScreenshot()
        {
            return GrabScreenshot(false);
        }

        public Bitmap GrabScreenshot(bool cropByAlphaChannel = false)
        {
            Bitmap bitmap = new Bitmap(this.ClientRectangle.Width, this.ClientRectangle.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bitmap.LockBits(this.ClientRectangle, ImageLockMode.WriteOnly, bitmap.PixelFormat);

            int stride = bmpData.Stride;
            byte[] sourceData = new byte[stride * bmpData.Height];
            byte[] targetData = new byte[stride * bmpData.Height];

            GL.Enable(EnableCap.AlphaTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.SrcAlphaSaturate, BlendingFactorDest.One);
            GL.ReadPixels(this.ClientRectangle.Left, this.ClientRectangle.Top, this.ClientRectangle.Width, this.ClientRectangle.Height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, sourceData);

            for (int y1 = bmpData.Height - 1, y2 = 0; y1 >= 0; y1--, y2++)
                Array.Copy(sourceData, y1 * stride, targetData, y2 * stride, stride);

            System.Runtime.InteropServices.Marshal.Copy(targetData, 0, bmpData.Scan0, targetData.Length);

            bitmap.UnlockBits(bmpData);

            if (cropByAlphaChannel)
            {
                // TODO: make less ugly?
                Rectangle cropRectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

                bool topFound = false;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    bool yNoPixels = true;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int pixelOffset = (y * stride) + (x * (Bitmap.GetPixelFormatSize(bitmap.PixelFormat) / 8));
                        if (targetData[pixelOffset + 3] != 0)
                        {
                            yNoPixels = false;
                            topFound = true;
                            break;
                        }
                    }

                    if (yNoPixels && !topFound)
                        cropRectangle.Y++;
                    else if (yNoPixels && topFound)
                        cropRectangle.Height--;
                }

                bool leftFound = false;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    bool xNoPixels = true;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        int pixelOffset = (y * stride) + (x * (Bitmap.GetPixelFormatSize(bitmap.PixelFormat) / 8));
                        if (targetData[pixelOffset + 3] != 0)
                        {
                            xNoPixels = false;
                            leftFound = true;
                            break;
                        }
                    }

                    if (xNoPixels && !leftFound)
                        cropRectangle.X++;
                    else if (xNoPixels && leftFound)
                        cropRectangle.Width--;
                }

                cropRectangle.Width -= cropRectangle.X;
                cropRectangle.Height -= cropRectangle.Y;
                bitmap = bitmap.Clone(cropRectangle, bitmap.PixelFormat);
            }

            return bitmap;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Core.IsReady)
            {
                e.Graphics.Clear(this.BackColor);
                using (Pen pen = new Pen(Color.Red, 3.0f))
                {
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    e.Graphics.DrawLine(pen, Point.Empty, new Point(this.ClientRectangle.Right, this.ClientRectangle.Bottom));
                    e.Graphics.DrawLine(pen, new Point(0, this.ClientRectangle.Bottom), new Point(this.ClientRectangle.Right, 0));
                }
                return;
            }

            this.OnRender(EventArgs.Empty);

            this.SwapBuffers();
        }

        protected virtual void OnRender(EventArgs e)
        {
            if (!Core.IsReady) return;

            EventHandler<EventArgs> handler = Render;
            if (handler != null) handler(this, e);
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!Core.IsReady) return;

            GL.ClearColor(this.BackColor);

            base.OnLoad(e);

            this.OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            if (!Core.IsReady) return;

            base.OnResize(e);
        }
    }
}
