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

        // Click fuera de la defensa → cerrar
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider.gameObject != gameObject)
                Close();
        }

        // Billboard + seguir la defensa
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

        isOpen = true;
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
        float t = 0f;
        float dur = 0.15f;
        panel.transform.localScale = Vector3.zero;
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            panel.transform.localScale = Vector3.one * n;
            yield return null;
        }
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