using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EnCare
{
    [DisallowMultipleComponent]
    public sealed class TrashGlow : MonoBehaviour
    {
        [Tooltip("Material using EnCare/TrashOutlineURP. Assign it to prevent shader stripping.")]
        [SerializeField] Material outlineMaterial;
        [Tooltip("Assign only the static model's MeshRenderers, not basket or collider helpers.")]
        [SerializeField] MeshRenderer[] sourceRenderers = new MeshRenderer[0];
        [ColorUsage(false, true)] [SerializeField] Color glowColor = new Color(0.1f, 1f, 0.8f, 1f);
        [Min(0)] [SerializeField] float intensity = 3f;
        [Range(0.001f, 0.03f)] [SerializeField] float width = 0.006f;
        [Tooltip("Smooth outline seams on hard-edged meshes. Enable Read/Write in the mesh import settings.")]
        [SerializeField] bool smoothOutlineNormals = true;
        readonly List<GameObject> shells = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Material runtimeMaterial;

        void Start()
        {
            if (outlineMaterial == null)
            {
                Debug.LogWarning("EnCare: assign an outline material on TrashGlow.", this);
                return;
            }
            runtimeMaterial = new Material(outlineMaterial);
            ApplyColor();
            foreach (MeshRenderer source in sourceRenderers)
            {
                if (source == null) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                if (smoothOutlineNormals && mesh.isReadable)
                {
                    mesh = Instantiate(mesh);
                    SmoothNormals(mesh);
                    meshes.Add(mesh);
                }
                else if (smoothOutlineNormals)
                    Debug.LogWarning("EnCare: enable Read/Write on this mesh to smooth outline seams.", source);
                var shell = new GameObject("EnCare Outline");
                shell.layer = source.gameObject.layer;
                shell.transform.SetParent(source.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = runtimeMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Bounds bounds = mesh.bounds;
                bounds.Expand(width * 4f);
                renderer.localBounds = bounds;
                shells.Add(shell);
            }
        }
        static void SmoothNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals.Length != vertices.Length) return;
            var sums = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                sums.TryGetValue(vertices[i], out Vector3 sum);
                sums[vertices[i]] = sum + normals[i];
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = sums[vertices[i]].normalized;
            mesh.normals = normals;
        }
        public void SetColor(Color color) { glowColor = color; ApplyColor(); }
        void ApplyColor()
        {
            if (runtimeMaterial == null) return;
            runtimeMaterial.SetColor("_OutlineColor", glowColor * intensity);
            runtimeMaterial.SetFloat("_OutlineWidth", width);
        }
        void OnValidate() { ApplyColor(); }
        void OnEnable() { foreach (GameObject shell in shells) if (shell != null) shell.SetActive(true); }
        void OnDisable() { foreach (GameObject shell in shells) if (shell != null) shell.SetActive(false); }
        void OnDestroy()
        {
            foreach (GameObject shell in shells) if (shell != null) Destroy(shell);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }
    }
}
