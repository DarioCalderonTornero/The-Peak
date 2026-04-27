using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Gestiona el vídeo explicativo en el reverso de la carta.
/// Crea su propio RenderTexture en runtime para que cada carta
/// tenga el suyo independiente.
/// </summary>
[RequireComponent(typeof(VideoPlayer))]
public class CardVideoPlayer : MonoBehaviour
{
    [Header("Referencias UI — dentro de la carta")]
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private Button expandButton;

    [Header("Overlay pantalla completa")]
    [SerializeField] private CardVideoOverlay videoOverlay;

    [Header("Resolución del RenderTexture interno")]
    [SerializeField] private int rtWidth = 512;
    [SerializeField] private int rtHeight = 288;

    private VideoPlayer vp;
    private RenderTexture ownRT;   // RT exclusivo de esta carta
    private bool isPrepared = false;

    // ─── Init ─────────────────────────────────────────────────────

    private void Awake()
    {
        vp = GetComponent<VideoPlayer>();

        // Crear RenderTexture propio (no compartido con otras cartas)
        ownRT = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.ARGB32);
        ownRT.Create();

        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = ownRT;
        vp.playOnAwake = false;
        vp.isLooping = true;
        vp.audioOutputMode = VideoAudioOutputMode.None; // evita problemas de audio

        vp.prepareCompleted += OnPrepareCompleted;

        if (videoRawImage != null)
            videoRawImage.texture = ownRT;

        if (expandButton != null)
        {
            expandButton.onClick.AddListener(OpenOverlay);
            // Aseguramos que el botón no está bloqueado
            expandButton.interactable = true;
        }
    }

    private void OnDestroy()
    {
        // Limpiar correctamente para evitar memory leaks y crashes
        if (vp != null)
        {
            vp.prepareCompleted -= OnPrepareCompleted;
            vp.Stop();
        }

        if (ownRT != null)
        {
            ownRT.Release();
            Destroy(ownRT);
            ownRT = null;
        }
    }

    // ─── Enable / Disable (al flipear la carta) ───────────────────

    private void OnEnable()
    {
        if (vp == null || vp.clip == null) return;

        if (!isPrepared)
            vp.Prepare();   // prepara primero, play al terminar
        else
            vp.Play();
    }

    private void OnDisable()
    {
        if (vp != null)
            vp.Pause();     // pausa en vez de Stop para evitar crashes al re-activar
    }

    private void OnPrepareCompleted(VideoPlayer source)
    {
        isPrepared = true;
        if (gameObject.activeInHierarchy)
            vp.Play();
    }

    // ─── API pública ──────────────────────────────────────────────

    public void SetClip(VideoClip clip)
    {
        if (clip == null) return;

        isPrepared = false;
        vp.Stop();
        vp.clip = clip;

        // Solo preparamos si el GameObject está activo (BackCard visible)
        if (gameObject.activeInHierarchy)
            vp.Prepare();
    }

    // ─── Overlay ──────────────────────────────────────────────────

    private void OpenOverlay()
    {
        if (videoOverlay == null)
            videoOverlay = FindFirstObjectByType<CardVideoOverlay>();

        // if (videoOverlay != null)
            // videoOverlay.Open(ownRT);
    }
}