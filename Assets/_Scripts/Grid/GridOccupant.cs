using System.Collections.Generic;
using UnityEngine;

public class GridOccupant : MonoBehaviour
{
    private List<Vector2Int> cells;

    public void Init(List<Vector2Int> occupiedCells)
    {
        cells = occupiedCells;
        GridOccupancyManager.Instance.Register(cells);
    }

    private void OnDestroy()
    {
        if (GridOccupancyManager.Instance != null && cells != null)
            GridOccupancyManager.Instance.Unregister(cells);
    }
}