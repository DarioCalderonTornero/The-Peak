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

    private void Awake()
    {
        rt = GetComponent<RectTransform>();

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
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (mainCamera == null)
        {
            var camObj = GameObject.Find(cameraName);
            if (camObj != null) mainCamera = camObj.GetComponent<Camera>();
            if (mainCamera == null) return;
        }

        // Mover la pala con el ratón
        rt.anchoredPosition += eventData.delta / GetComponentInParent<Canvas>().scaleFactor;

        // Raycast 3D hacia los obstáculos
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, defenseMask))
        {
            var defense = hit.collider.GetComponentInParent<BaseDefense>();
            if (defense != hoveredDefense)
            {
                // Quitar outline del anterior
                if (hoveredDefense != null)
                    SetOutline(hoveredDefense, false);

                hoveredDefense = defense;

                // Poner outline al nuevo
                if (hoveredDefense != null)
                    SetOutline(hoveredDefense, true);
            }
        }
        else
        {
            // No hay obstáculo bajo el cursor
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
            Destroy(hoveredDefense.gameObject);
            hoveredDefense = null;

            // +1 punto por destruir con la pala
            if (PointsManager.Instance != null)
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
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / returnDuration), 3f); // EaseOut
            rt.anchoredPosition = Vector2.Lerp(from, originalPosition, n);
            yield return null;
        }
        rt.anchoredPosition = originalPosition;
    }

    private void SetOutline(BaseDefense defense, bool active)
    {
        defense.SetShovelHover(active);
    }
}