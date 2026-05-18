using UnityEngine;

[ExecuteAlways]
public class SkyController : MonoBehaviour
{
    public Material skyMaterial;

    [Header("Control del Tiempo")]
    [Tooltip("0 = Medianoche, 0.25 = Amanecer, 0.5 = Mediodía, 0.75 = Atardecer")]
    [Range(0f, 1f)]
    public float timeOfDay = 0.5f;
    public float dayDurationInSeconds = 120f; // Cuánto dura un día entero

    [Header("Gradientes de Color")]
    public Gradient zenithGradient;
    public Gradient horizonGradient;
    public Gradient sunColorGradient;

    [Header("Estrellas")]
    [Tooltip("Curva para encender/apagar estrellas. Ponla en 1 en los extremos (noche) y 0 en el centro (día)")]
    public AnimationCurve starsIntensityCurve;

    void Update()
    {
        // 1. Avanzar el tiempo solo si el juego está en marcha
        if (Application.isPlaying)
        {
            timeOfDay += Time.deltaTime / dayDurationInSeconds;
            if (timeOfDay >= 1f) timeOfDay -= 1f; // Reiniciar el ciclo
        }

        // 2. Rotar el sol según la hora del día (360 grados)
        // Restamos 90 para que en 0.25 (amanecer) esté en el horizonte
        float sunAngle = timeOfDay * 360f - 90f;
        transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f); // El 170f es la orientación, ajústalo a tu gusto

        // 3. Obtener la dirección y enviarla al Shader
        Vector3 sunDir = -transform.forward;
        if (skyMaterial != null)
        {
            skyMaterial.SetVector("_SunDirection", sunDir);

            // 4. Leer los colores de los gradientes según la hora y enviarlos
            skyMaterial.SetColor("_ZenithColor", zenithGradient.Evaluate(timeOfDay));
            skyMaterial.SetColor("_HorizonColor", horizonGradient.Evaluate(timeOfDay));
            skyMaterial.SetColor("_SunColor", sunColorGradient.Evaluate(timeOfDay));

            // 5. Configurar la intensidad de las estrellas
            float starIntensity = starsIntensityCurve.Evaluate(timeOfDay);
            skyMaterial.SetFloat("_StarsIntensity", starIntensity);
        }
    }
}