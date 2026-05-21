using UnityEngine;

[ExecuteAlways]
public class SkyController : MonoBehaviour
{
    public Material skyMaterial;

    [Header("Referencias del Cielo")]
    public Transform skySphere;
    public Transform sunQuad;
    public Transform moonQuad;
    public float distance = 450f;

    [Header("Luces Reales (Sombra e Iluminación)")]
    public Light sunLight;
    public AnimationCurve sunIntensityCurve;
    public Light moonLight;
    public AnimationCurve moonIntensityCurve;

    [Header("Control del Tiempo")]
    [Range(0f, 1f)] public float timeOfDay = 0.5f;
    public float dayDurationInSeconds = 120f;

    [Header("Gradientes de Color del Cielo")]
    public Gradient zenithGradient;
    public Gradient horizonGradient;

    [Header("Control de Nubes y Estrellas")]
    public Gradient cloudColorGradient; // Color de las nubes según la hora
    public AnimationCurve cloudDensityCurve; // 0 = muchas nubes, 1 = sin nubes
    public AnimationCurve starsIntensityCurve;


    private int sunDirectionId;
    private int zenithColorId;
    private int horizonColorId;
    private int starsIntensityId;
    private int cloudColorId;
    private int cloudDensityId;
    void Start()
    {
        // Convertimos los textos en IDs una sola vez al inicio
        sunDirectionId = Shader.PropertyToID("_SunDirection");
        zenithColorId = Shader.PropertyToID("_ZenithColor");
        horizonColorId = Shader.PropertyToID("_HorizonColor");
        starsIntensityId = Shader.PropertyToID("_StarsIntensity");
        cloudColorId = Shader.PropertyToID("_CloudColor");
        cloudDensityId = Shader.PropertyToID("_CloudDensity");
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            timeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        // 1. Calcular rotación general
        float sunAngle = timeOfDay * 360f - 90f;
        Quaternion rotation = Quaternion.Euler(sunAngle, 170f, 0f);
        transform.rotation = rotation;

        // 2. Posicionar los Quads
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

        // 3. Actualizar Luces
        if (sunLight != null)
        {
            sunLight.intensity = sunIntensityCurve.Evaluate(timeOfDay);
        }

        if (moonLight != null)
        {
            moonLight.intensity = moonIntensityCurve.Evaluate(timeOfDay);
        }

        // 4. Pasar todos los datos al Shader usando los IDs optimizados
        if (skyMaterial != null)
        {
            skyMaterial.SetVector(sunDirectionId, -sunDirection);
            skyMaterial.SetColor(zenithColorId, zenithGradient.Evaluate(timeOfDay));
            skyMaterial.SetColor(horizonColorId, horizonGradient.Evaluate(timeOfDay));
            skyMaterial.SetFloat(starsIntensityId, starsIntensityCurve.Evaluate(timeOfDay));

            if (skyMaterial.HasProperty(cloudColorId))
                skyMaterial.SetColor(cloudColorId, cloudColorGradient.Evaluate(timeOfDay));
            if (skyMaterial.HasProperty(cloudDensityId))
                skyMaterial.SetFloat(cloudDensityId, cloudDensityCurve.Evaluate(timeOfDay));
        }
    }
}