using UnityEngine;

public class EdgeObstacleMarker : MonoBehaviour
{
    [Header("Radio lógico de bloqueo")]
    [Tooltip("Radio en unidades del mundo alrededor del que el grafo buscará aristas cercanas. Hazlo algo mayor que el tamaño visual de la roca.")]
    public float obstacleRadius = 3f;

    [Header("Debug")]
    public bool debugDrawGizmo = true;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.25f);
    public bool debugLog = false;

    private BaseObstacle obstacle;
    public BaseObstacle Obstacle => obstacle;

    private void Awake()
    {
        obstacle = GetComponent<BaseObstacle>();
        if (obstacle == null)
        {
            Debug.LogWarning("[EdgeObstacleMarker] No se ha encontrado BaseObstacle en el mismo GameObject. El grafo no podrá leer el tipo de obstáculo.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawGizmo)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, obstacleRadius);
    }
}
