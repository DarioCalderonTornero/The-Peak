using System.Collections.Generic;
using UnityEngine;

public class AvailableCellMarkerManager : MonoBehaviour
{
    public static AvailableCellMarkerManager Instance { get; private set; }

    [Header("Prefab cubo transparente (sin collider)")]
    [SerializeField] private GameObject availableCellMarkerPrefab;

    [Header("Visual")]
    [SerializeField] private Vector3 markerScale = new Vector3(0.95f, 0.05f, 0.95f);
    [SerializeField] private float surfaceOffset = 0.03f;

    // pool + “mapping” por celda visible
    private readonly Dictionary<CellKey, GameObject> markersByKey = new();
    private readonly HashSet<CellKey> usedThisFrame = new();

    // Para cumplir tu requisito: cuando cambias de GO/segmento, se limpia lo anterior
    private int currentOwnerId = int.MinValue; // segId si segmento, 0 si mundo (o collider id si quisieras)

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void HideAll()
    {
        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;
            Destroy(kvp.Value);
        }

        markersByKey.Clear();
        usedThisFrame.Clear();
        currentOwnerId = int.MinValue;
    }

    // =========================
    // SEGMENTO (grid real i/j)
    // =========================
    public void UpdateAvailableOnSegment(SegmentGridSettings seg, Vector3 hoverWorldPoint, int range, LayerMask placementMask)
    {
        if (availableCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;
        if (seg == null) { HideAll(); return; }

        int ownerId = seg.GetInstanceID();
        if (currentOwnerId != ownerId)
        {
            // Cambiaste de segmento => oculta lo anterior
            HideAll();
            currentOwnerId = ownerId;
        }

        usedThisFrame.Clear();

        if (!seg.TryWorldToCell(hoverWorldPoint, out int ci, out int cj))
        {
            HideUnusedThisFrame();
            return;
        }

        seg.EnsureMask();
        seg.GetPlaneBasis(out var U, out var V, out var N);

        float cs = Mathf.Max(0.01f, seg.cellSize);
        Vector3 origin = seg.OriginWorld;
        int segId = ownerId;

        for (int i = ci - range; i <= ci + range; i++)
        {
            for (int j = cj - range; j <= cj + range; j++)
            {
                if (!seg.InBounds(i, j)) continue;
                if (seg.IsBlocked(i, j)) continue; // <- “quitadas” por baker/painter

                var key = new CellKey(segId, i, j);

                if (GridOccupancyManager.Instance.IsOccupied(key)) // <- ocupada por defensa
                    continue;

                // Centro de celda en plano del segmento
                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                // Proyectar a la geometría real
                Vector3 rayOrigin = planeCenter + N * 5f;
                if (!Physics.Raycast(rayOrigin, -N, out RaycastHit hit, 30f, placementMask))
                    continue;

                // Rotación: up = normal real. Forward = eje V del segmento proyectado.
                Vector3 up = hit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(U, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);
                Vector3 pos = hit.point + up * surfaceOffset;

                var marker = GetOrCreate(key);
                ApplyPose(marker, pos, rot);
                usedThisFrame.Add(key);
            }
        }

        HideUnusedThisFrame();
    }

    // =========================
    // MUNDO (grid global x/z)
    // =========================
    public void UpdateAvailableOnWorld(Vector3 centerWorldPos, int range, LayerMask placementMask, PlacementMaskData maskData = null)
    {
        if (availableCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;

        int ownerId = 0;
        if (currentOwnerId != ownerId)
        {
            HideAll();
            currentOwnerId = ownerId;
        }

        usedThisFrame.Clear();

        int cx = Mathf.RoundToInt(centerWorldPos.x);
        int cz = Mathf.RoundToInt(centerWorldPos.z);

        for (int x = cx - range; x <= cx + range; x++)
        {
            for (int z = cz - range; z <= cz + range; z++)
            {
                var worldCell = new Vector2Int(x, z);

                // Respetar máscara global si existe
                if (maskData != null && maskData.IsBlocked(worldCell))
                    continue;

                // Evitar dibujar “mundo” encima de segmentos que tienen grid local real
                Vector3 probe = new Vector3(x, centerWorldPos.y + 20f, z);
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 60f, placementMask))
                    continue;

                if (hit.collider.GetComponentInParent<SegmentGridSettings>() != null)
                    continue;

                var key = new CellKey(0, x, z);
                if (GridOccupancyManager.Instance.IsOccupied(key))
                    continue;

                Vector3 up = hit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(Vector3.right, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);
                Vector3 pos = hit.point + up * surfaceOffset;

                var marker = GetOrCreate(key);
                ApplyPose(marker, pos, rot);
                usedThisFrame.Add(key);
            }
        }

        HideUnusedThisFrame();
    }

    // ---------- helpers ----------
    private GameObject GetOrCreate(CellKey key)
    {
        if (!markersByKey.TryGetValue(key, out var go) || go == null)
        {
            go = Instantiate(availableCellMarkerPrefab);
            go.name = $"AvailCell_{key.segmentId}_{key.x}_{key.y}";
            markersByKey[key] = go;
        }
        return go;
    }

    private void ApplyPose(GameObject go, Vector3 pos, Quaternion rot)
    {
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = markerScale;
        if (!go.activeSelf) go.SetActive(true);
    }

    private void HideUnusedThisFrame()
    {
        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;
            if (usedThisFrame.Contains(kvp.Key)) continue;
            kvp.Value.SetActive(false);
        }
    }
}