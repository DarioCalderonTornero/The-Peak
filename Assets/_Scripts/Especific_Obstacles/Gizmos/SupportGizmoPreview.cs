using UnityEngine;

public class SupportGizmoPreview : MonoBehaviour
{
    public CardData debugCardData;

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

        // “Arriba” del objeto = normal de la superficie
        Vector3 surfaceNormal = transform.up;

        // Centro de la huella levantado en la normal
        Vector3 center = transform.position + surfaceNormal * yOff;
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

        // 1) Dibujar cuadrado en el plano tangente a la montaña
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

        // 2) Rayos “hacia la montaña” (–normal)
        Gizmos.color = Color.yellow;
        foreach (var local in localOffsets)
        {
            Vector3 worldPoint = center + rotation * local;
            Vector3 start = worldPoint;
            Vector3 end = worldPoint - surfaceNormal * rayDistance;
            Gizmos.DrawLine(start, end);
        }
    }
}
