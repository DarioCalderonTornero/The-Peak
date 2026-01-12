using System;
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Pan")]
    [SerializeField] private float panSpeed = 0.2f;
    [SerializeField] private bool invertPanY = false;
    [SerializeField] private bool panVerticalUsesWorldUp = true;
    [SerializeField] private float panVerticalMultiplier = 1f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Zoom IN Collision (acercarse ilimitado hasta casi chocar)")]
    [SerializeField] private string mountainTag = "Mountain";
    [SerializeField] private float zoomInStopDistance = 1.0f;
    [SerializeField] private float zoomInCastRadius = 0.25f;
    [SerializeField] private LayerMask zoomInCollisionMask = ~0;

    [Header("Rotate")]
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

    private bool isSnapping;
    private float snapT;
    private Vector3 snapStartPos, snapTargetPos;
    private Quaternion snapStartRot, snapTargetRot;

    // -------------------- INSPECCIÓN (climber focus) --------------------
    [Header("Inspect Mode")]
    [Tooltip("Durante inspección, la cámara tenderá a mirar al escalador, PERO el jugador puede seguir moviéndola libremente.")]
    [SerializeField] private bool keepLookingAtTargetWhileInspecting = true;

    [Tooltip("Velocidad a la que la cámara reorienta hacia el objetivo mientras inspecciona (solo si keepLookingAtTargetWhileInspecting está activo).")]
    [SerializeField] private float inspectLookAtFollowSpeed = 12f;

    [Tooltip("Si está activo, al deseleccionar (tap toggle) se sale también del modo inspección.")]
    [SerializeField] private bool exitInspectOnDeselect = true;

    [Header("Inspect Cancel (al tocar inputs)")]
    [Tooltip("Si el jugador usa cualquier input de cámara (pan/rotate/zoom/presets), salimos de inspección y dejamos de mirar al escalador.")]
    [SerializeField] private bool cancelInspectOnAnyCameraInput = true;

    [Tooltip("Deadzone para considerar que el jugador ha movido el ratón/rueda/pan (evita cancelaciones por ruido).")]
    [SerializeField] private float cancelDeadzone = 0.001f;

    private bool isInspecting;
    private Transform currentInspectLookAt;
    // --------------------------------------------------------------------

    private void Start()
    {
        Vector3 currentEuler = transform.eulerAngles;
        yaw = currentEuler.y;

        pitch = currentEuler.x;
        if (pitch > 180f) pitch -= 360f;

        if (InputManager.Instance != null)
        {
            // IMPORTANTE: mantenemos los presets (caras de la montaña)
            InputManager.Instance.OnFrontalView += InputManager_OnFrontalView;
            InputManager.Instance.OnRightView += InputManager_OnRightView;
            InputManager.Instance.OnBackView += InputManager_OnBackView;
            InputManager.Instance.OnLeftView += InputManager_OnLeftView;
            InputManager.Instance.OnTopView += InputManager_OnTopView;
        }

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnClimberDeselected += HandleDeselected;
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

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnClimberDeselected -= HandleDeselected;
        }
    }

    // -------------------- INPUT PRESETS (caras montaña) --------------------

    private void InputManager_OnFrontalView(object sender, EventArgs e)
    {
        CancelInspectByUserInput();
        StartSnap(presetFront);
    }

    private void InputManager_OnRightView(object sender, EventArgs e)
    {
        CancelInspectByUserInput();
        StartSnap(presetRight);
    }

    private void InputManager_OnBackView(object sender, EventArgs e)
    {
        CancelInspectByUserInput();
        StartSnap(presetBack);
    }

    private void InputManager_OnLeftView(object sender, EventArgs e)
    {
        CancelInspectByUserInput();
        StartSnap(presetLeft);
    }

    private void InputManager_OnTopView(object sender, EventArgs e)
    {
        CancelInspectByUserInput();
        StartSnap(presetTop);
    }

    // -------------------- INSPECT API --------------------

    /*
    private void FocusOnClimber(ClimberMovement climber)
    {
        if (climber == null) return;

        isInspecting = true;
        currentInspectLookAt = climber.CamLookAt;

        Debug.Log($"[FreeCamera] Inspect: {climber.name}");

        // Snap hacia el anchor del escalador
        StartSnap(climber.InspectAnchor);
    }
    */

    private void HandleDeselected()
    {
        if (exitInspectOnDeselect)
            ExitInspect();
    }

    private void ExitInspect()
    {
        isInspecting = false;
        currentInspectLookAt = null;
    }

    private void CancelInspectByUserInput()
    {
        if (!cancelInspectOnAnyCameraInput) return;
        if (!isInspecting) return;

        ExitInspect();
    }

    // -------------------- SNAP --------------------

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
        // SmoothStep (suaviza aceleración y frenada)
        return t * t * (3f - 2f * t);
    }

    private void Update()
    {
        if (InputManager.Instance == null) return;

        // ======================= DETECTAR INPUTS (para cancelar inspect) =======================
        // Si el jugador usa cámara -> dejamos de trackear al escalador.
        // (Los presets ya lo cancelan arriba, pero aquí cubrimos pan/rotate/zoom)
        if (isInspecting && cancelInspectOnAnyCameraInput)
        {
            bool anyCameraInput = false;

            if (InputManager.Instance.IsCameraRotationHold())
            {
                Vector2 rot = InputManager.Instance.GetCameraRotationDelta();
                if (rot.sqrMagnitude > cancelDeadzone * cancelDeadzone)
                    anyCameraInput = true;
            }

            if (InputManager.Instance.IsCameraPanHold())
            {
                Vector2 pan = InputManager.Instance.GetCameraPanMovement();
                if (pan.sqrMagnitude > cancelDeadzone * cancelDeadzone)
                    anyCameraInput = true;
            }

            Vector2 zoom = InputManager.Instance.GetCameraZoom();
            if (Mathf.Abs(zoom.y) > cancelDeadzone)
                anyCameraInput = true;

            if (anyCameraInput)
                CancelInspectByUserInput();
        }

        // ======================= INPUTS SIEMPRE ACTIVOS =======================

        // --- ROTATE ---
        if (InputManager.Instance.IsCameraRotationHold())
        {
            Vector2 delta = InputManager.Instance.GetCameraRotationDelta();

            float dx = delta.x * rotateSensitivity;
            float dy = delta.y * rotateSensitivity * (invertY ? 1f : -1f);

            yaw += dx;
            pitch += dy;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // --- ZOOM DOLLY ---
        Vector2 zoomDelta = InputManager.Instance.GetCameraZoom();
        if (Mathf.Abs(zoomDelta.y) >= 0.01f)
        {
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

        // --- PAN ---
        if (InputManager.Instance.IsCameraPanHold())
        {
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
            {
                float panSpeedMultiplier = 2f;
                move *= panSpeedMultiplier;
            }

            transform.position += move;
        }

        // ======================= SNAP (SIN BLOQUEAR INPUTS) =======================
        if (isSnapping)
        {
            snapT += Time.deltaTime / Mathf.Max(0.0001f, snapDuration);
            float t01 = Mathf.Clamp01(snapT);
            float t = Smooth01(t01);

            // Adictivo: partimos desde donde esté la cámara ese frame
            snapStartPos = transform.position;
            snapStartRot = transform.rotation;

            transform.position = Vector3.Lerp(snapStartPos, snapTargetPos, t);

            // Si inspeccionas, durante el snap mira al escalador (solo si sigues inspeccionando)
            if (isInspecting && currentInspectLookAt != null)
            {
                Vector3 dir = currentInspectLookAt.position - transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 0.9f);

                    // sync yaw/pitch para evitar saltos
                    Vector3 euler = transform.eulerAngles;
                    yaw = euler.y;
                    pitch = euler.x;
                    if (pitch > 180f) pitch -= 360f;
                }
            }
            else
            {
                // Snap normal hacia preset (caras montaña) o cualquier otro target
                transform.rotation = Quaternion.Slerp(snapStartRot, snapTargetRot, t);

                // sync yaw/pitch
                Vector3 euler = transform.eulerAngles;
                yaw = euler.y;
                pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;
            }

            if (t01 >= 1f)
            {
                isSnapping = false;

                Vector3 euler = transform.eulerAngles;
                yaw = euler.y;

                pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;
            }
        }


        if (isInspecting && keepLookingAtTargetWhileInspecting && currentInspectLookAt != null)
        {
            Vector3 dir = currentInspectLookAt.position - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

                // empuje suave hacia el objetivo (pero nunca bloquea inputs)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    1f - Mathf.Exp(-inspectLookAtFollowSpeed * Time.deltaTime)
                );

                // sync yaw/pitch para que el control siga suave
                Vector3 euler = transform.eulerAngles;
                yaw = euler.y;
                pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;
            }
        }
    }
}
