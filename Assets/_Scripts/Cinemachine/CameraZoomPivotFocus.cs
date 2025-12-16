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
    [SerializeField] private AnimationCurve resetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Refocus
    [Header("Refocus")]
    [SerializeField] private float refocusDistanceThreshold = 0.5f;   // distancia para dar por completado el refocus
    [SerializeField] private float refocusZoomLerpSpeed = 5f;         // (ahora mismo no cambiará mucho el zoom)
    [SerializeField] private float refocusPivotSpeedMultiplier = 3f;  // pivote más rápido durante refocus

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

    // Estado de refocus
    private bool isRefocusing = false;
    private float desiredRefocusZoom;

    // Añadido para gestionar las posiciones de la cámara (izquierda, derecha, atrás)
    private enum CameraPosition { Default, Left, Right, Back }
    private CameraPosition currentCameraPosition = CameraPosition.Default;

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
            InputManager.Instance.OnResetLeftCameraInput += InputManager_OnResetLeftCameraInput;
            InputManager.Instance.OnResetRightCameraInput += InputManager_OnResetRightCameraInput;
            InputManager.Instance.OnResetBackCameraInput += InputManager_OnResetBackCameraInput;
        }

        yield return new WaitUntil(() => CardGameManager.Instance != null);
        CardGameManager.Instance.OnInventoryHide += CardGameManager_OnInventoryHide;
    }

    private void InputManager_OnResetBackCameraInput(object sender, System.EventArgs e)
    {
        if (currentCameraPosition == CameraPosition.Back)
            return; // Ya está en la posición de atrás, no hacer nada.

        if (!canZoom || !initialStateCaptured || pivotTransform == null || orbitalFollow == null)
            return;

        isRefocusing = false;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(SetCameraPosition(CameraPosition.Back)); // Fija la cámara en la posición de atrás
    }

    private void InputManager_OnResetRightCameraInput(object sender, System.EventArgs e)
    {
        if (currentCameraPosition == CameraPosition.Right)
            return; // Ya está en la posición de derecha, no hacer nada.

        if (!canZoom || !initialStateCaptured || pivotTransform == null || orbitalFollow == null)
            return;

        isRefocusing = false;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(SetCameraPosition(CameraPosition.Right)); // Fija la cámara en la posición de derecha
    }

    private void InputManager_OnResetLeftCameraInput(object sender, System.EventArgs e)
    {
        if (currentCameraPosition == CameraPosition.Left)
            return; // Ya está en la posición de izquierda, no hacer nada.

        if (!canZoom || !initialStateCaptured || pivotTransform == null || orbitalFollow == null)
            return;

        isRefocusing = false;

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);

        resetRoutine = StartCoroutine(SetCameraPosition(CameraPosition.Left)); // Fija la cámara en la posición de izquierda
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

        isRefocusing = false;

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

        pivotTransform.position = initialPivotPos;
        targetPivotPos = initialPivotPos;

        currentZoom = initialZoom;
        orbitalFollow.RadialAxis.Value = currentZoom;

        orbitalFollow.HorizontalAxis.Value = initialHorizontal;
        orbitalFollow.VerticalAxis.Value = initialVertical;

        focalPoint = initialPivotPos;
        hasFocalPoint = true;

        resetRoutine = null;

        currentCameraPosition = CameraPosition.Default;
    }

    private IEnumerator SetCameraPosition(CameraPosition targetPosition)
    {
        // Comprobamos si ya estamos en la posición deseada
        if (currentCameraPosition == targetPosition)
            yield break; // No hacer nada si ya estamos en la posición correcta

        float targetHorizontal = 0f;
        switch (targetPosition)
        {
            case CameraPosition.Left:
                targetHorizontal = 0;
                break;
            case CameraPosition.Right:
                targetHorizontal = 270;   
                break;
            case CameraPosition.Back:
                targetHorizontal = 180f; 
                break;
        }


        // Interpolamos de manera directa para asegurarnos de que no se acumula rotación
        float startHorizontal = orbitalFollow.HorizontalAxis.Value;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / resetDuration;
            float easedT = resetCurve != null ? resetCurve.Evaluate(t) : t;

            float newHorizontal = Mathf.LerpAngle(startHorizontal, targetHorizontal, easedT);
            orbitalFollow.HorizontalAxis.Value = newHorizontal;

            yield return null;
        }

        orbitalFollow.HorizontalAxis.Value = targetHorizontal;

        currentCameraPosition = targetPosition;
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null || orbitalFollow == null || !canZoom)
            return;

        if (isRefocusing)
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

            desiredRefocusZoom = currentZoom;

            isRefocusing = true;
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
            float speed = pivotMoveSpeed;
            if (isRefocusing)
                speed *= refocusPivotSpeedMultiplier;

            pivotTransform.position = Vector3.Lerp(
                pivotTransform.position,
                targetPivotPos,
                speed * Time.deltaTime
            );
        }
        else
        {
            pivotTransform.position = targetPivotPos;
        }

        if (isRefocusing)
        {
            currentZoom = Mathf.MoveTowards(
                currentZoom,
                desiredRefocusZoom,
                refocusZoomLerpSpeed * Time.deltaTime
            );
            orbitalFollow.RadialAxis.Value = currentZoom;

            float distToTarget = Vector3.Distance(pivotTransform.position, targetPivotPos);
            if (distToTarget <= refocusDistanceThreshold)
            {
                pivotTransform.position = targetPivotPos;
                isRefocusing = false;
            }
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
