using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class CardPlayButton : MonoBehaviour
{
    [Header("Botón Play")]
    [SerializeField] private Button playButton;

    [Header("Overlay (se busca automáticamente si se deja vacío)")]
    [SerializeField] private CardVideoOverlay videoOverlay;

    private CardData assignedCardData;

    private void Awake()
    {
        if (playButton == null)
            playButton = GetComponentInChildren<Button>();

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayPressed);
    }

    public void SetCardData(CardData cardData)
    {
        assignedCardData = cardData;

        // Si no hay vídeo, desactivar el botón
        if (playButton != null)
            playButton.gameObject.SetActive(cardData != null && cardData.explanationVideo != null);
    }

    // Mantener compatibilidad si algo llama al SetClip antiguo
    public void SetClip(VideoClip clip) { }

    private void OnPlayPressed()
    {
        Debug.Log($"[CardPlayButton] OnPlayPressed | assignedCardData={assignedCardData?.cardName ?? "NULL"}");
        if (assignedCardData == null) return;

        if (videoOverlay == null)
        {
            var all = Resources.FindObjectsOfTypeAll<CardVideoOverlay>();
            foreach (var o in all)
            {
                if (o.gameObject.scene.isLoaded)
                {
                    videoOverlay = o;
                    break;
                }
            }
        }

        if (videoOverlay != null)
            videoOverlay.Open(assignedCardData);
        else
            Debug.LogWarning("[CardPlayButton] No se encontró CardVideoOverlay en la escena.");
    }
}