using UnityEngine;
using UnityEngine.UIElements;

public class CameraZoomPivotFocus : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Transform pivotTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Raycast")]
    [SerializeField] private LayerMask rayLayerMask;
    [SerializeField] private float maxRayDistance = 500f;

    [Header("Pivot Movement")]
    [SerializeField] private float moveSpeed = 20f;
    private bool isSmoothMovement = true;

    [Header("Controls")]
    [SerializeField] private bool requireRightMouseButton = true;
    [SerializeField] private bool onlyOnZoomIn = true;

    private Vector3 targetPivotPos;

    private void Reset()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (pivotTransform != null)
            targetPivotPos = pivotTransform.position;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null || pivotTransform == null)
            return;

        if (requireRightMouseButton && !Input.GetMouseButton(1))
            return;

        float scroll = Input.mousePositionDelta.y;

        if (Mathf.Approximately(scroll, 0f))
            return;
    }
}
