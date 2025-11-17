using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public CardData cardData;

    public Camera mainCamera;
    public Image cardImage;
    public Image iconImage;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI nameText;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private Canvas canvas;

    private GameObject previewInstance;

    private bool inPlacementMode = false;
    private float placementThreshold = 50f;

    private bool canDragThisTime = false;

    private Vector3 fixedPlacementPosition;
    private bool useFixedPosition = false;

    private System.Action<DragCardUI> replacementCallback;
    private Button cardButton;

    public event System.Action<DragCardUI> OnCardUsed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();

        cardButton = GetComponent<Button>();
    }

    private void Start()
    {
        SetupCardUI();
        UpdateInteractable();
        PointsManager.Instance.OnPointsChanged += HandlePointsChanged;

        //  Esperar al siguiente frame para asegurar que el layout haya colocado la carta
        StartCoroutine(InitializeOriginalPosition());
    }

    private IEnumerator InitializeOriginalPosition()
    {
        // Espera un frame para que el VerticalLayoutGroup haya hecho su trabajo
        yield return null;

        // Ahora sí, guarda la posición correcta
        originalPosition = rectTransform.anchoredPosition;
    }

    public void SetupCardUI()
    {
        if (cardData == null) return;

        if (iconImage != null) iconImage.sprite = cardData.icon;
        if (costText != null) costText.text = cardData.cost.ToString();
        if (nameText != null) nameText.text = cardData.cardName;
    }

    private void HandlePointsChanged(int points)
    {
        UpdateInteractable();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!PointsManager.Instance.CanAfford(cardData.cost))
        {
            canDragThisTime = false;
            StartCoroutine(ShakeCard());
            return;
        }

        canDragThisTime = true;

        canvasGroup.blocksRaycasts = false;
        inPlacementMode = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        Vector2 newPos = rectTransform.anchoredPosition;
        newPos.x += eventData.delta.x / canvas.scaleFactor;
        rectTransform.anchoredPosition = newPos;

        float distanceToRight = rectTransform.anchoredPosition.x - originalPosition.x;
        float alpha = Mathf.Clamp01(1f - (distanceToRight / placementThreshold));
        canvasGroup.alpha = alpha;

        if (!inPlacementMode && distanceToRight >= placementThreshold)
        {
            EnterPlacementMode();
        }

        if (inPlacementMode && !useFixedPosition)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (previewInstance == null)
                {
                    previewInstance = Instantiate(cardData.defensePrefab);
                    DisablePreviewLogic(previewInstance);
                }

                previewInstance.transform.position = hit.point;
                previewInstance.transform.up = hit.normal;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = originalPosition;

        if (inPlacementMode)
        {
            Vector3 finalPosition = Vector3.zero;
            Vector3 finalNormal = Vector3.up;
            bool valid = false;

            if (useFixedPosition)
            {
                finalPosition = fixedPlacementPosition;
                valid = true;
            }
            else
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    finalPosition = hit.point;
                    finalNormal = hit.normal;   // guardamos la normal
                    valid = true;
                }
            }

            if (valid && PointsManager.Instance.SpendPoints(cardData.cost))
            {
                GameObject placed = DefensePlacer.Instance.PlaceDefense(cardData.defensePrefab, finalPosition, finalNormal);

                if (placed != null && DefensePlacementManager.Instance != null)
                    DefensePlacementManager.Instance.RegisterPlaced(placed);

                OnCardUsed?.Invoke(this);
            }

            if (previewInstance != null)
                Destroy(previewInstance);
        }

        inPlacementMode = false;
        useFixedPosition = false;
    }

    private void EnterPlacementMode()
    {
        inPlacementMode = true;
        canvasGroup.alpha = 0f;

        if (cardData.hasFixedPlacement)
        {
            useFixedPosition = true;
            fixedPlacementPosition = cardData.fixedPosition;

            if (previewInstance == null)
            {
                previewInstance = Instantiate(cardData.defensePrefab);
                DisablePreviewLogic(previewInstance);
            }

            previewInstance.transform.position = fixedPlacementPosition;
        }
    }

    public void EnableReplacementSelection(System.Action<DragCardUI> callback)
    {
        replacementCallback = callback;
    }

    public void DisableReplacementSelection()
    {
        replacementCallback = null;
    }

    private void DisablePreviewLogic(GameObject preview)
    {
        // 1) Quitar scripts que tienen dependencias en orden correcto
        var blocker = preview.GetComponent<BlockFaceOnPlacement>();
        if (blocker != null) Destroy(blocker);

        var rock = preview.GetComponent<RockDefense>();
        if (rock != null) Destroy(rock);

        // 2) Quitar el resto de comportamientos (excepto Transform, obvio)
        var behaviours = preview.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            // por si este GO tuviera otros scripts de gameplay
            if (b == null) continue;
            if (b is BlockFaceOnPlacement) continue;
            if (b is RockDefense) continue;
            Destroy(b);
        }

        // 3) Desactivar colliders del preview
        foreach (var col in preview.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // 4) Materiales de preview (transparente)
        if (cardData.previewMaterial == null)
        {
            Debug.LogWarning("No preview material asignado en CardData");
            return;
        }
        var renderers = preview.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = cardData.previewMaterial;
            rend.materials = mats;
        }
    }

    private bool isShaking = false;

    private IEnumerator ShakeCard(float duration = 0.2f, float magnitude = 10f)
    {
        if (isShaking)
            yield break;

        isShaking = true;

        Vector2 originalPos = originalPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            rectTransform.anchoredPosition = originalPos + new Vector2(offsetX, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        rectTransform.anchoredPosition = originalPos;
        isShaking = false;
    }

    public void UpdateInteractable()
    {
        if (this == null || canvasGroup == null) return; // seguridad extra
        bool canUse = PointsManager.Instance != null && PointsManager.Instance.CanAfford(cardData.cost);

        if (cardImage != null)
            cardImage.color = canUse ? Color.white : Color.gray;

        canvasGroup.interactable = canUse;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (replacementCallback != null)
        {
            replacementCallback.Invoke(this);
        }
    }

    private bool isHoveringForReplacement = false;
    private Coroutine hoverRoutine;
    private Vector2 hoverTargetOffset = new Vector2(30f, 0f); // distancia del movimiento
    private float hoverSpeed = 10f; // velocidad de interpolación

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (replacementCallback != null && !isHoveringForReplacement)
        {
            isHoveringForReplacement = true;

            if (hoverRoutine != null)
                StopCoroutine(hoverRoutine);

            hoverRoutine = StartCoroutine(MoveCardSmooth(originalPosition, originalPosition + hoverTargetOffset));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (replacementCallback != null && isHoveringForReplacement)
        {
            isHoveringForReplacement = false;

            if (hoverRoutine != null)
                StopCoroutine(hoverRoutine);

            hoverRoutine = StartCoroutine(MoveCardSmooth(rectTransform.anchoredPosition, originalPosition));
        }
    }

    private IEnumerator MoveCardSmooth(Vector2 from, Vector2 to)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * hoverSpeed;
            rectTransform.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
    }
    private void OnDestroy()
    {
        if (PointsManager.Instance != null)
            PointsManager.Instance.OnPointsChanged -= HandlePointsChanged;
    }

}
