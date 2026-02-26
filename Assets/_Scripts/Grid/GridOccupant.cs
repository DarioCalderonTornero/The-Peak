using System.Collections.Generic;
using UnityEngine;

public class GridOccupant : MonoBehaviour
{
    [SerializeField] private List<CellKey> keys;

    public IReadOnlyList<CellKey> Keys => keys;

    // ✅ API nueva (segmento o mundo)
    public void Init(List<CellKey> occupiedKeys)
    {
        keys = occupiedKeys;

        if (GridOccupancyManager.Instance != null)
            GridOccupancyManager.Instance.Register(keys);
    }

    // ✅ Compatibilidad: si alguien todavía pasa Vector2Int (mundo)
    public void Init(List<Vector2Int> worldCells)
    {
        keys = new List<CellKey>(worldCells.Count);
        for (int i = 0; i < worldCells.Count; i++)
            keys.Add(new CellKey(0, worldCells[i].x, worldCells[i].y));

        if (GridOccupancyManager.Instance != null)
            GridOccupancyManager.Instance.Register(keys);
    }

    private void OnDestroy()
    {
        if (GridOccupancyManager.Instance != null && keys != null)
            GridOccupancyManager.Instance.Unregister(keys);
    }
}