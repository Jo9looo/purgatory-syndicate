using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Floating triangle above the currently controlled ally, pointing down at them.
    /// Billboards toward the camera and bobs gently so it reads as "selected".
    /// </summary>
    public class SelectionMarker : MonoBehaviour
    {
        private static readonly Color MarkerColor = new Color(1f, 0.85f, 0.2f);

        private Transform followTarget;
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            BuildTriangleMesh();
        }

        private void BuildTriangleMesh()
        {
            Mesh mesh = new Mesh { name = "SelectionTriangle" };

            // Downward-pointing triangle, ~0.7 wide, 0.5 tall.
            mesh.vertices = new[]
            {
                new Vector3(-0.35f, 0.5f, 0f),
                new Vector3(0.35f, 0.5f, 0f),
                new Vector3(0f, 0f, 0f)
            };

            // Both windings so it is visible from any side.
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"))
            {
                color = MarkerColor
            };
            meshRenderer.enabled = false;
        }

        /// <summary>Follow this entity (null = hide the marker).</summary>
        public void Follow(BattleEntity entity)
        {
            followTarget = entity != null ? entity.transform : null;
            meshRenderer.enabled = followTarget != null;
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            float bob = Mathf.Sin(Time.time * 4f) * 0.12f;
            transform.position = followTarget.position + Vector3.up * (2.15f + bob);

            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }
    }
}
