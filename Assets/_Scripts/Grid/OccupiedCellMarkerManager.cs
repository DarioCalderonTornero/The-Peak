using System.Collections.Generic;
using UnityEngine;

public class OccupiedCellMarkerManager : MonoBehaviour
{
    public static OccupiedCellMarkerManager Instance { get; private set; }

    [Header("Prefab del cubo rojo (con OccupiedCellMarkerAnim)")]
    [SerializeField] private GameObject occupiedCellMarkerPrefab;

    // 1 marker por celda ocupada (persistente)
    private readonly Dictionary<Vector2Int, GameObject> markersByCell = new();
    private readonly HashSet<Vector2Int> usedThisFrame = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // opcional si cambias de escena:
        // DontDestroyOnLoad(gameObject);
    }

    public void UpdateMarkersAround(Vector3 centerWorldPos, int range, LayerMask placementMask)
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
                Vector2Int cell = new Vector2Int(x, z);

                if (!GridOccupancyManager.Instance.IsOccupied(cell))
                    continue;

                if (!markersByCell.TryGetValue(cell, out GameObject marker) || marker == null)
                {
                    marker = Instantiate(occupiedCellMarkerPrefab);
                    marker.name = $"OccupiedMarker_{cell.x}_{cell.y}";
                    markersByCell[cell] = marker;
                }

                // Proyectar sobre la montaña
                Vector3 rayOrigin = new Vector3(x, centerWorldPos.y + 20f, z);
                Vector3 targetPos;
                Quaternion targetRot;

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 60f, placementMask))
                {
                    targetPos = hit.point + hit.normal * 0.03f;
                    targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                }
                else
                {
                    targetPos = new Vector3(x, centerWorldPos.y, z);
                    targetRot = Quaternion.identity;
                }

                // ✅ pose siempre (aunque esté oculto)
                var anim = marker.GetComponent<OccupiedCellMarkerAnim>();
                if (anim != null) anim.SetTargetPose(targetPos, targetRot);
                else
                {
                    marker.transform.position = targetPos;
                    marker.transform.rotation = targetRot;
                }

                // ✅ mostrar con anim SOLO si aún no estaba visible
                if (anim != null) anim.Show();
                else marker.SetActive(true);

                usedThisFrame.Add(cell);
            }
        }

        // Ocultar los que ya no están en rango (sin destruir)
        foreach (var kvp in markersByCell)
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
        foreach (var kvp in markersByCell)
        {
            if (kvp.Value == null) continue;

            var anim = kvp.Value.GetComponent<OccupiedCellMarkerAnim>();
            if (anim != null) anim.Hide();
            else kvp.Value.SetActive(false);
        }
    }
}