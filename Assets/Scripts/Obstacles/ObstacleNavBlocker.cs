using System;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleNavBlocker : MonoBehaviour
{
    public static ObstacleNavBlocker Instance { get; private set; }
    private readonly HashSet<int> blockedFaces = new();
    private readonly Dictionary<int, int> refCounts = new(); // por si varias defensas bloquean la misma cara

    public event Action OnBlockedFacesChanged;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool IsBlocked(int faceId) => blockedFaces.Contains(faceId);
    public IReadOnlyCollection<int> GetBlockedFaces() => blockedFaces;

    public void BlockFace(int faceId)
    {
        if (faceId < 0) return;
        if (!refCounts.TryGetValue(faceId, out int c)) c = 0;
        refCounts[faceId] = c + 1;
        if (c == 0) blockedFaces.Add(faceId);
        OnBlockedFacesChanged?.Invoke();
    }

    public void UnblockFace(int faceId)
    {
        if (faceId < 0) return;
        if (!refCounts.TryGetValue(faceId, out int c)) return;
        c -= 1;
        if (c <= 0)
        {
            refCounts.Remove(faceId);
            blockedFaces.Remove(faceId);
        }
        else refCounts[faceId] = c;
        OnBlockedFacesChanged?.Invoke();
    }
}
