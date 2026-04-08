using UnityEngine;

public class RollingLogDefense : BaseDefense
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float probeForwardDistance = 0.9f;
    [SerializeField] private float minSlopeAngle = 5f;

    [Header("Rotación")]
    [SerializeField] private float logRadius = 0.5f;
    [SerializeField] private float rotationSpeedMultiplier = 1f;

    [Header("Detección")]
    [SerializeField] private LayerMask placementMask;

    private SegmentGridSettings currentSegment;
    private Vector3 currentDownhillDir;
    private bool initializedFromPlacement = false;
    private bool hasValidSurface = false;

    public void InitializeFromPlacement(SegmentGridSettings seg, Vector2Int cell)
    {
        currentSegment = seg;
        initializedFromPlacement = true;

        if (currentSegment != null)
        {
            currentSegment.GetPlaneBasis(out _, out _, out Vector3 n);
            currentDownhillDir = Vector3.ProjectOnPlane(Vector3.down, n).normalized;
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        if (!initializedFromPlacement)
        {
            if (!TryRefreshSurfaceData())
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            if (!TryRefreshSurfaceData())
            {
                Destroy(gameObject);
                return;
            }
        }

        if (!hasValidSurface)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        if (!hasValidSurface)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 previousPos = transform.position;

        // Movimiento continuo, no por centros de celda
        Vector3 nextPos = transform.position + currentDownhillDir * moveSpeed * dt;
        transform.position = nextPos;

        // Recolocar suavemente sobre la superficie real
        if (!SnapToGroundAndRefreshSegment())
        {
            Destroy(gameObject);
            return;
        }

        // Si hemos llegado a plano, destruir
        if (!hasValidSurface)
        {
            Destroy(gameObject);
            return;
        }

        // Rotación real del tronco
        Vector3 delta = transform.position - previousPos;
        float distance = delta.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 moveDir = delta.normalized;
            Vector3 groundNormal = GetGroundNormal();

            Vector3 rotationAxis = Vector3.Cross(groundNormal, moveDir);
            float axisMag = rotationAxis.magnitude;

            if (axisMag > 0.0001f)
            {
                rotationAxis /= axisMag;

                float safeRadius = Mathf.Max(0.01f, logRadius);
                float angle = (distance / safeRadius) * Mathf.Rad2Deg * rotationSpeedMultiplier;

                transform.Rotate(rotationAxis, angle, Space.World);
            }
        }
    }

    private bool TryRefreshSurfaceData()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 10f, placementMask))
            return false;

        SegmentGridSettings seg = hit.collider.GetComponentInParent<SegmentGridSettings>();
        if (seg == null)
            return false;

        currentSegment = seg;

        Vector3 normal = hit.normal;
        float slopeAngle = Vector3.Angle(Vector3.up, normal);

        if (slopeAngle < minSlopeAngle)
        {
            hasValidSurface = false;
            return true;
        }

        currentDownhillDir = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
        hasValidSurface = currentDownhillDir.sqrMagnitude > 0.0001f;

        return true;
    }

    private bool SnapToGroundAndRefreshSegment()
    {
        // Raycast recto debajo para pegarlo al suelo
        Ray downRay = new Ray(transform.position + Vector3.up * 2f, Vector3.down);
        if (!Physics.Raycast(downRay, out RaycastHit downHit, 10f, placementMask))
        {
            // Si justo está entre segmentos, buscar un poco hacia delante
            return TryFindForwardSurface();
        }

        transform.position = downHit.point;

        SegmentGridSettings seg = downHit.collider.GetComponentInParent<SegmentGridSettings>();
        if (seg == null)
            return false;

        currentSegment = seg;

        Vector3 normal = downHit.normal;
        float slopeAngle = Vector3.Angle(Vector3.up, normal);

        if (slopeAngle < minSlopeAngle)
        {
            hasValidSurface = false;
            return true;
        }

        currentDownhillDir = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
        hasValidSurface = currentDownhillDir.sqrMagnitude > 0.0001f;

        return true;
    }

    private bool TryFindForwardSurface()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 downhill = currentDownhillDir.sqrMagnitude > 0.0001f ? currentDownhillDir : Vector3.forward;

        const int steps = 4;
        float stepDist = Mathf.Max(0.25f, probeForwardDistance * 0.5f);

        for (int i = 1; i <= steps; i++)
        {
            Vector3 probe = origin + downhill * (i * stepDist);
            Ray ray = new Ray(probe, Vector3.down);

            if (!Physics.Raycast(ray, out RaycastHit hit, 10f, placementMask))
                continue;

            SegmentGridSettings seg = hit.collider.GetComponentInParent<SegmentGridSettings>();
            if (seg == null)
                continue;

            transform.position = hit.point;
            currentSegment = seg;

            Vector3 normal = hit.normal;
            float slopeAngle = Vector3.Angle(Vector3.up, normal);

            if (slopeAngle < minSlopeAngle)
            {
                hasValidSurface = false;
                return true;
            }

            currentDownhillDir = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            hasValidSurface = currentDownhillDir.sqrMagnitude > 0.0001f;

            return true;
        }

        return false;
    }

    private Vector3 GetGroundNormal()
    {
        Ray ray = new Ray(transform.position + Vector3.up * 2f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 10f, placementMask))
            return hit.normal;

        return Vector3.up;
    }
}