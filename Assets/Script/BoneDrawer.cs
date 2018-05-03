using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
[ExecuteInEditMode]
public class BoneDrawer : MonoBehaviour
{
    public Transform[] bones;

    void Start()
    {
        var skin = GetComponent<SkinnedMeshRenderer>();

        if (bones == null || bones.Length == 0)
        {
            bones = skin.bones;

            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.SetColor("_Color", Color.green);

            for (int i = 0; i < bones.Length; ++i)
            {
                var sp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sp.transform.parent = bones[i];
                sp.transform.localPosition = Vector3.zero;
                sp.transform.localRotation = Quaternion.identity;
                sp.transform.localScale = Vector3.one * 0.02f;

                var r = sp.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
            }

            gameObject.SetActive(false);
        }
    }
}
