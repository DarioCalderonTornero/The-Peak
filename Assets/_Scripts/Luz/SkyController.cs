using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering;
using System.Collections;

[ExecuteAlways]
public class SkyController : MonoBehaviour
{
    public static SkyController Instance { get; private set; }

    public Material skyMaterial;

    [Header("Referencias del Cielo y Astros")]
    public Transform skySphere;
    public Transform sunQuad;
    public Transform moonQuad;
    public float distance = 450f;

    [Header("Materiales de los Astros")]
    public Material sunMaterial;
    [ColorUsage(true, true)] public Color sunBaseColor = Color.white;
    public AnimationCurve sunAlphaCurve;

    public Material moonMaterial;
    [ColorUsage(true, true)] public Color moonBaseColor = Color.white;
    public AnimationCurve moonAlphaCurve;

    [Header("Luces Reales")]
    public Light sunLight;
    public AnimationCurve sunIntensityCurve;
    public Light moonLight;
    public AnimationCurve moonIntensityCurve;

    [Header("Efecto de Destello (Sol)")]
    public LensFlareComponentSRP sunLensFlare;
    public AnimationCurve sunLensFlareIntensityCurve;

    [Header("Efectos Visuales (Luciérnagas)")]
    public VisualEffect[] fireflyEffects;
    public AnimationCurve firefliesIntensityCurve;

    [Header("Control del Agua")]
    public Material waterMaterial;
    public Gradient waterHorizonGradient;

    [Header("Control del Tiempo")]
    [Range(0f, 1f)] public float timeOfDay = 0.5f;
    public float dayDurationInSeconds = 120f;
    public bool isTimePaused = false;

    [Header("Gradientes de Color del Cielo")]
    public Gradient zenithGradient;
    public Gradient horizonGradient;

    [Header("Control de Nubes y Estrellas")]
    public Gradient cloudColorGradient;
    public AnimationCurve cloudDensityCurve;
    public AnimationCurve starsIntensityCurve;

    [Header("--- EVENTO ECLIPSE ROJO ---")]
    public Transform fakeMoonQuad;
    public float eclipseMoonStartAngle = 45f;
    public float eclipseTransitionTime = 5f;
    public Material fakeMoonMaterial;

    [Space(10)]
    public Light eclipseLight;
    public AnimationCurve eclipseLightIntensityCurve;

    [Space(10)]
    [ColorUsage(true, true)] public Color eclipseZenithColor = Color.black;
    [ColorUsage(true, true)] public Color eclipseHorizonColor = Color.red;
    [ColorUsage(true, true)] public Color eclipseWaterColor = Color.red;
    [ColorUsage(true, true)] public Color eclipseCloudColor = new Color(0.1f, 0.1f, 0.1f); // Gris oscuro/Negro para las nubes

    private bool isEclipseActive = false;
    private bool isWaitingForMidday = false;
    private float eclipseFactor = 0f;

