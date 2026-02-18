using System;
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    // =========================================================
    //  CONFIG
    // =========================================================

    [Header("Fly (RMB)")]
    [Tooltip("Velocidad lineal del vuelo (WASD + Q/E) cuando holdeas RMB.")]
    [SerializeField] private float flySpeed = 25f;

    [Header("Pan (MMB)")]
    [SerializeField] private float panSpeed = 0.2f;
    [SerializeField] private bool invertPanY = false;
    [SerializeField] private bool panVerticalUsesWorldUp = true;
    [SerializeField] private float panVerticalMultiplier = 1f;

    [Header("Zoom (Always)")]
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Zoom IN Collision (acercarse ilimitado hasta casi chocar)")]
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

    [Tooltip("Distancia a partir de la cual consideramos que ya hemos llegado y cortamos el snap.")]
    [SerializeField] private float snapPositionEpsilon = 0.05f;

    [Tooltip("Ángulo (grados) a partir del cual consideramos que ya hemos llegado y cortamos el snap.")]
    [SerializeField] private float snapAngleEpsilon = 0.75f;

    [Tooltip("Para evitar cortar demasiado pronto, solo permitimos corte temprano cuando el snap ya ha avanzado X%.")]
    [Range(0f, 1f)]
    [SerializeField] private float snapEarlyCompleteMinT = 0.85f;

    private bool isSnapping;
    private float snapT;
    private Vector3 snapStartPos, snapTargetPos;
    private Quaternion snapStartRot, snapTargetRot;

    // =========================================================
    //  UNITY
    // =========================================================

    private void Start()
    {
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
        if (InputManager.Instance == null) return;

        // 1) Zoom siempre activo (rueda)
        HandleZoomAlways();

        // 2) Paneo (MMB hold)
        HandlePanMMB();

        // 3) Cámara libre estilo Unity (RMB hold)
        HandleFlyRMB();

        // 4) Snap a presets (si está activo, manda al final del frame)
        HandleSnap();
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
    //  ZOOM (SIEMPRE)
    // =========================================================

    private void HandleZoomAlways()
    {
        Vector2 zoomDelta = InputManager.Instance.GetCameraZoom();
        if (Mathf.Abs(zoomDelta.y) < 0.01f) return;

        float step = zoomDelta.y * zoomSpeed;
        Vector3 forward = transform.forward;

        // step > 0 => acercarse
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
            // step < 0 => alejarse (sin límite)
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

        // (si mantienes Shift como multiplicador de pan en tu input actual)
        if (InputManager.Instance.isCameraPanSpeedMultiplierHold())
        {
            float panSpeedMultiplier = 2f;
            move *= panSpeedMultiplier;
        }

        transform.position += move;
    }

    // =========================================================
    //  FLY + ROTATE (RMB)  -> "Unity Scene View"
    // =========================================================

    private void HandleFlyRMB()
    {
        if (!InputManager.Instance.IsCameraRotationHold()) return;

        // --- ROTATE (mouse delta) ---
        Vector2 delta = InputManager.Instance.GetCameraRotationDelta();

        float dx = delta.x * rotateSensitivity;
        float dy = delta.y * rotateSensitivity * (invertY ? 1f : -1f);

        yaw += dx;
        pitch += dy;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // --- FLY (WASD + Q/E) ---
        Vector2 fly2D = InputManager.Instance.GetCameraFlyMovement(); // x=strife, y=forward
        float upDown = InputManager.Instance.GetCameraFlyUpDown();    // E=+1, Q=-1

        if (fly2D.sqrMagnitude < 0.000001f && Mathf.Abs(upDown) < 0.000001f)
            return;

        Vector3 move =
            (transform.right * fly2D.x) +
            (transform.forward * fly2D.y) +
            (Vector3.up * upDown);

        transform.position += move * (flySpeed * Time.deltaTime);
    }

    // =========================================================
    //  SNAP (presets)
    // =========================================================

    private void StartSnap(Transform target)
    {
        if (target == null) return;

        isSnapping = true;
        snapT = 0f;

        // Start fijo: esto elimina la "cola" lenta del final.
        snapStartPos = transform.position;
        snapStartRot = transform.rotation;

        snapTargetPos = target.position;
        snapTargetRot = target.rotation;
    }

    private static float Smooth01(float t)
    {
        // SmoothStep (suaviza aceleración y frenada)
        return t * t * (3f - 2f * t);
    }

    private void FinishSnap()
    {
        // Clavamos exacto al objetivo para evitar drift y devolvemos control ya.
        transform.position = snapTargetPos;
        transform.rotation = snapTargetRot;

        // Sync yaw/pitch para que el control siga suave tras el snap
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;

        pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;

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

        // Corte temprano cuando visualmente "ya está"
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

        // Fin normal
        if (t01 >= 1f)
        {
            FinishSnap();
        }
    }

    //GETTERS
    public float GetCameraSpeed()
    {
        return flySpeed;    
    }

    public float GetCameraPan()
    {
        return panSpeed;
    }

    //SETTERS
    public void SetCameraSpeed(float speed)
{
    flySpeed = speed;
}

public void SetCameraPan(float pan)
{
    panSpeed = pan;
}
}
