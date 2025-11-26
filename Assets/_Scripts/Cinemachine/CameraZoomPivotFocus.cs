using UnityEngine;
using Unity.Cinemachine;

public class CameraZoomPivotFocus : MonoBehaviour
{
    /*
    [Header("Referencias")]
    [SerializeField] private Transform pivotTransform;          // Empty del centro de la montaña
    [SerializeField] private Camera mainCamera;                 // Main Camera (con el CinemachineBrain)
    [SerializeField] private CinemachineCamera cinemachineCam;  // CM_MainOrbit
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Raycast")]
    [SerializeField] private LayerMask rayLayerMask;            // Capas que puede enfocar la cámara
    [SerializeField] private float maxRayDistance = 500f;

    [Header("Movimiento del pivote")]
    [SerializeField] private float pivotMoveSpeed = 20f;
    [SerializeField] private bool smoothMovement = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.25f;   // Velocidad con la que cambia el RadialAxis
    [SerializeField] private float minZoom = 0.5f;      // Valor mínimo del RadialAxis
    [SerializeField] private float maxZoom = 2.0f;      // Valor máximo del RadialAxis

    [Header("Controles")]
    [SerializeField] private bool requireRightMouseButton = true; // Solo funciona con RMB
    [SerializeField] private bool onlyOnZoomIn = true;            // Solo mueve el pivote al acercar

    private Vector3 targetPivotPos;
    private Vector3 focalPoint;
    private bool hasFocalPoint = false;

    private float currentZoom = 1f;

    private void Reset()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cinemachineCam == null)
            cinemachineCam = GetComponent<CinemachineCamera>();

        if (cinemachineCam != null && orbitalFollow == null)
            orbitalFollow = cinemachineCam.GetComponent<CinemachineOrbitalFollow>();
    }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cinemachineCam == null)
            cinemachineCam = GetComponent<CinemachineCamera>();

        if (cinemachineCam != null && orbitalFollow == null)
            orbitalFollow = cinemachineCam.GetComponent<CinemachineOrbitalFollow>();

        if (pivotTransform != null)
            targetPivotPos = pivotTransform.position;

        if (orbitalFollow != null)
            currentZoom = orbitalFollow.RadialAxis.Value;   // Valor inicial del zoom
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null || orbitalFollow == null)
            return;

        // Si queremos requerir RMB, salimos si no está pulsado
        if (requireRightMouseButton && !Input.GetMouseButton(1))
        {
            hasFocalPoint = false;
            return;
        }

        float scroll = Input.mouseScrollDelta.y;

        // Si no hay scroll este frame, no hacemos nada
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        // --- ZOOM: modificamos el RadialAxis de Cinemachine ---
        // Scroll positivo = acercar (disminuir RadialAxis.Value)
        currentZoom -= scroll * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        orbitalFollow.RadialAxis.Value = currentZoom;

        // --- FOCAL POINT (pivote dinámico) ---
        // Solo actualizamos el pivote cuando nos estamos acercando
        if (onlyOnZoomIn && scroll < 0f)
        {
            // Alejar: no cambiamos el focalPoint ni el targetPivotPos
            return;
        }

        // Primer "tick" de scroll hacia dentro calculamos el punto bajo el ratón
        if (!hasFocalPoint)
        {
            hasFocalPoint = true;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, rayLayerMask))
            {
                focalPoint = hit.point;
            }
            else
            {
                // Si no golpea nada, usamos un punto arbitrario hacia delante
                focalPoint = ray.GetPoint(50f);
            }
        }

        // Movemos el pivote hacia el punto de foco (aplanando Y para evitar “paredes locas”)
        Vector3 flatFocal = new Vector3(focalPoint.x, pivotTransform.position.y, focalPoint.z);
        targetPivotPos = flatFocal;
    }

    private void LateUpdate()
    {
        if (pivotTransform == null)
            return;

        if (smoothMovement)
        {
            pivotTransform.position = Vector3.Lerp(
                pivotTransform.position,
                targetPivotPos,
                pivotMoveSpeed * Time.deltaTime
            );
        }
        else
        {
            pivotTransform.position = targetPivotPos;
        }

        // Cuando el pivote ya está prácticamente en el punto de foco,
        // podemos permitir recalcular otro focalPoint en el siguiente gesto de zoom
        if (Vector3.SqrMagnitude(pivotTransform.position - targetPivotPos) < 0.0001f)
        {
            hasFocalPoint = false;
        }
    }
    */
}
