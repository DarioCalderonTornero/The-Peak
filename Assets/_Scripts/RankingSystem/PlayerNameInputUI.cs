using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerNameInputUI : MonoBehaviour
{
    [SerializeField] private GameObject inputPanel;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton;

    private void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);

        if (nameInputField != null)
            nameInputField.characterLimit = 8;

        if (RankingManager.Instance == null || !RankingManager.Instance.RankingEnabled)
        {
            if (inputPanel != null) inputPanel.SetActive(false);
            return;
        }

        if (inputPanel != null) inputPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        string name = nameInputField != null ? nameInputField.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(name))
        {
            StartCoroutine(ShakeInputField());
            return;
        }

        // Comprobar si el nombre ya existe en el ranking
        if (RankingManager.Instance != null)
        {
            var entries = RankingManager.Instance.GetRankingSorted();
            foreach (var entry in entries)
            {
                if (entry.playerName.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                {
                    StartCoroutine(ShakeInputField());
                    return;
                }
            }

            RankingManager.Instance.SetPlayerName(name);
            RankingManager.Instance.StartSession();
        }

        if (inputPanel != null) inputPanel.SetActive(false);
    }

    private IEnumerator ShakeInputField()
    {
        if (nameInputField == null) yield break;

        RectTransform rt = nameInputField.GetComponent<RectTransform>();
        Vector2 originalPos = rt.anchoredPosition;
        float duration = 0.3f;
        float magnitude = 10f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration);
            rt.anchoredPosition = originalPos + new Vector2(
                UnityEngine.Random.Range(-1f, 1f) * strength, 0f);
            yield return null;
        }

        rt.anchoredPosition = originalPos;
    }
}