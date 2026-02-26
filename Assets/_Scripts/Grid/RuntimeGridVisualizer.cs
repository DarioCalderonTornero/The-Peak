using System.Collections.Generic;
using UnityEngine;

public class RuntimeGridVisualizer : MonoBehaviour
{
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private float yOffset = 0.1f;

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material lineMat;

    public void Setup(Vector2Int gridSize)
    {
        Clear();

        // Material que ignore profundidad (como estabas usando)
        lineMat = new Material(Shader.Find("Sprites/Default"));

        float halfW = gridSize.x / 2f;
        float halfH = gridSize.y / 2f;

        // ----- BORDE EXTERIOR (4 lados) -----
        AddLine(new Vector3(-halfW, yOffset, -halfH), new Vector3(halfW, yOffset, -halfH)); // abajo
        AddLine(new Vector3(halfW, yOffset, -halfH), new Vector3(halfW, yOffset, halfH)); // derecha
        AddLine(new Vector3(halfW, yOffset, halfH), new Vector3(-halfW, yOffset, halfH)); // arriba
        AddLine(new Vector3(-halfW, yOffset, halfH), new Vector3(-halfW, yOffset, -halfH)); // izquierda

        // ----- LÍNEAS INTERNAS -----
        // Verticales (separan columnas): i = 1..gridSize.x-1
        for (int i = 1; i < gridSize.x; i++)
        {
            float x = -halfW + i;
            AddLine(
                new Vector3(x, yOffset, -halfH),
                new Vector3(x, yOffset, halfH)
            );
        }

        // Horizontales (separan filas): j = 1..gridSize.y-1
        for (int j = 1; j < gridSize.y; j++)
        {
            float z = -halfH + j;
            AddLine(
                new Vector3(-halfW, yOffset, z),
                new Vector3(halfW, yOffset, z)
            );
        }
    }

    public void SetColor(Color color)
    {
        color.a = 0.8f;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == null) continue;
            lines[i].startColor = color;
            lines[i].endColor = color;
        }
    }

    private void AddLine(Vector3 a, Vector3 b)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = lineMat;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        lines.Add(lr);
    }

    private void Clear()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != null) Destroy(lines[i].gameObject);
        }
        lines.Clear();
    }

    private void OnDestroy()
    {
        // Por si acaso, evita materiales “colgados”
        if (lineMat != null) Destroy(lineMat);
    }
}