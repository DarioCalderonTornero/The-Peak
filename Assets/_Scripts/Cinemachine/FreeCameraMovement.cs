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
    [SerializeField] private float zoomInStopDistance = 1.0f;     // margen antes de chocar
    [SerializeField] private float zoomInCastRadius = 0.25f;      // 0 = Raycast, >0 = SphereCast
    [SerializeField] private LayerMask zoomInCollisionMask = ~0;  // puedes limitarlo a la layer de la montaña

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

    private void Start()
    {
        // Inicializa yaw/pitch desde la rotación actual (evita saltos)
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
        if (InputManager.Instance == null) return;

        InputManager.Instance.OnFrontalView -= InputManager_OnFrontalView;
        InputManager.Instance.OnRightView -= InputManager_OnRightView;
        InputManager.Instance.OnBackView -= InputManager_OnBackView;
        InputManager.Instance.OnLeftView -= InputManager_OnLeftView;
        InputManager.Instance.OnTopView -= InputManager_OnTopView;
    }

    private void InputManager_OnFrontalView(object sender, EventArgs e) => StartSnap(presetFront);
    private void InputManager_OnRightView(object sender, EventArgs e) => StartSnap(presetRight);
    private void InputManager_OnBackView(object sender, EventArgs e) => StartSnap(presetBack);
    private void InputManager_OnLeftView(object sender, EventArgs e) => StartSnap(presetLeft);
    private void InputManager_OnTopView(object sender, EventArgs e) => StartSnap(presetTop);

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

    private void Update()
    {
        if (InputManager.Instance == null) return;

        // --- SNAP (bloquea inputs mientras se mueve) ---
        if (isSnapping)
        {
            snapT += Time.deltaTime / Mathf.Max(0.0001f, snapDuration);
            float t = Mathf.Clamp01(snapT);

            transform.position = Vector3.Lerp(snapStartPos, snapTargetPos, t);
            transform.rotation = Quaternion.Slerp(snapStartRot, snapTargetRot, t);

            if (t >= 1f)
            {
                isSnapping = false;

                // Resync yaw/pitch para que no pegue salto al volver a rotar
                Vector3 euler = transform.eulerAngles;
                yaw = euler.y;

                pitch = euler.x;
                if (pitch > 180f) pitch -= 360f;
            }

            return;
        }

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

        // --- ZOOM DOLLY (nuevo enfoque) ---
        Vector2 zoomDelta = InputManager.Instance.GetCameraZoom();
        if (Mathf.Abs(zoomDelta.y) >= 0.01f)
        {
            float step = zoomDelta.y * zoomSpeed;
            Vector3 forward = transform.forward;

            // Zoom IN (step > 0): se para solo si va a chocar con montaña
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
                    // Coloca la cámara justo antes del impacto (margen)
                    float targetDist = Mathf.Max(0f, hit.distance - zoomInStopDistance);
                    transform.position += forward * targetDist;
                }
                else
                {
                    transform.position += forward * step;
                }
            }
            // Zoom OUT (step < 0): infinito (por ahora)
            else
            {
                transform.position += forward * step; // step es negativo
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
            transform.position += move;
        }
    }
}
