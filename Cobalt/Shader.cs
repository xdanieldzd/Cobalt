using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

using OpenTK;
using OpenTK.Graphics.OpenGL;

using Cobalt.Internals;

namespace Cobalt
{
    public enum ShaderCommonUniform
    {
        ProjectionMatrix,
        ModelViewMatrix,
        MaterialTexture,
        MaterialAmbientColor,
        MaterialDiffuseColor,
        MaterialSpecularColor,
    }

    public class Shader : IDisposable
    {
        static int lastShaderBound = -1;

        int vertexShaderHandle, fragmentShaderHandle, shaderProgramHandle;

        public int ProgramHandle { get { return shaderProgramHandle; } }

        static readonly string[] uniformSetMethods =
        {
            "Uniform1", "Uniform2", "Uniform3", "Uniform4"
        };

        static readonly string[] uniformSetMethodsMatrix =
        {
            "UniformMatrix2", "UniformMatrix2x3", "UniformMatrix2x4",
            "UniformMatrix3", "UniformMatrix3x2", "UniformMatrix3x4",
            "UniformMatrix4", "UniformMatrix4x2", "UniformMatrix4x3"
        };

        Dictionary<ShaderCommonUniform, string> commonUniformNames;
        Dictionary<string, int> uniformLocations;
        Dictionary<string, dynamic> uniformData;
        Dictionary<Type, FastInvokeHandler> uniformMethods;

        bool disposed = false;

        Shader()
        {
            vertexShaderHandle = fragmentShaderHandle = shaderProgramHandle = -1;

            commonUniformNames = new Dictionary<ShaderCommonUniform, string>();
            uniformLocations = new Dictionary<string, int>();
            uniformData = new Dictionary<string, dynamic>();
            uniformMethods = new Dictionary<Type, FastInvokeHandler>();
        }

        public Shader(string vertexShader, string fragmentShader)
            : this()
        {
            CompileShader(ShaderType.VertexShader, vertexShader, out vertexShaderHandle);
            CompileShader(ShaderType.FragmentShader, fragmentShader, out fragmentShaderHandle);
            CreateProgram();
        }

        public void SetUniformName(ShaderCommonUniform uniformType, string name)
        {
            if (!commonUniformNames.ContainsKey(uniformType))
                commonUniformNames.Add(uniformType, name);
            else
                commonUniformNames[uniformType] = name;
        }

        public string GetUniformName(ShaderCommonUniform uniformType)
        {
            if (commonUniformNames.ContainsKey(uniformType))
                return commonUniformNames[uniformType];
            else
                return null;
        }

        public bool IsUniformNameSet(ShaderCommonUniform uniformType)
        {
            return commonUniformNames.ContainsKey(uniformType);
        }

        public void SetUniform(ShaderCommonUniform uniformType, dynamic data)
        {
            if (commonUniformNames.ContainsKey(uniformType))
                SetUniform(commonUniformNames[uniformType], data);
            else
                throw new Exception("Name for uniform type not set");
        }

        public void SetUniform(string name, dynamic data)
        {
            Activate();

            Type type = data.GetType();

            if (!uniformLocations.ContainsKey(name))
                uniformLocations.Add(name, GL.GetUniformLocation(shaderProgramHandle, name));

            uniformData[name] = data;

            if (uniformMethods.ContainsKey(type))
            {
                uniformMethods[type](null, new object[] { uniformLocations[name], data });
            }
            else
            {
                foreach (string methodName in uniformSetMethods)
                {
                    Type[] argTypes = new Type[] { typeof(int), type };
                    MethodInfo methodInfo = typeof(GL).GetMethod(methodName, argTypes);

                    if (methodInfo != null)
                    {
                        uniformMethods[type] = FastMethodInvoker.GetMethodInvoker(methodInfo);
                        uniformMethods[type](null, new object[] { uniformLocations[name], data });
                        return;
                    }
                }

                throw new Exception("No Uniform method found");
            }
        }

        public void SetUniformMatrix(string name, bool transpose, dynamic data)
        {
            Activate();

            Type type = data.GetType();
            if (!uniformLocations.ContainsKey(name))
                uniformLocations.Add(name, GL.GetUniformLocation(shaderProgramHandle, name));

            uniformData[name] = data;

            if (uniformMethods.ContainsKey(type))
            {
                uniformMethods[type](null, new object[] { uniformLocations[name], transpose, data });
            }
            else
            {
                foreach (string methodName in uniformSetMethodsMatrix)
                {
                    Type[] argTypes = new Type[] { typeof(int), typeof(bool), data.GetType().MakeByRefType() };
                    MethodInfo methodInfo = typeof(GL).GetMethod(methodName, argTypes);

                    if (methodInfo != null)
                    {
                        uniformMethods[type] = FastMethodInvoker.GetMethodInvoker(methodInfo);
                        uniformMethods[type](null, new object[] { uniformLocations[name], transpose, data });
                        return;
                    }
                }

                throw new Exception("No UniformMatrix method found");
            }
        }

        public dynamic GetUniform(ShaderCommonUniform uniformType)
        {
            if (commonUniformNames.ContainsKey(uniformType))
                return GetUniform(commonUniformNames[uniformType]);
            else
                throw new Exception("Name for uniform type not set");
        }

        public dynamic GetUniform(string name)
        {
            if (!uniformData.ContainsKey(name)) throw new ArgumentException();
            return uniformData[name];
        }

        public void CompileShader(ShaderType shaderType, string shaderString)
        {
            if (shaderType == ShaderType.VertexShader)
                CompileShader(shaderType, shaderString, out vertexShaderHandle);
            else if (shaderType == ShaderType.FragmentShader)
                CompileShader(shaderType, shaderString, out fragmentShaderHandle);
        }

        private void CompileShader(ShaderType shaderType, string shaderString, out int handle)
        {
            handle = GL.CreateShader(shaderType);
            GL.ShaderSource(handle, shaderString);
            GL.CompileShader(handle);

            int statusCode;
            string infoLog;
            GL.GetShaderInfoLog(handle, out infoLog);
            GL.GetShader(handle, ShaderParameter.CompileStatus, out statusCode);
            if (statusCode != 1) throw new Exception(infoLog);
        }

        public void CreateProgram()
        {
            if (vertexShaderHandle == -1 || fragmentShaderHandle == -1) throw new Exception("Invalid vertex shader or fragment shader handle");

            shaderProgramHandle = GL.CreateProgram();
            GL.AttachShader(shaderProgramHandle, vertexShaderHandle);
            GL.AttachShader(shaderProgramHandle, fragmentShaderHandle);

            GL.LinkProgram(shaderProgramHandle);
            GL.UseProgram(shaderProgramHandle);
        }

        public void Activate()
        {
            if (lastShaderBound != shaderProgramHandle)
            {
                if (shaderProgramHandle == -1) throw new InvalidOperationException("Invalid shader program handle");
                GL.UseProgram(shaderProgramHandle);
                lastShaderBound = shaderProgramHandle;
            }
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
                if (GL.IsProgram(shaderProgramHandle))
                {
                    if (GL.IsShader(vertexShaderHandle))
                    {
                        GL.DetachShader(shaderProgramHandle, vertexShaderHandle);
                        GL.DeleteShader(vertexShaderHandle);
                    }

                    if (GL.IsShader(fragmentShaderHandle))
                    {
                        GL.DetachShader(shaderProgramHandle, fragmentShaderHandle);
                        GL.DeleteShader(fragmentShaderHandle);
                    }

                    GL.DeleteProgram(shaderProgramHandle);
                }

                disposed = true;
            }
        }
    }
}
