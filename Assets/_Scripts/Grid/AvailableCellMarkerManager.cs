using System.Collections;
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

    [Header("Detección")]
    [Tooltip("Máscara para detectar colliders de segmentos cercanos (si lo dejas en 0 usa placementMask).")]
    [SerializeField] private LayerMask segmentDetectMask = 0;

    private readonly Dictionary<CellKey, GameObject> markersByKey = new();
    private readonly HashSet<CellKey> usedThisFrame = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ✅ destruye todo (llámalo al salir del drag / cancelar)
    public void HideAll()
    {
        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;

            var anim = kvp.Value.GetComponent<OccupiedCellMarkerAnim>();
            if (anim != null)
            {
                // que baje con animación y luego se desactive
                anim.Hide();
            }
            else
            {
                kvp.Value.SetActive(false);
            }
        }

        // Si quieres liberar memoria igualmente, destruye después de un pequeño delay
        StartCoroutine(DestroyAllAfter(0.2f));
    }

    private IEnumerator DestroyAllAfter(float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (var kvp in markersByKey)
        {
            if (kvp.Value == null) continue;
            Destroy(kvp.Value);
        }

        markersByKey.Clear();
        usedThisFrame.Clear();
    }

    /// <summary>
    /// Mundo + segmentos por proximidad. Llamar cada frame durante el drag.
    /// </summary>
    public void UpdateAvailableAround(
        Vector3 centerWorldPos,
        int range,
        float segmentSearchRadius,
        LayerMask placementMask,
        PlacementMaskData maskData = null)
    {
        if (availableCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;

        usedThisFrame.Clear();

        // 1) Mundo alrededor (cuadrado como siempre)
        AddAvailableWorld_NoClear(centerWorldPos, range, placementMask, maskData);

        // 2) Segmentos cercanos (aunque el ratón no esté encima)
        int detectMask = (segmentDetectMask.value != 0) ? segmentDetectMask.value : placementMask.value;
        Collider[] cols = Physics.OverlapSphere(centerWorldPos, segmentSearchRadius, detectMask);

        if (cols != null && cols.Length > 0)
        {
            var seen = new HashSet<SegmentGridSettings>();
            for (int k = 0; k < cols.Length; k++)
            {
                var col = cols[k];
                if (col == null) continue;

                var seg = col.GetComponentInParent<SegmentGridSettings>();
                if (seg == null) continue;
                if (!seen.Add(seg)) continue;

                AddAvailableSegment_ByProximity_NoClear(seg, centerWorldPos, range, placementMask);
            }
        }

        HideUnusedThisFrame();
    }

    // =========================
    // SEGMENTO: por proximidad real
    // =========================
    private void AddAvailableSegment_ByProximity_NoClear(
    SegmentGridSettings seg,
    Vector3 centerWorldPos,
    int range,
    LayerMask placementMask)
    {
        seg.EnsureMask();
        seg.GetPlaneBasis(out var U, out var V, out var N);

        float cs = Mathf.Max(0.01f, seg.cellSize);
        Vector3 origin = seg.OriginWorld;
        int segId = seg.GetInstanceID();

        Vector3 rel = centerWorldPos - origin;
        float u = Vector3.Dot(rel, U) / cs;
        float v = Vector3.Dot(rel, V) / cs;

        int iMin = Mathf.Clamp(Mathf.FloorToInt(u - range), 0, seg.gridDims.x - 1);
        int iMax = Mathf.Clamp(Mathf.FloorToInt(u + range), 0, seg.gridDims.x - 1);
        int jMin = Mathf.Clamp(Mathf.FloorToInt(v - range), 0, seg.gridDims.y - 1);
        int jMax = Mathf.Clamp(Mathf.FloorToInt(v + range), 0, seg.gridDims.y - 1);

        float radiusWorld = (range + 0.25f) * cs;
        float radiusSqr = radiusWorld * radiusWorld;

        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                if (seg.IsBlocked(i, j)) continue;

                var key = new CellKey(segId, i, j);
                if (GridOccupancyManager.Instance.IsOccupied(key))
                    continue;

                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                if ((planeCenter - centerWorldPos).sqrMagnitude > radiusSqr)
                    continue;

                // 🔥 RAYCAST CORRECTO
                Vector3 rayOrigin = planeCenter + N * 4.0f;

                if (!Physics.Raycast(rayOrigin, -N, out RaycastHit hit, 7.0f, placementMask))
                    continue;

                // 🔥 SOLO SU SEGMENTO
                var hitSeg = hit.collider.GetComponentInParent<SegmentGridSettings>();
                if (hitSeg != seg)
                    continue;

                // 🔥 OPCIONAL: evitar caras raras
                if (Vector3.Dot(hit.normal, N) < 0.5f)
                    continue;

                Vector3 up = hit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(U, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);
                Vector3 pos = hit.point + up * surfaceOffset;

                var marker = GetOrCreate(key);
                ApplyPoseAndShow(marker, pos, rot);
                usedThisFrame.Add(key);
            }
        }
    }

    // =========================
    // MUNDO
    // =========================
    private void AddAvailableWorld_NoClear(Vector3 centerWorldPos, int range, LayerMask placementMask, PlacementMaskData maskData)
    {
        int cx = Mathf.RoundToInt(centerWorldPos.x);
        int cz = Mathf.RoundToInt(centerWorldPos.z);

        for (int x = cx - range; x <= cx + range; x++)
        {
            for (int z = cz - range; z <= cz + range; z++)
            {
                var worldCell = new Vector2Int(x, z);

                if (maskData != null && maskData.IsBlocked(worldCell))
                    continue;

                Vector3 probe = new Vector3(x, centerWorldPos.y + 25f, z);
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 80f, placementMask))
                    continue;

                // no pintar mundo encima de segmentos (ellos lo pintan)
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
                ApplyPoseAndShow(marker, pos, rot);
                usedThisFrame.Add(key);
            }
        }
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

    public void UpdateAvailableAroundSegment(
    SegmentGridSettings seg,
    int centerX,
    int centerY,
    int range)
    {
        if (availableCellMarkerPrefab == null) return;
        if (GridOccupancyManager.Instance == null) return;
        if (seg == null) return;

        usedThisFrame.Clear();

        seg.EnsureMask();
        seg.GetPlaneBasis(out var U, out var V, out var N);

        float cs = Mathf.Max(0.01f, seg.cellSize);
        Vector3 origin = seg.OriginWorld;
        int segId = seg.GetInstanceID();

        for (int i = centerX - range; i <= centerX + range; i++)
        {
            for (int j = centerY - range; j <= centerY + range; j++)
            {
                if (!seg.InBounds(i, j)) continue;
                if (seg.IsBlocked(i, j)) continue;

                var key = new CellKey(segId, i, j);

                if (GridOccupancyManager.Instance.IsOccupied(key))
                    continue;

                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;

                Vector3 rayOrigin = planeCenter + N * 5f;
                if (!Physics.Raycast(rayOrigin, -N, out RaycastHit hit, 30f))
                    continue;

                Vector3 up = hit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(V, up).normalized;
                if (fwd.sqrMagnitude < 0.0001f)
                    fwd = Vector3.ProjectOnPlane(U, up).normalized;

                Quaternion rot = Quaternion.LookRotation(fwd, up);
                Vector3 pos = hit.point + up * surfaceOffset;

                var marker = GetOrCreate(key);
                ApplyPoseAndShow(marker, pos, rot);
                usedThisFrame.Add(key);
            }
        }

        HideUnusedThisFrame();
    }

    private void ApplyPoseAndShow(GameObject go, Vector3 pos, Quaternion rot)
    {
        var anim = go.GetComponent<OccupiedCellMarkerAnim>();
        if (anim != null)
        {
            anim.SetTargetPose(pos, rot);
            go.transform.localScale = markerScale;
            anim.Show();
            return;
        }

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

            var anim = kvp.Value.GetComponent<OccupiedCellMarkerAnim>();
            if (anim != null) anim.Hide();
            else kvp.Value.SetActive(false);
        }
    }
}