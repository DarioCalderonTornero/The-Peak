using UnityEngine;
using UnityEngine.UI;

public class DayNightLightController : MonoBehaviour
{
    [Header("Referencia al Slider de hora")]
    public Slider timeSlider;  // Debe ir de 0 a 24

    [Header("Hora inicial")]
    [Range(0f, 24f)]
    public float startHour = 8f;  // Hora con la que quieres que empiece el juego

    [Header("Debug / Estado actual")]
    [Range(0f, 24f)]
    public float currentHour = 12f; // Solo para ver en el inspector

    [Header("Rotación de la luz")]
    [Tooltip("Ángulo Y (rotación horizontal) del sol. Cambia esto para ajustar la dirección del amanecer/atardecer.")]
    public float sunDirectionY = 0f;

    private void Start()
    {
        // Aseguramos el rango
        startHour = Mathf.Clamp(startHour, 0f, 24f);
        currentHour = Mathf.Clamp(startHour, 0f, 24f);

        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 24f;

            // Ponemos el slider en la hora inicial
            timeSlider.value = currentHour;

            // Conectamos evento
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        // Actualizamos la luz con la hora inicial
        UpdateLightRotation();
    }

    public void OnTimeSliderChanged(float value)
    {
        currentHour = value;
        UpdateLightRotation();
    }

    private void UpdateLightRotation()
    {
        // Normalizamos la hora (0-24) a 0-1
        float t = currentHour / 24f;

        // -90 = medianoche, 0 = amanecer, 90 = mediodía, 180 = atardecer, 270 = medianoche
        float sunAngleX = Mathf.Lerp(-90f, 270f, t);

        transform.rotation = Quaternion.Euler(sunAngleX, sunDirectionY, 0f);
    }
}
