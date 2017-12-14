using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;

namespace Cobalt
{
    public static class MatrixHelper
    {
        public static Matrix4 GetMatrix(Vector3 scale, Vector3 rotation, Vector3 translation)
        {
            Matrix4 matrix = Matrix4.Identity;
            matrix *= Matrix4.CreateScale(scale);
            matrix *= Matrix4.CreateRotationZ(rotation.Z);
            matrix *= Matrix4.CreateRotationY(rotation.Y);
            matrix *= Matrix4.CreateRotationX(rotation.X);
            matrix *= Matrix4.CreateTranslation(translation);
            return matrix;
        }
    }
}
