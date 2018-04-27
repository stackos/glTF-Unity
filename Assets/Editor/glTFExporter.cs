using UnityEditor;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public class glTFExporter : EditorWindow
{
    GameObject root;
    string gltfPath;
    Cache cache;

    class Cache
    {
        public List<Mesh> meshes = new List<Mesh>();
        public List<Material> materials = new List<Material>();
        public List<glTFAccessor> accessors = new List<glTFAccessor>();
        public List<byte> buffer = new List<byte>();
    }

    class glTFBufferView
    {
        public int buffer = 0;
        public int byteOffset;
        public int byteLength;
        public int byteStride;
        public int target;

        public const int GL_ARRAY_BUFFER = 34962;
        public const int GL_ELEMENT_ARRAY_BUFFER = 34963;
    }

    class glTFAccessor
    {
        public glTFBufferView bufferViewObject = new glTFBufferView();
        public int bufferView;
        public int byteOffset = 0;
        public int componentType;
        public string type; // SCALAR VEC2 VEC3 VEC4 MAT4
        public int count;
        // min
        // max

        public const int GL_UNSIGNED_SHORT = 5123;
        public const int GL_FLOAT = 5126;
    }

    [MenuItem("glTF/Exporter")]
    public static void ShowWindow()
    {
        EditorWindow win = EditorWindow.GetWindow(typeof(glTFExporter));
        win.minSize = new Vector2(800, 600);
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Select GameObject", GUILayout.Width(115));
        root = (GameObject) EditorGUILayout.ObjectField(root, typeof(GameObject), true);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("glTF path", GUILayout.Width(115));
        GUILayout.TextField(gltfPath);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Export", GUILayout.Width(80)))
        {
            if (root)
            {
                string dir = null;
                if (File.Exists(gltfPath))
                {
                    dir = new FileInfo(gltfPath).Directory.FullName;
                }

                gltfPath = EditorUtility.SaveFilePanel("Select gltf path to save", dir, root.name, "gltf");

                if (!string.IsNullOrEmpty(gltfPath))
                {
                    ExportGLTF();
                }
            }
        }
    }

    void ExportGLTF()
    {
        JObject gltf = new JObject();

        JObject asset = new JObject();
        gltf["asset"] = asset;
        asset["version"] = "2.0";
        asset["generator"] = "glTF-Unity";

        JArray scenes = new JArray();
        gltf["scenes"] = scenes;
        gltf["scene"] = 0;

        JObject scene = new JObject();
        scenes.Add(scene);

        JArray nodes = new JArray();
        scene["nodes"] = nodes;
        nodes.Add(0);

        gltf["nodes"] = new JArray();

        cache = new Cache();
        ExportGLTFNode(gltf["nodes"] as JArray, root);
        ExportMeshes(gltf);
        ExportAccessors(gltf);
        ExportMaterials(gltf);
        cache = null;

        File.WriteAllText(gltfPath, gltf.ToString());

        EditorUtility.RevealInFinder(gltfPath);
    }

    int ExportGLTFNode(JArray nodes, GameObject obj)
    {
        Transform transform = obj.transform;
        JObject node = new JObject();
        int nodeIndex = nodes.Count;
        nodes.Add(node);

        node["name"] = obj.name;

        JArray translation = new JArray();
        node["translation"] = translation;
        translation.Insert(0, transform.localPosition.x);
        translation.Insert(1, transform.localPosition.y);
        translation.Insert(2, transform.localPosition.z);

        JArray rotation = new JArray();
        node["rotation"] = rotation;
        rotation.Insert(0, transform.localRotation.x);
        rotation.Insert(1, transform.localRotation.y);
        rotation.Insert(2, transform.localRotation.z);
        rotation.Insert(3, transform.localRotation.w);

        JArray scale = new JArray();
        node["scale"] = scale;
        scale.Insert(0, transform.localScale.x);
        scale.Insert(1, transform.localScale.y);
        scale.Insert(2, transform.localScale.z);

        JObject extras = new JObject();
        node["extras"] = extras;

        JArray components = new JArray();
        extras["components"] = components;

        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

        if (meshRenderer && meshFilter)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh)
            {
                int index = cache.meshes.IndexOf(mesh);
                if (index < 0)
                {
                    index = cache.meshes.Count;
                    cache.meshes.Add(mesh);
                }
                node["mesh"] = index;
            }

            JObject component = new JObject();
            components.Add(component);

            component["typeName"] = "MeshRenderer";
            JArray materials = new JArray();
            component["materials"] = materials;

            var mats = meshRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; ++i)
            {
                int index = -1;
                Material mat = mats[i];
                if (mat)
                {
                    index = cache.materials.IndexOf(mat);
                    if (index < 0)
                    {
                        index = cache.materials.Count;
                        cache.materials.Add(mat);
                    }
                }
                materials.Add(index);
            }
        }

        int childCount = transform.childCount;
        if (childCount > 0)
        {
            JArray children = new JArray();
            node["children"] = children;

            for (int i = 0; i < childCount; ++i)
            {
                Transform child = transform.GetChild(i);
                int index = ExportGLTFNode(nodes, child.gameObject);
                children.Add(index);
            }
        }

        return nodeIndex;
    }

    void pushBufferVector2(Vector2[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write(vecs[i].x);
            bw.Write(vecs[i].y);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferVector3(Vector3[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write(vecs[i].x);
            bw.Write(vecs[i].y);
            bw.Write(vecs[i].z);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferVector4(Vector4[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write(vecs[i].x);
            bw.Write(vecs[i].y);
            bw.Write(vecs[i].z);
            bw.Write(vecs[i].w);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferColor(Color[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write(vecs[i].r);
            bw.Write(vecs[i].g);
            bw.Write(vecs[i].b);
            bw.Write(vecs[i].a);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferJoints(BoneWeight[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write((ushort) vecs[i].boneIndex0);
            bw.Write((ushort) vecs[i].boneIndex1);
            bw.Write((ushort) vecs[i].boneIndex2);
            bw.Write((ushort) vecs[i].boneIndex3);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferWeights(BoneWeight[] vecs, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < vecs.Length; ++i)
        {
            bw.Write(vecs[i].weight0);
            bw.Write(vecs[i].weight1);
            bw.Write(vecs[i].weight2);
            bw.Write(vecs[i].weight3);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    void pushBufferIndices(int[] indices, out int offset, out int size)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        for (int i = 0; i < indices.Length; ++i)
        {
            bw.Write((ushort) indices[i]);
        }

        offset = cache.buffer.Count;
        size = (int) ms.Length;
        cache.buffer.AddRange(ms.ToArray());
    }

    int pushBuffer(byte[] data)
    {
        int offset = cache.buffer.Count;
        cache.buffer.AddRange(data);
        return offset;
    }

    void ExportMeshes(JObject gltf)
    {
        JArray meshes = new JArray();
        gltf["meshes"] = meshes;

        for (int i = 0; i < cache.meshes.Count; ++i)
        {
            Mesh mesh = cache.meshes[i];

            JObject jmesh = new JObject();
            meshes.Add(jmesh);

            jmesh["name"] = mesh.name;

            JArray primitives = new JArray();
            jmesh["primitives"] = primitives;

            for (int j = 0; j < mesh.subMeshCount; ++j)
            {
                JObject primitive = new JObject();
                primitives.Add(primitive);

                JObject attributes = new JObject();
                primitive["attributes"] = attributes;
                
                if (j == 0)
                {
                    int offset;
                    int size;

                    var vertices = mesh.vertices;
                    var normals = mesh.normals;
                    var tangents = mesh.tangents;
                    var uv = mesh.uv;
                    var uv2 = mesh.uv2;
                    var colors = mesh.colors;
                    var boneWeights = mesh.boneWeights;

                    if (vertices.Length > 0)
                    {
                        pushBufferVector3(mesh.vertices, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / vertices.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC3";
                        accessor.count = vertices.Length;

                        attributes["POSITION"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (normals.Length > 0)
                    {
                        pushBufferVector3(mesh.normals, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / normals.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC3";
                        accessor.count = normals.Length;

                        attributes["NORMAL"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (tangents.Length > 0)
                    {
                        pushBufferVector4(mesh.tangents, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / tangents.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC4";
                        accessor.count = tangents.Length;

                        attributes["TANGENT"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (uv.Length > 0)
                    {
                        pushBufferVector2(mesh.uv, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / uv.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC2";
                        accessor.count = uv.Length;

                        attributes["TEXCOORD_0"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (uv2.Length > 0)
                    {
                        pushBufferVector2(mesh.uv2, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / uv2.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC2";
                        accessor.count = uv2.Length;

                        attributes["TEXCOORD_1"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (colors.Length > 0)
                    {
                        pushBufferColor(mesh.colors, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / colors.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC4";
                        accessor.count = colors.Length;

                        attributes["COLOR_0"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (boneWeights.Length > 0)
                    {
                        pushBufferJoints(mesh.boneWeights, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / boneWeights.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_UNSIGNED_SHORT;
                        accessor.type = "VEC4";
                        accessor.count = boneWeights.Length;

                        attributes["JOINTS_0"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }

                    if (boneWeights.Length > 0)
                    {
                        pushBufferWeights(mesh.boneWeights, out offset, out size);

                        glTFAccessor accessor = new glTFAccessor();
                        accessor.bufferViewObject.byteOffset = offset;
                        accessor.bufferViewObject.byteLength = size;
                        accessor.bufferViewObject.byteStride = size / boneWeights.Length;
                        accessor.bufferViewObject.target = glTFBufferView.GL_ARRAY_BUFFER;
                        accessor.bufferView = cache.accessors.Count;
                        accessor.componentType = glTFAccessor.GL_FLOAT;
                        accessor.type = "VEC4";
                        accessor.count = boneWeights.Length;

                        attributes["WEIGHTS_0"] = cache.accessors.Count;
                        cache.accessors.Add(accessor);
                    }
                }

                var indices = mesh.GetTriangles(j);
                if (indices.Length > 0)
                {
                    int offset;
                    int size;

                    pushBufferIndices(indices, out offset, out size);

                    glTFAccessor accessor = new glTFAccessor();
                    accessor.bufferViewObject.byteOffset = offset;
                    accessor.bufferViewObject.byteLength = size;
                    accessor.bufferViewObject.byteStride = size / indices.Length;
                    accessor.bufferViewObject.target = glTFBufferView.GL_ELEMENT_ARRAY_BUFFER;
                    accessor.bufferView = cache.accessors.Count;
                    accessor.componentType = glTFAccessor.GL_UNSIGNED_SHORT;
                    accessor.type = "SCALAR";
                    accessor.count = indices.Length;

                    primitive["indices"] = cache.accessors.Count;
                    cache.accessors.Add(accessor);
                }

                primitive["mode"] = 4;
            }
        }
    }

    void ExportAccessors(JObject gltf)
    {
        JArray accessors = new JArray();
        gltf["accessors"] = accessors;

        for (int i = 0; i < cache.accessors.Count; ++i)
        {
            glTFAccessor a = cache.accessors[i];
            JObject jaccessor = new JObject();
            accessors.Add(jaccessor);

            jaccessor["bufferView"] = a.bufferView;
            jaccessor["byteOffset"] = a.byteOffset;
            jaccessor["componentType"] = a.componentType;
            jaccessor["type"] = a.type;
            jaccessor["count"] = a.count;
        }

        JArray bufferViews = new JArray();
        gltf["bufferViews"] = bufferViews;

        for (int i = 0; i < cache.accessors.Count; ++i)
        {
            glTFBufferView v = cache.accessors[i].bufferViewObject;
            JObject jview = new JObject();
            bufferViews.Add(jview);

            jview["buffer"] = v.buffer;
            jview["byteOffset"] = v.byteOffset;
            jview["byteLength"] = v.byteLength;
            jview["byteStride"] = v.byteStride;
            jview["target"] = v.target;
        }

        JArray buffers = new JArray();
        gltf["buffers"] = buffers;

        JObject jbuffer = new JObject();
        buffers.Add(jbuffer);

        string uri = root.name + ".bin";

        jbuffer["byteLength"] = cache.buffer.Count;
        jbuffer["uri"] = uri;
        
        File.WriteAllBytes(new FileInfo(gltfPath).Directory.FullName + "/" + uri, cache.buffer.ToArray());
    }

    void ExportMaterials(JObject gltf)
    {
        JArray materials = new JArray();
        gltf["materials"] = materials;

        for (int i = 0; i < cache.materials.Count; ++i)
        {
            Material material = cache.materials[i];
            JObject jmaterial = new JObject();
            materials.Add(jmaterial);

            jmaterial["name"] = material.name;

            JObject extras = new JObject();
            jmaterial["extras"] = extras;

            extras["shader"] = material.shader.name;
        }
    }
}
