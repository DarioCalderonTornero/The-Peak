using UnityEngine;

public class PlacementOverlapGizmo : MonoBehaviour
{
    public CardData debugCardData;

    public Vector3 extents = new Vector3(0.5f, 0.5f, 0.5f);

    private void OnDrawGizmos()
    {
        if (debugCardData != null)
        {
            extents = debugCardData.placementCheckExtents;
        }

        Gizmos.color = Color.cyan;

        // Dibujamos un cubo wireframe en el espacio local del preview
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // extents son half extents → tamaño completo = extents * 2
        Gizmos.DrawWireCube(Vector3.up * 0.1f, extents * 2f);

        Gizmos.matrix = old;
    }
}
