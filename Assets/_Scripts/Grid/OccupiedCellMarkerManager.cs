using System.Collections.Generic;
using UnityEngine;

public class OccupiedCellMarkerManager : MonoBehaviour
{
    public static OccupiedCellMarkerManager Instance { get; private set; }

    [Header("Prefab del cubo rojo (con OccupiedCellMarkerAnim)")]
    [SerializeField] private GameObject occupiedCellMarkerPrefab;

    // 1 marker por celda ocupada (persistente)
    private readonly Dictionary<CellKey, GameObject> markersByKey = new();
    private readonly HashSet<CellKey> usedThisFrame = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================
    // MODO GLOBAL (mundo X/Z)
    // =========================
    public void UpdateMarkersAroundWorld(Vector3 centerWorldPos, int range, LayerMask placementMask)
    {
        if (occupiedCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;

        usedThisFrame.Clear();

        int cx = Mathf.RoundToInt(centerWorldPos.x);
        int cz = Mathf.RoundToInt(centerWorldPos.z);

        for (int x = cx - range; x <= cx + range; x++)
        {
            for (int z = cz - range; z <= cz + range; z++)
            {
                var key = new CellKey(0, x, z);

                if (!GridOccupancyManager.Instance.IsOccupied(key))
                    continue;

                GameObject marker = GetOrCreateMarker(key);

                // Proyectar sobre la montaña (vertical)
                Vector3 rayOrigin = new Vector3(x, centerWorldPos.y + 20f, z);
                Vector3 targetPos;
                Quaternion targetRot;

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, placementMask))
                {
                    targetPos = hit.point; // sin offset, lo hace el anim
                    targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                }
                else
                {
                    targetPos = new Vector3(x, centerWorldPos.y, z);
                    targetRot = Quaternion.identity;
                }

                ApplyPoseAndShow(marker, targetPos, targetRot);
                usedThisFrame.Add(key);
            }
        }

        HideUnusedThisFrame();
    }

    // =========================
    // MODO SEGMENTO (i/j local)
    // =========================
    public void UpdateMarkersAroundSegment(SegmentGridSettings seg, Vector3 worldPoint, int range, LayerMask placementMask)
    {
        if (occupiedCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;
        if (seg == null) return;

        usedThisFrame.Clear();

        // Centro en celdas del segmento
        if (!seg.TryWorldToCell(worldPoint, out int ci, out int cj))
        {
            HideUnusedThisFrame();
            return;
        }

        seg.EnsureMask();
        seg.GetPlaneBasis(out var U, out var V, out var N);

        float cs = Mathf.Max(0.01f, seg.cellSize);
        Vector3 origin = seg.OriginWorld;
        int segId = seg.GetInstanceID();

        for (int i = ci - range; i <= ci + range; i++)
        {
            for (int j = cj - range; j <= cj + range; j++)
            {
                if (!seg.InBounds(i, j))
                    continue;

                var key = new CellKey(segId, i, j);

                if (!GridOccupancyManager.Instance.IsOccupied(key))
                    continue;

                GameObject marker = GetOrCreateMarker(key);

                // Centro de celda en el plano del segmento
                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                // Proyectar a la geometría real (a lo largo de N del segmento)
                Vector3 rayOrigin = planeCenter + N * 5f;
                Vector3 targetPos;
                Quaternion targetRot;

                if (Physics.Raycast(rayOrigin, -N, out RaycastHit hit, 30f, placementMask))
                {
                    targetPos = hit.point; // sin offset, lo hace el anim

                    // Rotación: up = normal real, yaw = eje V del segmento
                    Vector3 up = hit.normal;
                    Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                    if (fwd.sqrMagnitude < 0.0001f)
                        fwd = Vector3.ProjectOnPlane(U, up).normalized;

                    targetRot = Quaternion.LookRotation(fwd, up);
                }
                else
                {
                    // fallback
                    targetPos = planeCenter;
                    targetRot = Quaternion.LookRotation(V, N);
                }

                ApplyPoseAndShow(marker, targetPos, targetRot);
                usedThisFrame.Add(key);
            }
        }

        HideUnusedThisFrame();
    }

    // =========================
    // Helpers
    // =========================
    private GameObject GetOrCreateMarker(CellKey key)
    {
        if (!markersByKey.TryGetValue(key, out GameObject marker) || marker == null)
        {
            marker = Instantiate(occupiedCellMarkerPrefab);
            marker.name = $"OccupiedMarker_{key.segmentId}_{key.x}_{key.y}";
            markersByKey[key] = marker;
        }
        return marker;
    }

    private void ApplyPoseAndShow(GameObject marker, Vector3 pos, Quaternion rot)
    {
        var anim = marker.GetComponent<OccupiedCellMarkerAnim>();
        if (anim != null)
        {
            anim.SetTargetPose(pos, rot);
            anim.Show();
        }
        else
        {
            marker.transform.position = pos;
            marker.transform.rotation = rot;
            marker.SetActive(true);
        }
    }

    private void HideUnusedThisFrame()
    {
        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;
            if (usedThisFrame.Contains(kvp.Key)) continue;

            var anim = kvp.Value.GetComponent<OccupiedCellMarkerAnim>();
            if (anim != null) anim.Hide();
            else kvp.Value.SetActive(false);
        }
    }

    public void HideAll()
    {
        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;

            var anim = kvp.Value.GetComponent<OccupiedCellMarkerAnim>();
            if (anim != null) anim.Hide();
            else kvp.Value.SetActive(false);
        }
    }
}