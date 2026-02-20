// DragCardUI.cs
using System.Collections;
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
        if (!inPlacementMode || previewInstance == null || useFixedPosition) return;

        // --- ROTACIÓN POR PASOS (90 GRADOS) ---
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationDegrees += 90f;
            if (currentRotationDegrees >= 360f) currentRotationDegrees = 0f;

            // ✅ Forzamos que se actualice la rotación con la inclinación actual
            ApplyRotationFromNormalAndYaw();
        }

        // ❌ BORRA ESTA LÍNEA: previewInstance.transform.rotation = targetRotation;
    }

    private void ApplyRotationFromNormalAndYaw()
    {
        if (previewInstance == null) return;

        // 1. Rotación de la rampa (Normal)
        Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, lastHitNormal);

        // 2. Rotación del jugador (0, 90, 180...)
        Quaternion playerRotation = Quaternion.Euler(0, currentRotationDegrees, 0);

        // 3. Combinación: Primero inclinamos y luego giramos sobre esa inclinación
        previewInstance.transform.rotation = slopeRotation * playerRotation;
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
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (PointsManager.Instance != null && !PointsManager.Instance.CanAfford(cardData.cost))
        {
            canDragThisTime = false;
            StartCoroutine(ShakeCard());
            return;
        }

        canDragThisTime = true;
        canvasGroup.blocksRaycasts = false;

        // ✅ Reset SOLO de estados temporales (NO tocamos scale/rot persistentes)
        ResetTransientStates();

        CleanupPreview();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        // Movimiento de la carta UI (se mantiene igual)
        Vector2 newPos = rectTransform.anchoredPosition;
        newPos.x += eventData.delta.x / canvas.scaleFactor;
        rectTransform.anchoredPosition = newPos;

        float distanceToRight = rectTransform.anchoredPosition.x - originalPosition.x;
        float alpha = Mathf.Clamp01(1f - (distanceToRight / placementThreshold));
        canvasGroup.alpha = alpha;

        bool overUI = IsPointerOverUI();

        if (!inPlacementMode && distanceToRight >= placementThreshold && !overUI)
            EnterPlacementMode();

        if (inPlacementMode && overUI)
        {
            CleanupPreview();
            inPlacementMode = false;
            useFixedPosition = false;
            currentPreviewIsValid = false;
            isFreeRotating = false;
            isFreeScaling = false;
            return;
        }

        if (!inPlacementMode || isFreeScaling || useFixedPosition)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
        {
            if (previewInstance == null)
            {
                previewInstance = Instantiate(cardData.defensePrefab);
                DisablePreviewLogic(previewInstance);

                // 1. CAPTURAR ESCALA ORIGINAL (Vital para que no sea 0)
                originalPreviewScale = previewInstance.transform.localScale;
                if (originalPreviewScale.sqrMagnitude == 0) originalPreviewScale = Vector3.one;

                // 2. AÑADIR VISUALIZADOR DE GRILLA
                var visualizer = previewInstance.AddComponent<RuntimeGridVisualizer>();
                visualizer.Setup(cardData.gridSize);

                // 3. APLICAR ESCALA INICIAL
                ApplyScaleFactorToPreview(currentScaleFactor);

                currentPreviewIsValid = false;
                ApplyPreviewMaterial(false);

                bramblePreviewSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                var bramble = previewInstance.GetComponent<BrambleDefense>();
                if (bramble != null)
                    bramble.SetupPreview(bramblePreviewSeed, cardData.previewMaterial);
            }

            GridPlacementInfo gridInfo = GetSnappedInfoOnRampa(hit.point, cardData.gridSize.x, cardData.gridSize.y);

            if (gridInfo.hitFound)
            {
                previewInstance.transform.position = gridInfo.position;
                lastHitNormal = gridInfo.normal;

                // Mover el Decal si existe
                if (globalDecal != null)
                {
                    // Lo posicionamos sobre el punto de impacto
                    globalDecal.transform.position = gridInfo.position + Vector3.up * 5f;
                }

                ApplyRotationFromNormalAndYaw();

                float slopeAngle = Vector3.Angle(Vector3.up, gridInfo.normal);
                bool angleOk = slopeAngle <= 60f;

                string validityReason = angleOk ?
                    CheckPlacementValidity(gridInfo.position, gridInfo.normal, previewInstance.transform.rotation) :
                    "Pendiente excesiva";

                currentPreviewIsValid = (validityReason == "Válido");
                ApplyPreviewMaterial(currentPreviewIsValid);
            }

            if (globalDecal != null)
            {
                int padding = 2;
                float newSizeX = cardData.gridSize.x + padding;
                float newSizeZ = cardData.gridSize.y + padding;

                globalDecal.size = new Vector3(newSizeX, newSizeZ, globalDecal.size.z);
                globalDecal.uvScale = new Vector2(newSizeX, newSizeZ);

                // Offset físico: solo la diferencia de paridad entre el objeto y el nuevo size
                // Si ambos son impares o ambos pares → se anulan → offset 0
                // Si uno es par y otro impar → offset 0.5
                float physOffsetX = (cardData.gridSize.x % 2 != newSizeX % 2) ? 0.5f : 0f;
                float physOffsetZ = (cardData.gridSize.y % 2 != newSizeZ % 2) ? 0.5f : 0f;

                globalDecal.transform.position = new Vector3(
                    gridInfo.position.x + physOffsetX,
                    gridInfo.position.y + 5f,
                    gridInfo.position.z + physOffsetZ
                );

                globalDecal.uvBias = new Vector2(0f, 0f);

                if (!globalDecal.enabled) globalDecal.enabled = true;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !canDragThisTime)
            return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        rectTransform.anchoredPosition = new Vector2(originalPosition.x, rectTransform.anchoredPosition.y);

        // Si sueltas encima de UI -> no colocar (mantener scale/rot)
        if (IsPointerOverUI())
        {
            CleanupPreview();
            ResetTransientStates(); // ✅ NO resetea rot/scale
            return;
        }

        if (!inPlacementMode || previewInstance == null)
        {
            CleanupPreview();
            ResetTransientStates(); // ✅ NO resetea rot/scale
            return;
        }

        Vector3 finalPosition;
        Quaternion finalRotation;
        Vector3 finalNormal = lastHitNormal;

        if (isFreeScaling)
        {
            finalPosition = freeScalePivot;
            finalRotation = freeScaleLockedRotation;
            finalNormal = freeScaleLockedNormal;
        }
        else if (useFixedPosition)
        {
            finalPosition = fixedPlacementPosition;
            finalNormal = Vector3.up;
            finalRotation = previewInstance.transform.rotation;
        }
        else if (isFreeRotating)
        {
            finalPosition = freeRotatePivot;
            finalRotation = previewInstance.transform.rotation;
        }
        else
        {
            // ✅ usa la rotación persistente
            finalPosition = previewInstance.transform.position;
            finalRotation = previewInstance.transform.rotation;
        }

        string placementReason = CheckPlacementValidity(finalPosition, finalNormal, finalRotation);
        if (placementReason != "Válido")
        {
            StartCoroutine(ShakeCard());
            CleanupPreview();
            ResetTransientStates(); // ✅ NO resetea rot/scale
            return;
        }

        bool placedOk = PointsManager.Instance != null && PointsManager.Instance.SpendPoints(cardData.cost);
        if (placedOk)
        {
            Vector3 finalScale = originalPreviewScale * currentScaleFactor;

            GameObject placed = DefensePlacer.Instance.PlaceDefense(
                cardData.defensePrefab,
                finalPosition,
                finalRotation,
                beforeInitialize: (go) =>
                {
                    var bramble = go.GetComponent<BrambleDefense>();
                    if (bramble != null)
                        bramble.SetupRuntimeFromPreviewSeed(bramblePreviewSeed);
                },
                afterInitialize: (go) =>
                {
                    var lodo = go.GetComponent<LodoDefense>();
                    if (lodo != null)
                    {
                        lodo.ApplyExternalScale(finalScale);
                        return;
                    }

                    var arena = go.GetComponent<QuicksandDefense>();
                    if (arena != null)
                    {
                        arena.ApplyExternalScale(finalScale);
                        return;
                    }

                    go.transform.localScale = finalScale;
                }
            );

            Temporal_Sound_Music.Instance.PlaySound(defensePlacementAudioClip, 1f);
            CameraShake.Instance.SetCurrentStateCameraShake(4.0f, 5.5f, 0.2f);

            if (placed != null && DefensePlacementManager.Instance != null)
                DefensePlacementManager.Instance.RegisterPlaced(placed);

            OnCardUsed?.Invoke(this);

            // ✅ AHORA SÍ: si quieres que al colocar se resetee para la próxima carta/nueva copia
            ResetPersistentTransform(); // <-- quítalo si NO quieres resetear nunca
        }
        else
        {
            StartCoroutine(ShakeCard());
        }

        CleanupPreview();
        ResetTransientStates(); // ✅ flags a cero, pero rot/scale persisten salvo que hayas llamado ResetPersistentTransform()
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
        canvasGroup.alpha = 0f;

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

                bramblePreviewSeed = Random.Range(int.MinValue, int.MaxValue);
                var bramble = previewInstance.GetComponent<BrambleDefense>();
                if (bramble != null)
                    bramble.SetupPreview(bramblePreviewSeed, cardData.previewMaterial);

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
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
            return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    private string CheckPlacementValidity(Vector3 position, Vector3 normal, Quaternion rotation)
    {
        // 1. ✅ NUEVO: Comprobar ángulo de inclinación
        float slopeAngle = Vector3.Angle(Vector3.up, normal);
        if (slopeAngle > 60f)
        {
            return "Pendiente demasiado pronunciada";
        }

        // 2. Comprobar colisiones (Grilla)
        // Usamos el tamaño de la grilla definido en CardData
        Vector3 boxSize = new Vector3(
            cardData.gridSize.x - 0.1f,
            0.5f, // Altura de detección
            cardData.gridSize.y - 0.1f
        );

        // Levantamos un poco el centro para que la caja de colisión siga la normal de la rampa
        Vector3 center = position + (normal * 0.25f);

        Vector3 halfExtents = boxSize / 2f;

        if (Physics.CheckBox(center, halfExtents, rotation, defenseMask))
            return "Casilla ocupada";

        if (Physics.CheckBox(center, halfExtents, rotation, campMask))
            return "Demasiado cerca de un campamento";

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
            visualizer.SetColor(valid ? Color.green : Color.red);
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
        replacementCallback?.Invoke(this);
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

    // Estructura para devolver posición y normal ajustadas
    private struct GridPlacementInfo
    {
        public Vector3 position;
        public Vector3 normal;
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
        }
        else
        {
            // Si fallamos (ej: fuera del mapa), usamos el original redondeado plano
            result.position = new Vector3(snappedX, originalHitPoint.y, snappedZ);
            result.normal = Vector3.up;
        }

        return result;
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
                        Gizmos.matrix = Matrix4x4.TRS(
                            cellHit.point + (cellHit.normal * 0.005f),
                            Quaternion.FromToRotation(Vector3.up, cellHit.normal),
                            Vector3.one
                        );

                        Gizmos.color = new Color(1f, 1f, 1f, 0.15f); // Blanco tenue
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

    private void OnDestroy()
    {
        if (PointsManager.Instance != null)
            PointsManager.Instance.OnPointsChanged -= HandlePointsChanged;

        if (InputManager.Instance != null)
            InputManager.Instance.OnRotateCardInput -= InputManager_OnRotateCardInput;
    }
}
