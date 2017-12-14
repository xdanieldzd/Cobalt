using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

using OpenTK;
using OpenTK.Graphics;

using Cobalt.IO;
using Cobalt.Texture;

namespace Cobalt.Mesh
{
    /* FIXME:
     * - Make parser accumulate geometry based on material used, instead of based on the group, i.e.:
     *    1) Fetch all geometry using material X, regardless of group.
     *    2) Create all meshes at the end of the process, not on every 'g' statement.
     *    3) Name meshes based on material name.
     * - NOTE: Creating meshes based on groups AND materials would probably be ideal, not sure I want to go that way...
     */

    // TODO: Remove FileStream casts; do NOT assume input stream will always be a FileStream

    [FileNamePattern("^.*\\.obj$")]
    internal class WavefrontObj : IMeshLoader
    {
        static char[] tokenSeperator = { ' ', '\t' };

        Dictionary<string, Mesh> meshes;
        Dictionary<string, Material> materials;

        List<CommonVertex> vertices;
        List<Vector4> positions;
        List<Vector3> normals;
        List<Color4> colors;
        List<Vector2> texCoords;

        string currentMeshName;
        string currentMaterialName;

        protected WavefrontObj()
        {
            meshes = new Dictionary<string, Mesh>();
            materials = new Dictionary<string, Material>();

            vertices = new List<CommonVertex>();
            positions = new List<Vector4>();
            normals = new List<Vector3>();
            colors = new List<Color4>();
            texCoords = new List<Vector2>();

            currentMeshName = currentMaterialName = string.Empty;
        }

        public Dictionary<string, Mesh> GetMeshes(Stream input)
        {
            return LoadObj(input);
        }

        private Dictionary<string, Mesh> LoadObj(Stream input)
        {
            TextReader reader = new StreamReader(input);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] tokenized = line.Split(tokenSeperator, StringSplitOptions.RemoveEmptyEntries);
                if (tokenized.Length == 0) continue;

                switch (tokenized[0])
                {
                    case "#": break;
                    case "g":
                        {
                            if (currentMeshName != string.Empty && !meshes.ContainsKey(currentMeshName) && vertices.Count != 0)
                                GenerateMesh(currentMeshName, vertices, (currentMaterialName != string.Empty && materials.ContainsKey(currentMaterialName) ? materials[currentMaterialName] : null), ref meshes);

                            if (tokenized.Length == 1)
                                currentMeshName = tokenized.GetHashCode().ToString();   /* TODO: better way to name meshes? */
                            else
                                currentMeshName = line.Substring(line.IndexOf(tokenized[1]));
                        }
                        break;

                    case "mtllib":
                        {
                            string mtlPath = FileMethods.CreateFullPath((input as FileStream).Name, line.Substring(line.IndexOf(tokenized[1])));

                            if (File.Exists(mtlPath))
                            {
                                using (Stream mtlStream = File.Open(mtlPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    ParseMtl(mtlStream, ref materials);
                                }
                            }
                        }
                        break;

                    case "usemtl":
                        {
                            currentMaterialName = line.Substring(line.IndexOf(tokenized[1]));
                        }
                        break;

                    case "v":
                        {
                            if (tokenized.Length == 5)
                            {
                                positions.Add(
                                    new Vector4(
                                        float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[2], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[3], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[4], CultureInfo.InvariantCulture)));
                            }
                            else if (tokenized.Length == 4)
                            {
                                positions.Add(
                                    new Vector4(
                                        float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[2], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[3], CultureInfo.InvariantCulture),
                                        1.0f));
                            }
                            else if (tokenized.Length == 3)
                            {
                                positions.Add(
                                    new Vector4(
                                        float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                        float.Parse(tokenized[2], CultureInfo.InvariantCulture),
                                        0.0f,
                                        1.0f));
                            }
                        }
                        break;

                    case "vt":
                        {
                            texCoords.Add(
                                new Vector2(
                                    float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                    -float.Parse(tokenized[2], CultureInfo.InvariantCulture)));
                        }
                        break;

                    case "vn":
                        {
                            normals.Add(
                                new Vector3(
                                    float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                    float.Parse(tokenized[2], CultureInfo.InvariantCulture),
                                    float.Parse(tokenized[3], CultureInfo.InvariantCulture)));
                        }
                        break;

