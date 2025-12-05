using UnityEngine;
using Unity.Cinemachine;

public class CameraZoomPivotFocus : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform pivotTransform;         
    [SerializeField] private Camera mainCamera;             
    [SerializeField] private CinemachineCamera cinemachineCam; 
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;

    [Header("Raycast")]
    [SerializeField] private LayerMask rayLayerMask;           
    [SerializeField] private float maxRayDistance = 500f;

    [Header("Movimiento del pivote")]
    [SerializeField] private float pivotMoveSpeed = 20f;
    [SerializeField] private bool smoothMovement = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.25f;   
    [SerializeField] private float minZoom = 0.5f;     
    [SerializeField] private float maxZoom = 2.0f;     

    [Header("Controles")]
    [SerializeField] private bool requireRightMouseButton = true; 
    [SerializeField] private bool onlyOnZoomIn = true;           

    [Header("Cambio de foco por movimiento de ratón")]
    [SerializeField] private float mouseMoveThresholdPixels = 30f;

    private Vector3 targetPivotPos;
    private Vector3 focalPoint;
    private bool hasFocalPoint = false;
    private Vector2 lastFocusMousePos;

    private float currentZoom = 1f;

    private bool canZoom = false;

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
        {
            targetPivotPos = pivotTransform.position;
            focalPoint = pivotTransform.position;
            hasFocalPoint = true;
            lastFocusMousePos = Input.mousePosition;
        }

        if (orbitalFollow != null)
            currentZoom = orbitalFollow.RadialAxis.Value;
    }

    private void Start()
    {
        canZoom = false;
        CardGameManager.Instance.OnInventoryHide += CardGameManager_OnInventoryHide;
    }

    private void CardGameManager_OnInventoryHide(object sender, System.EventArgs e)
    {
        canZoom = true;
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null || orbitalFollow == null || !canZoom)
            return;

        // if (requireRightMouseButton && !Input.GetMouseButton(1))
        //     return;

        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector2 currentMousePos = Input.mousePosition;

        bool shouldRecalculateFocus =
            !hasFocalPoint ||
            Vector2.Distance(currentMousePos, lastFocusMousePos) > mouseMoveThresholdPixels;

        if (onlyOnZoomIn && scroll < 0f)
        {
            ApplyZoom(scroll);
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

            focalPoint = hit.point;
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

    private void OnDestroy()
    {
        CardGameManager.Instance.OnInventoryHide -= CardGameManager_OnInventoryHide;
    }
}
