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

    static T[] AccessBuffer<T>(JObject gltf, Cache cache, int bufferViewIndex, int index, int getCount, int getSize)
    {
        var array = new T[getCount];

    //    if (accessor.bufferView != null) {
    //  let bufferView = gltf.bufferViews[accessor.bufferView]
    //  if (bufferView.buffer != null) {
    //    let stride = getCount * getSize
    //    if (bufferView.byteStride != null) {
    //      stride = bufferView.byteStride
    //    }
    //    let offset = 0
    //    if (bufferView.byteOffset != null) {
    //      offset += bufferView.byteOffset
    //    }
    //    if (accessor.byteOffset != null) {
    //      offset += accessor.byteOffset
    //    }
    //    let data = cache.buffers[bufferView.buffer]
    //    let result = new Array(getCount)
    //    for (let i = 0; i < getCount; ++i) {
    //       result[i] = data[getFunc](offset + stride * index + i * getSize, true)
    //    }
    //    return result
    //  }
    //}
    //return null

        var bytes = new byte[getCount * getSize];
        for (int i = 0; i < bytes.Length; ++i)
        {
            bytes[i] = 0;
        }

        System.Buffer.BlockCopy(bytes, 0, array, 0, getCount);

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
                    attrs.Add(AccessBuffer<T>(gltf, cache, bufferViewIndex, i, getCount, getSize));
                }
            }
        }
    }

    static Mesh LoadGLTFMesh(JObject gltf, Cache cache, JObject jmesh)
    {
        List<float[]> vertexValues = new List<float[]>();

        var jprimitives = jmesh["primitives"] as JArray;
        int primitiveCount = jprimitives.Count;
        for (int i = 0; i < primitiveCount; ++i)
        {
            var jprimitive = jprimitives[i] as JObject;

            var jattributes = jprimitive["attributes"] as JObject;
            AccessGLTFBuffer(gltf, cache, jattributes, "POSITION", vertexValues, 3, 4);

            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.POSITION, 'VEC3', gl.FLOAT, vertices, 'getFloat32', 3, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.NORMAL, 'VEC3', gl.FLOAT, normals, 'getFloat32', 3, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.TANGENT, 'VEC4', gl.FLOAT, tangents, 'getFloat32', 4, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.TEXCOORD_0, 'VEC2', gl.FLOAT, uv, 'getFloat32', 2, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.TEXCOORD_1, 'VEC2', gl.FLOAT, uv2, 'getFloat32', 2, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.COLOR_0, 'VEC4', gl.FLOAT, colors, 'getFloat32', 4, 4)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.JOINTS_0, 'VEC4', gl.SHORT, boneIndices, 'getInt16', 4, 2)
            //Resources.AccessGLTFBuffer(gltf, cache, p.attributes.WEIGHTS_0, 'VEC4', gl.FLOAT, boneWeights, 'getFloat32', 4, 4)

            int indicesAccessor = GetPropertyInt(jprimitive, "indices", -1);
            if (indicesAccessor >= 0)
            {

            }

            int materialIndex = GetPropertyInt(jprimitive, "material", -1);
            if (materialIndex >= 0)
            {

            }
        }

        Vector3[] vertices = new Vector3[vertexValues.Count];

        for (int i = 0; i < vertexValues.Count; ++i)
        {
            vertices[i] = new Vector3(vertexValues[i][0], vertexValues[i][1], vertexValues[i][2]);
        }

        var mesh = new Mesh();
        mesh.vertices = vertices;

        return mesh;
    }

    static List<Mesh> LoadGLTFMeshes(string path)
    {
        List<Mesh> meshes = new List<Mesh>();

        string json = File.ReadAllText(path);
        JObject gltf = JObject.Parse(json);

        Cache cache = new Cache();
        cache.buffers = new List<byte[]>();
        int bufferCount = (gltf["buffers"] as JArray).Count;
        for (int i = 0; i < bufferCount; ++i)
        {
            cache.buffers.Add(null);
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
