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
                LoadGLTFMeshes(gltfPath);
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

    class Cache
    {
        public List<byte[]> buffers;
    }

    static void LoadGLTFBuffer(JObject gltf, Cache cache, int index)
    {
        JToken jbuffer = gltf["buffers"][index];

        string uri = GetPropertyString(jbuffer, "uri", null);
        string dir = new FileInfo((string) gltf["path"]).Directory.FullName;
        string binPath = dir + "/" + uri;

        if (File.Exists(binPath))
        {
            cache.buffers[index] = File.ReadAllBytes(binPath);
        }
        else
        {
            Debug.LogError("can not load ext bin file:" + binPath);
        }
    }

    static T[] AccessBuffer<T>(JObject gltf, Cache cache, JToken jaccessor, int bufferViewIndex, int index, int getCount, int getSize)
    {
        var array = new T[getCount];

        JToken jbufferView = gltf["bufferViews"][bufferViewIndex];

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
            JToken jaccessor = gltf["accessors"][accessorIndex];

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

    static Mesh LoadGLTFMesh(JObject gltf, Cache cache, JObject jmesh)
    {
        List<float[]> vertexValues = new List<float[]>();
        List<float[]> normalValues = new List<float[]>();
        List<float[]> tangentValues = new List<float[]>();
        List<float[]> uvValues = new List<float[]>();
        List<float[]> uv2Values = new List<float[]>();
        List<float[]> colorValues = new List<float[]>();
        List<short[]> blendIndexValues = new List<short[]>();
        List<float[]> blendWeightValues = new List<float[]>();
        List<int[]> indices = new List<int[]>();

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
                JToken jaccessor = gltf["accessors"][indicesAccessor];

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
            if (materialIndex >= 0)
            {

            }

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

        return mesh;
    }

    static List<Mesh> LoadGLTFMeshes(string path)
    {
        List<Mesh> meshes = new List<Mesh>();

        string json = File.ReadAllText(path);
        JObject gltf = JObject.Parse(json);
        gltf.Add("path", path);

        Cache cache = new Cache();
        cache.buffers = new List<byte[]>();
        int bufferCount = (gltf["buffers"] as JArray).Count;
        for (int i = 0; i < bufferCount; ++i)
        {
            cache.buffers.Add(null);
            LoadGLTFBuffer(gltf, cache, i);
        }

        var fileInfo = new FileInfo(path);
        string gltfName = fileInfo.Name;
        gltfName = gltfName.Substring(0, gltfName.Length - fileInfo.Extension.Length);

        string dir = string.Format("Assets/glTF/{0}", gltfName);
        if (Directory.Exists(dir) == false)
        {
            Directory.CreateDirectory(dir);
        }

        var jmeshes = gltf["meshes"] as JArray;
        int meshCount = jmeshes.Count;
        for (int i = 0; i < meshCount; ++i)
        {
            var jmesh = jmeshes[i] as JObject;

            var mesh = LoadGLTFMesh(gltf, cache, jmesh);

            string name = GetPropertyString(jmesh, "name", null);
            if (string.IsNullOrEmpty(name) == false)
            {
                name = string.Format("mesh_{0}", name);
            }
            else
            {
                name = string.Format("mesh_{0}", i);
            }

            AssetDatabase.CreateAsset(mesh, string.Format("Assets/glTF/{0}/{1}.asset", gltfName, name));
            meshes.Add(mesh);
        }

        return meshes;
    }
}
