using System.Collections.Generic;
using UnityEngine;

public class GridOccupancyManager : MonoBehaviour
{
    public static GridOccupancyManager Instance { get; private set; }

    // Casillas ocupadas
    private readonly HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool IsOccupied(Vector2Int cell) => occupied.Contains(cell);

    public bool AnyOccupied(IEnumerable<Vector2Int> cells)
    {
        foreach (var c in cells)
            if (occupied.Contains(c)) return true;
        return false;
    }

    public void Register(IEnumerable<Vector2Int> cells)
    {
        foreach (var c in cells)
            occupied.Add(c);
    }

    public void Unregister(IEnumerable<Vector2Int> cells)
    {
        foreach (var c in cells)
            occupied.Remove(c);
    }
}