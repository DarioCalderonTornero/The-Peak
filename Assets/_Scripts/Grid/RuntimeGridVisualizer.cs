using System.Collections.Generic;
using UnityEngine;

public class RuntimeGridVisualizer : MonoBehaviour
{
    [SerializeField] private float yOffset = 0.05f;

    private readonly List<GameObject> cells = new();
    private Material validMaterial;
    private Material invalidMaterial;
    private bool isValid = false;

    public void Setup(Vector2Int gridSize, GameObject cellPrefab, Material valid, Material invalid)
    {
        Clear();

        validMaterial = valid;
        invalidMaterial = invalid;

        float startX = -(gridSize.x / 2f) + 0.5f;
        float startZ = -(gridSize.y / 2f) + 0.5f;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                GameObject go = cellPrefab != null
                    ? Instantiate(cellPrefab)
                    : CreateDefaultCube();

                go.name = $"Cell_{x}_{z}";
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(startX + x, yOffset, startZ + z);

                // Quitar collider para no interferir con raycasts
                foreach (var col in go.GetComponentsInChildren<Collider>())
                    Destroy(col);

                cells.Add(go);
            }
        }

        // Aplicar material inicial (inválido por defecto)
        ApplyMaterial(invalidMaterial);
    }

    public void SetColor(Color color)
    {
        // Determina si es verde (válido) o rojo (inválido) por el canal G
        bool valid = color.g > color.r;
        if (valid == isValid) return;
        isValid = valid;
        ApplyMaterial(isValid ? validMaterial : invalidMaterial);
    }

    private void ApplyMaterial(Material mat)
    {
        if (mat == null) return;
        foreach (var go in cells)
        {
            if (go == null) continue;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
                mr.material = mat;
        }
    }

    private GameObject CreateDefaultCube()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(0.92f, 0.15f, 0.92f);
        Destroy(go.GetComponent<Collider>());
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    private void Clear()
    {
        foreach (var c in cells)
            if (c != null) Destroy(c);
        cells.Clear();
    }

    private void OnDestroy() => Clear();
}