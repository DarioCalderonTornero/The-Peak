using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CicloDiaNocheHDRP : MonoBehaviour
{
    [Header("Referencias HDRP")]
    [Tooltip("Volume global que contiene HDRI Sky + Exposure")]
    public Volume skyVolume;

    [Tooltip("Luz direccional que hace de sol (y/o luna)")]
    public Light sunLight;

    private HDRISky _hdriSky;
    private Exposure _exposure;

    [Header("Cubemaps de cielo")]
    [Tooltip("HDRI/Cubemap para el cielo de día (handpainted)")]
    public Cubemap dayCubemap;
    [Tooltip("HDRI/Cubemap para el cielo de noche (handpainted)")]
    public Cubemap nightCubemap;

    [Header("Configuración del ciclo")]
    [Tooltip("Duración de un día completo del juego (0-24h) en segundos reales")]
    public float dayDurationInSeconds = 120f; // 2 minutos = 1 día de juego

    [Tooltip("Hora (0-24) a partir de la cual se considera noche (ej: 20 = 20:00)")]
    [Range(0f, 24f)]
    public float nightStartHour = 20f;

    [Tooltip("Hora (0-24) hasta la cual se considera noche (ej: 6 = 06:00)")]
    [Range(0f, 24f)]
    public float nightEndHour = 6f;

    [Header("Exposición (valores fijos)")]
    [Tooltip("Exposición más clara (mediodía)")]
    public float dayMidExposure = 10f;   // 12–13h
    [Tooltip("Exposición al inicio/fin del día (6h y 20h)")]
    public float dayEdgeExposure = 13f;  // amanecer/atardecer
    [Tooltip("Exposición en el centro de la noche (alrededor de la 1h)")]
    public float nightMidExposure = 15f; // noche profunda

    [Header("Sol (intensidad)")]
    [Tooltip("Intensidad del sol al mediodía")]
    public float daySunIntensity = 5000f;
    [Tooltip("Intensidad mínima del sol durante la noche")]
    public float nightSunIntensity = 0.1f;

    [Header("Debug")]
    [Tooltip("Hora actual del juego (0–24). Puedes cambiarla en el inspector para probar.")]
    public float currentHour = 12f;

    private float _elapsedTime;
    private bool _isPaused = false;

    private void Awake()
    {
        if (skyVolume == null)
        {
            Debug.LogError("[CicloDiaNocheHDRP] Falta asignar el Volume.");
            enabled = false;
            return;
        }

        if (!skyVolume.profile.TryGet(out _hdriSky))
        {
            Debug.LogError("[CicloDiaNocheHDRP] El Volume no tiene override de HDRI Sky.");
        }

        if (!skyVolume.profile.TryGet(out _exposure))
        {
            Debug.LogError("[CicloDiaNocheHDRP] El Volume no tiene override de Exposure.");
        }
    }

    private void Start()
    {
        currentHour = Mathf.Repeat(currentHour, 24f);

        float dayProgress = currentHour / 24f;
        _elapsedTime = dayProgress * dayDurationInSeconds;

        ApplySkyAndLighting();
    }

    private void Update()
    {
        if (_isPaused || dayDurationInSeconds <= 0f)
            return;

        _elapsedTime += Time.deltaTime;

        float dayProgress = (_elapsedTime / dayDurationInSeconds) % 1f;

        currentHour = dayProgress * 24f;

        ApplySkyAndLighting();
    }

    private void ApplySkyAndLighting()
    {
        bool isNight = IsNight(currentHour);

        // --- Cielo HDRI día/noche ---
        if (_hdriSky != null)
        {
            _hdriSky.hdriSky.value = isNight ? nightCubemap : dayCubemap;

            float rotation = (currentHour / 24f) * 360f;
            _hdriSky.rotation.value = rotation;
        }

        // --- Exposición global ---
        if (_exposure != null)
        {
            float exposure = isNight
                ? GetNightExposure(currentHour)
                : GetDayExposure(currentHour);

            _exposure.mode.value = ExposureMode.Fixed;
            _exposure.fixedExposure.value = exposure;
        }

        // --- Sol (rotación + intensidad) ---
        if (sunLight != null)
        {
            float sunAngle = (currentHour / 24f) * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 0f, 0f);

            float sunIntensity = isNight
                ? Mathf.Lerp(daySunIntensity, nightSunIntensity, GetNightSunFactor(currentHour))
                : Mathf.Lerp(nightSunIntensity, daySunIntensity, GetDaySunFactor(currentHour));

            sunLight.intensity = sunIntensity;
        }
    }

    private bool IsNight(float hour)
    {
        if (nightEndHour > nightStartHour)
        {
            return hour >= nightStartHour && hour < nightEndHour;
        }

        return hour >= nightStartHour || hour < nightEndHour;
    }

    /// <summary>
    /// DÍA: 
    ///  - 6h  -> 13 (dayEdgeExposure, más oscuro)
    ///  - 12–13h -> 10 (dayMidExposure, más claro)
    ///  - 20h -> 13 (dayEdgeExposure, más oscuro)
    /// Fuera 6–20h no debería usarse (es noche).
    /// </summary>
    private float GetDayExposure(float hour)
    {
        // Seguridad: si está fuera del día, usa el borde del día
        if (hour <= 6f) return dayEdgeExposure;
        if (hour >= 20f) return dayEdgeExposure;

        // 6 -> 12 : 13 -> 10 (oscuro -> claro)
        if (hour <= 12f)
        {
            float t = Mathf.InverseLerp(6f, 12f, hour); // 6 -> 0, 12 -> 1
            return Mathf.Lerp(dayEdgeExposure, dayMidExposure, t);
        }
        // 12 -> 20 : 10 -> 13 (claro -> oscuro)
        else
        {
            float t = Mathf.InverseLerp(12f, 20f, hour); // 12 -> 0, 20 -> 1
            return Mathf.Lerp(dayMidExposure, dayEdgeExposure, t);
        }
    }

    /// <summary>
    /// NOCHE (20–6, cruzando medianoche):
    ///  - 20h -> 13 (borde atardecer)
    ///  - ~1h -> 15 (nightMidExposure, más oscuro)
    ///  - 6h  -> 13 (borde amanecer)
    /// </summary>
    private float GetNightExposure(float hour)
    {
        // Mapeo 20–6 a 0–10
        float nightTime;
        if (hour >= 20f)
        {
            nightTime = hour - 20f; // 20–24 => 0–4
        }
        else
        {
            nightTime = hour + 4f;  // 0–6  => 4–10
        }

        // 0 -> 20:00   (borde)
        // 5 -> 1:00    (noche profunda)
        // 10 -> 6:00   (borde)

        if (nightTime <= 5f)
        {
            // 0–5: 13 -> 15 (borde -> más oscuro)
            float t = nightTime / 5f;
            return Mathf.Lerp(dayEdgeExposure, nightMidExposure, t);
        }
        else
        {
            // 5–10: 15 -> 13 (más oscuro -> borde)
            float t = (nightTime - 5f) / 5f;
            return Mathf.Lerp(nightMidExposure, dayEdgeExposure, t);
        }
    }

    private float GetDaySunFactor(float hour)
    {
        if (hour < 6f || hour > 20f)
            return 0f;

        if (hour <= 12f)
        {
            return Mathf.InverseLerp(6f, 12f, hour);   // amanecer -> mediodía
        }
        else
        {
            return 1f - Mathf.InverseLerp(12f, 20f, hour); // mediodía -> atardecer
        }
    }

    private float GetNightSunFactor(float hour)
    {
        float nightTime;
        if (hour >= 20f)
        {
            nightTime = hour - 20f; // 20–24 => 0–4
        }
        else
        {
            nightTime = hour + 4f;  // 0–6  => 4–10
        }

        if (nightTime <= 2f)
        {
            return nightTime / 2f;          // 0–2 => 0–1
        }
        else if (nightTime >= 8f)
        {
            return (10f - nightTime) / 2f;  // 8–10 => 1–0
        }
        else
        {
            return 0f;
        }
    }

    public void PauseCycle() => _isPaused = true;
    public void ResumeCycle() => _isPaused = false;
    public void TogglePause() => _isPaused = !_isPaused;

    public void SetHour(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        currentHour = hour;

        float dayProgress = currentHour / 24f;
        _elapsedTime = dayProgress * dayDurationInSeconds;

        ApplySkyAndLighting();
    }
}