    // --- VARIABLES PARA OPTIMIZACIÓN ---
    private int sunDirectionId;
    private int zenithColorId;
    private int horizonColorId;
    private int starsIntensityId;
    private int cloudColorId;
    private int cloudDensityId;
    private int baseColorId;
    private int alphaId;
    private int fireflyFloatId;
    private int waterHorizonColorId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
    }

    void Start()
    {
        sunDirectionId = Shader.PropertyToID("_SunDirection");
        zenithColorId = Shader.PropertyToID("_ZenithColor");
        horizonColorId = Shader.PropertyToID("_HorizonColor");
        starsIntensityId = Shader.PropertyToID("_StarsIntensity");
        cloudColorId = Shader.PropertyToID("_CloudColor");
        cloudDensityId = Shader.PropertyToID("_CloudDensity");

        baseColorId = Shader.PropertyToID("_BaseColor");
        alphaId = Shader.PropertyToID("_Alpha");

        fireflyFloatId = Shader.PropertyToID("IntensidadLuciernagas");
        waterHorizonColorId = Shader.PropertyToID("_HorizonBlendColor");

        if (eclipseLight != null && Application.isPlaying)
        {
            eclipseLight.intensity = 0f;
            eclipseLight.gameObject.SetActive(false);
        }
    }

    public void IniciarEclipse()
    {
        if (!isEclipseActive && !isWaitingForMidday)
        {
            isWaitingForMidday = true;
            Debug.Log("Eclipse en espera: aguardando a que sea mediodía...");
        }
    }

    public void TerminarEclipse()
    {
        if (isEclipseActive)
        {
            isWaitingForMidday = false;
            StartCoroutine(TransicionEclipse(false));
        }
    }

    private IEnumerator TransicionEclipse(bool haciaEclipse)
    {
        isEclipseActive = haciaEclipse;

        if (haciaEclipse)
        {
            isTimePaused = true;
            timeOfDay = 0.5f;
            if (eclipseLight != null) eclipseLight.gameObject.SetActive(true);
        }

        float startFactor = eclipseFactor;
        float targetFactor = haciaEclipse ? 1f : 0f;

        float t = 0f;
        while (t < eclipseTransitionTime)
        {
            t += Time.deltaTime;
            float normalized = t / eclipseTransitionTime;

            eclipseFactor = Mathf.Lerp(startFactor, targetFactor, normalized);
            yield return null;
        }

        eclipseFactor = targetFactor;

        if (!haciaEclipse)
        {
            isTimePaused = false;
            if (eclipseLight != null) eclipseLight.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (Application.isPlaying && isWaitingForMidday)
        {
            if (timeOfDay >= 0.49f && timeOfDay <= 0.51f)
            {
                isWaitingForMidday = false;
                StartCoroutine(TransicionEclipse(true));
            }
        }

        if (Application.isPlaying && !isTimePaused)
        {
            timeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        float sunAngle = timeOfDay * 360f - 90f;
        Quaternion rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        transform.rotation = rotation;

        Vector3 sunDirection = transform.forward;
        Vector3 centerPosition = skySphere != null ? skySphere.position : Vector3.zero;

        if (sunQuad != null)
        {
            sunQuad.position = centerPosition - (sunDirection * distance);
            sunQuad.forward = sunDirection;
        }

        if (moonQuad != null)
        {
            Vector3 moonDirection = -sunDirection;
            moonQuad.position = centerPosition - (moonDirection * distance);
            moonQuad.forward = moonDirection;
        }

        // --- ANIMACIÓN DE LA LUNA FALSA (CORREGIDA) ---
        if (fakeMoonQuad != null)
        {
            if (eclipseFactor > 0f)
            {
                if (!fakeMoonQuad.gameObject.activeSelf) fakeMoonQuad.gameObject.SetActive(true);

                // Ahora rotamos sobre el eje local del sistema del cielo, garantizando el barrido visual
                float offsetAngle = Mathf.Lerp(eclipseMoonStartAngle, 0f, eclipseFactor);
                Vector3 offsetDir = Quaternion.AngleAxis(offsetAngle, transform.up) * sunDirection;

                fakeMoonQuad.position = centerPosition - (offsetDir * (distance - 2f));

                // Hacemos que la luna siempre mire al centro para no deformarse
                fakeMoonQuad.forward = offsetDir;
                if (fakeMoonMaterial != null)
                {
                    Color moonColor = fakeMoonMaterial.GetColor(baseColorId);
                    // Usamos una curva matemática rápida (elevar a 2) para que se vuelva opaca casi al final
                    moonColor.a = Mathf.Pow(eclipseFactor, 2f);
                    fakeMoonMaterial.SetColor(baseColorId, moonColor);
                }
            }
            else
            {
                if (fakeMoonQuad.gameObject.activeSelf) fakeMoonQuad.gameObject.SetActive(false);
                if (fakeMoonMaterial != null)
                {
                    Color moonColor = fakeMoonMaterial.GetColor(baseColorId);
                    moonColor.a = 0f;
                    fakeMoonMaterial.SetColor(baseColorId, moonColor);
                }
            }
        }

        if (sunMaterial != null)
        {
            sunMaterial.SetColor(baseColorId, sunBaseColor);
            sunMaterial.SetFloat(alphaId, sunAlphaCurve.Evaluate(timeOfDay));
        }

        if (moonMaterial != null)
        {
            moonMaterial.SetColor(baseColorId, moonBaseColor);
            moonMaterial.SetFloat(alphaId, moonAlphaCurve.Evaluate(timeOfDay));
        }

        // --- CROSSFADE DE LUCES Y CURVA ---
        if (sunLight != null)
        {
            float normalIntensity = sunIntensityCurve.Evaluate(timeOfDay);
            sunLight.intensity = Mathf.Lerp(normalIntensity, 0f, eclipseFactor);
        }

        if (eclipseLight != null && eclipseFactor > 0f)
        {
            eclipseLight.intensity = eclipseLightIntensityCurve.Evaluate(eclipseFactor);
            if (sunLight != null) eclipseLight.transform.rotation = sunLight.transform.rotation;
        }

        if (moonLight != null)
            moonLight.intensity = moonIntensityCurve.Evaluate(timeOfDay);

        if (sunLensFlare != null)
        {
            float normalFlare = sunLensFlareIntensityCurve.Evaluate(timeOfDay);
            sunLensFlare.intensity = Mathf.Lerp(normalFlare, 0f, eclipseFactor);
        }

        float currentFireflyIntensity = firefliesIntensityCurve.Evaluate(timeOfDay);
        for (int i = 0; i < fireflyEffects.Length; i++)
        {
            if (fireflyEffects[i] != null)
            {
                fireflyEffects[i].SetFloat(fireflyFloatId, currentFireflyIntensity);
            }
        }

        // --- COLORES (Cielo, Nubes y Agua) ---
        Color finalZenith = Color.Lerp(zenithGradient.Evaluate(timeOfDay), eclipseZenithColor, eclipseFactor);
        Color finalHorizon = Color.Lerp(horizonGradient.Evaluate(timeOfDay), eclipseHorizonColor, eclipseFactor);
        Color finalWater = Color.Lerp(waterHorizonGradient.Evaluate(timeOfDay), eclipseWaterColor, eclipseFactor);

        // ¡NUEVO! Color de las Nubes
        Color normalCloudColor = cloudColorGradient.Evaluate(timeOfDay);
        Color finalCloudColor = Color.Lerp(normalCloudColor, eclipseCloudColor, eclipseFactor);

        if (waterMaterial != null)
        {
            waterMaterial.SetColor(waterHorizonColorId, finalWater);
        }

        if (skyMaterial != null)
        {
            skyMaterial.SetVector(sunDirectionId, -sunDirection);
            skyMaterial.SetColor(zenithColorId, finalZenith);
            skyMaterial.SetColor(horizonColorId, finalHorizon);
            skyMaterial.SetFloat(starsIntensityId, starsIntensityCurve.Evaluate(timeOfDay));

            if (skyMaterial.HasProperty(cloudColorId))
                skyMaterial.SetColor(cloudColorId, finalCloudColor);

            if (skyMaterial.HasProperty(cloudDensityId))
                skyMaterial.SetFloat(cloudDensityId, cloudDensityCurve.Evaluate(timeOfDay));
        }
    }
}