using System.Collections.Generic;
using UnityEngine;

public class PlacementMaskManager : MonoBehaviour
{
    public static PlacementMaskManager Instance { get; private set; }
    public PlacementMaskData data;

    public LayerMask paintMask = ~0; 

    private void Awake() => Instance = this;

    public bool IsBuildable(Vector2Int cell)
        => data == null || !data.IsBlocked(cell);

    public bool AreBuildable(List<Vector2Int> cells)
    {
        if (data == null) return true;
        for (int i = 0; i < cells.Count; i++)
            if (data.IsBlocked(cells[i])) return false;
        return true;
    }
}