using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace Cobalt.Mesh
{
    public class Mesh : IDisposable
    {
        static int lastElementBufferBound = -1, lastTextureBound = -1;

        static readonly Dictionary<Type, VertexAttribPointerType> pointerTypeTranslator = new Dictionary<Type, VertexAttribPointerType>()
        {
            { typeof(byte), VertexAttribPointerType.UnsignedByte },
            { typeof(sbyte), VertexAttribPointerType.Byte },
            { typeof(ushort), VertexAttribPointerType.UnsignedShort },
            { typeof(short), VertexAttribPointerType.Short },
            { typeof(uint), VertexAttribPointerType.UnsignedInt },
            { typeof(int), VertexAttribPointerType.Int },
            { typeof(float), VertexAttribPointerType.Float },
            { typeof(double), VertexAttribPointerType.Double },
            { typeof(Vector2), VertexAttribPointerType.Float },
            { typeof(Vector3), VertexAttribPointerType.Float },
            { typeof(Vector4), VertexAttribPointerType.Float },
            { typeof(Color4), VertexAttribPointerType.Float },
        };

        int vaoHandle, vboHandle, numElementsToDraw;

        Shader shader;

        PrimitiveType primitiveType;
        Type vertexType;

        int elementBufferHandle;
        DrawElementsType drawElementsType;

        List<VertexElement> vertexElements;
        int vertexStructSize;

        Material material;

        bool disposed = false;

        public Mesh()
        {
            vaoHandle = GL.GenVertexArray();
            vboHandle = numElementsToDraw = -1;

            primitiveType = PrimitiveType.Triangles;
            vertexType = typeof(CommonVertex);

            elementBufferHandle = -1;
            drawElementsType = DrawElementsType.UnsignedByte;

            vertexElements = null;
            vertexStructSize = -1;

            material = null;
        }

        private void InitializeVertexProperties()
        {
            vertexElements = new List<VertexElement>();
            vertexStructSize = Marshal.SizeOf(vertexType);

            foreach (FieldInfo field in vertexType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var attribs = field.GetCustomAttributes(typeof(VertexElementAttribute), false);
                if (attribs == null || attribs.Length != 1) continue;

                VertexElementAttribute elementAttribute = (attribs[0] as VertexElementAttribute);

                int numComponents = Marshal.SizeOf(field.FieldType);

                if (field.FieldType.IsValueType && !field.FieldType.IsEnum)
                {
                    var structFields = field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (structFields == null || structFields.Length < 1 || structFields.Length > 4) throw new Exception("Invalid number of fields in struct");
                    numComponents = structFields.Length;
                }

                vertexElements.Add(new VertexElement()
                {
                    AttributeIndex = elementAttribute.AttributeIndex,
                    DataType = field.FieldType,
                    NumComponents = numComponents,
                    OffsetInVertex = Marshal.OffsetOf(vertexType, field.Name).ToInt32()
                });
            }
        }

        public void SetShader(Shader shader)
        {
            this.shader = shader;
        }

        public Shader GetShader()
        {
            return shader;
        }

        public void SetPrimitiveType(PrimitiveType primType)
        {
            primitiveType = primType;
        }

        public PrimitiveType GetPrimitiveType()
        {
            return primitiveType;
        }

        public void SetMaterial(Material material)
        {
            this.material = material;
        }

        public Material GetMaterial()
        {
            return material;
        }

        public void SetVertexData<TVertex>(TVertex[] vertices) where TVertex : struct, IVertexStruct
        {
            vertexType = typeof(TVertex);
            InitializeVertexProperties();

            GL.BindVertexArray(vaoHandle);

            vboHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboHandle);
            GL.BufferData<TVertex>(BufferTarget.ArrayBuffer, new IntPtr(vertexStructSize * vertices.Length), vertices, BufferUsageHint.StaticDraw);

            foreach (VertexElement element in vertexElements)
            {
                GL.EnableVertexAttribArray(element.AttributeIndex);
                GL.VertexAttribPointer(element.AttributeIndex, element.NumComponents, GetVertexAttribPointerType(element.DataType), false, vertexStructSize, element.OffsetInVertex);
            }

            numElementsToDraw = vertices.Length;

            GL.BindVertexArray(0);
        }

        public void SetIndices<TIndex>(TIndex[] indices) where TIndex : struct, IConvertible
        {
            drawElementsType = GetDrawElementsType(typeof(TIndex));

            elementBufferHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferHandle);
            GL.BufferData<TIndex>(BufferTarget.ElementArrayBuffer, new IntPtr(Marshal.SizeOf(typeof(TIndex)) * indices.Length), indices, BufferUsageHint.StaticDraw);

            numElementsToDraw = indices.Length;
        }

        private VertexAttribPointerType GetVertexAttribPointerType(Type type)
        {
            if (pointerTypeTranslator.ContainsKey(type))
                return pointerTypeTranslator[type];
            else
                throw new ArgumentException("Unimplemented or unsupported datatype");
        }

        private DrawElementsType GetDrawElementsType(Type type)
        {
            if (type == typeof(byte))
                return DrawElementsType.UnsignedByte;
            else if (type == typeof(ushort))
                return DrawElementsType.UnsignedShort;
            else if (type == typeof(uint))
                return DrawElementsType.UnsignedInt;
            else
                throw new ArgumentException("Unsupported data type");
        }

        public void Render()
        {
            if (shader != null)
            {
                shader.Activate();
            }

            GL.BindVertexArray(vaoHandle);

            // TODO: handle untextured materials properly
            if (material != null && lastTextureBound != material.Texture.Handle)
            {
                material.Texture.Activate();

                if (shader != null)
                {
                    if (shader.IsUniformNameSet(ShaderCommonUniform.MaterialAmbientColor))
                        shader.SetUniform(ShaderCommonUniform.MaterialAmbientColor, material.Ambient);

                    if (shader.IsUniformNameSet(ShaderCommonUniform.MaterialDiffuseColor))
                        shader.SetUniform(ShaderCommonUniform.MaterialDiffuseColor, material.Diffuse);

                    if (shader.IsUniformNameSet(ShaderCommonUniform.MaterialSpecularColor))
                        shader.SetUniform(ShaderCommonUniform.MaterialSpecularColor, material.Specular);
                }

                lastTextureBound = material.Texture.Handle;
            }

            if (elementBufferHandle != -1)
            {
                if (lastElementBufferBound != elementBufferHandle)
                {
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferHandle);
                    lastElementBufferBound = elementBufferHandle;
                }

                GL.DrawElements(primitiveType, numElementsToDraw, drawElementsType, 0);
            }
            else
                GL.DrawArrays(primitiveType, 0, numElementsToDraw);
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
                if (GL.IsVertexArray(vaoHandle))
                    GL.DeleteVertexArray(vaoHandle);

                if (GL.IsBuffer(vboHandle))
                    GL.DeleteBuffer(vboHandle);

                disposed = true;
            }
        }
    }

    public interface IVertexStruct { }

    internal class VertexElement
    {
        public int AttributeIndex { get; set; }
        public Type DataType { get; set; }
        public int NumComponents { get; set; }
        public int OffsetInVertex { get; set; }

        public VertexElement()
        {
            AttributeIndex = -1;
            DataType = null;
            NumComponents = -1;
            OffsetInVertex = -1;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class VertexElementAttribute : Attribute
    {
        public int AttributeIndex { get; set; }

        public VertexElementAttribute()
        {
            AttributeIndex = -1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CommonVertex : IVertexStruct
    {
        [VertexElement(AttributeIndex = 0)]
        public Vector4 Position;
        [VertexElement(AttributeIndex = 1)]
        public Vector3 Normal;
        [VertexElement(AttributeIndex = 2)]
        public Color4 Color;
        [VertexElement(AttributeIndex = 3)]
        public Vector2 TexCoord;
    }
}
