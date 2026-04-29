using System;
using System.Collections;
using UnityEngine;

public class FreeCameraMovement : MonoBehaviour
{
    // =========================================================
    //  CONFIG
    // =========================================================

    [Header("Fly (RMB)")]
    [SerializeField] private float flySpeed = 25f;

    [Header("Pan (MMB)")]
    [SerializeField] private float panSpeed = 0.5f;
    [SerializeField] private bool invertPanY = false;
    [SerializeField] private bool panVerticalUsesWorldUp = true;
    [SerializeField] private float panVerticalMultiplier = 1f;

    [Header("Zoom (Always)")]
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Zoom IN Collision")]
    [SerializeField] private string mountainTag = "Mountain";
    [SerializeField] private float zoomInStopDistance = 1.0f;
    [SerializeField] private float zoomInCastRadius = 0.25f;
    [SerializeField] private LayerMask zoomInCollisionMask = ~0;

    [Header("Rotate (RMB)")]
    [SerializeField] private float rotateSensitivity = 0.15f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY = false;

    private float yaw;
    private float pitch;

    [Header("View Presets (Transforms)")]
    [SerializeField] private Transform presetFront;
    [SerializeField] private Transform presetRight;
    [SerializeField] private Transform presetBack;
    [SerializeField] private Transform presetLeft;
    [SerializeField] private Transform presetTop;

    [Header("Snap To Preset")]
    [SerializeField] private float snapDuration = 0.35f;
    [SerializeField] private float snapPositionEpsilon = 0.05f;
    [SerializeField] private float snapAngleEpsilon = 0.75f;
    [Range(0f, 1f)]
    [SerializeField] private float snapEarlyCompleteMinT = 0.85f;

    private bool isSnapping;
    private float snapT;
    private Vector3 snapStartPos, snapTargetPos;
    private Quaternion snapStartRot, snapTargetRot;

    // =========================================================
    //  CAMERA BOUNDS
    // =========================================================

    [Header("Camera Bounds")]
    [SerializeField] private bool enableBounds = true;
    [SerializeField] private Vector3 minBounds = new Vector3(-100f, 5f, -100f);
    [SerializeField] private Vector3 maxBounds = new Vector3(100f, 80f, 100f);

    [Header("Boundary Feedback")]
    [SerializeField] private Transform mountainCenter;
    [SerializeField] private float bounceDuration = 0.75f;
    [SerializeField] private float bounceStrength = 5f;
    [SerializeField] private float lookAtSpeed = 5f;

    private bool isBouncing = false;
    private Coroutine bounceRoutine;

    // =========================================================
    //  SPEED
    // =========================================================

    private float baseFlySpeed;
    private float basePanSpeed;

    // =========================================================
    //  UNITY
    // =========================================================

