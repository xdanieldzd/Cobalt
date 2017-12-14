using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

using OpenTK;
using OpenTK.Graphics;

using Cobalt.IO;
using Cobalt.Mesh;

namespace Cobalt.Mesh
{
    public static class MeshLoader
    {
        public static Dictionary<Type, Func<Stream, Dictionary<string, Mesh>>> KnownLoaders = new Dictionary<Type, Func<Stream, Dictionary<string, Mesh>>>();

        static MeshLoader()
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().Where(x => x.GetInterfaces().Contains(typeof(IMeshLoader)) && !x.IsInterface))
            {
                var method = type.GetMethod("GetMeshes", BindingFlags.Instance | BindingFlags.Public);
                if (method == null || method.ReturnType != typeof(Dictionary<string, Mesh>)) continue;

                var instance = Activator.CreateInstance(type, true);
                var getMeshesDelegate = (Func<Stream, Dictionary<string, Mesh>>)Delegate.CreateDelegate(typeof(Func<Stream, Dictionary<string, Mesh>>), instance, method);

                KnownLoaders.Add(type, getMeshesDelegate);
            }
        }

        public static Dictionary<string, Mesh> Load<TMeshLoader>(string filename) where TMeshLoader : IMeshLoader
        {
            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Load<TMeshLoader>(stream);
            }
        }

        public static Dictionary<string, Mesh> Load<TMeshLoader>(Stream stream) where TMeshLoader : IMeshLoader
        {
            return KnownLoaders[typeof(TMeshLoader)](stream);
        }

        public static Dictionary<string, Mesh> Load(string filename)
        {
            Dictionary<string, Mesh> meshes = null;

            Type assumedType = FileMethods.IdentifyFileByName(filename, KnownLoaders.Select(x => x.Key).ToList());
            if (assumedType != null)
            {
                using (FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    meshes = KnownLoaders[assumedType](stream);
                }
                return meshes;
            }
            else
                throw new NotImplementedException();
        }
    }
}
