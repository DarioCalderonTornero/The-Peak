using UnityEngine;

//[RequireComponent(typeof(RockDefense))]
public class BlockFaceOnPlacement : MonoBehaviour
{
    /*
    private int faceId = -1;

    private void Start()
    {
        var path = FindFirstObjectByType<MountainPathfinder>();
        if (path == null || path.scanner == null || path.scanner.triangles == null || path.scanner.triangles.Count == 0)
        {
            Debug.LogError("[BlockFaceOnPlacement] Pathfinder/escaneo no listo.");
            return;
        }

        // Cara más cercana al punto de colocación
        faceId = path.GetClosestFaceId(transform.position);
        if (faceId >= 0) ObstacleNavBlocker.Instance?.BlockFace(faceId);
        else Debug.LogWarning("[BlockFaceOnPlacement] No se encontró cara válida para bloquear.");
    }

    private void OnDestroy()
    {
        if (faceId >= 0) ObstacleNavBlocker.Instance?.UnblockFace(faceId);
    }
    */
}
