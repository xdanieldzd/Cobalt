using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Cobalt
{
    public enum FontType { Monospace, SansSerif, Serif }

    // TODO: make less crappy

    public class Font : IDisposable
    {
        const char startChar = ' ', endChar = '~';

        const string vertexShader = @"
#version 330

layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
out vec2 vertTexCoord;

uniform mat4 projection_matrix;

uniform float textPosX, textPosY, scaleX, scaleY;

void main()
{
    vertTexCoord = in_texCoord;
    gl_Position = projection_matrix * vec4(((textPosX + in_position.x) * scaleX) - 1.0, ((textPosY + in_position.y) * scaleY) + 1.0, 0.0, 1.0);
}
";

        const string fragmentShader = @"
#version 330

in vec2 vertTexCoord;
out vec4 fragColor;

uniform sampler2D tex;
uniform vec4 textColor;

void main()
{
    vec4 text = texture(tex, vertTexCoord.st);
    fragColor.rgb = textColor.rgb;
    fragColor.a = text.a;
}
";

        static readonly Color4[] textColors = new Color4[]
        {
            new Color4(1.0f, 1.0f, 1.0f, 1.0f),
            new Color4(0.0f, 0.0f, 0.0f, 0.0f),
        };

        Shader shader;
        int projectionMatrixLoc, scaleXLoc, scaleYLoc, textColorLoc, textPosXLoc, textPosYLoc;

        float size, charHeight, scaleX, scaleY;
        Dictionary<char, FontCharacter> characters;
        Dictionary<float, int> vaoWidthMap;
        List<int> usedArrayBuffers;

        bool disposed = false;

        Font()
        {
            GL.UseProgram(0);

            shader = new Shader(vertexShader, fragmentShader);
            GL.Uniform1(GL.GetUniformLocation(shader.ProgramHandle, "tex"), 0);

            projectionMatrixLoc = GL.GetUniformLocation(shader.ProgramHandle, "projection_matrix");
            scaleXLoc = GL.GetUniformLocation(shader.ProgramHandle, "scaleX");
            scaleYLoc = GL.GetUniformLocation(shader.ProgramHandle, "scaleY");
            textColorLoc = GL.GetUniformLocation(shader.ProgramHandle, "textColor");
            textPosXLoc = GL.GetUniformLocation(shader.ProgramHandle, "textPosX");
            textPosYLoc = GL.GetUniformLocation(shader.ProgramHandle, "textPosY");

            size = 24.0f;
            scaleX = scaleY = 0.5f;
            characters = new Dictionary<char, FontCharacter>();
            vaoWidthMap = new Dictionary<float, int>();
            usedArrayBuffers = new List<int>();
        }

        public Font(string fontName)
            : this()
        {
            Load(new FontFamily(fontName));
        }

        public Font(FontType style)
            : this()
        {
            GenericFontFamilies genericFamily;
            switch (style)
            {
                case FontType.Monospace:
                    genericFamily = GenericFontFamilies.Monospace;
                    break;
                case FontType.SansSerif:
                    genericFamily = GenericFontFamilies.SansSerif;
                    break;
                case FontType.Serif:
                default:
                    genericFamily = GenericFontFamilies.Serif;
                    break;
            }

            Load(new FontFamily(genericFamily));
        }

        public void SetScreenSize(int width, int height)
        {
            float w = width, h = height;

            Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0.0f, w, h, 0.0f, -1.0f, 1.0f);
            shader.Activate();
            GL.UniformMatrix4(projectionMatrixLoc, false, ref projection);
        }

        private double NextPowerOfTwo(double value)
        {
            return Math.Pow(2.0, Math.Ceiling(Math.Log(value) / Math.Log(2.0)));
        }

        private void Load(FontFamily family)
        {
            SetScreenSize(640, 480);

            System.Drawing.Font font = new System.Drawing.Font(family, size, FontStyle.Bold, GraphicsUnit.Pixel);

            charHeight = (float)NextPowerOfTwo((double)font.Size);
            for (char c = startChar; c < endChar; c++)
            {
                SizeF charSize;
                using (Graphics g = Graphics.FromImage(new Bitmap(1, 1)))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    charSize = g.MeasureString(c.ToString(), font, 256, StringFormat.GenericTypographic);
                    if (charSize.Width == 0)
                        charSize = g.MeasureString(c.ToString(), font);
                }

                int charTextureWidth = (int)NextPowerOfTwo(charSize.Width);
                Bitmap charTextureImage = new Bitmap(charTextureWidth, (int)charHeight);
                using (Graphics g = Graphics.FromImage(charTextureImage))
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(c.ToString(), font, Brushes.White, PointF.Empty, StringFormat.GenericTypographic);
                }
                //charTextureImage.Save(@"C:\Temp\glsl\" + ((int)c).ToString() + ".png");

                FontCharacter fontChar = new FontCharacter((float)Math.Round(charSize.Width, 1), charTextureImage);

                if (!vaoWidthMap.ContainsKey(fontChar.Width))
                {
                    float u1 = (float)fontChar.Width * (1.0f / (float)charTextureWidth);
                    float v1 = (float)font.Height * (1.0f / charHeight);

                    CharacterVertex[] vertices = new CharacterVertex[6]
                    {
                        new CharacterVertex(0.0f, charHeight, 0.0f, v1),
                        new CharacterVertex(fontChar.Width, 0.0f, u1, 0.0f),
                        new CharacterVertex(0.0f, 0.0f, 0.0f, 0.0f),
                        
                        new CharacterVertex(fontChar.Width, charHeight, u1, v1),
                        new CharacterVertex(fontChar.Width, 0.0f, u1, 0.0f),
                        new CharacterVertex(0.0f, charHeight, 0.0f, v1),
                    };

                    int vaoHandle = GL.GenVertexArray();
                    GL.BindVertexArray(vaoHandle);

                    int bufferHandle = GL.GenBuffer();
                    GL.BindBuffer(BufferTarget.ArrayBuffer, bufferHandle);
                    GL.BufferData<CharacterVertex>(BufferTarget.ArrayBuffer, new IntPtr(CharacterVertex.StructSize * vertices.Length), vertices, BufferUsageHint.StaticDraw);

                    GL.EnableVertexAttribArray(0);
                    GL.EnableVertexAttribArray(1);

                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, CharacterVertex.StructSize, 0);
                    GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, CharacterVertex.StructSize, Marshal.OffsetOf(typeof(CharacterVertex), "U"));

                    GL.BindVertexArray(0);

                    usedArrayBuffers.Add(bufferHandle);
                    vaoWidthMap.Add(fontChar.Width, vaoHandle);
                }

                characters.Add(c, fontChar);
            }
        }

        public void DrawString(float x, float y, string text)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            shader.Activate();

            GL.PushAttrib(AttribMask.AllAttribBits);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Always, 0.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            GL.Uniform1(scaleXLoc, scaleX);
            GL.Uniform1(scaleYLoc, scaleY);

            for (int i = textColors.Length - 1; i >= 0; i--)
            {
                float mx = x + (i * 2.0f);
                float my = y + (i * 2.0f);

                GL.Uniform4(textColorLoc, textColors[i]);
                GL.Uniform1(textPosYLoc, my);

                float cx = mx;
                for (int j = 0; j < text.Length; j++)
                {
                    char c = text[j];

                    if (c == '\n' || (c == '\r' && text[j + 1] == '\n'))
                    {
                        cx = mx;
                        my += charHeight;
                        GL.Uniform1(textPosYLoc, my);
                        if (c == '\r' && text[j + 1] == '\n') j++;
                        continue;
                    }

                    GL.Uniform1(textPosXLoc, cx);

                    if (!characters.ContainsKey(c)) continue;
                    FontCharacter fontChar = characters[c];

                    if (c != ' ')
                    {
                        if (!vaoWidthMap.ContainsKey(fontChar.Width)) continue;
                        int vaoHandle = vaoWidthMap[fontChar.Width];

                        GL.BindTexture(TextureTarget.Texture2D, fontChar.TextureHandle);

                        GL.BindVertexArray(vaoHandle);
                        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                        GL.BindVertexArray(0);
                    }

                    cx += fontChar.Width;
                }
            }

            GL.PopAttrib();

            GL.UseProgram(0);
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
                foreach (KeyValuePair<float, int> vao in vaoWidthMap.Where(x => GL.IsVertexArray(x.Value)))
                    GL.DeleteVertexArray(vao.Value);

                foreach (int vbo in usedArrayBuffers.Where(x => GL.IsBuffer(x)))
                    GL.DeleteBuffer(vbo);

                shader.Dispose();

                disposed = true;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct CharacterVertex
    {
        public static readonly int StructSize = Marshal.SizeOf(typeof(CharacterVertex));

        public float X, Y, U, V;

        public CharacterVertex(float x, float y, float u, float v)
        {
            X = x; Y = y; U = u; V = v;
        }
    }

    internal class FontCharacter
    {
        public float Width { get; set; }
        public int TextureHandle { get; set; }

        public FontCharacter(float width, Bitmap texture)
        {
            Width = width;

            TextureHandle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            if (texture.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb) throw new ArgumentException("Texture is not 32bpp Argb");

            BitmapData bmpData = texture.LockBits(new Rectangle(0, 0, texture.Width, texture.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmpData.Width, bmpData.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
            texture.UnlockBits(bmpData);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }
}
