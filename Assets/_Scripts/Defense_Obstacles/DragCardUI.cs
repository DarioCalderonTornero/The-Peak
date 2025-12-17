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
    // public Image cardImage;
    public Image iconImage;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI nameText;

    public Image worldSpriteImage;

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
    [SerializeField] private LayerMask campMask;
    [SerializeField] private AudioClip defensePlacementAudioClip;

    private float currentRotationDegrees = 0f;
    private Vector3 lastHitNormal = Vector3.up;

    private int bramblePreviewSeed = 0;

    [Header("Preview Materials")]
    [SerializeField] private Material previewValidMaterial;   // verde
    [SerializeField] private Material previewInvalidMaterial; // rojo

    private bool currentPreviewIsValid = false;

    [SerializeField] private RectTransform slotsContainerRect;
    
    // --- Rotación libre ---
    private bool isFreeRotating = false;
    private Vector3 freeRotatePivot;      // punto fijo donde se queda el preview al pulsar R
    private Vector3 freeRotateForward;    // dirección base para la rotación
    private float freeRotateStartAngle;   // ángulo al iniciar la rotación libre

    private Quaternion freeRotateBaseRotation;

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
        if (!inPlacementMode || useFixedPosition || previewInstance == null)
            return;

        if (isFreeRotating)
        {
            // Salir de modo rotación libre si quieres toggle
            isFreeRotating = false;
            return;
        }

        isFreeRotating = true;

        // Punto fijo donde se queda el preview
        freeRotatePivot = previewInstance.transform.position;

        // 🔹 Rotación base EXACTA en el momento de pulsar R (incluye X y Z de la rampa)
        freeRotateBaseRotation = previewInstance.transform.rotation;

        // Empezamos a acumular desde 0 sobre esa base
        currentRotationDegrees = 0f;
    }

    private void UpdateFreeRotation()
    {
        if (previewInstance == null) return;

        // Mantener posición clavada
        previewInstance.transform.position = freeRotatePivot;

        // Acumular Y "de inspector"
        float mouseDelta = Input.GetAxis("Mouse X") * 6.5f;
        currentRotationDegrees += mouseDelta;
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees, 360f);

        // 🔹 Aplicar rotación final:
        // - freeRotateBaseRotation: la inclinación original (X/Z correctos en la rampa)
        // - AngleAxis sobre el eje up LOCAL de esa rotación (como girar Y en el inspector)
        Vector3 localUp = freeRotateBaseRotation * Vector3.up;
        Quaternion extraY = Quaternion.AngleAxis(currentRotationDegrees, localUp);

        previewInstance.transform.rotation = extraY * freeRotateBaseRotation;
    }

    private string CheckPlacementValidity(Vector3 position, Vector3 normal, Quaternion rotation)
    {
        Vector3 checkCenter = position + normal * 0.1f;
        Vector3 halfExtents = (cardData != null && cardData.placementCheckExtents != Vector3.zero)
            ? cardData.placementCheckExtents
            : new Vector3(0.5f, 0.5f, 0.5f);

        // Defensas
        if (Physics.CheckBox(checkCenter, halfExtents, rotation, defenseMask))
            return "Demasiado cerca de otro obstáculo";

        // Campamentos
        if (Physics.CheckBox(checkCenter, halfExtents, rotation, campMask))
            return "Demasiado cerca de un campamento";

        // Soporte completo
        if (cardData != null && cardData.requireFullSupport)
        {
            if (!HasFullSupport(position, rotation))
                return "Quedaría flotando";
        }

        return "Válido";
    }

    private void ApplyPreviewMaterial(bool valid)
    {
        if (previewInstance == null)
            return;

        var renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        Material mat = valid ? previewValidMaterial : previewInvalidMaterial;

        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
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

        if (worldSpriteImage != null && cardData.worldSprite != null)
            worldSpriteImage.sprite = cardData.worldSprite;
    }

    private void HandlePointsChanged(int points)
    {
        UpdateInteractable();
    }

    private void Update()
    {
        if (inPlacementMode && !useFixedPosition && previewInstance != null)
        {
            if (isFreeRotating)
            {
                UpdateFreeRotation();
            }
            else
            {
                // Rotación clásica (45° steps)
                Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, lastHitNormal);
                Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, lastHitNormal);
                previewInstance.transform.rotation = baseRot * extraRot;
            }
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

        // 🔹 RESET COMPLETO DE ESTADO al empezar nuevo drag
        canvasGroup.blocksRaycasts = false;
        inPlacementMode = false;
        isFreeRotating = false;  // ← ¡RESET!
        currentRotationDegrees = 0f;
        previewInstance = null;
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

        // 🔹 si el puntero sigue sobre UI, NO entramos en placement mode
        bool overUI = IsPointerOverUI();

        if (!inPlacementMode && distanceToRight >= placementThreshold && !overUI)
        {
            EnterPlacementMode();
        }

        // 🔹 si ya estábamos en placement mode y volvemos a pasar por UI, apagamos preview y salimos
        if (inPlacementMode && overUI)
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            inPlacementMode = false;
            useFixedPosition = false;
            currentPreviewIsValid = false;
            // canvasGroup.alpha = 1f;
            return;
        }

        if (inPlacementMode && !useFixedPosition)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
            {
                if (previewInstance == null)
                {
                    previewInstance = Instantiate(cardData.defensePrefab);
                    DisablePreviewLogic(previewInstance);

                    bramblePreviewSeed = Random.Range(int.MinValue, int.MaxValue);

                    var bramble = previewInstance.GetComponent<BrambleDefense>();
                    if (bramble != null)
                        bramble.SetupPreview(bramblePreviewSeed, cardData.previewMaterial);

                    if (cardData.requireFullSupport)
                    {
                        var supportGiz = previewInstance.GetComponent<SupportGizmoPreview>();
                        if (supportGiz == null)
                            supportGiz = previewInstance.AddComponent<SupportGizmoPreview>();

                        supportGiz.debugCardData = cardData;
                    }

                    var overlapGiz = previewInstance.GetComponent<PlacementOverlapGizmo>();
                    if (overlapGiz == null)
                        overlapGiz = previewInstance.AddComponent<PlacementOverlapGizmo>();

                    overlapGiz.debugCardData = cardData;
                }

                // 🔹 POSICIÓN: solo si NO rotando libre
                if (!isFreeRotating)
                {
                    previewInstance.transform.position = hit.point;
                }

                lastHitNormal = hit.normal;

                // En OnDrag, dentro del bloque if (Physics.Raycast...)
                Vector3 checkPosition = isFreeRotating ? freeRotatePivot : hit.point;

                // Para la rotación en CheckPlacementValidity, usa la rotación actual del preview
                Quaternion previewRotation = previewInstance.transform.rotation;

                string validityReason = CheckPlacementValidity(checkPosition, lastHitNormal, previewRotation);
                bool isValid = validityReason == "Válido";

                if (isValid != currentPreviewIsValid)
                {
                    currentPreviewIsValid = isValid;
                    ApplyPreviewMaterial(isValid);
                }
            }
        }
    }

    private bool IsPointerOverUI()
    {
        // Para PC y editor (mouse)
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = new Vector2(originalPosition.x, rectTransform.anchoredPosition.y);

        if (IsPointerOverUI())
        {
            CleanupPreview();
            ResetAllStates();
            return;
        }

        if (!inPlacementMode)
        {
            ResetAllStates();
            return;
        }

        // Determinar posición final [TU CÓDIGO EXACTO]
        Vector3 finalPosition;
        Quaternion finalRotation;
        Vector3 finalNormal = Vector3.up;

        if (useFixedPosition)
        {
            finalPosition = fixedPlacementPosition;
            finalNormal = Vector3.up;
            finalRotation = previewInstance ? previewInstance.transform.rotation : Quaternion.identity;
        }
        else if (isFreeRotating && previewInstance != null)
        {
            finalPosition = freeRotatePivot;
            finalNormal = lastHitNormal;
            finalRotation = previewInstance.transform.rotation;
        }
        else
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
            {
                finalPosition = hit.point;
                finalNormal = hit.normal;
                Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, finalNormal);
                Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, finalNormal);
                finalRotation = extraRot * baseRot;
            }
            else
            {
                StartCoroutine(ShakeCard());
                CleanupPreview();
                ResetAllStates();
                return;
            }
        }

        // 🔹 CHECKEO FINAL CON MOTIVO
        string placementReason = CheckPlacementValidity(finalPosition, finalNormal, finalRotation);
        if (placementReason != "Válido")
        {
            // 🎯 MOSTRAR TEXTO SOLO AL SOLTAR
            PlacementFeedbackUI feedback = FindObjectOfType<PlacementFeedbackUI>();
            if (feedback != null)
                feedback.ShowMessage(placementReason);

            StartCoroutine(ShakeCard());
            CleanupPreview();
            ResetAllStates();
            return;
        }

        // COLOCAR (tu código exacto)
        if (PointsManager.Instance.SpendPoints(cardData.cost))
        {
            GameObject placed = DefensePlacer.Instance.PlaceDefense(
                cardData.defensePrefab,
                finalPosition,
                finalRotation,
                (go) =>
                {
                    var bramble = go.GetComponent<BrambleDefense>();
                    if (bramble != null)
                        bramble.SetupRuntimeFromPreviewSeed(bramblePreviewSeed);
                }
            );

            Temporal_Sound_Music.Instance.PlaySound(defensePlacementAudioClip, 1f);
            CameraShake.Instance.SetCurrentStateCameraShake(4.0f, 5.5f, 0.2f);

            if (placed != null && DefensePlacementManager.Instance != null)
                DefensePlacementManager.Instance.RegisterPlaced(placed);

            OnCardUsed?.Invoke(this);
        }

        CleanupPreview();
        ResetAllStates();
    }

    private void ResetAllStates()
    {
        isFreeRotating = false;
        currentRotationDegrees = 0f;
        inPlacementMode = false;
        useFixedPosition = false;
    }

    private void CleanupPreview()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        currentPreviewIsValid = false;
    }

    private void EnterPlacementMode()
    {
        inPlacementMode = true;

        // Carta completamente invisible mientras estés en placement
        canvasGroup.alpha = 0f;

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
            if (b is BrambleDefense) continue;
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

        // if (cardImage != null)
            // cardImage.color = canUse ? Color.white : Color.gray;

        canvasGroup.interactable = canUse;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Si hay replacement callback
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
