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

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask;     // aquí pondrás la capa Mountain
    [SerializeField] private LayerMask defenseMask;       // capa o máscaras donde están las defensas
    [SerializeField] private float placementCheckRadius = 0.5f;

    // 🔹 ROTACIÓN DEL PREVIEW
    private float currentRotationDegrees = 0f;
    private Vector3 lastHitNormal = Vector3.up;

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

        // Esperar un frame para que el VerticalLayoutGroup haya hecho su trabajo
        StartCoroutine(InitializeOriginalPosition());
    }

    private IEnumerator InitializeOriginalPosition()
    {
        yield return null;
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

    // 🔹 ROTACIÓN CON R DEL PREVIEW
    private void Update()
    {
        if (inPlacementMode && !useFixedPosition && previewInstance != null)
        {
            // Pulsar R → rotar 45º alrededor de la normal de la superficie
            if (Input.GetKeyDown(KeyCode.R))
            {
                currentRotationDegrees += 45f;
                if (currentRotationDegrees >= 360f)
                    currentRotationDegrees -= 360f;
            }

            // Orientación base: "up" del objeto alineado con la normal del terreno
            Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, lastHitNormal);
            // Rotación extra alrededor de esa normal
            Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, lastHitNormal);

            previewInstance.transform.rotation = extraRot * baseRot;
        }
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

            // Solo golpea las capas de placementMask (Mountain)
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
            {
                if (previewInstance == null)
                {
                    previewInstance = Instantiate(cardData.defensePrefab);
                    DisablePreviewLogic(previewInstance);
                }

                previewInstance.transform.position = hit.point;

                // 🔹 guardamos la normal actual para la orientación del preview y del colocado
                lastHitNormal = hit.normal;
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
                finalNormal = Vector3.up; // si quieres puedes guardar otra normal para fixed
                valid = true;
            }
            else
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

                // solo montaña
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
                {
                    finalPosition = hit.point;
                    finalNormal = hit.normal;
                    valid = true;
                }
            }

            // Comprobar que no hay otra defensa demasiado cerca
            if (valid)
            {
                Vector3 checkCenter = finalPosition + finalNormal * 0.1f;

                bool overlapsDefense = Physics.CheckSphere(checkCenter, placementCheckRadius, defenseMask);
                if (overlapsDefense)
                {
                    valid = false;
                    StartCoroutine(ShakeCard());
                    Debug.Log("[DragCardUI] No se puede colocar: ya hay una defensa en ese sitio.");
                }
            }

            if (valid && PointsManager.Instance.SpendPoints(cardData.cost))
            {
                // 🔹 RECREAMOS LA MISMA ROTACIÓN QUE TENÍA EL PREVIEW
                Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, finalNormal);
                Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, finalNormal);
                Quaternion finalRotation = extraRot * baseRot;

                GameObject placed = DefensePlacer.Instance.PlaceDefense(cardData.defensePrefab, finalPosition, finalRotation);

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

        // 🔹 reseteamos rotación al empezar el placement
        currentRotationDegrees = 0f;
        lastHitNormal = Vector3.up;

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
        var blocker = preview.GetComponent<BlockFaceOnPlacement>();
        if (blocker != null) Destroy(blocker);

        var rock = preview.GetComponent<RockDefense>();
        if (rock != null) Destroy(rock);

        var behaviours = preview.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            if (b is BlockFaceOnPlacement) continue;
            if (b is RockDefense) continue;
            Destroy(b);
        }

        foreach (var col in preview.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

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
        if (this == null || canvasGroup == null) return;
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
    private Vector2 hoverTargetOffset = new Vector2(30f, 0f);
    private float hoverSpeed = 10f;

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
