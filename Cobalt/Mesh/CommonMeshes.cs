using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Cobalt.Mesh
{
    public static class CommonMeshes
    {
        static readonly CommonVertex[] cubeVertices = new CommonVertex[]
        {
            new CommonVertex() { Position = new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), Normal = new Vector3(-1.0f, -1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, -1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, -1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(-1.0f, 1.0f, 1.0f, 1.0f), Normal = new Vector3(-1.0f, 1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), Normal = new Vector3(-1.0f, -1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, -1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, -1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, 1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, 1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(-1.0f, 1.0f, -1.0f, 1.0f), Normal = new Vector3(-1.0f, 1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) }
        };
        static readonly byte[] cubeIndices = new byte[]
        {
            0, 1, 2, 2, 3, 0,
            3, 2, 6, 6, 7, 3,
            7, 6, 5, 5, 4, 7,
            4, 0, 3, 3, 7, 4,
			0, 5, 1, 4, 5, 0,
			1, 5, 6, 6, 2, 1
        };
        static Mesh cubeMesh;

        static readonly CommonVertex[] pyramidVertices = new CommonVertex[]
        {
            new CommonVertex() { Position = new Vector4(-1.0f, -1.0f, 1.0f, 1.0f), Normal = new Vector3(-1.0f, -1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, -1.0f, 1.0f, 1.0f), Normal = new Vector3(1.0f, -1.0f, 1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(-1.0f, -1.0f, -1.0f, 1.0f), Normal = new Vector3(-1.0f, -1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(1.0f, -1.0f, -1.0f, 1.0f), Normal = new Vector3(1.0f, -1.0f, -1.0f), Color = new Color4(0, 71, 171, 255) },
            new CommonVertex() { Position = new Vector4(0.0f, 1.0f, 0.0f, 1.0f), Normal = new Vector3(0.0f, 1.0f, 0.0f), Color = new Color4(0, 71, 171, 255) }
        };
        static readonly byte[] pyramidIndices = new byte[]
        {
            0, 3, 1, 2, 3, 0,
            0, 1, 4, 1, 3, 4,
            4, 3, 2, 4, 2, 0
        };
        static Mesh pyramidMesh;

        public static Mesh GetCubeMesh()
        {
            if (cubeMesh == null)
            {
                cubeMesh = new Mesh();
                cubeMesh.SetPrimitiveType(PrimitiveType.Triangles);
                cubeMesh.SetVertexData(cubeVertices);
                cubeMesh.SetIndices(cubeIndices);
            }
            return cubeMesh;
        }

        public static Mesh GetPyramidMesh()
        {
            if (pyramidMesh == null)
            {
                pyramidMesh = new Mesh();
                pyramidMesh.SetPrimitiveType(PrimitiveType.Triangles);
                pyramidMesh.SetVertexData(pyramidVertices);
                pyramidMesh.SetIndices(pyramidIndices);
            }
            return pyramidMesh;
        }
    }
}
