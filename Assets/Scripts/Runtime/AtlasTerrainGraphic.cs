using UnityEngine;
using UnityEngine.UI;

namespace Pyatnashki
{
    /// <summary>Procedural top-down atlas: one decorative mesh, no input or imported texture.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class AtlasTerrainGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float w = rectTransform.rect.width, h = rectTransform.rect.height;
            Vector2 offset = new Vector2(-w * rectTransform.pivot.x, -h * rectTransform.pivot.y);
            System.Action<Vector2, Vector2, Color> ellipse = (p, size, tint) => Ellipse(mesh, offset + p, size, tint);
            Quad(mesh, offset, offset + new Vector2(w, 0), offset + new Vector2(w, h), offset + new Vector2(0, h), new Color(0.12f, 0.42f, 0.48f));
            ellipse(new Vector2(w * 0.47f, h * 0.52f), new Vector2(w * 0.54f, h * 0.57f), new Color(0.32f, 0.64f, 0.56f));
            ellipse(new Vector2(w * 0.45f, h * 0.52f), new Vector2(w * 0.50f, h * 0.54f), new Color(0.72f, 0.72f, 0.40f));
            ellipse(new Vector2(w * 0.48f, h * 0.49f), new Vector2(w * 0.45f, h * 0.48f), new Color(0.52f, 0.65f, 0.29f));
            ellipse(new Vector2(w * 0.28f, h * 0.73f), new Vector2(w * 0.30f, h * 0.24f), new Color(0.77f, 0.61f, 0.33f));
            ellipse(new Vector2(w * 0.49f, h * 0.91f), new Vector2(w * 0.50f, h * 0.18f), new Color(0.73f, 0.82f, 0.80f));
            ellipse(new Vector2(w * 0.35f, h * 0.24f), new Vector2(w * 0.34f, h * 0.25f), new Color(0.22f, 0.44f, 0.25f));
            // Sinuous river through the plains, from the snowy north to the eastern coast.
            Vector2 previous = offset + new Vector2(w * 0.53f, h * 0.86f);
            for (int i = 1; i <= 44; i++)
            {
                float t = i / 44f;
                Vector2 next = offset + new Vector2(w * (0.53f + 0.11f * Mathf.Sin(t * 9) + t * 0.20f), h * (0.86f - t * 0.64f));
                Stroke(mesh, previous, next, 15, new Color(0.66f, 0.75f, 0.55f));
                Stroke(mesh, previous, next, 10, new Color(0.14f, 0.48f, 0.53f)); previous = next;
            }
            for (int i = 0; i < 45; i++)
            {
                float x = w * (0.12f + (i * 17 % 37) / 100f);
                float y = h * (0.08f + (i * 11 % 34) / 100f);
                Vector2 p = offset + new Vector2(x, y);
                Ellipse(mesh, p + new Vector2(3, -3), new Vector2(10, 5), new Color(0.13f, 0.29f, 0.19f));
                Triangle(mesh, p + new Vector2(-10, -2), p + new Vector2(10, -2), p + new Vector2(0, 23),
                    i % 2 == 0 ? new Color(0.15f, 0.35f, 0.21f) : new Color(0.24f, 0.47f, 0.25f));
            }
            for (int i = 0; i < 20; i++)
            {
                Vector2 p = offset + new Vector2(w * (0.09f + (i * 13 % 34) / 100f), h * (0.57f + (i * 7 % 28) / 100f));
                Triangle(mesh, p + new Vector2(-22, -13), p + new Vector2(22, -13), p + new Vector2(0, 35), new Color(0.39f, 0.40f, 0.33f));
                Triangle(mesh, p + new Vector2(0, -13), p + new Vector2(22, -13), p + new Vector2(0, 35), new Color(0.60f, 0.57f, 0.43f));
                Triangle(mesh, p + new Vector2(-6, 23), p + new Vector2(6, 23), p + new Vector2(0, 35), new Color(0.84f, 0.86f, 0.75f));
            }
            for (int i = 0; i < 12; i++)
                ellipse(new Vector2(w * (0.76f + (i % 3) * 0.05f), h * (0.08f + i * 0.06f)),
                    new Vector2(9, 5), new Color(0.70f, 0.69f, 0.39f));
        }

        private static void Ellipse(VertexHelper mesh, Vector2 centre, Vector2 radius, Color tint)
        {
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28, b = (i + 1) * Mathf.PI * 2 / 28;
                Triangle(mesh, centre, centre + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                    centre + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), tint);
            }
        }
        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 direction = (b - a).normalized, normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }
        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero); mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }
        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            Triangle(mesh, a, b, c, tint); Triangle(mesh, a, c, d, tint);
        }
    }
}
