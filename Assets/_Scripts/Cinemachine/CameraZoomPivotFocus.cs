using UnityEngine;
using Unity.Cinemachine;

public class CameraZoomPivotFocus : MonoBehaviour
{
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

    [Header("Cambio de foco por movimiento de ratón")]
    [SerializeField] private float mouseMoveThresholdPixels = 30f;

    [Header("Altura del foco")]
    [SerializeField] private float minNormalYForHeight = 0.3f;    // Si la superficie es más vertical que esto, ignoramos su Y
    [SerializeField] private float verticalFollowStrength = 0.8f; // 0..1 cuánto seguimos la Y del hit cuando es válida

    private Vector3 targetPivotPos;
    private Vector3 focalPoint;
    private bool hasFocalPoint = false;
    private Vector2 lastFocusMousePos;

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
            currentZoom = orbitalFollow.RadialAxis.Value;   
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null || orbitalFollow == null)
            return;

        
        //if (requireRightMouseButton && !Input.GetMouseButton(1))
            //return;

        float scroll = Input.mouseScrollDelta.y;

        // Si no hay scroll, no hacemos nada
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        // Posición actual del ratón
        Vector2 currentMousePos = Input.mousePosition;

        bool shouldRecalculateFocus =
            !hasFocalPoint ||
            Vector2.Distance(currentMousePos, lastFocusMousePos) > mouseMoveThresholdPixels;

        
        if (onlyOnZoomIn && scroll < 0f)
        {
            ApplyZoom(scroll);
            // No tocamos el foco ni el targetPivotPos aquí
            return;
        }

      
        if (scroll > 0f && shouldRecalculateFocus)
        {
            TryRecalculateFocalPoint(currentMousePos);
            return;
        }

       
        ApplyZoom(scroll);

        
        if (hasFocalPoint)
        {
            targetPivotPos = focalPoint;
        }
    }

    private void ApplyZoom(float scroll)
    {
        currentZoom -= scroll * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        orbitalFollow.RadialAxis.Value = currentZoom;
    }

    private void TryRecalculateFocalPoint(Vector2 currentMousePos)
    {
        Ray ray = mainCamera.ScreenPointToRay(currentMousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, rayLayerMask))
        {
            lastFocusMousePos = currentMousePos;
            hasFocalPoint = true;

            Vector3 point = hit.point;
            float nY = hit.normal.y;

            if (nY > minNormalYForHeight)
            {
                float newY = Mathf.Lerp(pivotTransform.position.y, point.y, verticalFollowStrength);
                point.y = newY;
            }
            else
            {
                point.y = pivotTransform.position.y;
            }

            focalPoint = point;
            targetPivotPos = focalPoint;
        }
        else
        {
            Debug.Log("No mountain on mouse");
        }
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
    }
}