    private void Start()
    {
        baseFlySpeed = flySpeed;
        basePanSpeed = panSpeed;

        if (PlayerPrefs.HasKey("CameraSpeedMultiplier"))
            flySpeed = baseFlySpeed * PlayerPrefs.GetFloat("CameraSpeedMultiplier");

        if (PlayerPrefs.HasKey("CameraPanMultiplier"))
            panSpeed = basePanSpeed * PlayerPrefs.GetFloat("CameraPanMultiplier");

        Vector3 currentEuler = transform.eulerAngles;
        yaw = currentEuler.y;
        pitch = currentEuler.x;
        if (pitch > 180f) pitch -= 360f;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnFrontalView += InputManager_OnFrontalView;
            InputManager.Instance.OnRightView += InputManager_OnRightView;
            InputManager.Instance.OnBackView += InputManager_OnBackView;
            InputManager.Instance.OnLeftView += InputManager_OnLeftView;
            InputManager.Instance.OnTopView += InputManager_OnTopView;
        }
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnFrontalView -= InputManager_OnFrontalView;
            InputManager.Instance.OnRightView -= InputManager_OnRightView;
            InputManager.Instance.OnBackView -= InputManager_OnBackView;
            InputManager.Instance.OnLeftView -= InputManager_OnLeftView;
            InputManager.Instance.OnTopView -= InputManager_OnTopView;
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null &&
           (GameManager.Instance.CurrentState == GameManager.GameState.Cinematic ||
            GameManager.Instance.CurrentState == GameManager.GameState.GamePause ||
            GameManager.Instance.CurrentState == GameManager.GameState.GameOver ||
            GameManager.Instance.CurrentState == GameManager.GameState.Initializing))
        {
            return;
        }

        if (InputManager.Instance == null) return;

        // Durante el bounce bloqueamos todo input de cámara
        if (isBouncing) return;

        HandleZoomAlways();
        HandlePanMMB();
        HandleFlyRMB();
        HandleSnap();
        HandleBounds();
    }

    // =========================================================
    //  PRESETS
    // =========================================================

    private void InputManager_OnFrontalView(object sender, EventArgs e) => StartSnap(presetFront);
    private void InputManager_OnRightView(object sender, EventArgs e) => StartSnap(presetRight);
    private void InputManager_OnBackView(object sender, EventArgs e) => StartSnap(presetBack);
    private void InputManager_OnLeftView(object sender, EventArgs e) => StartSnap(presetLeft);
    private void InputManager_OnTopView(object sender, EventArgs e) => StartSnap(presetTop);

    // =========================================================
    //  ZOOM
    // =========================================================

    private void HandleZoomAlways()
    {
        Vector2 zoomDelta = InputManager.Instance.GetCameraZoom();
        if (Mathf.Abs(zoomDelta.y) < 0.01f) return;

        float step = zoomDelta.y * zoomSpeed;
        Vector3 forward = transform.forward;

        if (step > 0f)
        {
            float castDist = step + zoomInStopDistance;
            RaycastHit hit;

            bool blocked;
            if (zoomInCastRadius > 0f)
            {
                blocked = Physics.SphereCast(
                    transform.position,
                    zoomInCastRadius,
                    forward,
                    out hit,
                    castDist,
                    zoomInCollisionMask,
                    QueryTriggerInteraction.Ignore
                );
            }
            else
            {
                blocked = Physics.Raycast(
                    transform.position,
                    forward,
                    out hit,
                    castDist,
                    zoomInCollisionMask,
                    QueryTriggerInteraction.Ignore
                );
            }

            if (blocked && hit.collider != null && hit.collider.CompareTag(mountainTag))
            {
                float targetDist = Mathf.Max(0f, hit.distance - zoomInStopDistance);
                transform.position += forward * targetDist;
            }
            else
            {
                transform.position += forward * step;
            }
        }
        else
        {
            transform.position += forward * step;
        }
    }

    // =========================================================
    //  PAN (MMB)
    // =========================================================

    private void HandlePanMMB()
    {
        if (!InputManager.Instance.IsCameraPanHold()) return;

        Vector2 delta = InputManager.Instance.GetCameraPanMovement();

        Vector3 right = transform.right;
        Vector3 forwardOnGround = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        float x = -delta.x * panSpeed;
        float z = -delta.y * panSpeed;
        float ySign = invertPanY ? 1f : -1f;
        float y = delta.y * panSpeed * ySign * panVerticalMultiplier;

        Vector3 upAxis = panVerticalUsesWorldUp ? Vector3.up : transform.up;
        Vector3 move = (right * x) + (forwardOnGround * z) + (upAxis * y);

        if (InputManager.Instance.isCameraPanSpeedMultiplierHold())
            move *= 2f;

        transform.position += move;
    }

    // =========================================================
    //  FLY + ROTATE (RMB)
    // =========================================================

    private void HandleFlyRMB()
    {
        if (!InputManager.Instance.IsCameraRotationHold()) return;

        Vector2 delta = InputManager.Instance.GetCameraRotationDelta();

        float dx = delta.x * rotateSensitivity;
        float dy = delta.y * rotateSensitivity * (invertY ? 1f : -1f);

        yaw += dx;
        pitch += dy;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector2 fly2D = InputManager.Instance.GetCameraFlyMovement();
        float upDown = InputManager.Instance.GetCameraFlyUpDown();

        if (fly2D.sqrMagnitude < 0.000001f && Mathf.Abs(upDown) < 0.000001f)
            return;

        Vector3 move =
            (transform.right * fly2D.x) +
            (transform.forward * fly2D.y) +
            (Vector3.up * upDown);

        transform.position += move * (flySpeed * Time.deltaTime);
    }

    // =========================================================
    //  BOUNDS
    // =========================================================

    private void HandleBounds()
    {
        if (!enableBounds) return;

        Vector3 pos = transform.position;
        Vector3 clamped = new Vector3(
            Mathf.Clamp(pos.x, minBounds.x, maxBounds.x),
            Mathf.Clamp(pos.y, minBounds.y, maxBounds.y),
            Mathf.Clamp(pos.z, minBounds.z, maxBounds.z)
        );

        // Clamp siempre para impedir traspasar físicamente el límite
        transform.position = clamped;

        bool wasOutOfBounds = (pos != clamped);
        if (wasOutOfBounds && !isBouncing)
        {
            if (bounceRoutine != null)
                StopCoroutine(bounceRoutine);
            bounceRoutine = StartCoroutine(BounceRoutine(clamped));
        }
    }

    private IEnumerator BounceRoutine(Vector3 clampedPos)
    {
        isBouncing = true;

        Vector3 startPos = clampedPos;

        Vector3 dirToCenter = mountainCenter != null
            ? (mountainCenter.position - clampedPos).normalized
            : Vector3.zero;

        Vector3 bounceTarget = clampedPos + dirToCenter * bounceStrength;
        bounceTarget = new Vector3(
            Mathf.Clamp(bounceTarget.x, minBounds.x, maxBounds.x),
            Mathf.Clamp(bounceTarget.y, minBounds.y, maxBounds.y),
            Mathf.Clamp(bounceTarget.z, minBounds.z, maxBounds.z)
        );

        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot;

        if (mountainCenter != null)
        {
            Vector3 dirToMountain = (mountainCenter.position - clampedPos).normalized;
            if (dirToMountain.sqrMagnitude > 0.001f)
                targetRot = Quaternion.LookRotation(dirToMountain);
        }

        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smooth01(Mathf.Clamp01(elapsed / bounceDuration));

            // Posición — efecto goma elástica
            transform.position = Vector3.Lerp(startPos, bounceTarget, t);

            // Rotación — giro suave y consistente usando t directamente
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        // Al terminar el bounce la cámara mira exactamente al mountainCenter
        transform.rotation = targetRot;

        SyncRotation();

        isBouncing = false;
        bounceRoutine = null;
    }

    // =========================================================
    //  SNAP
    // =========================================================

    private void StartSnap(Transform target)
    {
        if (target == null) return;

        isSnapping = true;
        snapT = 0f;

        snapStartPos = transform.position;
        snapStartRot = transform.rotation;

        snapTargetPos = target.position;
        snapTargetRot = target.rotation;
    }

    private static float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private void FinishSnap()
    {
        transform.position = snapTargetPos;
        transform.rotation = snapTargetRot;

        SyncRotation();
        isSnapping = false;
    }

    private void HandleSnap()
    {
        if (!isSnapping) return;

        snapT += Time.deltaTime / Mathf.Max(0.0001f, snapDuration);
        float t01 = Mathf.Clamp01(snapT);
        float t = Smooth01(t01);

        transform.position = Vector3.Lerp(snapStartPos, snapTargetPos, t);
        transform.rotation = Quaternion.Slerp(snapStartRot, snapTargetRot, t);

        if (t01 >= snapEarlyCompleteMinT)
        {
            float posDist = Vector3.Distance(transform.position, snapTargetPos);
            float angDist = Quaternion.Angle(transform.rotation, snapTargetRot);

            if (posDist <= snapPositionEpsilon && angDist <= snapAngleEpsilon)
            {
                FinishSnap();
                return;
            }
        }

        if (t01 >= 1f)
            FinishSnap();
    }

    // =========================================================
    //  GETTERS
    // =========================================================

    public float GetCameraSpeed() => flySpeed;
    public float GetCameraPan() => panSpeed;
    public float GetCameraSpeedMultiplier() => baseFlySpeed > 0f ? flySpeed / baseFlySpeed : 1f;
    public float GetCameraPanMultiplier() => basePanSpeed > 0f ? panSpeed / basePanSpeed : 1f;

    // =========================================================
    //  SETTERS
    // =========================================================

    public void SetCameraSpeed(float speed) => flySpeed = speed;
    public void SetCameraPan(float pan) => panSpeed = pan;

    public void SetCameraSpeedMultiplier(float multiplier)
    {
        flySpeed = baseFlySpeed * multiplier;
    }

    public void SetCameraPanMultiplier(float multiplier)
    {
        panSpeed = basePanSpeed * multiplier;
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        SyncRotation();
    }

    // =========================================================
    //  HELPERS
    // =========================================================

    private void SyncRotation()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableBounds) return;

        Vector3 center = (minBounds + maxBounds) * 0.5f;
        Vector3 size = maxBounds - minBounds;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}