                    case "vc":
                        {
                            colors.Add(
                                new Color4(
                                    float.Parse(tokenized[1], CultureInfo.InvariantCulture),
                                    float.Parse(tokenized[2], CultureInfo.InvariantCulture),
                                    float.Parse(tokenized[3], CultureInfo.InvariantCulture),
                                    float.Parse(tokenized[4], CultureInfo.InvariantCulture)));
                        }
                        break;

                    case "f":
                        {
                            int[] positionIndices = new int[tokenized.Length - 1];
                            int[] texCoordIndices = new int[tokenized.Length - 1];
                            int[] normalIndices = new int[tokenized.Length - 1];
                            int[] colorIndices = new int[tokenized.Length - 1];

                            for (int i = 0; i < tokenized.Length - 1; i++)
                            {
                                string[] values = tokenized[i + 1].Split('/');

                                int[] positionLocal = new int[3];
                                int[] texCoordLocal = new int[3];
                                int[] normalLocal = new int[3];
                                int[] colorLocal = new int[3];

                                int.TryParse(values[0], out positionIndices[i]);

                                if (values.Length >= 2 && values[1] != string.Empty)
                                    int.TryParse(values[1], out texCoordIndices[i]);
                                else
                                    texCoordIndices[i] = positionIndices[i];

                                if (values.Length >= 3 && values[2] != string.Empty)
                                    int.TryParse(values[2], out normalIndices[i]);
                                else
                                    normalIndices[i] = positionIndices[i];

                                if (values.Length >= 4 && values[3] != string.Empty)
                                    int.TryParse(values[3], out colorIndices[i]);
                                else
                                    colorIndices[i] = positionIndices[i];

                                positionIndices[i] -= 1;
                                texCoordIndices[i] -= 1;
                                normalIndices[i] -= 1;
                                colorIndices[i] -= 1;

                                if (i >= 2)
                                {
                                    if (positionIndices[0 + (i - 2)] != -1 && positionIndices[1 + (i - 2)] != -1 && positionIndices[2 + (i - 2)] != -1)
                                    {
                                        positionLocal[0] = positionIndices[0]; texCoordLocal[0] = texCoordIndices[0]; normalLocal[0] = normalIndices[0]; colorLocal[0] = colorIndices[0];
                                        positionLocal[1] = positionIndices[i - 1]; texCoordLocal[1] = texCoordIndices[i - 1]; normalLocal[1] = normalIndices[i - 1]; colorLocal[1] = colorIndices[i - 1];
                                        positionLocal[2] = positionIndices[i]; texCoordLocal[2] = texCoordIndices[i]; normalLocal[2] = normalIndices[i]; colorLocal[2] = colorIndices[i];

                                        CommonVertex[] localVerts = new CommonVertex[3];
                                        for (int j = 0; j < localVerts.Length; j++)
                                        {
                                            Vector4 localPosition = (positionIndices[j] != -1 && positionIndices[j] < positions.Count ? positions[positionIndices[j]] : Vector4.Zero);
                                            Vector2 localTexCoords = (texCoordIndices[j] != -1 && texCoordIndices[j] < texCoords.Count ? texCoords[texCoordIndices[j]] : Vector2.Zero);
                                            Vector3 localNormals = (normalIndices[j] != -1 && normalIndices[j] < normals.Count ? normals[normalIndices[j]] : Vector3.Zero);
                                            Color4 localColor = (colorIndices[j] != -1 && colorIndices[j] < colors.Count ? colors[colorIndices[j]] : Color4.White);

                                            localVerts[j] = new CommonVertex()
                                            {
                                                Position = localPosition,
                                                TexCoord = localTexCoords,
                                                Normal = localNormals,
                                                Color = localColor
                                            };
                                        }
                                        vertices.AddRange(localVerts);
                                    }
                                }
                            }
                        }
                        break;
                }
            }

            reader.Close();

            if (meshes.Count == 0 || vertices.Count != 0)
                GenerateMesh("mesh", vertices, (currentMaterialName != string.Empty && materials.ContainsKey(currentMaterialName) ? materials[currentMaterialName] : null), ref meshes);

            return meshes;
        }

        private void GenerateMesh<TVertex>(string name, List<TVertex> vertices, Material material, ref Dictionary<string, Mesh> meshes) where TVertex : struct, IVertexStruct
        {
            if (meshes.ContainsKey(name)) return;

            meshes.Add(name, new Mesh());
            meshes[name].SetVertexData(vertices.ToArray());
            meshes[name].SetMaterial(material);

            vertices.Clear();
        }

        private void ParseMtl(Stream input, ref Dictionary<string, Material> materials)
        {
            bool materialOpen = false;

            string currentMaterialName = string.Empty;

            string texturePathAmbient = string.Empty, texturePathDiffuse = string.Empty, texturePathSpecular = string.Empty;
            Color4 ambientColor = Color4.White, diffuseColor = Color4.Gray, specularColor = Color4.Black;
            float alpha = -1.0f;

            TextReader reader = new StreamReader(input);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] tokenized = line.Split(tokenSeperator, StringSplitOptions.RemoveEmptyEntries);
                if (tokenized.Length == 0) continue;

                switch (tokenized[0])
                {
                    case "#": break;

                    case "newmtl":
                        {
                            if (materialOpen)
                                GenerateMaterial(currentMaterialName, texturePathAmbient, texturePathDiffuse, texturePathSpecular, ambientColor, diffuseColor, specularColor, alpha, ref materials);

                            materialOpen = true;

                            currentMaterialName = line.Substring(line.IndexOf(tokenized[1]));

                            ambientColor = Color4.White;
                            diffuseColor = Color4.Gray;
                            specularColor = Color4.Black;
                            alpha = -1.0f;
                        }
                        break;

                    case "Ka":
                        ambientColor = new Color4(
                            float.Parse(tokenized[1], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[2], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[3], NumberStyles.Float, CultureInfo.InvariantCulture),
                            1.0f);
                        break;

                    case "Kd":
                        diffuseColor = new Color4(
                            float.Parse(tokenized[1], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[2], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[3], NumberStyles.Float, CultureInfo.InvariantCulture),
                            1.0f);
                        break;

                    case "Ks":
                        specularColor = new Color4(
                            float.Parse(tokenized[1], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[2], NumberStyles.Float, CultureInfo.InvariantCulture),
                            float.Parse(tokenized[3], NumberStyles.Float, CultureInfo.InvariantCulture),
                            1.0f);
                        break;

                    case "Tr":
                    case "d":
                        float newAlpha = float.Parse(tokenized[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        if (alpha == -1.0f) alpha = newAlpha;
                        break;

                    case "map_Ka":
                    case "mapKa":
                        texturePathAmbient = FileMethods.CreateFullPath((input as FileStream).Name, line.Substring(line.IndexOf(tokenized[1])));
                        break;

                    case "map_Kd":
                    case "mapKd":
                        texturePathDiffuse = FileMethods.CreateFullPath((input as FileStream).Name, line.Substring(line.IndexOf(tokenized[1])));
                        break;

                    case "map_Ks":
                    case "mapKs":
                        texturePathSpecular = FileMethods.CreateFullPath((input as FileStream).Name, line.Substring(line.IndexOf(tokenized[1])));
                        break;
                }
            }

            reader.Close();

            if (materialOpen)
                GenerateMaterial(currentMaterialName, texturePathAmbient, texturePathDiffuse, texturePathSpecular, ambientColor, diffuseColor, specularColor, alpha, ref materials);
        }

        private void GenerateMaterial(string name, string texAmbient, string texDiffuse, string texSpecular,
            Color4 ambient, Color4 diffuse, Color4 specular, float alpha, ref Dictionary<string, Material> materials)
        {
            if (materials.ContainsKey(name)) return;

            if (alpha != -1.0f) ambient.A = diffuse.A = alpha;

            Texture.Texture texture = null;

            if (texAmbient != string.Empty)
                texture = TextureLoader.Load(texAmbient);
            else if (texDiffuse != string.Empty)
                texture = TextureLoader.Load(texDiffuse);
            else if (texSpecular != string.Empty)
                texture = TextureLoader.Load(texSpecular);
            else
                return;

            materials.Add(name, new Material(texture, ambient, diffuse, specular));
        }
    }
}
