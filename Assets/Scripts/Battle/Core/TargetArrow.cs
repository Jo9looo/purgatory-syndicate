using UnityEngine;

namespace PurgatorySyndicate.Battle
{
    /// <summary>
    /// Curved targeting arrow rendered with a LineRenderer: rises above the source
    /// character and arcs down to the target (or the cursor while dragging).
    /// Width tapers toward the tip so the direction reads clearly.
    /// </summary>
    public class TargetArrow : MonoBehaviour
    {
        private const int Segments = 18;

        private LineRenderer line;

        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = Segments;
            line.startWidth = 0.18f;
            line.endWidth = 0.03f;
            line.numCapVertices = 4;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.enabled = false;
        }

        /// <summary>Draws the arc from a source head to an end point (enemy or cursor).</summary>
        public void Show(Vector3 from, Vector3 to, Color color)
        {
            line.enabled = true;
            line.startColor = new Color(color.r, color.g, color.b, 0.55f);
            line.endColor = color;

            // Quadratic bezier: apex floats above the midpoint, higher for longer arrows.
            float distance = Vector3.Distance(from, to);
            Vector3 apex = (from + to) * 0.5f + Vector3.up * (0.9f + distance * 0.18f);

            for (int i = 0; i < Segments; i++)
            {
                float t = i / (float)(Segments - 1);
                float inverse = 1f - t;
                Vector3 point =
                    inverse * inverse * from +
                    2f * inverse * t * apex +
                    t * t * to;
                line.SetPosition(i, point);
            }
        }

        public void Hide()
        {
            if (line != null)
            {
                line.enabled = false;
            }
        }
    }
}
