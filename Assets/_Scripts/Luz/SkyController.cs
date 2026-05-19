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

    void Update()
    {
        if (Application.isPlaying)
        {
            timeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f;
        }

        // 1. Calcular rotación
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

        // 3. Pasar todos los datos al Shader
        if (skyMaterial != null)
        {
            skyMaterial.SetVector("_SunDirection", -sunDirection);
            skyMaterial.SetColor("_ZenithColor", zenithGradient.Evaluate(timeOfDay));
            skyMaterial.SetColor("_HorizonColor", horizonGradient.Evaluate(timeOfDay));
            skyMaterial.SetFloat("_StarsIntensity", starsIntensityCurve.Evaluate(timeOfDay));

            // Nuevas variables de nubes
            skyMaterial.SetColor("_CloudColor", cloudColorGradient.Evaluate(timeOfDay));
            skyMaterial.SetFloat("_CloudDensity", cloudDensityCurve.Evaluate(timeOfDay));
        }
    }
}