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
        if (!inPlacementMode || previewInstance == null || useFixedPosition)
            return;

        // 1) Si estamos escalando con T: NO mover ni rotar, solo escala
        HandleFreeScaling();
        if (isFreeScaling)
            return;

        // 2) Rotación libre (R) o rotación normal (ambas usan currentRotationDegrees persistente)
        if (isFreeRotating)
        {
            UpdateFreeRotation();
        }
        else
        {
            ApplyRotationFromNormalAndYaw();
        }
    }

    private void ApplyRotationFromNormalAndYaw()
    {
        if (previewInstance == null) return;

        Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, lastHitNormal);
        Quaternion extraRot = Quaternion.AngleAxis(currentRotationDegrees, lastHitNormal);
        previewInstance.transform.rotation = extraRot * baseRot;
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
        if (isValid != currentPreviewIsValid)
        {
            currentPreviewIsValid = isValid;
            ApplyPreviewMaterial(isValid);
        }
    }

    private void ApplyScaleFactorToPreview(float factor)
    {
        if (previewInstance == null) return;
        previewInstance.transform.localScale = originalPreviewScale * factor;
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

        // mover carta UI
        Vector2 newPos = rectTransform.anchoredPosition;
        newPos.x += eventData.delta.x / canvas.scaleFactor;
        rectTransform.anchoredPosition = newPos;

        float distanceToRight = rectTransform.anchoredPosition.x - originalPosition.x;
        float alpha = Mathf.Clamp01(1f - (distanceToRight / placementThreshold));
        canvasGroup.alpha = alpha;

        bool overUI = IsPointerOverUI();

        // Entrar en placement
        if (!inPlacementMode && distanceToRight >= placementThreshold && !overUI)
            EnterPlacementMode();

        // Si vuelves a UI: salir de placement y destruir preview (PERO mantener scale/rot)
        if (inPlacementMode && overUI)
        {
            CleanupPreview();
            inPlacementMode = false;
            useFixedPosition = false;
            currentPreviewIsValid = false;

            // ✅ solo flags
            isFreeRotating = false;
            isFreeScaling = false;

            return;
        }

        if (!inPlacementMode)
            return;

        if (isFreeScaling)
            return;

        if (useFixedPosition)
            return;

        // Seguimiento por raycast
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, placementMask))
        {
            if (previewInstance == null)
            {
                previewInstance = Instantiate(cardData.defensePrefab);
                DisablePreviewLogic(previewInstance);

                // escala base del prefab
                originalPreviewScale = previewInstance.transform.localScale;

                // seed preview
                bramblePreviewSeed = Random.Range(int.MinValue, int.MaxValue);
                var bramble = previewInstance.GetComponent<BrambleDefense>();
                if (bramble != null)
                    bramble.SetupPreview(bramblePreviewSeed, cardData.previewMaterial);

                // ✅ Aplicar PERSISTENTES al nacer el preview
                ApplyScaleFactorToPreview(currentScaleFactor);
            }

            if (!isFreeRotating)
                previewInstance.transform.position = hit.point;

            lastHitNormal = hit.normal;

            // ✅ aplicar rotación persistente siempre que no estés en free-rot
            if (!isFreeRotating)
                ApplyRotationFromNormalAndYaw();

            // Validación y material
            Vector3 checkPos = isFreeRotating ? freeRotatePivot : hit.point;
            Quaternion checkRot = previewInstance.transform.rotation;

            string validityReason = CheckPlacementValidity(checkPos, lastHitNormal, checkRot);
            bool isValid = validityReason == "Válido";

            if (isValid != currentPreviewIsValid)
            {
                currentPreviewIsValid = isValid;
                ApplyPreviewMaterial(isValid);
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
        Vector3 checkCenter = position + normal * 0.1f;

        Vector3 halfExtents =
            (cardData != null && basePlacementCheckExtents != Vector3.zero)
            ? basePlacementCheckExtents * currentScaleFactor
            : new Vector3(0.5f, 0.5f, 0.5f);

        if (Physics.CheckBox(checkCenter, halfExtents, rotation, defenseMask))
            return "Demasiado cerca de otro obstáculo";

        if (Physics.CheckBox(checkCenter, halfExtents, rotation, campMask))
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
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            r.materials = mats;
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

        if (cardData == null || cardData.previewMaterial == null)
            return;

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

        if (InputManager.Instance != null)
            InputManager.Instance.OnRotateCardInput -= InputManager_OnRotateCardInput;
    }
}
