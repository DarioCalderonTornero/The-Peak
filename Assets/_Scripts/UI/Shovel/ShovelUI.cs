using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShovelUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Referencias")]
    [SerializeField] private string cameraName = "Main Camera";
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask defenseMask; // capa de los obstáculos

    [Header("Animación retorno")]
    [SerializeField] private float returnDuration = 0.3f;

    [SerializeField] private AudioClip shovelAudioClip;

    private RectTransform rt;
    private Vector2 originalPosition;
    private Coroutine returnRoutine;

    private BaseDefense hoveredDefense;

    [Header("Control por turno")]
    [SerializeField] private GameObject shovelGameObject;

    [Header("Sprites")]
    [SerializeField] private Sprite spriteIdle;
    [SerializeField] private Sprite spriteDrag;
    private Image shovelImage;

    private Vector3 originalScale;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        shovelImage = GetComponent<Image>();
        originalScale = transform.localScale;

        // Buscar la cámara — primero Camera.main, luego cualquier cámara activa
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
    }

    private void Start()
    {
        originalPosition = rt.anchoredPosition;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnPlayerTurnStart += () => shovelGameObject.SetActive(true);
            TurnManager.Instance.OnClimberTurnStart += () =>
            {
                // Si estaba arrastrando, cancelar
                if (hoveredDefense != null)
                {
                    hoveredDefense.SetShovelHover(false);
                    hoveredDefense = null;
                }
                rt.anchoredPosition = originalPosition;
                shovelGameObject.SetActive(false);
            };
        }

        // Empieza desactivada hasta que llegue el turno del jugador
        shovelGameObject.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        if (shovelImage != null)
        {
            shovelImage.sprite = spriteIdle;
            shovelImage.transform.localScale = originalScale;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (mainCamera == null)
        {
            var camObj = GameObject.Find(cameraName);
            if (camObj != null) mainCamera = camObj.GetComponent<Camera>();
            if (mainCamera == null) return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();

        // Convertir al espacio local del PADRE de la pala, no del canvas raíz
        RectTransform parentRT = rt.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRT,
            eventData.position,
            canvas.worldCamera,
            out Vector2 localPoint
        );
        rt.anchoredPosition = localPoint;

        if (shovelImage != null && spriteDrag != null)
        {
            shovelImage.sprite = spriteDrag;

            if (eventData.delta.x > 0.5f)
                shovelImage.transform.localScale = new Vector3(-1f, 1f, 1f);
            else if (eventData.delta.x < -0.5f)
                shovelImage.transform.localScale = new Vector3(1f, 1f, 1f);
        }

        // Raycast 3D
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, defenseMask))
        {
            var defense = hit.collider.GetComponentInParent<BaseDefense>();
            if (defense != hoveredDefense)
            {
                if (hoveredDefense != null) SetOutline(hoveredDefense, false);
                hoveredDefense = defense;
                if (hoveredDefense != null) SetOutline(hoveredDefense, true);
            }
        }
        else
        {
            if (hoveredDefense != null)
            {
                SetOutline(hoveredDefense, false);
                hoveredDefense = null;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (hoveredDefense != null)
        {
            hoveredDefense.SetShovelHover(false);
            Temporal_Sound_Music.Instance.Play2DSound(shovelAudioClip, 1f);

            // Guardar posición de la defensa antes de destruirla
            Vector3 defenseWorldPos = hoveredDefense.transform.position;

            Destroy(hoveredDefense.gameObject);
            hoveredDefense = null;

            // Animar 1 punto desde la posición de la defensa
            if (BalloonEventManager.Instance != null)
                BalloonEventManager.Instance.SpawnPointsFromWorldPosition(defenseWorldPos, 1);
            else if (PointsManager.Instance != null)
                PointsManager.Instance.AddPoints(1);
        }

        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = StartCoroutine(ReturnToOrigin());
    }

    private IEnumerator ReturnToOrigin()
    {
        Vector2 from = rt.anchoredPosition;
        float t = 0f;
        while (t < returnDuration)
        {
            t += Time.unscaledDeltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / returnDuration), 3f);
            rt.anchoredPosition = Vector2.Lerp(from, originalPosition, n);
            yield return null;
        }
        rt.anchoredPosition = originalPosition;
        if (shovelImage != null)
        {
            shovelImage.sprite = spriteIdle;
            shovelImage.transform.localScale = originalScale;
        }
    }

    private void SetOutline(BaseDefense defense, bool active)
    {
        defense.SetShovelHover(active);
    }
}