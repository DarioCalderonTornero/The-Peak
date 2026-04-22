using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIGradient : BaseMeshEffect
{
    [SerializeField] private Color leftColor = new Color(0.49f, 0.78f, 0.64f); // #7EC8A4
    [SerializeField] private Color rightColor = new Color(0.18f, 0.62f, 0.42f); // #2D9E6B

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        int count = vh.currentVertCount;
        if (count == 0) return;

        // Encontrar xMin y xMax
        UIVertex vertex = default;
        float xMin = float.MaxValue;
        float xMax = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            if (vertex.position.x < xMin) xMin = vertex.position.x;
            if (vertex.position.x > xMax) xMax = vertex.position.x;
        }

        float width = xMax - xMin;
        if (width <= 0f) return;

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float t = (vertex.position.x - xMin) / width;
            vertex.color = Color.Lerp(leftColor, rightColor, t);
            vh.SetUIVertex(vertex, i);
        }
    }
}