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
    [SerializeField] private LayerMask placementMask;    
    [SerializeField] private LayerMask defenseMask;
    [SerializeField] private AudioClip defensePlacementAudioClip;

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
        InputManager.Instance.OnRotateCardInput += InputManager_OnRotateCardInput;

        // Esperar un frame para que el VerticalLayoutGroup haya hecho su trabajo
        StartCoroutine(InitializeOriginalPosition());

    }

    private void InputManager_OnRotateCardInput(object sender, System.EventArgs e)
    {
        {
            if (!inPlacementMode || useFixedPosition || previewInstance == null)
                return;

            currentRotationDegrees += 45f;
            if (currentRotationDegrees >= 360f)
                currentRotationDegrees -= 360f;
        }
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

    private void Update()
    {
        if (inPlacementMode && !useFixedPosition && previewInstance != null)
        {
            Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, lastHitNormal);
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

                    // Gizmo de soporte (cuadrado + rayos), si procede
                    if (cardData.requireFullSupport)
                    {
                        var supportGiz = previewInstance.GetComponent<SupportGizmoPreview>();
                        if (supportGiz == null)
                            supportGiz = previewInstance.AddComponent<SupportGizmoPreview>();

                        supportGiz.debugCardData = cardData;
                    }

                    // 🔹 Gizmo del cubo de colisión entre defensas (siempre que quieras verlo)
                    var overlapGiz = previewInstance.GetComponent<PlacementOverlapGizmo>();
                    if (overlapGiz == null)
                        overlapGiz = previewInstance.AddComponent<PlacementOverlapGizmo>();

                    overlapGiz.debugCardData = cardData;
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
                finalNormal = Vector3.up; 
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

            if (valid)
            { 

                // Un pelín separado de la montaña en la normal
                Vector3 checkCenter = finalPosition + finalNormal * 0.1f;

                // Mitades del cubo desde CardData
                Vector3 halfExtents =
                    (cardData != null && cardData.placementCheckExtents != Vector3.zero)
                    ? cardData.placementCheckExtents
                    : new Vector3(0.5f, 0.5f, 0.5f);

                // Rotación del cubo igual que la defensa final
                Quaternion cubeRotation = Quaternion.FromToRotation(Vector3.up, finalNormal);
                cubeRotation = Quaternion.AngleAxis(currentRotationDegrees, finalNormal) * cubeRotation;

                bool overlapsDefense = Physics.CheckBox(checkCenter, halfExtents, cubeRotation, defenseMask);
                if (overlapsDefense)
                {
                    valid = false;
                    StartCoroutine(ShakeCard());
                    Debug.Log("[DragCardUI] No se puede colocar: ya hay una defensa en ese sitio (cubo).");
                }
            }

            // 🔹 Calculamos la rotación final tal como hacemos con el preview (base + extra)
            Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, finalNormal);
            Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, finalNormal);
            Quaternion finalRotation = extraRot * baseRot;

            // 🔹 Chequeo de soporte completo, solo si la carta lo exige
            if (valid && cardData != null && cardData.requireFullSupport)
            {
                if (!HasFullSupport(finalPosition, finalRotation))
                {
                    valid = false;
                    StartCoroutine(ShakeCard());
                    Debug.Log("[DragCardUI] No se puede colocar: quedaría flotando.");
                }
            }

            if (valid && PointsManager.Instance.SpendPoints(cardData.cost))
            {
                GameObject placed = DefensePlacer.Instance.PlaceDefense(cardData.defensePrefab, finalPosition, finalRotation);

                //Sound Manager audioClip ref
                Temporal_Sound_Music.Instance.PlaySound(defensePlacementAudioClip, 1f);
                CameraShake.Instance.SetCurrentStateCameraShake(4.0f, 5.5f, 0.2f);

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

    /// <summary>
    /// Comprueba que toda la "huella" de la defensa está apoyada en la montaña.
    /// Lanza raycasts verticales hacia abajo desde el centro y las 4 esquinas
    /// de un cuadrado definido por supportCheckExtents.
    /// </summary>
    private bool HasFullSupport(Vector3 center, Quaternion rotation)
    {
        if (cardData == null || !cardData.requireFullSupport)
            return true;

        Vector2 ext = cardData.supportCheckExtents;
        float maxDist = cardData.supportRayDistance;
        float yOff = cardData.supportYOffset;

        // La normal de la superficie a partir de la rotación final
        // (recuerda que rotation es la que hace que Vector3.up -> normal de la montaña)
        Vector3 surfaceNormal = rotation * Vector3.up;

        // Centro de la “huella” un poco separado en la normal
        Vector3 supportCenter = center + surfaceNormal * yOff;

        // Offsets locales en el plano XZ local de la defensa
        Vector3[] localOffsets =
        {
        Vector3.zero,
        new Vector3( ext.x, 0f,  ext.y),
        new Vector3(-ext.x, 0f,  ext.y),
        new Vector3( ext.x, 0f, -ext.y),
        new Vector3(-ext.x, 0f, -ext.y),
    };

        foreach (var local in localOffsets)
        {
            // Pasar el offset local a mundo respetando la rotación
            Vector3 worldOffset = rotation * local;

            // Origen del raycast
            Vector3 origin = supportCenter + worldOffset;

            // Dirección “hacia la montaña”: opuesta a la normal de la superficie
            Vector3 dir = -surfaceNormal;

            if (!Physics.Raycast(origin, dir, maxDist, placementMask))
            {
                Debug.Log("[Support] Falta soporte en: " + origin);
                return false;
            }
        }

        return true;
    }

    private void OnDestroy()
    {
        if (PointsManager.Instance != null)
            PointsManager.Instance.OnPointsChanged -= HandlePointsChanged;

        if (InputManager.Instance != null)
            InputManager.Instance.OnRotateCardInput += InputManager_OnRotateCardInput;
    }
}
