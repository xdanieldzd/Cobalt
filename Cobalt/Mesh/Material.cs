using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;

using Cobalt.Texture;

namespace Cobalt.Mesh
{
    public class Material : IDisposable
    {
        public Texture.Texture Texture { get; set; }
        public Color4 Ambient { get; set; }
        public Color4 Diffuse { get; set; }
        public Color4 Specular { get; set; }

        bool disposed = false;

        public Material()
        {
            Texture = null;
            Ambient = Color4.Gray;
            Diffuse = Color4.LightGray;
            Specular = Color4.White;
        }

        public Material(Texture.Texture texture)
            : this()
        {
            Texture = texture;
        }

        public Material(Texture.Texture texture, Color4 ambient, Color4 diffuse, Color4 specular)
            : this()
        {
            Texture = texture;
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
        }

        public Material(Texture.Texture texture, Vector4 ambient, Vector4 diffuse, Vector4 specular)
            : this()
        {
            Texture = texture;
            Ambient = new Color4(ambient.X, ambient.Y, ambient.Z, ambient.W);
            Diffuse = new Color4(diffuse.X, diffuse.Y, diffuse.Z, diffuse.W);
            Specular = new Color4(specular.X, specular.Y, specular.Z, specular.W);
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
                if (Texture != null)
                    Texture.Dispose();

                disposed = true;
            }
        }
    }
}
