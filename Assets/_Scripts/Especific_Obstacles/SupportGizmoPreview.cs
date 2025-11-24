using UnityEngine;

public class SupportGizmoPreview : MonoBehaviour
{
    // Referencia al CardData que manda
    public CardData debugCardData;

    // Estos se rellenan desde debugCardData
    public Vector2 extents = new Vector2(0.5f, 0.5f);
    public float rayDistance = 0.5f;

    private void OnDrawGizmos()
    {
        if (debugCardData != null)
        {
            extents = debugCardData.supportCheckExtents;
            rayDistance = debugCardData.supportRayDistance;
        }

        float yOff = debugCardData != null ? debugCardData.supportYOffset : 0.1f;

        Vector3 center = transform.position + Vector3.up * yOff;
        Quaternion rotation = transform.rotation;

        Vector2 ext = extents;

        Vector3[] localOffsets =
        {
        Vector3.zero,
        new Vector3( ext.x, 0f,  ext.y),
        new Vector3(-ext.x, 0f,  ext.y),
        new Vector3( ext.x, 0f, -ext.y),
        new Vector3(-ext.x, 0f, -ext.y),
    };

        // 1) dibujar cuadrado
        Vector3[] worldCorners = new Vector3[4];
        worldCorners[0] = center + rotation * new Vector3(ext.x, 0f, ext.y);
        worldCorners[1] = center + rotation * new Vector3(-ext.x, 0f, ext.y);
        worldCorners[2] = center + rotation * new Vector3(-ext.x, 0f, -ext.y);
        worldCorners[3] = center + rotation * new Vector3(ext.x, 0f, -ext.y);

        Gizmos.color = Color.red;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(worldCorners[i], worldCorners[(i + 1) % 4]);
        }

        // 2) rayos hacia abajo
        Gizmos.color = Color.yellow;
        foreach (var local in localOffsets)
        {
            Vector3 worldPoint = center + rotation * local;
            Vector3 start = worldPoint;
            Vector3 end = worldPoint + Vector3.down * rayDistance;
            Gizmos.DrawLine(start, end);
        }
    }
}
