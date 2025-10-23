using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private RectTransform buttonTransform;
    [SerializeField] private GameObject inventoryPanel;

    [Header("Animación")]
    [SerializeField] private float pressScale = 0.9f;
    [SerializeField] private float pressDuration = 0.1f;

    [SerializeField] private CanvasGroup inventoryCanvasGroup;
    [SerializeField] private float fadeDuration = 0.25f;

    private bool isOpen = false;
    private bool isAnimating = false;

    private void Start()
    {
        if (inventoryButton != null)
            inventoryButton.onClick.AddListener(ToggleInventory);

        // Mantener inactivo el panel al inicio (manteniendo CanvasGroup preparado)
        inventoryPanel.SetActive(false);

        // Aseguramos que el CanvasGroup esté en alpha = 0 al inicio para que la primera apertura haga fade
        if (inventoryCanvasGroup != null)
        {
            inventoryCanvasGroup.alpha = 0f;
            inventoryCanvasGroup.interactable = false;
            inventoryCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ToggleInventory()
    {
        if (isAnimating)
            return;

        StartCoroutine(PressAnimation());

        isOpen = !isOpen;
        StartCoroutine(FadeInventory(isOpen));
    }

    private IEnumerator PressAnimation()
    {
        isAnimating = true;

        Vector3 originalScale = buttonTransform.localScale;
        Vector3 targetScale = originalScale * pressScale;

        float elapsed = 0f;
        while (elapsed < pressDuration)
        {
            buttonTransform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / pressDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonTransform.localScale = targetScale;

        elapsed = 0f;
        while (elapsed < pressDuration)
        {
            buttonTransform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / pressDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        buttonTransform.localScale = originalScale;

        isAnimating = false;
    }

    private IEnumerator FadeInventory(bool show)
    {
        if (inventoryCanvasGroup == null || inventoryPanel == null)
        {
            yield break; // seguridad
        }

        if (show)
        {
            // Prepara para animar: activa panel y asegura que el alpha parte de 0
            inventoryPanel.SetActive(true);
            inventoryCanvasGroup.alpha = 0f;
            inventoryCanvasGroup.interactable = false;
            inventoryCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            // Si cerramos, dejamos interactable=false para no recibir clicks durante fade out
            inventoryCanvasGroup.interactable = false;
            inventoryCanvasGroup.blocksRaycasts = false;
        }

        float startAlpha = inventoryCanvasGroup.alpha;
        float endAlpha = show ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            inventoryCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        inventoryCanvasGroup.alpha = endAlpha;

        if (show)
        {
            inventoryCanvasGroup.interactable = true;
            inventoryCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            // cierre: desactivar panel al final para mantener lo que tenías
            inventoryPanel.SetActive(false);
        }
    }
}
