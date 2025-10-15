using UnityEngine;
using UnityEngine.EventSystems;

public class DragCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject defensePrefab;
    public Camera mainCamera;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Canvas canvas;

    private GameObject previewInstance;

    private bool inPlacementMode = false;
    private float placementThreshold = 50f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
        originalPosition = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false;
        inPlacementMode = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Mueve la carta solo horizontalmente (X)
        Vector2 newPos = rectTransform.anchoredPosition;
        newPos.x += eventData.delta.x / canvas.scaleFactor;
        rectTransform.anchoredPosition = newPos;

        float distanceToRight = rectTransform.anchoredPosition.x - originalPosition.x;

        // Transición de alpha en base al desplazamiento horizontal
        float alpha = Mathf.Clamp01(1f - (distanceToRight / placementThreshold));
        canvasGroup.alpha = alpha;

        // Si ya superó el umbral, activa modo de colocación (una sola vez)
        if (!inPlacementMode && distanceToRight >= placementThreshold)
        {
            EnterPlacementMode();
        }

        // Si está en modo de colocar, seguir actualizando el preview
        if (inPlacementMode)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (previewInstance == null)
                {
                    previewInstance = Instantiate(defensePrefab);
                    DisablePreviewLogic(previewInstance);
                }

                previewInstance.transform.position = hit.point;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f; // Restaurar visibilidad
        rectTransform.anchoredPosition = originalPosition;

        if (inPlacementMode)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                DefensePlacer.Instance.PlaceDefense(defensePrefab, hit.point);
            }

            if (previewInstance != null)
                Destroy(previewInstance);
        }

        inPlacementMode = false;
    }

    private void EnterPlacementMode()
    {
        inPlacementMode = true;
        canvasGroup.alpha = 0f;
    }

    private void DisablePreviewLogic(GameObject preview)
    {
        foreach (var behavior in preview.GetComponents<MonoBehaviour>())
        {
            Destroy(behavior);
        }

        foreach (var col in preview.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        var renderers = preview.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                Color c = mat.color;
                c.a = 0.4f;
                mat.color = c;
            }
        }
    }
}
