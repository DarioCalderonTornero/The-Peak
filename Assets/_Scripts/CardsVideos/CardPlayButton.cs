using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Botón "Play" en el reverso de la carta.
/// Al pulsarlo abre el overlay con el vídeo de esta carta.
/// NO hay VideoPlayer por carta — todo va al overlay centralizado.
///
/// Adjunta al BackCard (o al botón Play directamente).
/// </summary>
public class CardPlayButton : MonoBehaviour
{
    [Header("Botón Play")]
    [SerializeField] private Button playButton;

    [Header("Overlay (se busca automáticamente si se deja vacío)")]
    [SerializeField] private CardVideoOverlay videoOverlay;

    // El clip se asigna desde CardBackSetup al configurar la carta
    private VideoClip assignedClip;

    private void Awake()
    {
        if (playButton == null)
            playButton = GetComponentInChildren<Button>();

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayPressed);
    }

    public void SetClip(UnityEngine.Video.VideoClip clip)
    {
        assignedClip = clip;

        // Si no hay clip, desactivar el botón
        if (playButton != null)
            playButton.gameObject.SetActive(clip != null);
    }

    private void OnPlayPressed()
    {
        if (assignedClip == null) return;

        if (videoOverlay == null)
        {
            // FindFirstObjectByType no encuentra objetos inactivos,
            // hay que usar Resources o buscar en todos los objetos
            var all = Resources.FindObjectsOfTypeAll<CardVideoOverlay>();
            foreach (var o in all)
            {
                // Filtramos assets (solo queremos instancias en escena)
                if (o.gameObject.scene.isLoaded)
                {
                    videoOverlay = o;
                    break;
                }
            }
        }

        if (videoOverlay != null)
            videoOverlay.Open(assignedClip);
        else
            Debug.LogWarning("[CardPlayButton] No se encontró CardVideoOverlay en la escena.");
    }
}