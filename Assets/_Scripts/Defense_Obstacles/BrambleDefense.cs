using UnityEngine;

public class BrambleDefense : BaseDefense
{
    [Header("Segmentos")]
    public GameObject segmentPrefab;
    public int segments = 6;
    public float segmentLength = 1f;
    public float raycastHeight = 5f;
    public LayerMask terrainMask;

    [Header("Ralentización")]
    public float slowAmount = 0.5f;

    [Header("Variaciones de apariencia")]
    public Vector2 scaleVariation = new Vector2(0.9f, 1.1f); // min-max
    public float yRotationVariation = 10f; // grados
    public float zRotationVariation = 5f;  // grados

    public override void Initialize()
    {
        base.Initialize();
        CreateBrambleSegments();
    }

    private void CreateBrambleSegments()
    {
        Vector3 startPos = transform.position;

        for (int i = 0; i < segments; i++)
        {
            // Posición aproximada del segmento
            Vector3 segPos = startPos + transform.right * i * segmentLength;

            // Raycast desde arriba
            if (Physics.Raycast(segPos + Vector3.up * raycastHeight, Vector3.down, out RaycastHit hit, 20f, terrainMask))
            {
                GameObject seg = Instantiate(segmentPrefab, hit.point, Quaternion.identity, transform);

                // Alineamos con la normal del terreno
                seg.transform.up = hit.normal;

                // Variaciones para que no se vean iguales
                float randomScale = Random.Range(scaleVariation.x, scaleVariation.y);
                seg.transform.localScale = new Vector3(randomScale, 1f, randomScale);

                float randomY = Random.Range(-yRotationVariation, yRotationVariation);
                float randomZ = Random.Range(-zRotationVariation, zRotationVariation);
                seg.transform.Rotate(0, randomY, randomZ, Space.Self);

                // Ajuste de posición
                seg.transform.position = hit.point;

                // Añadimos trigger para ralentización
                BoxCollider trigger = seg.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(segmentLength, 1f, 1f);

                var slow = seg.AddComponent<SlowZone>();
                // slow.slowAmount = slowAmount;
            }
        }
    }
}

public class SlowZone : MonoBehaviour
{
    /* public float slowAmount = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        var enemy = other.GetComponent<EnemyMovement>();
        if (enemy != null)
            enemy.ModifySpeed(slowAmount);
    }

    private void OnTriggerExit(Collider other)
    {
        var enemy = other.GetComponent<EnemyMovement>();
        if (enemy != null)
            enemy.ModifySpeed(1f); // restaurar velocidad original
    }*/
}
