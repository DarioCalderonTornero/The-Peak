// DragCardUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public CardData cardData;

    public Camera mainCamera;
    public Image iconImage;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI nameText;
    public Image worldSpriteImage;
    
    [Header("Counter")]
    public Image counterIconImage;
    public Image counterColorImage;

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
    [SerializeField] private AudioClip cardPointerAudioClip;

    // ✅ PERSISTENTES (se quedan aunque vuelvas a slots)
    [Header("Persisted Transform (per-card)")]
    [SerializeField] private float currentRotationDegrees = 0f;  // yaw relativo (se guarda)
    [SerializeField] private float currentScaleFactor = 1f;      // factor relativo (se guarda)

    private Vector3 lastHitNormal = Vector3.up;
    private int bramblePreviewSeed = 0;

    [Header("Preview Materials")]
    [SerializeField] private Material previewValidMaterial;   // verde
    [SerializeField] private Material previewInvalidMaterial; // rojo

    private bool currentPreviewIsValid = false;

    // --- Rotación libre (R) ---
    private bool isFreeRotating = false;
    private Vector3 freeRotatePivot;
    private Quaternion freeRotateBaseRotation;

    // --- Escala libre (mantener T) ---
    [Header("Scaling")]
    [SerializeField] private float minRelativeScale = 0.2f;
    [SerializeField] private float maxRelativeScale = 5f;
    [SerializeField] private float freeScaleSensitivity = 0.005f; // sensibilidad con mouse Y

    private bool isFreeScaling = false;
    private Vector3 freeScalePivot;
    private Quaternion freeScaleLockedRotation;
    private Vector3 freeScaleLockedNormal;
    private float freeScaleStartMouseY;
    private float freeScaleStartFactor;

    // escala ORIGINAL del prefab instanciado en preview
    private Vector3 originalPreviewScale;

    // Valores base para checks (no escalados)
    private Vector2 baseSupportCheckExtents;
    private float baseSupportRayDistance;
    private float baseSupportYOffset;
    private Vector3 basePlacementCheckExtents;

    [Header("Rejilla Global HDRP")]
    private UnityEngine.Rendering.HighDefinition.DecalProjector globalDecal;

    [SerializeField] private GameObject occupiedCellMarkerPrefab;
    [SerializeField] private int occupiedMarkerRange = 8; // radio alrededor del ratón

    private Vector3 lastGroundForward = Vector3.forward;


    private RaycastHit lastPlacementHit;
    private bool hasLastPlacementHit = false;

    private SegmentGridSettings activeSegment = null;
    private bool usingSegmentGrid = false;

    // cache del último footprint real
    private readonly List<CellKey> currentFootprintKeys = new List<CellKey>(32);

    /* [Header("Hover FX - Outline Cartoon")]
    [SerializeField] private CardOutlineController frontOutlineController;
    [SerializeField] private CardOutlineController backOutlineController;
    [SerializeField] private Color outlineHoverColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color outlineNormalColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float outlineHoverWidth = 6f;
    [SerializeField] private float outlineNormalWidth = 2f;
    [SerializeField] private float outlineDuration = 0.25f;

    [Header("Hover FX - Estrella Sparkle")]
    [SerializeField] private GameObject sparkleObject;

    [Header("Hover FX - Glow exterior (opcional)")]
    [SerializeField] private Image glowImage;
    [SerializeField] private float glowAlphaHover = 0.8f;

    private Coroutine outlineRoutine;
    private Coroutine glowRoutine;*/

    private bool isClickPlaceMode = false;
    public static bool AnyCardInClickPlaceMode = false;
    private bool _clickConsumedThisFrame = false;

    [Header("Grid Visualizer")]
    [SerializeField] private GameObject gridCellPrefab;
    [SerializeField] private Material gridValidMaterial;
    [SerializeField] private Material gridInvalidMaterial;

    private Vector3 lastValidPreviewPos;
    private Quaternion lastValidPreviewRot;
    private bool hasValidPreviewPos = false;


    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
        cardButton = GetComponent<Button>();

        // Desactivar el Glow en gameplay
        var glowTransform = transform.Find("Glow");
        if (glowTransform != null)
        {
            var glowImg = glowTransform.GetComponent<Image>();
            if (glowImg != null)
            {
                Color c = glowImg.color;
                c.a = 0f;
                glowImg.color = c;
            }
        }

        // ── Auto-detectar outlines si no están asignados ──
        /* if (frontOutlineController == null || backOutlineController == null)
        {
            var controllers = GetComponentsInChildren<CardOutlineController>(true);
            foreach (var c in controllers)
            {
                if (c.transform.IsChildOf(transform))
                {
                    // Asignar al frente o al reverso según el nombre del padre
                    if (frontOutlineController == null &&
                        c.transform.parent != null &&
                        c.transform.parent.parent != null &&
                        c.transform.parent.parent.name.ToLower().Contains("front"))
                        frontOutlineController = c;
                    else if (backOutlineController == null)
                        backOutlineController = c;
                }
            }
        }

        if (sparkleObject != null) sparkleObject.SetActive(false);

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = 0f;
            glowImage.color = c;
        }*/
    }

    private void Start()
    {
        SetupCardUI();
        UpdateInteractable();

        if (PointsManager.Instance != null)
            PointsManager.Instance.OnPointsChanged += HandlePointsChanged;

        if (InputManager.Instance != null)
            InputManager.Instance.OnRotateCardInput += InputManager_OnRotateCardInput;

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

        if (worldSpriteImage != null && cardData.worldSprite != null)
            worldSpriteImage.sprite = cardData.worldSprite;

        if (rectTransform != null)
            rectTransform.anchoredPosition = new Vector2(originalPosition.x, originalPosition.y);

        // ── NUEVO: configurar el reverso ──
        var backSetup = GetComponentInChildren<CardBackSetup>(true);
        if (backSetup != null)
            backSetup.Setup(cardData);

        if (counterIconImage != null && cardData.counterIcon != null)
            counterIconImage.sprite = cardData.counterIcon;

        if (counterColorImage != null && cardData.counterColorSprite != null)
            counterColorImage.sprite = cardData.counterColorSprite;
    }

    public void SetupVisualOnly()
    {
        if (cardData == null) return;
        if (iconImage != null) iconImage.sprite = cardData.icon;
        if (costText != null) costText.text = cardData.cost.ToString();
        if (nameText != null) nameText.text = cardData.cardName;
        if (worldSpriteImage != null && cardData.worldSprite != null)
            worldSpriteImage.sprite = cardData.worldSprite;

        var backSetup = GetComponentInChildren<CardBackSetup>(true);
        if (backSetup != null)
            backSetup.Setup(cardData);
    }

    private void HandlePointsChanged(int points) => UpdateInteractable();

    // Toggle rotación libre con evento (R)
    private void InputManager_OnRotateCardInput(object sender, System.EventArgs e)
    {
        if (!inPlacementMode || useFixedPosition || previewInstance == null)
            return;

        if (isFreeScaling)
            return;

        if (isFreeRotating)
        {
            isFreeRotating = false;
            return;
        }

        isFreeRotating = true;
        freeRotatePivot = previewInstance.transform.position;
        freeRotateBaseRotation = previewInstance.transform.rotation;

        // ✅ OJO: NO reseteamos currentRotationDegrees (persistente)
        // Queremos seguir desde el valor acumulado.
    }

    private void Update()
    {
        // ── Modo click-to-place — SIEMPRE primero ────────────────────
        if (isClickPlaceMode)
        {
            if (Input.GetMouseButtonDown(1))
            {
                CancelClickPlaceMode();
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask)
                && hit.normal.y >= 0.4f)
            {
                if (!inPlacementMode)
                    EnterPlacementMode();

                hasLastPlacementHit = true;
                lastPlacementHit = hit;

                if (previewInstance == null)
                {
                    previewInstance = Instantiate(cardData.defensePrefab);
                    DisablePreviewLogic(previewInstance);
                    originalPreviewScale = previewInstance.transform.localScale;
                    if (originalPreviewScale.sqrMagnitude == 0)
                        originalPreviewScale = Vector3.one;

                    var visualizer = previewInstance.AddComponent<RuntimeGridVisualizer>();
                    visualizer.Setup(cardData.gridSize, gridCellPrefab, gridValidMaterial, gridInvalidMaterial);
                    ApplyScaleFactorToPreview(currentScaleFactor);
                    currentPreviewIsValid = false;
                    ApplyPreviewMaterial(false);
                }

                UpdatePlacementFromHit(hit);

                // ── Marcadores de casillas (igual que en OnDrag) ──────────────
                if (AvailableCellMarkerManager.Instance != null && previewInstance != null)
                {
                    AvailableCellMarkerManager.Instance.UpdateAvailableAround(
                        centerWorldPos: previewInstance.transform.position,
                        range: occupiedMarkerRange,
                        segmentSearchRadius: occupiedMarkerRange * 2f,
                        placementMask: placementMask,
                        maskData: PlacementMaskManager.Instance != null
                            ? PlacementMaskManager.Instance.data
                            : null
                    );
                }

                if (OccupiedCellMarkerManager.Instance != null)
                {
                    if (usingSegmentGrid && activeSegment != null)
                    {
                        OccupiedCellMarkerManager.Instance.UpdateMarkersAroundSegment(
                            activeSegment,
                            previewInstance.transform.position,
                            occupiedMarkerRange,
                            placementMask
                        );
                    }
                    else
                    {
                        OccupiedCellMarkerManager.Instance.UpdateMarkersAroundWorld(
                            previewInstance.transform.position,
                            occupiedMarkerRange,
                            placementMask
                        );
                    }
                }
            }
            else if (inPlacementMode)
            {
                CleanupPreview();
                inPlacementMode = false;
                canvasGroup.alpha = 0.5f;
                canvasGroup.blocksRaycasts = false;
            }

            if (Input.GetMouseButtonDown(0) && inPlacementMode && previewInstance != null)
            {
                _clickConsumedThisFrame = true;
                TryPlaceFromClickMode();
            }

            // Rotación con R también en click mode
            if (Input.GetKeyDown(KeyCode.R) && inPlacementMode
                && previewInstance != null && !useFixedPosition)
            {
                currentRotationDegrees += 90f;
                if (currentRotationDegrees >= 360f) currentRotationDegrees = 0f;
                ApplyRotationFromNormalAndYaw();
                if (hasLastPlacementHit)
                    UpdatePlacementFromHit(lastPlacementHit);
            }

            return;
        }

        // ── Modo drag normal ─────────────────────────────────────────
        if (!inPlacementMode || previewInstance == null || useFixedPosition) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationDegrees += 90f;
            if (currentRotationDegrees >= 360f) currentRotationDegrees = 0f;
            ApplyRotationFromNormalAndYaw();
            if (hasLastPlacementHit)
                UpdatePlacementFromHit(lastPlacementHit);
        }
    }

    private void TryPlaceFromClickMode()
    {
        Vector3 finalPosition = previewInstance.transform.position;
        Quaternion finalRotation = previewInstance.transform.rotation;
        Vector3 finalNormal = lastHitNormal;

        string placementReason = CheckPlacementValidity(finalPosition, finalNormal, finalRotation);
        if (placementReason != "Válido") { StartCoroutine(ShakeCard()); return; }

        bool placedOk = PointsManager.Instance != null && PointsManager.Instance.SpendPoints(cardData.cost);
        if (!placedOk) { StartCoroutine(ShakeCard()); return; }

        Vector3 finalScale = originalPreviewScale * currentScaleFactor;

        // Guardar yaw ANTES de que PlaceDefense destruya el preview
        float cloudPreviewYaw = 0f;
        var cloudPreviewComp = previewInstance.GetComponent<CloudDefensePreview>();
        Debug.Log($"[Cloud] cloudPreviewComp={cloudPreviewComp} | yaw={cloudPreviewComp?.GetCurrentYaw()}");
        if (cloudPreviewComp != null)
            cloudPreviewYaw = cloudPreviewComp.GetCurrentYaw();

        GameObject placed = DefensePlacer.Instance.PlaceDefense(
            cardData.defensePrefab, finalPosition, finalRotation,
            beforeInitialize: (go) => { },
            afterInitialize: (go) =>
            {
                var cloudDefense = go.GetComponent<CloudKillDefense>();
                if (cloudDefense != null)
                {
                    cloudDefense.SetPlacedYaw(cloudPreviewYaw);
                    cloudDefense.Initialize();
                }

                var lodo = go.GetComponent<LodoDefense>();
                if (lodo != null) { lodo.ApplyExternalScale(finalScale); return; }

                var arena = go.GetComponent<QuicksandDefense>();
                if (arena != null) { arena.ApplyExternalScale(finalScale); return; }

                go.transform.localScale = finalScale;

                if (cardData.cardType == CardData.CardType.Temporal)
                {
                    var temp = go.AddComponent<TemporaryDefense>();
                    temp.Initialize(cardData.temporalTurns);
                }
            }
        );

        if (placed == null)
        {
            if (PointsManager.Instance != null) PointsManager.Instance.AddPoints(cardData.cost);
            StartCoroutine(ShakeCard());
            CancelClickPlaceMode();
            return;
        }

        if (GridOccupancyManager.Instance != null)
        {
            var occ = placed.GetComponent<GridOccupant>();
            if (occ == null) occ = placed.AddComponent<GridOccupant>();
            occ.Init(new List<CellKey>(currentFootprintKeys));
        }

        Temporal_Sound_Music.Instance.PlaySound(defensePlacementAudioClip, 1f);
        CameraShake.Instance.ShakeMainCamera(4.0f, 5.5f, 0.2f);
        if (DefensePlacementManager.Instance != null) DefensePlacementManager.Instance.RegisterPlaced(placed);

        OnCardUsed?.Invoke(this);
        ResetPersistentTransform();
        CleanupPreview();
        ResetTransientStates();
        AnyCardInClickPlaceMode = false;
        isClickPlaceMode = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
    }

    private void ApplyRotationFromNormalAndYaw()
    {
        if (previewInstance == null) return;

        Vector3 up = lastHitNormal;

        // Yaw en ejes del mundo (0/90/180/270) -> coherente con casillas
        Quaternion yawWorld = Quaternion.Euler(0f, currentRotationDegrees, 0f);

        // Forward del mundo rotado por yaw, proyectado en el plano de la normal
        Vector3 desiredFwd = yawWorld * Vector3.forward;
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(desiredFwd, up).normalized;

        if (fwdOnPlane.sqrMagnitude < 0.0001f)
        {
            // fallback
            desiredFwd = yawWorld * Vector3.right;
            fwdOnPlane = Vector3.ProjectOnPlane(desiredFwd, up).normalized;
        }

        previewInstance.transform.rotation = Quaternion.LookRotation(fwdOnPlane, up);
    }

    private void UpdateFreeRotation()
    {
        if (previewInstance == null) return;

        // Mantener posición clavada
        previewInstance.transform.position = freeRotatePivot;

        float mouseDelta = Input.GetAxis("Mouse X") * 6.5f;
        currentRotationDegrees += mouseDelta;
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees, 360f);

        Vector3 localUp = freeRotateBaseRotation * Vector3.up;
        Quaternion extraY = Quaternion.AngleAxis(currentRotationDegrees, localUp);

        previewInstance.transform.rotation = extraY * freeRotateBaseRotation;
    }

    // Mantener T: clava pos/rot/normal y escala con mouse Y
    private void HandleFreeScaling()
    {
        if (!inPlacementMode || previewInstance == null || useFixedPosition)
            return;

        if (Input.GetKeyDown(KeyCode.T))
        {
            isFreeScaling = true;

            // Mientras escalas, no rotas
            isFreeRotating = false;

            freeScalePivot = previewInstance.transform.position;
            freeScaleLockedRotation = previewInstance.transform.rotation;
            freeScaleLockedNormal = lastHitNormal;

            freeScaleStartMouseY = Input.mousePosition.y;
            freeScaleStartFactor = currentScaleFactor;
        }

        if (Input.GetKeyUp(KeyCode.T))
        {
            isFreeScaling = false;
            return;
        }

        if (!isFreeScaling)
            return;

        // Clavado
        previewInstance.transform.position = freeScalePivot;
        previewInstance.transform.rotation = freeScaleLockedRotation;
        lastHitNormal = freeScaleLockedNormal;

        float deltaY = (Input.mousePosition.y - freeScaleStartMouseY);
        float factor = freeScaleStartFactor * (1f + deltaY * freeScaleSensitivity);
        currentScaleFactor = Mathf.Clamp(factor, minRelativeScale, maxRelativeScale);

        ApplyScaleFactorToPreview(currentScaleFactor);

        // actualizar valid/invalid
        string validityReason = CheckPlacementValidity(freeScalePivot, lastHitNormal, previewInstance.transform.rotation);
        bool isValid = validityReason == "Válido";

        currentPreviewIsValid = isValid;
        ApplyPreviewMaterial(isValid);
    }

    private void ApplyScaleFactorToPreview(float factor)
    {
        if (previewInstance == null) return;

        // Evita que el objeto desaparezca por escala 0
        float safeFactor = Mathf.Max(factor, 0.1f);
        previewInstance.transform.localScale = originalPreviewScale * safeFactor;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (AnyCardInClickPlaceMode) return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        isHovering = false;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);

        if (PointsManager.Instance != null && !PointsManager.Instance.CanAfford(cardData.cost))
        {
            canDragThisTime = false;
            StartCoroutine(ShakeCard());
            return;
        }

        canDragThisTime = true;
        canvasGroup.blocksRaycasts = false;

        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        dragStartY = rectTransform.anchoredPosition.y;

        ResetTransientStates();
        CleanupPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        Vector2 newPos = rectTransform.anchoredPosition;
        newPos.y += eventData.delta.y / canvas.scaleFactor;
        // newPos.x = originalPosition.x;
        rectTransform.anchoredPosition = newPos;

        float distanceUp = rectTransform.anchoredPosition.y - dragStartY;
        float alpha = Mathf.Clamp01(1f - (distanceUp / placementThreshold));
        canvasGroup.alpha = alpha;

        bool overUI = IsPointerOverUI();

        if (!inPlacementMode && distanceUp >= placementThreshold && !overUI)
            EnterPlacementMode();

        if (inPlacementMode && overUI)
        {
            CleanupPreview();
            inPlacementMode = false;
            useFixedPosition = false;
            currentPreviewIsValid = false;
            isFreeRotating = false;
            isFreeScaling = false;
            usingSegmentGrid = false;
            activeSegment = null;
            currentFootprintKeys.Clear();

            if (OccupiedCellMarkerManager.Instance != null)
                OccupiedCellMarkerManager.Instance.HideAll();

            if (AvailableCellMarkerManager.Instance != null)
                AvailableCellMarkerManager.Instance.HideAll();
            return;
        }

        // Si no estamos en el modo de colocación, o estamos escalando o usando una posición fija, no hacer nada
        if (!inPlacementMode || isFreeScaling || useFixedPosition)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
            return;

        // 🔥 NUEVA REGLA: Si la superficie a la que apuntamos es una pared vertical (lateral), la ignoramos.
        // hit.normal.y vale 1.0 en suelo plano y 0.0 en una pared totalmente vertical.
        // 0.4f permite rampas inclinadas de hasta unos 65 grados, pero ignora paredes.
        if (hit.normal.y < 0.4f)
            return;

        // Guardamos el último hit para realizar un resnap si giramos sin mover el ratón
        hasLastPlacementHit = true;
        lastPlacementHit = hit;

        // Crear el preview si aún no existe
        if (previewInstance == null)
        {
            previewInstance = Instantiate(cardData.defensePrefab);
            DisablePreviewLogic(previewInstance);

            originalPreviewScale = previewInstance.transform.localScale;
            if (originalPreviewScale.sqrMagnitude == 0) originalPreviewScale = Vector3.one;

            var visualizer = previewInstance.AddComponent<RuntimeGridVisualizer>();
            visualizer.Setup(cardData.gridSize, gridCellPrefab, gridValidMaterial, gridInvalidMaterial);

            ApplyScaleFactorToPreview(currentScaleFactor);

            currentPreviewIsValid = false;
            ApplyPreviewMaterial(false);

        }

        // Actualizar la colocación según el hit
        UpdatePlacementFromHit(hit);

        if (AvailableCellMarkerManager.Instance != null && previewInstance != null)
        {
            float segRadius = occupiedMarkerRange * 2f;

            AvailableCellMarkerManager.Instance.UpdateAvailableAround(
                centerWorldPos: previewInstance.transform.position,
                range: occupiedMarkerRange,
                segmentSearchRadius: segRadius,
                placementMask: placementMask,
                maskData: (PlacementMaskManager.Instance != null)
                    ? PlacementMaskManager.Instance.data
                    : null
            );
        }

        // OCCUPIED (esto sí puede seguir usando segment o global)
        if (OccupiedCellMarkerManager.Instance != null)
        {
            if (usingSegmentGrid && activeSegment != null)
            {
                OccupiedCellMarkerManager.Instance.UpdateMarkersAroundSegment(
                    activeSegment,
                    previewInstance.transform.position,
                    occupiedMarkerRange,
                    placementMask
                );
            }
            else
            {
                OccupiedCellMarkerManager.Instance.UpdateMarkersAroundWorld(
                    previewInstance.transform.position,
                    occupiedMarkerRange,
                    placementMask
                );
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        // Animar vuelta a posición original en diagonal
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(AnimateHoverLift(false));

        // Si sueltas encima de UI -> no colocar
        if (IsPointerOverUI())
        {
            CleanupPreview();
            ResetTransientStates();
            return;
        }

        StartCoroutine(AnimateHoverLift(false));

        if (!inPlacementMode || previewInstance == null)
        {
            CleanupPreview();
            ResetTransientStates();
            return;
        }

        // 🔥 SIEMPRE usar preview real
        Vector3 finalPosition = previewInstance.transform.position;
        Quaternion finalRotation = previewInstance.transform.rotation;
        Vector3 finalNormal = lastHitNormal;

        Debug.Log("PREVIEW POS: " + previewInstance.transform.position);
        Debug.Log("FINAL POS: " + finalPosition);

        // ✅ Validación FINAL (ya con grid occupancy)
        string placementReason = CheckPlacementValidity(finalPosition, finalNormal, finalRotation);
        if (placementReason != "Válido")
        {
            StartCoroutine(ShakeCard());
            CleanupPreview();
            ResetTransientStates();
            return;
        }

        // ✅ Cobrar puntos
        bool placedOk = PointsManager.Instance != null && PointsManager.Instance.SpendPoints(cardData.cost);
        if (!placedOk)
        {
            StartCoroutine(ShakeCard());
            CleanupPreview();
            ResetTransientStates();
            return;
        }

        // ✅ Escala final
        Vector3 finalScale = originalPreviewScale * currentScaleFactor;

        // Guardar yaw ANTES de que PlaceDefense destruya el preview
        float cloudPreviewYaw = 0f;
        var cloudPreviewComp = previewInstance.GetComponent<CloudDefensePreview>();
        Debug.Log($"[Cloud] cloudPreviewComp={cloudPreviewComp} | yaw={cloudPreviewComp?.GetCurrentYaw()}");
        if (cloudPreviewComp != null)
            cloudPreviewYaw = cloudPreviewComp.GetCurrentYaw();

        GameObject placed = DefensePlacer.Instance.PlaceDefense(
    cardData.defensePrefab, finalPosition, finalRotation,
    beforeInitialize: (go) => { },
    afterInitialize: (go) =>
    {
        var cloudDefense = go.GetComponent<CloudKillDefense>();
        if (cloudDefense != null)
        {
            cloudDefense.SetPlacedYaw(cloudPreviewYaw);
            cloudDefense.Initialize();
        }

        var lodo = go.GetComponent<LodoDefense>();
        if (lodo != null) { lodo.ApplyExternalScale(finalScale); return; }

        var arena = go.GetComponent<QuicksandDefense>();
        if (arena != null) { arena.ApplyExternalScale(finalScale); return; }

        var log = go.GetComponent<RollingLogDefense>();
        if (log != null)
        {
            if (usingSegmentGrid && activeSegment != null && currentFootprintKeys.Count > 0)
            {
                var key = currentFootprintKeys[0];
                var seg = SegmentRegistry.Get(key.segmentId);
                if (seg != null) log.InitializeFromPlacement(seg, new Vector2Int(key.x, key.y));
            }
        }

        go.transform.localScale = finalScale;

        if (cardData.cardType == CardData.CardType.Temporal)
        {
            var temp = go.AddComponent<TemporaryDefense>();
            temp.Initialize(cardData.temporalTurns);
        }
    }
);
        Debug.Log("PLACED POS: " + placed.transform.position);


        // ✅ Si por cualquier razón no se instanció, devolvemos puntos y salimos
        if (placed == null)
        {
            // Si no tienes AddPoints, quita esto
            if (PointsManager.Instance != null)
                PointsManager.Instance.AddPoints(cardData.cost);

            StartCoroutine(ShakeCard());
            CleanupPreview();
            ResetTransientStates();
            return;
        }

        // ✅ REGISTRAR OCUPACIÓN POR CASILLAS (NO MESH)
        if (GridOccupancyManager.Instance != null)
        {
            var cells = ComputeFootprintCells(finalPosition, finalRotation, cardData.gridSize);

            // currentFootprintKeys ya tiene lo real (segmento o global)
            var occ = placed.GetComponent<GridOccupant>();
            if (occ == null) occ = placed.AddComponent<GridOccupant>();
            occ.Init(new List<CellKey>(currentFootprintKeys));
        }

        Temporal_Sound_Music.Instance.PlaySound(defensePlacementAudioClip, 1f);
        CameraShake.Instance.ShakeMainCamera(4.0f, 5.5f, 0.2f);

        if (DefensePlacementManager.Instance != null)
            DefensePlacementManager.Instance.RegisterPlaced(placed);

        OnCardUsed?.Invoke(this);

        ResetPersistentTransform();

        CleanupPreview();
        ResetTransientStates();
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cg.alpha = to;
    }

    private void EnterPlacementMode()
    {
        inPlacementMode = true;
        // ✅ NUEVO: Buscar el decal en la escena por su Tag
        if (globalDecal == null)
        {
            GameObject decalObj = GameObject.FindWithTag("GridDecal");
            if (decalObj != null)
            {
                globalDecal = decalObj.GetComponent<UnityEngine.Rendering.HighDefinition.DecalProjector>();
            }
        }

        if (globalDecal != null)
            globalDecal.enabled = true;
        // canvasGroup.alpha = 0f;

        lastHitNormal = Vector3.up;
        currentPreviewIsValid = false;

        if (cardData != null)
        {
            baseSupportCheckExtents = cardData.supportCheckExtents;
            baseSupportRayDistance = cardData.supportRayDistance;
            baseSupportYOffset = cardData.supportYOffset;
            basePlacementCheckExtents = cardData.placementCheckExtents;
        }

        if (cardData != null && cardData.hasFixedPlacement)
        {
            useFixedPosition = true;
            fixedPlacementPosition = cardData.fixedPosition;

            if (previewInstance == null)
            {
                previewInstance = Instantiate(cardData.defensePrefab);
                DisablePreviewLogic(previewInstance);

                originalPreviewScale = previewInstance.transform.localScale;

                // ✅ aplicar persistentes al crear
                ApplyScaleFactorToPreview(currentScaleFactor);
            }

            previewInstance.transform.position = fixedPlacementPosition;

            // ✅ aplicar rot persistente también en fixed
            ApplyRotationFromNormalAndYaw();
        }
    }

    // ✅ Reset SOLO flags y placement state (NO rot/scale)
    private void ResetTransientStates()
    {
        isFreeRotating = false;
        isFreeScaling = false;

        inPlacementMode = false;
        useFixedPosition = false;

        currentPreviewIsValid = false;
        lastHitNormal = Vector3.up;
    }

    // ✅ Reset de lo que quieres “olvidar” al colocar (o cuando quieras)
    private void ResetPersistentTransform()
    {
        currentRotationDegrees = 0f;
        currentScaleFactor = 1f;
    }

    private void CleanupPreview()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        if (globalDecal != null) globalDecal.enabled = false;
        currentPreviewIsValid = false;
        if (OccupiedCellMarkerManager.Instance != null)
            OccupiedCellMarkerManager.Instance.HideAll();

        if (AvailableCellMarkerManager.Instance != null)
            AvailableCellMarkerManager.Instance.HideAll();
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private string CheckPlacementValidity(Vector3 position, Vector3 normal, Quaternion rotation)
    {
        // Pendiente
        float slopeAngle = Vector3.Angle(Vector3.up, normal);

        if (cardData != null && cardData.cardName == "Tronco") // o usa un flag mejor
        {
            if (slopeAngle < 5f)
                return "Solo se puede colocar en rampas";
        }

        if (slopeAngle > 61f)
            return "Pendiente demasiado pronunciada";

        // Necesitamos footprint calculado (segmento o mundo)
        if (currentFootprintKeys == null || currentFootprintKeys.Count == 0)
            return "Fuera del suelo";

        // ✅ Si estamos en segmento, valida con SegmentGridSettings (no con PlacementMaskData)
        if (usingSegmentGrid)
        {
            for (int k = 0; k < currentFootprintKeys.Count; k++)
            {
                var key = currentFootprintKeys[k];

                var seg = SegmentRegistry.Get(key.segmentId);
                if (seg == null)
                    return "Fuera del segmento";

                if (!seg.InBounds(key.x, key.y))
                    return "Fuera del segmento";

                if (seg.IsBlocked(key.x, key.y))
                    return "Zona bloqueada";

                if (!HasPerfectGroundContact(position, rotation, cardData.gridSize))
                    return "Mal contacto con el suelo";
            }
        }
        else
        {
            // GLOBAL: aquí sí usamos tu máscara global (PlacementMaskData)
            if (PlacementMaskManager.Instance != null)
            {
                // convertir keys (segId=0) a Vector2Int
                var tmp = new List<Vector2Int>(currentFootprintKeys.Count);
                for (int i = 0; i < currentFootprintKeys.Count; i++)
                    tmp.Add(new Vector2Int(currentFootprintKeys[i].x, currentFootprintKeys[i].y));

                if (!PlacementMaskManager.Instance.AreBuildable(tmp))
                    return "Restricted area";
            }

            // Y si quieres mantener el “suelo completo” del modo global:
            if (!HasFullFootprintGround(position, rotation, cardData.gridSize))
                return "Off the ground";
        }

        // Ocupación (segmento o mundo)
        if (GridOccupancyManager.Instance != null)
        {
            if (GridOccupancyManager.Instance.AnyOccupied(currentFootprintKeys))
                return "Checkbox selected";
        }

        // Soporte completo
        if (cardData != null && cardData.requireFullSupport)
        {
            if (!HasFullSupport(position, rotation))
                return "Quedaría flotando";
        }

        return "Válido";
    }

    private bool HasFullSupport(Vector3 center, Quaternion rotation)
    {
        if (cardData == null || !cardData.requireFullSupport)
            return true;

        Vector2 ext = baseSupportCheckExtents * currentScaleFactor;
        float maxDist = baseSupportRayDistance * currentScaleFactor;
        float yOff = baseSupportYOffset * currentScaleFactor;

        Vector3 surfaceNormal = rotation * Vector3.up;
        Vector3 supportCenter = center + surfaceNormal * yOff;

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
            Vector3 worldOffset = rotation * local;
            Vector3 origin = supportCenter + worldOffset;
            Vector3 dir = -surfaceNormal;

            if (!Physics.Raycast(origin, dir, maxDist, placementMask))
                return false;
        }

        return true;
    }

    private bool HasPerfectGroundContact(Vector3 position, Quaternion rotation, Vector2Int gridSize)
    {
        const float rayHeight = 5f;
        const float maxVerticalTolerance = 0.15f; // 🔥 MUCHO más estricto

        float startX = -(gridSize.x / 2f) + 0.5f;
        float startZ = -(gridSize.y / 2f) + 0.5f;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 local = new Vector3(startX + x, 0f, startZ + z);
                Vector3 worldCenter = position + rotation * local;

                Vector3 rayOrigin = worldCenter + Vector3.up * rayHeight;

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayHeight * 2f, placementMask))
                    return false;

                // 🔥 CLAVE: diferencia vertical REAL
                float verticalDelta = Mathf.Abs(worldCenter.y - hit.point.y);

                if (verticalDelta > maxVerticalTolerance)
                    return false;
            }
        }

        return true;
    }

    private void ApplyPreviewMaterial(bool valid)
    {
        if (previewInstance == null)
            return;

        var renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        Material mat = valid ? previewValidMaterial : previewInvalidMaterial;

        foreach (var r in renderers)
        {
            // NO cambiar el material si es el visualizador de líneas
            if (r is LineRenderer) continue;

            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
        }

        // Sincronizar el color de la grilla de casillas
        var visualizer = previewInstance.GetComponent<RuntimeGridVisualizer>();
        if (visualizer != null)
        {
            visualizer.SetColor(valid ? Color.blue : Color.red);
        }
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
            if (b is CloudDefensePreview) continue;
            Destroy(b);
        }

        foreach (var col in preview.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        /* if (cardData == null || cardData.previewMaterial == null)
            return;

        var renderers = preview.GetComponentsInChildren<Renderer>(true);
        foreach (var rend in renderers)
        {
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = cardData.previewMaterial;
            rend.materials = mats;
        }*/
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
        canvasGroup.interactable = canUse;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_clickConsumedThisFrame) return;
        if (AnyCardInClickPlaceMode && !isClickPlaceMode) return;
        // Click derecho → cancelar modo click-to-place
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isClickPlaceMode)
                CancelClickPlaceMode();
            return;
        }

        // Doble click izquierdo → activar modo click-to-place
        if (eventData.button == PointerEventData.InputButton.Left
            && eventData.clickCount == 2)
        {
            if (!isClickPlaceMode)
                EnterClickPlaceMode();
            return;
        }

        // Click simple izquierdo → callback de selección (comportamiento anterior)
        if (eventData.button == PointerEventData.InputButton.Left
            && eventData.clickCount == 1)
        {
            replacementCallback?.Invoke(this);
        }
    }

    private void LateUpdate()
    {
        _clickConsumedThisFrame = false;
    }

    private void EnterClickPlaceMode()
    {
        if (PointsManager.Instance != null && !PointsManager.Instance.CanAfford(cardData.cost))
        {
            StartCoroutine(ShakeCard());
            return;
        }

        AnyCardInClickPlaceMode = true;
        isClickPlaceMode = true;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.5f;
        ResetTransientStates();
        CleanupPreview();
    }

    private void CancelClickPlaceMode()
    {
        AnyCardInClickPlaceMode = false;
        isClickPlaceMode = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        CleanupPreview();
        ResetTransientStates();
    }

    [Header("Hover Animation")]
    [SerializeField] private float hoverLiftY = 120f;   // cuánto sube (px de canvas)
    [SerializeField] private float hoverDuration = 0.2f;
    [HideInInspector] public float handRotationAngle = 0f;

    private Coroutine hoverRoutine;
    private bool isHovering = false;
    private float dragStartY;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AnyCardInClickPlaceMode && !isClickPlaceMode) return;
        if (isHovering) return;
        isHovering = true;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(AnimateHoverLift(true));
        Temporal_Sound_Music.Instance.Play2DSound(cardPointerAudioClip, 0.3f);
        // ── NUEVO: Activar efectos hover ──
        TriggerHoverFX(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isHovering) return;
        isHovering = false;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(AnimateHoverLift(false));

        // ── NUEVO: Desactivar efectos hover ──
        TriggerHoverFX(false);
    }

    /* private void SetupOutline(Outline outline)
    {
        if (outline == null) return;
        outline.effectColor = outlineNormalColor;
        outline.effectDistance = new Vector2(outlineNormalWidth, outlineNormalWidth);
    }*/

    
    private void TriggerHoverFX(bool active)
    {
        /* frontOutlineController?.SetHover(active);
        backOutlineController?.SetHover(active);

        if (glowImage != null) {  }
        if (sparkleObject != null) sparkleObject.SetActive(active);*/
    }


    /*private IEnumerator AnimateGlow(float fromA, float toA, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float n = Mathf.Clamp01(t / dur);
            Color c = glowImage.color;
            c.a = Mathf.Lerp(fromA, toA, n);
            glowImage.color = c;
            yield return null;
        }
        Color fc = glowImage.color;
        fc.a = toA;
        glowImage.color = fc;
    }*/

    private IEnumerator AnimateHoverLift(bool lifting)
    {
        Vector2 startPos = rectTransform.anchoredPosition;

        Vector2 liftDir;
        if (Mathf.Abs(handRotationAngle) > 0.1f)
        {
            // Dirección en que apunta la carta según su rotación
            float rad = handRotationAngle * Mathf.Deg2Rad;
            liftDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        }
        else
        {
            liftDir = Vector2.up;
        }

        Vector2 targetPos = lifting
            ? originalPosition + liftDir * hoverLiftY
            : originalPosition;

        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / hoverDuration;
            float smooth = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, smooth);
            yield return null;
        }
        rectTransform.anchoredPosition = targetPos;
    }

    private Vector3 GetSnappedPosition(Vector3 hitPoint, int width, int height, Quaternion rotation)
    {
        // 1. Determinar dimensiones actuales según la rotación
        // Si rotamos 90 o 270 grados, invertimos ancho y alto
        bool rotated = Mathf.RoundToInt(rotation.eulerAngles.y) % 180 != 0;
        int currentWidth = rotated ? height : width;
        int currentHeight = rotated ? width : height;

        // 2. Calcular el offset para centrar
        // Si es par (2, 4), necesitamos offset de 0.5. Si es impar (1, 3), offset de 0.
        float offsetX = (currentWidth % 2 == 0) ? 0.5f : 0f;
        float offsetZ = (currentHeight % 2 == 0) ? 0.5f : 0f;

        // 3. Redondear a la unidad más cercana y aplicar offset
        float x = Mathf.Round(hitPoint.x - offsetX) + offsetX;
        float z = Mathf.Round(hitPoint.z - offsetZ) + offsetZ;

        // Mantenemos la Y del punto de impacto (o fíjalo a 0 si tu suelo es plano)
        return new Vector3(x, hitPoint.y, z);
    }

    // Estructura para devolver posición y normal ajustadas
    private struct GridPlacementInfo
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 groundForward;
        public bool hitFound;
    }

    private GridPlacementInfo GetSnappedInfoOnRampa(Vector3 originalHitPoint, int width, int height)
    {
        GridPlacementInfo result = new GridPlacementInfo();
        result.hitFound = false;

        // 1. Determinar offset según si el tamaño es Par o Impar (para centrar en grilla Unity)
        // Si rotamos 90 grados (en Y), invertimos ancho/alto para el cálculo
        bool rotated = Mathf.RoundToInt(currentRotationDegrees) % 180 != 0;
        int currentWidth = rotated ? height : width;
        int currentHeight = rotated ? width : height;

        float offsetX = (currentWidth % 2 == 0) ? 0.5f : 0f;
        float offsetZ = (currentHeight % 2 == 0) ? 0.5f : 0f;

        // 2. Calcular X y Z de la grilla (sin tocar Y todavía)
        float snappedX = Mathf.Round(originalHitPoint.x - offsetX) + offsetX;
        float snappedZ = Mathf.Round(originalHitPoint.z - offsetZ) + offsetZ;

        // 3. RE-PROYECCIÓN: Lanzar rayo desde arriba en la coordenada X/Z calculada
        // Subimos 10 unidades desde el punto original para asegurar que estamos sobre el suelo
        Vector3 rayOrigin = new Vector3(snappedX, originalHitPoint.y + 10f, snappedZ);
        Ray ray = new Ray(rayOrigin, Vector3.down);

        // Usamos placementMask para detectar solo el suelo válido
        if (Physics.Raycast(ray, out RaycastHit hitInfo, 50f, placementMask))
        {
            result.position = hitInfo.point;
            result.normal = hitInfo.normal;
            result.hitFound = true;

            // ✅ Forward del segmento golpeado (yaw base)
            Transform seg = hitInfo.collider.transform;

            // Proyectamos seg.forward sobre el plano de la normal para quitar componente vertical
            Vector3 fwd = Vector3.ProjectOnPlane(seg.forward, hitInfo.normal).normalized;

            // Fallback por si forward es casi paralelo a la normal (caso raro)
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.ProjectOnPlane(seg.right, hitInfo.normal).normalized;

            // Último fallback
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.ProjectOnPlane(Vector3.forward, hitInfo.normal).normalized;

            result.groundForward = fwd;
        }
        else
        {
            result.position = new Vector3(snappedX, originalHitPoint.y, snappedZ);
            result.normal = Vector3.up;
            result.groundForward = Vector3.forward; // <-- NUEVO
        }

        return result;
    }

    /* private IEnumerator MoveCardSmooth(Vector2 from, Vector2 to)
    {
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * hoverSpeed;
            rectTransform.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        rectTransform.anchoredPosition = to;
    }*/

    [Header("Configuración Visual de Grilla")]
    [SerializeField] private bool showGlobalGrid = true;
    [SerializeField] private int gridViewRange = 6; // Radio de casillas visibles alrededor del ratón

    private void OnDrawGizmos()
    {
        // Solo dibujamos si estamos en modo placement y el juego corre
        if (!Application.isPlaying || !inPlacementMode || mainCamera == null) return;

        // --- PARTE 1: REJILLA GLOBAL (Suelo de la montaña) ---
        // Lanzamos un rayo desde la cámara al ratón para saber dónde centrar la rejilla global
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit centerHit, 1000f, placementMask))
        {
            // Guardamos la matriz original para no afectar a otros Gizmos de Unity
            Matrix4x4 originalMatrix = Gizmos.matrix;

            int centerX = Mathf.RoundToInt(centerHit.point.x);
            int centerZ = Mathf.RoundToInt(centerHit.point.z);

            for (int x = -gridViewRange; x <= gridViewRange; x++)
            {
                for (int z = -gridViewRange; z <= gridViewRange; z++)
                {
                    Vector3 cellCheckPos = new Vector3(centerX + x, centerHit.point.y + 5f, centerZ + z);

                    // Solo dibujamos la casilla si hay suelo de "Mountain" debajo
                    if (Physics.Raycast(cellCheckPos, Vector3.down, out RaycastHit cellHit, 15f, placementMask))
                    {
                        // Inclinamos cada casilla individualmente según la normal del terreno
                        Vector3 up = cellHit.normal;

                        // Forward base del segmento (del collider que has hitteado)
                        Transform seg = cellHit.collider.transform;

                        Vector3 fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
                        if (fwd.sqrMagnitude < 0.0001f)
                            fwd = Vector3.ProjectOnPlane(Vector3.right, up).normalized;

                        // Último fallback
                        if (fwd.sqrMagnitude < 0.0001f)
                            fwd = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;

                        // Rotación completa: yaw del segmento + inclinación por normal
                        Quaternion rot = Quaternion.LookRotation(fwd, up);

                        Gizmos.matrix = Matrix4x4.TRS(
                            cellHit.point + up * 0.005f,
                            rot,
                            Vector3.one
                        );

                        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.9f, 0f, 0.9f));
                    }
                }
            }

            // Restauramos la matriz antes de pasar a la siguiente parte
            Gizmos.matrix = originalMatrix;
        }


        // --- PARTE 2: HUELLA DE LA DEFENSA (Lo que ocupa el objeto) ---
        if (cardData != null && previewInstance != null)
        {
            // Configuramos la matriz para que siga la posición e inclinación del preview
            Gizmos.matrix = Matrix4x4.TRS(
                previewInstance.transform.position,
                previewInstance.transform.rotation,
                Vector3.one
            );

            // Color según validez (Verde si se puede, Rojo si no)
            Gizmos.color = currentPreviewIsValid ? Color.green : Color.red;

            // 1. Dibujar el marco exterior grueso
            Vector3 totalSize = new Vector3(cardData.gridSize.x, 0.05f, cardData.gridSize.y);
            Gizmos.DrawWireCube(Vector3.up * 0.01f, totalSize);

            // 2. Dibujar las casillas internas que ocupa este objeto específico
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.4f); // Más opaco que la rejilla global

            float startX = -(cardData.gridSize.x / 2f) + 0.5f;
            float startZ = -(cardData.gridSize.y / 2f) + 0.5f;

            for (int x = 0; x < cardData.gridSize.x; x++)
            {
                for (int z = 0; z < cardData.gridSize.y; z++)
                {
                    Vector3 localCellPos = new Vector3(startX + x, 0.02f, startZ + z);
                    Gizmos.DrawWireCube(localCellPos, new Vector3(0.95f, 0f, 0.95f));
                }
            }

            // Reset final de la matriz
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private List<Vector2Int> ComputeFootprintCells(Vector3 worldPos, Quaternion rotation, Vector2Int gridSize)
    {
        var cells = new List<Vector2Int>(gridSize.x * gridSize.y);

        float startX = -(gridSize.x / 2f) + 0.5f;
        float startZ = -(gridSize.y / 2f) + 0.5f;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                // Centro de cada casilla local dentro del footprint
                Vector3 local = new Vector3(startX + x, 0f, startZ + z);

                // Lo llevamos a mundo siguiendo la rotación del preview (incluye giro 90º y slope)
                Vector3 world = worldPos + rotation * local;

                // Convertimos a coordenadas de casilla (centros están en enteros)
                int cx = Mathf.RoundToInt(world.x);
                int cz = Mathf.RoundToInt(world.z);

                cells.Add(new Vector2Int(cx, cz));
            }
        }

        return cells;
    }

    private bool HasFullFootprintGround(Vector3 position, Quaternion rotation, Vector2Int gridSize)
    {
        // Ajusta si quieres: altura de inicio y distancia máxima
        const float rayStartHeight = 10f;
        const float rayMaxDistance = 50f;

        // Centros locales de las casillas del footprint (igual que ComputeFootprintCells)
        float startX = -(gridSize.x / 2f) + 0.5f;
        float startZ = -(gridSize.y / 2f) + 0.5f;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int z = 0; z < gridSize.y; z++)
            {
                Vector3 local = new Vector3(startX + x, 0f, startZ + z);
                Vector3 world = position + rotation * local;

                Vector3 rayOrigin = world + Vector3.up * rayStartHeight;

                // IMPORTANTE: solo placementMask (montaña)
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayMaxDistance, placementMask))
                    return false;
            }
        }

        return true;
    }

    private void UpdatePlacementFromHit(RaycastHit hit)
    {
        // Detectar si estamos en un segmento con grid
        SegmentGridSettings seg = hit.collider.GetComponentInParent<SegmentGridSettings>();

        if (seg != null)
        {
            activeSegment = seg;
            usingSegmentGrid = true;

            if (TrySnapOnSegmentGrid(seg, hit.point, currentRotationDegrees, cardData.gridSize,
                out var pos, out var rot, out var nrm,
                currentFootprintKeys, out var snapReason))
            {
                if (snapReason == "Válido")
                {
                    if (!previewInstance.activeSelf)
                        previewInstance.SetActive(true);

                    previewInstance.transform.SetPositionAndRotation(pos, rot);
                    lastHitNormal = nrm;
                    lastValidPreviewPos = pos;
                    lastValidPreviewRot = rot;
                    hasValidPreviewPos = true;
                    currentPreviewIsValid = true;
                    ApplyPreviewMaterial(true);
                }
                // Cualquier otro resultado → no hacer nada, preview se queda donde estaba
            }
            // Snap fallido → no hacer nada

            return;
        }

        // B) GLOBAL => tu sistema actual
        activeSegment = null;
        usingSegmentGrid = false;
        currentFootprintKeys.Clear();

        GridPlacementInfo gridInfo = GetSnappedInfoOnRampa(hit.point, cardData.gridSize.x, cardData.gridSize.y);
        if (!gridInfo.hitFound)
        {
            ApplyPreviewMaterial(false);
            currentPreviewIsValid = false;
            return;
        }

        previewInstance.transform.position = gridInfo.position;
        lastHitNormal = gridInfo.normal;

        ApplyRotationFromNormalAndYaw();

        // cache footprint global en CellKey (segmentId=0)
        var worldFoot = ComputeFootprintCells(gridInfo.position, previewInstance.transform.rotation, cardData.gridSize);
        for (int i = 0; i < worldFoot.Count; i++)
            currentFootprintKeys.Add(new CellKey(0, worldFoot[i].x, worldFoot[i].y));

        string validityReason = CheckPlacementValidity(gridInfo.position, gridInfo.normal, previewInstance.transform.rotation);
        currentPreviewIsValid = (validityReason == "Válido");
        ApplyPreviewMaterial(currentPreviewIsValid);
    }


    private bool TrySnapOnSegmentGrid(
SegmentGridSettings seg,
Vector3 hitPoint,
float yawDegrees,
Vector2Int baseGridSize,
out Vector3 snappedPos,
out Quaternion snappedRot,
out Vector3 avgNormal,
List<CellKey> outFootprintKeys,
out string reason)
    {
        snappedPos = default;
        snappedRot = default;
        avgNormal = Vector3.up;
        reason = "Fuera del segmento";

        outFootprintKeys.Clear();

        seg.EnsureMask();
        seg.GetPlaneBasis(out Vector3 U, out Vector3 V, out Vector3 N);

        // Obtener la normal REAL de la superficie del segmento original en el punto del cursor
        Vector3 segmentNormal = N;
        Vector3 probeOrigin = hitPoint + N * 0.5f;
        if (Physics.Raycast(probeOrigin, -N, out RaycastHit probeHit, 5f, placementMask))
        {
            var probeSeg = probeHit.collider.GetComponentInParent<SegmentGridSettings>();
            if (probeSeg == seg)
                segmentNormal = probeHit.normal;
        }

        float cs = Mathf.Max(0.01f, seg.cellSize);
        Vector3 origin = seg.OriginWorld;

        int step = Mathf.RoundToInt(yawDegrees / 90f) & 3;
        bool rotated = (step % 2) != 0;

        int w = rotated ? baseGridSize.y : baseGridSize.x;
        int h = rotated ? baseGridSize.x : baseGridSize.y;

        Vector3 rel = hitPoint - origin;
        float u = Vector3.Dot(rel, U) / cs;
        float v = Vector3.Dot(rel, V) / cs;

        float offsetU = (w % 2 == 0) ? 0f : 0.5f;
        float offsetV = (h % 2 == 0) ? 0f : 0.5f;

        float centerU = (float)System.Math.Round(u - offsetU, System.MidpointRounding.AwayFromZero) + offsetU;
        float centerV = (float)System.Math.Round(v - offsetV, System.MidpointRounding.AwayFromZero) + offsetV;

        float startU = -(w / 2f) + 0.5f;
        float startV = -(h / 2f) + 0.5f;

        Vector3 fwdBase = step switch
        {
            0 => V,
            1 => U,
            2 => -V,
            _ => -U
        };

        bool anyBlocked = false;
        Vector3 sumPos = Vector3.zero;
        Vector3 sumN = Vector3.zero;
        int count = 0;

        for (int ix = 0; ix < w; ix++)
        {
            for (int iz = 0; iz < h; iz++)
            {
                float cu = centerU + startU + ix;
                float cv = centerV + startV + iz;

                int i = Mathf.FloorToInt(cu);
                int j = Mathf.FloorToInt(cv);

                Vector3 planeCenter = origin + (i + 0.5f) * cs * U + (j + 0.5f) * cs * V;
                Vector3 rayOrigin = planeCenter + N * 5f;

                if (!Physics.Raycast(rayOrigin, -N, out RaycastHit cellHit, 30f, placementMask))
                {
                    reason = "Fuera del suelo";
                    return false;
                }

                var realSeg = cellHit.collider.GetComponentInParent<SegmentGridSettings>();

                if (realSeg == null)
                {
                    reason = "Fuera del suelo";
                    return false;
                }

                realSeg.GetPlaneBasis(out _, out _, out Vector3 realN);
                if (Vector3.Dot(realN, N) < 0.7f)
                {
                    reason = "Fuera del segmento";
                    return false;
                }

                if (!realSeg.TryWorldToCell(cellHit.point, out int ri, out int rj))
                {
                    reason = "Fuera del segmento";
                    return false;
                }

                if (!realSeg.InBounds(ri, rj))
                {
                    reason = "Fuera del segmento";
                    return false;
                }

                outFootprintKeys.Add(new CellKey(realSeg.GetInstanceID(), ri, rj));

                if (realSeg.IsBlocked(ri, rj))
                    anyBlocked = true;

                sumPos += cellHit.point;
                sumN += cellHit.normal;
                count++;
            }
        }

        if (count == 0)
        {
            reason = "Fuera del suelo";
            return false;
        }

        snappedPos = sumPos / count;

        // Proyectar a la superficie usando la normal del segmento original
        Vector3 centerRayOrigin = snappedPos + segmentNormal * 2f;
        if (Physics.Raycast(centerRayOrigin, -segmentNormal, out RaycastHit centerHit, 10f, placementMask))
            snappedPos = centerHit.point;

        avgNormal = segmentNormal;

        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(fwdBase, avgNormal).normalized;
        if (fwdOnPlane.sqrMagnitude < 0.0001f)
            fwdOnPlane = Vector3.ProjectOnPlane(V, avgNormal).normalized;

        snappedRot = Quaternion.LookRotation(fwdOnPlane, avgNormal);

        reason = anyBlocked ? "Zona bloqueada" : "Válido";
        return true;
    }

    private void OnDestroy()
    {
        if (PointsManager.Instance != null)
            PointsManager.Instance.OnPointsChanged -= HandlePointsChanged;

        if (InputManager.Instance != null)
            InputManager.Instance.OnRotateCardInput -= InputManager_OnRotateCardInput;
    }
}
