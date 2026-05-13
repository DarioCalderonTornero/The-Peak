using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DefenseInfoUI : MonoBehaviour
{
    [Header("Referencias CardData")]
    [SerializeField] private CardData cardData;

    [Header("Prefab del panel")]
    [SerializeField] private GameObject infoPanelPrefab; // prefab con el layout

    private GameObject panelInstance;
    private bool isOpen = false;
    private static DefenseInfoUI currentOpen; // solo un panel abierto a la vez

    private Camera mainCamera;

    private bool justOpened = false;

    [Header("Estado Géiser")]
    [SerializeField] private Sprite geyserActiveSprite;   // sprite verde
    [SerializeField] private Sprite geyserCooldownSprite; // sprite rojo

    private void Start()
    {
        mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
    }

    private void OnMouseDown()
    {
        if (!TurnManager.Instance.IsPlayerTurn()) return;

        if (isOpen)
        {
            Close();
            return;
        }

        // Cerrar cualquier otro panel abierto
        if (currentOpen != null && currentOpen != this)
            currentOpen.Close();

        Open();
    }

    private void Update()
    {
        if (!isOpen) return;

        if (justOpened)
        {
            justOpened = false; // ← ignorar el primer frame
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider.gameObject != gameObject)
                Close();
        }

        if (panelInstance != null)
        {
            panelInstance.transform.position = transform.position + Vector3.up * 2.5f;
            BillboardToCamera(panelInstance.transform);
        }
    }

    private void Open()
    {
        if (cardData == null || infoPanelPrefab == null) return;

        panelInstance = Instantiate(infoPanelPrefab);
        panelInstance.transform.position = transform.position + Vector3.up * 2.5f;
        BillboardToCamera(panelInstance.transform);

        // ← AÑADIR AQUÍ los logs
        var counterIcon = panelInstance.transform.Find("CounterIcon")?.GetComponent<Image>();
        Debug.Log($"[DefenseInfoUI] counterIcon encontrado={counterIcon != null} | cardData.counterIcon={cardData?.counterIcon}");

        if (counterIcon != null && cardData.counterIcon != null)
            counterIcon.sprite = cardData.counterIcon;

        var counterColor = panelInstance.transform.Find("CounterColorSprite")?.GetComponent<Image>();
        if (counterColor != null && cardData.counterColorSprite != null)
            counterColor.sprite = cardData.counterColorSprite;

        var turnsText = panelInstance.transform.Find("TurnsText")?.GetComponent<TextMeshProUGUI>();
        var temp = GetComponent<TemporaryDefense>();
        Debug.Log($"[DefenseInfoUI] temp={temp} | turns={temp?.GetTurnsRemaining()}");

        if (turnsText != null)
        {
            if (temp != null)
            {
                turnsText.text = temp.GetTurnsRemaining().ToString();
                turnsText.gameObject.SetActive(true);
            }
            else
            {
                turnsText.gameObject.SetActive(false);
            }
        }

        var geyser = GetComponent<GeyserDefense>();
        var geyserIcon = panelInstance.transform.Find("GeyserStatus")?.GetComponent<Image>();
        var geyserTurns = panelInstance.transform.Find("GeyserTurns")?.GetComponent<TextMeshProUGUI>();

        if (geyserIcon != null)
        {
            if (geyser != null && geyser.IsInCooldown())
            {
                // En cooldown: no mostrar nada en el panel
                geyserIcon.gameObject.SetActive(false);
                if (geyserTurns != null) geyserTurns.gameObject.SetActive(false);
            }
            else if (geyser != null)
            {
                // Activo: mostrar sprite verde
                if (geyserActiveSprite != null) geyserIcon.sprite = geyserActiveSprite;
                geyserIcon.gameObject.SetActive(true);
                if (geyserTurns != null) geyserTurns.gameObject.SetActive(false);
            }
            else
            {
                // No es géiser: ocultar
                geyserIcon.gameObject.SetActive(false);
                if (geyserTurns != null) geyserTurns.gameObject.SetActive(false);
            }
        }

        isOpen = true;
        justOpened = true;
        currentOpen = this;
        StartCoroutine(AnimateOpen(panelInstance));
    }

    public void Close()
    {
        if (panelInstance != null)
            StartCoroutine(AnimateClose(panelInstance));

        isOpen = false;
        if (currentOpen == this) currentOpen = null;
        panelInstance = null;
    }

    private IEnumerator AnimateOpen(GameObject panel)
    {
        if (panel == null) yield break;

        float t = 0f;
        float dur = 0.15f;
        panel.transform.localScale = Vector3.zero;
        while (t < dur)
        {
            if (panel == null) yield break; // ← check cada frame
            t += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            panel.transform.localScale = Vector3.one * n;
            yield return null;
        }

        if (panel == null) yield break;
        panel.transform.localScale = Vector3.one;
    }

    private IEnumerator AnimateClose(GameObject panel)
    {
        float t = 0f;
        float dur = 0.1f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            panel.transform.localScale = Vector3.one * (1f - n);
            yield return null;
        }
        Destroy(panel);
    }

    private void BillboardToCamera(Transform t)
    {
        if (mainCamera == null) return;
        t.LookAt(t.position + mainCamera.transform.rotation * Vector3.forward,
                 mainCamera.transform.rotation * Vector3.up);
    }

    private void OnDestroy()
    {
        if (panelInstance != null) Destroy(panelInstance);
        if (currentOpen == this) currentOpen = null;
    }
}