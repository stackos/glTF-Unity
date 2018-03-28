using UnityEditor;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class glTFImporter : EditorWindow
{
    string gltfPath;

    [MenuItem("glTF/Importer")]
    public static void ShowWindow()
    {
        EditorWindow win = EditorWindow.GetWindow(typeof(glTFImporter));
        win.minSize = new Vector2(800, 600);
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("glTF path", GUILayout.Width(55));
        GUILayout.TextField(gltfPath);
        if (GUILayout.Button("load", GUILayout.Width(80)))
        {
            string dir = null;
            if (File.Exists(gltfPath))
            {
                dir = new FileInfo(gltfPath).Directory.FullName;
            }
            gltfPath = EditorUtility.OpenFilePanelWithFilters("Select gltf model file", dir, new string[]{ "gltf", "gltf" });

            if (File.Exists(gltfPath))
            {
                LoadGLTF(gltfPath);
            }
        }
        GUILayout.EndHorizontal();
    }

    static string GetPropertyString(JToken obj, string name, string defaultValue)
    {
        JToken value;
        if ((obj as JObject).TryGetValue(name, out value))
        {
            return (string) value;
        }
        return defaultValue;
    }

    static int GetPropertyInt(JToken obj, string name, int defaultValue)
    {
        JToken value;
        if ((obj as JObject).TryGetValue(name, out value))
        {
            return (int) value;
        }
        return defaultValue;
    }

    static float GetPropertyFloat(JToken obj, string name, float defaultValue)
    {
        JToken value;
        if ((obj as JObject).TryGetValue(name, out value))
        {
            return (float) value;
        }
        return defaultValue;
    }

    class MeshPrimitives
    {
        public Mesh mesh;
        public List<int> materials;
    }

    class Cache
    {
        public List<byte[]> buffers = new List<byte[]>();
        public List<MeshPrimitives> meshes = new List<MeshPrimitives>();
        public List<Material> materials = new List<Material>();
        public List<Texture2D> textures = new List<Texture2D>();
    }

    static byte[] LoadGLTFBuffer(JObject gltf, Cache cache, int index)
    {
        var jbuffer = gltf["buffers"][index];

        string uri = GetPropertyString(jbuffer, "uri", null);
        string dir = new FileInfo((string) gltf["path"]).Directory.FullName;
        string binPath = dir + "/" + uri;

        if (File.Exists(binPath))
        {
            return File.ReadAllBytes(binPath);
        }
        else
        {
            Debug.LogError("can not load ext bin file:" + binPath);
        }

        return null;
    }

    static T[] AccessBuffer<T>(JObject gltf, Cache cache, JToken jaccessor, int bufferViewIndex, int index, int getCount, int getSize)
    {
        var array = new T[getCount];

        var jbufferView = gltf["bufferViews"][bufferViewIndex];

        int bufferIndex = GetPropertyInt(jbufferView, "buffer", -1);
        if (bufferIndex >= 0)
        {
            int stride = GetPropertyInt(jbufferView, "byteStride", getCount * getSize);
            int offset = 0;
            offset += GetPropertyInt(jbufferView, "byteOffset", 0);
            offset += GetPropertyInt(jaccessor, "byteOffset", 0);

            byte[] data = cache.buffers[bufferIndex];
            System.Buffer.BlockCopy(data, offset + stride * index, array, 0, getCount * getSize);
        }

        return array;
    }

    static void AccessGLTFBuffer<T>(JObject gltf, Cache cache, JObject jattributes, string attributeName, List<T[]> attrs, int getCount, int getSize)
    {
        int accessorIndex = GetPropertyInt(jattributes, attributeName, -1);
        if (accessorIndex >= 0)
        {
            var jaccessor = gltf["accessors"][accessorIndex];

            int bufferViewIndex = GetPropertyInt(jaccessor, "bufferView", -1);
            if (bufferViewIndex >= 0)
            {
                int count = GetPropertyInt(jaccessor, "count", -1);
                for (int i = 0; i < count; ++i)
                {
                    T[] attr = AccessBuffer<T>(gltf, cache, jaccessor, bufferViewIndex, i, getCount, getSize);
                    attrs.Add(attr);
                }
            }
        }
    }

    static MeshPrimitives LoadGLTFMesh(JObject gltf, Cache cache, int meshIndex)
    {
        var jmesh = gltf["meshes"][meshIndex];

        List<float[]> vertexValues = new List<float[]>();
        List<float[]> normalValues = new List<float[]>();
        List<float[]> tangentValues = new List<float[]>();
        List<float[]> uvValues = new List<float[]>();
        List<float[]> uv2Values = new List<float[]>();
        List<float[]> colorValues = new List<float[]>();
        List<short[]> blendIndexValues = new List<short[]>();
        List<float[]> blendWeightValues = new List<float[]>();
        List<int[]> indices = new List<int[]>();
        List<int> materials = new List<int>();

        int vertexCount = 0;

        var jprimitives = jmesh["primitives"] as JArray;
        int primitiveCount = jprimitives.Count;
        for (int i = 0; i < primitiveCount; ++i)
        {
            var jprimitive = jprimitives[i] as JObject;

            var jattributes = jprimitive["attributes"] as JObject;
            AccessGLTFBuffer(gltf, cache, jattributes, "POSITION", vertexValues, 3, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "NORMAL", normalValues, 3, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "TANGENT", tangentValues, 4, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "TEXCOORD_0", uvValues, 2, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "TEXCOORD_1", uv2Values, 2, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "COLOR_0", colorValues, 4, 4);
            AccessGLTFBuffer(gltf, cache, jattributes, "JOINTS_0", blendIndexValues, 4, 2);
            AccessGLTFBuffer(gltf, cache, jattributes, "WEIGHTS_0", blendWeightValues, 4, 4);

            int indicesAccessor = GetPropertyInt(jprimitive, "indices", -1);
            if (indicesAccessor >= 0)
            {
                var jaccessor = gltf["accessors"][indicesAccessor];

                int bufferViewIndex = GetPropertyInt(jaccessor, "bufferView", -1);
                if (bufferViewIndex >= 0)
                {
                    int count = GetPropertyInt(jaccessor, "count", -1);
                    int[] subIndices = new int[count];
                    for (int j = 0; j < count; ++j)
                    {
                        int index = AccessBuffer<ushort>(gltf, cache, jaccessor, bufferViewIndex, j, 1, 2)[0];
                        subIndices[j] = index + vertexCount;
                    }
                    indices.Add(subIndices);
                }
            }

            int materialIndex = GetPropertyInt(jprimitive, "material", -1);
            materials.Add(materialIndex);

            vertexCount += vertexValues.Count;
        }

        Vector3[] vertices = new Vector3[vertexValues.Count];
        for (int i = 0; i < vertexValues.Count; ++i)
        {
            vertices[i] = new Vector3(vertexValues[i][0], vertexValues[i][1], -vertexValues[i][2]);
        }

        Vector3[] normals = new Vector3[normalValues.Count];
        for (int i = 0; i < normalValues.Count; ++i)
        {
            normals[i] = new Vector3(normalValues[i][0], normalValues[i][1], -normalValues[i][2]);
        }

        Vector4[] tangents = new Vector4[tangentValues.Count];
        for (int i = 0; i < tangentValues.Count; ++i)
        {
            tangents[i] = new Vector4(tangentValues[i][0], tangentValues[i][1], tangentValues[i][2], tangentValues[i][3]);
        }

        Vector2[] uv = new Vector2[uvValues.Count];
        for (int i = 0; i < uvValues.Count; ++i)
        {
            uv[i] = new Vector2(uvValues[i][0], -uvValues[i][1]);
        }

        Vector2[] uv2 = new Vector2[uv2Values.Count];
        for (int i = 0; i < uv2Values.Count; ++i)
        {
            uv2[i] = new Vector2(uv2Values[i][0], -uv2Values[i][1]);
        }

        Color[] colors = new Color[colorValues.Count];
        for (int i = 0; i < colorValues.Count; ++i)
        {
            colors[i] = new Color(colorValues[i][0], colorValues[i][1], colorValues[i][2], colorValues[i][3]);
        }

        BoneWeight[] boneWeights = new BoneWeight[blendIndexValues.Count];
        for (int i = 0; i < blendIndexValues.Count; ++i)
        {
            BoneWeight w = new BoneWeight();
            w.boneIndex0 = blendIndexValues[i][0];
            w.boneIndex1 = blendIndexValues[i][1];
            w.boneIndex2 = blendIndexValues[i][2];
            w.boneIndex3 = blendIndexValues[i][3];
            w.weight0 = blendWeightValues[i][0];
            w.weight1 = blendWeightValues[i][1];
            w.weight2 = blendWeightValues[i][2];
            w.weight3 = blendWeightValues[i][3];
            boneWeights[i] = w;
        }

        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uv;
        mesh.uv2 = uv2;
        mesh.colors = colors;
        mesh.boneWeights = boneWeights;
        mesh.subMeshCount = indices.Count;

        for (int i = 0; i < indices.Count; ++i)
        {
            int triangleCount = indices[i].Length / 3;
            int[] subIndices = new int[triangleCount * 3];
            for (int j = 0; j < triangleCount; ++j)
            {
                subIndices[j * 3 + 0] = indices[i][j * 3 + 0];
                subIndices[j * 3 + 1] = indices[i][j * 3 + 2];
                subIndices[j * 3 + 2] = indices[i][j * 3 + 1];
            }
            mesh.SetTriangles(subIndices, i);
        }

        string name = GetPropertyString(jmesh, "name", null);
        if (string.IsNullOrEmpty(name) == false)
        {
            name = string.Format("mesh_{0}", name);
        }
        else
        {
            name = string.Format("mesh_{0}", meshIndex);
        }
        mesh.name = name;

        MeshPrimitives mp = new MeshPrimitives();
        mp.mesh = mesh;
        mp.materials = materials;

        return mp;
    }

    static Material LoadGLTFMaterial(JObject gltf, Cache cache, int materialIndex)
    {
        var jmaterial = gltf["materials"][materialIndex];

        Material material = new Material(Shader.Find("PBR"));

        var jpbrMetallicRoughness = jmaterial["pbrMetallicRoughness"];
        if (jpbrMetallicRoughness != null)
        {
            var jbaseColorTexture = jpbrMetallicRoughness["baseColorTexture"];
            if (jbaseColorTexture != null)
            {
                int index = GetPropertyInt(jbaseColorTexture, "index", -1);
                if (index >= 0)
                {
                    material.SetTexture("u_BaseColorSampler", cache.textures[index]);
                }
            }

            var jmetallicRoughnessTexture = jpbrMetallicRoughness["metallicRoughnessTexture"];
            if (jmetallicRoughnessTexture != null)
            {
                int index = GetPropertyInt(jmetallicRoughnessTexture, "index", -1);
                if (index >= 0)
                {
                    material.SetTexture("u_MetallicRoughnessSampler", cache.textures[index]);
                }
            }

            float metallicFactor = GetPropertyFloat(jpbrMetallicRoughness, "metallicFactor", 1.0f);
            material.SetFloat("u_Metallic", metallicFactor);

            float roughnessFactor = GetPropertyFloat(jpbrMetallicRoughness, "roughnessFactor", 1.0f);
            material.SetFloat("u_Roughness", roughnessFactor);

            var jbaseColorFactor = jpbrMetallicRoughness["baseColorFactor"];
            if (jbaseColorFactor != null)
            {
                Color baseColorFactor = new Color((float) jbaseColorFactor[0], (float) jbaseColorFactor[1], (float) jbaseColorFactor[2], (float) jbaseColorFactor[3]);
                material.SetColor("u_BaseColorFactor", baseColorFactor);
            }
        }

        var jnormalTexture = jmaterial["normalTexture"];
        if (jnormalTexture != null)
        {
            int index = GetPropertyInt(jnormalTexture, "index", -1);
            if (index >= 0)
            {
                material.SetTexture("u_NormalSampler", cache.textures[index]);

                string texturePath = AssetDatabase.GetAssetPath(cache.textures[index]);
                TextureImporter im = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                im.textureType = TextureImporterType.NormalMap;
                //AssetDatabase.ImportAsset(texturePath);
            }

            float scale = GetPropertyFloat(jnormalTexture, "scale", 1.0f);
            material.SetFloat("u_NormalScale", scale);
        }

        var jocclusionTexture = jmaterial["occlusionTexture"];
        if (jocclusionTexture != null)
        {
            int index = GetPropertyInt(jocclusionTexture, "index", -1);
            if (index >= 0)
            {
                material.SetTexture("u_OcclusionSampler", cache.textures[index]);
            }

            float strength = GetPropertyFloat(jocclusionTexture, "strength", 1.0f);
            material.SetFloat("u_OcclusionStrength", strength);
        }

        var jemissiveTexture = jmaterial["emissiveTexture"];
        if (jemissiveTexture != null)
        {
            int index = GetPropertyInt(jemissiveTexture, "index", -1);
            if (index >= 0)
            {
                material.SetTexture("u_EmissiveSampler", cache.textures[index]);
            }
        }

        var jemissiveFactor = jmaterial["emissiveFactor"];
        if (jemissiveFactor != null)
        {
            Color emissiveFactor = new Color((float) jemissiveFactor[0], (float) jemissiveFactor[1], (float) jemissiveFactor[2], 1);
            material.SetColor("u_EmissiveFactor", emissiveFactor);
        }

        string name = GetPropertyString(jmaterial, "name", null);
        if (string.IsNullOrEmpty(name) == false)
        {
            name = string.Format("material_{0}", name);
        }
        else
        {
            name = string.Format("material_{0}", materialIndex);
        }
        material.name = name;

        return material;
    }

    static Texture2D LoadGLTFTexture(JObject gltf, Cache cache, int textureIndex, string assetDir)
    {
        var jtexture = gltf["textures"][textureIndex];

        Texture2D texture = null;

        int source = GetPropertyInt(jtexture, "source", -1);
        if (source >= 0)
        {
            var jimage = gltf["images"][source];

            string uri = GetPropertyString(jimage, "uri", null);
            string assetPath = assetDir + "/" + uri;
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (texture == null)
            {
                string dir = new FileInfo((string) gltf["path"]).Directory.FullName;
                string imagePath = dir + "/" + uri;

                if (File.Exists(imagePath))
                {
                    File.Copy(imagePath, assetPath);
                    AssetDatabase.Refresh();

                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                }
                else
                {
                    Debug.LogError("can not load ext image file:" + imagePath);
                }
            }
        }

        int sampler = GetPropertyInt(jtexture, "sampler", -1);
        if (sampler >= 0)
        {
            var jsampler = gltf["samplers"][sampler];
        }

        return texture;
    }

    static void LoadGLTFNode(JObject gltf, Cache cache, int nodeIndex, GameObject parent)
    {
        var jnode = gltf["nodes"][nodeIndex] as JObject;

        string name = GetPropertyString(jnode, "name", "node_" + nodeIndex);
        GameObject obj = new GameObject(name);
        if (parent)
        {
            obj.transform.parent = parent.transform;
        }

        JToken jtranslation;
        if (jnode.TryGetValue("translation", out jtranslation))
        {
            float x = (float) jtranslation[0];
            float y = (float) jtranslation[1];
            float z = (float) jtranslation[2];

            obj.transform.localPosition = new Vector3(x, y, z);
        }

        JToken jrotation;
        if (jnode.TryGetValue("rotation", out jrotation))
        {
            float x = (float) jrotation[0];
            float y = (float) jrotation[1];
            float z = (float) jrotation[2];
            float w = (float) jrotation[3];

            obj.transform.localRotation = new Quaternion(x, y, z, w);
        }

        JToken jscale;
        if (jnode.TryGetValue("scale", out jscale))
        {
            float x = (float) jscale[0];
            float y = (float) jscale[1];
            float z = (float) jscale[2];

            obj.transform.localScale = new Vector3(x, y, z);
        }

        int meshIndex = GetPropertyInt(jnode, "mesh", -1);
        if (meshIndex >= 0)
        {
            obj.AddComponent<MeshFilter>().sharedMesh = cache.meshes[meshIndex].mesh;
            var renderer = obj.AddComponent<MeshRenderer>();

            Material[] materials = new Material[cache.meshes[meshIndex].materials.Count];
            for (int i = 0; i < materials.Length; ++i)
            {
                int materialIndex = cache.meshes[meshIndex].materials[i];
                if (materialIndex >= 0)
                {
                    materials[i] = cache.materials[materialIndex];
                }
            }
            renderer.sharedMaterials = materials;
        }

        JToken jchildren;
        if (jnode.TryGetValue("children", out jchildren))
        {
            int childCount = (jchildren as JArray).Count;
            for (int i = 0; i < childCount; ++i)
            {
                int child = (int) jchildren[i];
                LoadGLTFNode(gltf, cache, child, obj);
            }
        }
    }

    static void LoadGLTF(string path)
    {
        string json = File.ReadAllText(path);
        JObject gltf = JObject.Parse(json);
        gltf.Add("path", path);

        var fileInfo = new FileInfo(path);
        string gltfName = fileInfo.Name;
        gltfName = gltfName.Substring(0, gltfName.Length - fileInfo.Extension.Length);

        string dir = string.Format("Assets/glTF/{0}", gltfName);
        if (Directory.Exists(dir) == false)
        {
            Directory.CreateDirectory(dir);
        }

        Cache cache = new Cache();

        // load buffers
        int bufferCount = (gltf["buffers"] as JArray).Count;
        for (int i = 0; i < bufferCount; ++i)
        {
            cache.buffers.Add(LoadGLTFBuffer(gltf, cache, i));
        }

        // load meshes
        int meshCount = (gltf["meshes"] as JArray).Count;
        for (int i = 0; i < meshCount; ++i)
        {
            MeshPrimitives mp = LoadGLTFMesh(gltf, cache, i);
            cache.meshes.Add(mp);

            AssetDatabase.CreateAsset(mp.mesh, string.Format("Assets/glTF/{0}/{1}.asset", gltfName, mp.mesh.name));
        }

        // load textures
        int textureCount = (gltf["textures"] as JArray).Count;
        for (int i = 0; i < textureCount; ++i)
        {
            Texture2D texture = LoadGLTFTexture(gltf, cache, i, dir);
            cache.textures.Add(texture);
        }

        // load materials
        int materialCount = (gltf["materials"] as JArray).Count;
        for (int i = 0; i < materialCount; ++i)
        {
            Material material = LoadGLTFMaterial(gltf, cache, i);
            cache.materials.Add(material);

            AssetDatabase.CreateAsset(material, string.Format("Assets/glTF/{0}/{1}.mat", gltfName, material.name));
        }

        // load nodes
        var root = new GameObject(gltfName);

        int scene = GetPropertyInt(gltf, "scene", 0);
        var jnodes = gltf["scenes"][scene]["nodes"] as JArray;
        for (int i = 0; i < jnodes.Count; ++i)
        {
            int node = (int) jnodes[i];
            LoadGLTFNode(gltf, cache, node, root);
        }

        PrefabUtility.CreatePrefab(string.Format("Assets/glTF/{0}/{1}.prefab", gltfName, gltfName), root, ReplacePrefabOptions.ConnectToPrefab);
    }
}
