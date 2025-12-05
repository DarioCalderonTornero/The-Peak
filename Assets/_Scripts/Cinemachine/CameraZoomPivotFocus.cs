using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

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

    [Header("Reset de cámara")]
    [SerializeField] private float resetDuration = 0.3f;
    [SerializeField]
    private AnimationCurve resetCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 targetPivotPos;
    private Vector3 focalPoint;
    private bool hasFocalPoint = false;
    private Vector2 lastFocusMousePos;

    private float currentZoom = 1f;
    private bool canZoom = false;

    private Vector3 initialPivotPos;
    private float initialZoom;
    private float initialHorizontal;
    private float initialVertical;
    private bool initialStateCaptured = false;

    private Coroutine resetRoutine;

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
        {
            currentZoom = orbitalFollow.RadialAxis.Value;
            // Valores actuales de órbita (los usaremos como base)
            initialHorizontal = orbitalFollow.HorizontalAxis.Value;
            initialVertical = orbitalFollow.VerticalAxis.Value;
        }
    }

    private IEnumerator Start()
    {
        canZoom = false;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnResetCameraInput += InputManager_OnResetCameraInput;
        }

        yield return new WaitUntil(() => CardGameManager.Instance != null);
        CardGameManager.Instance.OnInventoryHide += CardGameManager_OnInventoryHide;
    }

    private void CardGameManager_OnInventoryHide(object sender, System.EventArgs e)
    {
        canZoom = true;

        if (pivotTransform != null)
            initialPivotPos = pivotTransform.position;

        if (orbitalFollow != null)
        {
            initialZoom = orbitalFollow.RadialAxis.Value;
            initialHorizontal = orbitalFollow.HorizontalAxis.Value;
            initialVertical = orbitalFollow.VerticalAxis.Value;
        }

        initialStateCaptured = true;

        focalPoint = initialPivotPos;
        targetPivotPos = initialPivotPos;
        hasFocalPoint = true;
    }

    private void InputManager_OnResetCameraInput(object sender, System.EventArgs e)
    {
        if (!canZoom || !initialStateCaptured || pivotTransform == null || orbitalFollow == null)
            return;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(ResetCameraRoutine());
    }

    private IEnumerator ResetCameraRoutine()
    {
        Vector3 startPivot = pivotTransform.position;
        float startZoom = currentZoom;

        float startHorizontal = orbitalFollow.HorizontalAxis.Value;
        float startVertical = orbitalFollow.VerticalAxis.Value;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / resetDuration;
            float easedT = resetCurve != null ? resetCurve.Evaluate(t) : t;

            Vector3 newPivot = Vector3.Lerp(startPivot, initialPivotPos, easedT);
            pivotTransform.position = newPivot;
            targetPivotPos = newPivot;

            float newZoom = Mathf.Lerp(startZoom, initialZoom, easedT);
            currentZoom = newZoom;
            orbitalFollow.RadialAxis.Value = currentZoom;

            float newHorizontal = Mathf.LerpAngle(startHorizontal, initialHorizontal, easedT);
            float newVertical = Mathf.LerpAngle(startVertical, initialVertical, easedT);

            orbitalFollow.HorizontalAxis.Value = newHorizontal;
            orbitalFollow.VerticalAxis.Value = newVertical;

            yield return null;
        }

        // Snap final por si acaso
        pivotTransform.position = initialPivotPos;
        targetPivotPos = initialPivotPos;

        currentZoom = initialZoom;
        orbitalFollow.RadialAxis.Value = currentZoom;

        orbitalFollow.HorizontalAxis.Value = initialHorizontal;
        orbitalFollow.VerticalAxis.Value = initialVertical;

        focalPoint = initialPivotPos;
        hasFocalPoint = true;

        resetRoutine = null;
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null || orbitalFollow == null || !canZoom)
            return;

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
        if (CardGameManager.Instance != null)
            CardGameManager.Instance.OnInventoryHide -= CardGameManager_OnInventoryHide;

        if (InputManager.Instance != null)
            InputManager.Instance.OnResetCameraInput -= InputManager_OnResetCameraInput;
    }
}
