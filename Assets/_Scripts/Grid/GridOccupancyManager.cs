using System.Collections.Generic;
using UnityEngine;

public class GridOccupancyManager : MonoBehaviour
{
    public static GridOccupancyManager Instance { get; private set; }

    // ✅ Ocupación unificada: mundo + segmentos
    private readonly HashSet<CellKey> occupied = new HashSet<CellKey>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================
    // API NUEVA (CellKey)
    // =========================

    public bool IsOccupied(CellKey cell) => occupied.Contains(cell);

    public bool AnyOccupied(IEnumerable<CellKey> cells)
    {
        foreach (var c in cells)
            if (occupied.Contains(c)) return true;
        return false;
    }

    public void Register(IEnumerable<CellKey> cells)
    {
        foreach (var c in cells)
            occupied.Add(c);
    }

    public void Unregister(IEnumerable<CellKey> cells)
    {
        foreach (var c in cells)
            occupied.Remove(c);
    }

    // ==========================================
    // COMPATIBILIDAD (tu sistema antiguo Vector2Int)
    // segmentId = 0 => grid mundo
    // ==========================================

    public bool IsOccupied(Vector2Int worldCell)
        => occupied.Contains(new CellKey(0, worldCell.x, worldCell.y));

    public bool AnyOccupied(IEnumerable<Vector2Int> worldCells)
    {
        foreach (var c in worldCells)
            if (occupied.Contains(new CellKey(0, c.x, c.y))) return true;
        return false;
    }

    public void Register(IEnumerable<Vector2Int> worldCells)
    {
        foreach (var c in worldCells)
            occupied.Add(new CellKey(0, c.x, c.y));
    }

    public void Unregister(IEnumerable<Vector2Int> worldCells)
    {
        foreach (var c in worldCells)
            occupied.Remove(new CellKey(0, c.x, c.y));
    }
}