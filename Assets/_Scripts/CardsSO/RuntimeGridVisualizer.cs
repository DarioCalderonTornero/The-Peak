using UnityEngine;

public class RuntimeGridVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    public void Setup(Vector2Int gridSize)
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;

        // Usamos un shader que ignore la profundidad para que no se "entierre" en la rampa
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        lineRenderer.loop = true;
        lineRenderer.positionCount = 4;
        lineRenderer.useWorldSpace = false;

        float halfW = gridSize.x / 2f;
        float halfH = gridSize.y / 2f;

        // Subimos el offset a 0.1f para evitar parpadeos con el suelo
        float yOffset = 0.1f;

        lineRenderer.SetPosition(0, new Vector3(-halfW, yOffset, -halfH));
        lineRenderer.SetPosition(1, new Vector3(halfW, yOffset, -halfH));
        lineRenderer.SetPosition(2, new Vector3(halfW, yOffset, halfH));
        lineRenderer.SetPosition(3, new Vector3(-halfW, yOffset, halfH));
    }

    public void SetColor(Color color)
    {
        if (lineRenderer != null)
        {
            // Aplicamos un poco de transparencia para que quede mejor
            color.a = 0.8f;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }
}