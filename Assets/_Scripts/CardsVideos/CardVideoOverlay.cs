using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Panel pantalla completa con un único VideoPlayer centralizado.
/// Se abre al pulsar el botón Play de cualquier carta.
///
/// JERARQUÍA en el Canvas raíz:
///   VideoOverlay  (este script + CanvasGroup, desactivado al inicio)
///   ├── Background       Image negra semitransparente, stretch completo
///   ├── VideoRawImage    RawImage 16:9 centrada
///   └── CloseButton      Botón X esquina superior derecha
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CardVideoOverlay : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RawImage overlayRawImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 0.2f;

    private CanvasGroup cg;
    private RenderTexture rt;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        gameObject.SetActive(false);

        // Crear RenderTexture única para el overlay
        rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        rt.Create();

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = rt;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        overlayRawImage.texture = rt;

        videoPlayer.prepareCompleted += _ => videoPlayer.Play();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    // ─── API pública ──────────────────────────────────────────────

    public void Open(VideoClip clip)
    {
        if (clip == null) return;

        gameObject.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.Prepare();

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(0f, 1f, true));
    }

    public void Close()
    {
        videoPlayer.Stop();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(1f, 0f, false));
    }

    // ─── Fade ─────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, bool interactive)
    {
        cg.interactable = false;
        cg.blocksRaycasts = false;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        cg.alpha = to;

        if (!interactive)
            gameObject.SetActive(false);

        cg.interactable = interactive;
        cg.blocksRaycasts = interactive;
    }
}