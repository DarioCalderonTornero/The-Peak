using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CicloDiaNocheHDRP : MonoBehaviour
{

    [Header("Referencias")]
    [Tooltip("Arrastra aquí el objeto que contiene el componente Volume con el cielo.")]
    public Volume targetVolume; // <--- Aquí asignarás tu volumen manualmente

    [Header("Configuración")]
    [Tooltip("Velocidad de rotación en grados por segundo.")]
    public float rotationSpeed = 0.4f;

    private HDRISky hdriSky;

    void Start()
    {
        // 1. Verificación de seguridad
        if (targetVolume == null)
        {
            Debug.LogError("¡Ojo! No has asignado el 'Target Volume' en el inspector del script HDRISkyRotator.");
            this.enabled = false; // Desactivamos el script para evitar errores
            return;
        }

        // 2. Buscamos el HDRI Sky dentro del volumen que tú asignaste
        if (targetVolume.profile.TryGet<HDRISky>(out hdriSky))
        {
            Debug.Log("HDRI Sky encontrado correctamente.");
        }
        else
        {
            Debug.LogWarning("El Volumen que asignaste no tiene el override 'HDRI Sky' añadido.");
        }
    }

    void Update()
    {
        if (hdriSky != null)
        {
            float currentRotation = hdriSky.rotation.value;
            float newRotation = currentRotation + (rotationSpeed * Time.deltaTime);
            newRotation %= 360f;
            hdriSky.rotation.value = newRotation;
        }
    }
    /*  [Header("Referencias HDRP")]
      [Tooltip("Volume global que contiene HDRI Sky + Exposure + Indirect Lighting Controller")]
      public Volume skyVolume;

      [Tooltip("Luz direccional que hace de sol (y/o luna)")]
      public Light sunLight;

      private HDRISky _hdriSky;
      private Exposure _exposure;
      private IndirectLightingController _indirectLighting;

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

      [Tooltip("Intensidad del sol durante la noche")]
      public float nightSunIntensity = 1000f; // como pediste

      [Header("Sol (temperatura de color)")]
      [Tooltip("Temperatura del sol al mediodía (luz blanca)")]
      public float daySunTemperature = 6500f;   // Kelvin

      [Tooltip("Temperatura del sol en amanecer/atardecer (luz cálida)")]
      public float twilightSunTemperature = 3500f; // más naranja

      [Tooltip("Temperatura del sol durante la noche (luz muy fría/azulada)")]
      public float nightSunTemperature = 9000f; // azulada

      [Header("Indirect Lighting")]
      [Tooltip("Diffuse de día (sin fade, valor fijo)")]
      public float dayIndirectDiffuse = 1f;
      [Tooltip("Diffuse de noche (sin fade, valor fijo)")]
      public float nightIndirectDiffuse = 50f;

      [Tooltip("Reflection de día (se usará en el inicio del fade)")]
      public float dayReflection = 5f;
      [Tooltip("Reflection de noche (fin del fade)")]
      public float nightReflection = 50f;

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

          // Indirect Lighting Controller (puede ser opcional)
          skyVolume.profile.TryGet(out _indirectLighting);
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

          // --- Indirect Lighting (Diffuse fijo, Reflection con fade suave) ---
          UpdateIndirectLighting(isNight);

          // --- Temperatura del sol (fade día/amanecer/noche) ---
          UpdateSunTemperature(currentHour, isNight);
      }

      private void UpdateIndirectLighting(bool isNight)
      {
          if (_indirectLighting == null) return;

          // Diffuse: sin fade, solo día vs noche
          _indirectLighting.indirectDiffuseLightingMultiplier.value =
              isNight ? nightIndirectDiffuse : dayIndirectDiffuse;

          // Reflection: sí hace fade en amanecer/atardecer
          float nightFactor = GetNightFactor(currentHour); // 0 día puro, 1 noche pura
          float refl = Mathf.Lerp(dayReflection, nightReflection, nightFactor);
          _indirectLighting.reflectionLightingMultiplier.value = refl;
      }

      private void UpdateSunTemperature(float hour, bool isNight)
      {
          if (sunLight == null) return;

          float targetTemp;

          // Tramos:
          //  6–8  : Noche -> Amanecer -> Día
          //  8–18 : Día
          // 18–20 : Día -> Atardecer -> Noche
          // Resto : Noche

          if (hour >= 6f && hour < 8f)
          {
              // Amanecer: noche -> twilight -> día
              float t = Mathf.InverseLerp(6f, 8f, hour); // 6->0, 8->1

              if (t < 0.5f)
              {
                  // 6–7: noche -> twilight
                  float tt = t / 0.5f;
                  targetTemp = Mathf.Lerp(nightSunTemperature, twilightSunTemperature, tt);
              }
              else
              {
                  // 7–8: twilight -> día
                  float tt = (t - 0.5f) / 0.5f;
                  targetTemp = Mathf.Lerp(twilightSunTemperature, daySunTemperature, tt);
              }
          }
          else if (hour >= 18f && hour < 20f)
          {
              // Atardecer: día -> twilight -> noche
              float t = Mathf.InverseLerp(18f, 20f, hour); // 18->0, 20->1

              if (t < 0.5f)
              {
                  // 18–19: día -> twilight
                  float tt = t / 0.5f;
                  targetTemp = Mathf.Lerp(daySunTemperature, twilightSunTemperature, tt);
              }
              else
              {
                  // 19–20: twilight -> noche
                  float tt = (t - 0.5f) / 0.5f;
                  targetTemp = Mathf.Lerp(twilightSunTemperature, nightSunTemperature, tt);
              }
          }
          else if (isNight)
          {
              // Noche pura
              targetTemp = nightSunTemperature;
          }
          else
          {
              // Día puro (8–18)
              targetTemp = daySunTemperature;
          }

          sunLight.useColorTemperature = true;
          sunLight.colorTemperature = targetTemp;
      }

      private bool IsNight(float hour)
      {
          if (nightEndHour > nightStartHour)
          {
              return hour >= nightStartHour && hour < nightEndHour;
          }

          return hour >= nightStartHour || hour < nightEndHour;
      }

      private float GetDayExposure(float hour)
      {
          if (hour <= 6f) return dayEdgeExposure;
          if (hour >= 20f) return dayEdgeExposure;

          if (hour <= 12f)
          {
              float t = Mathf.InverseLerp(6f, 12f, hour);
              return Mathf.Lerp(dayEdgeExposure, dayMidExposure, t);
          }
          else
          {
              float t = Mathf.InverseLerp(12f, 20f, hour);
              return Mathf.Lerp(dayMidExposure, dayEdgeExposure, t);
          }
      }

      private float GetNightExposure(float hour)
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

          if (nightTime <= 5f)
          {
              float t = nightTime / 5f;
              return Mathf.Lerp(dayEdgeExposure, nightMidExposure, t);
          }
          else
          {
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

      /// <summary>
      /// 0 = día puro, 1 = noche pura, valores intermedios sólo en 6–8 y 18–20.
      /// </summary>
      private float GetNightFactor(float hour)
      {
          // 18–20: día -> noche (atardecer)
          if (hour >= 18f && hour < 20f)
          {
              return Mathf.InverseLerp(18f, 20f, hour); // 18->0, 20->1
          }

          // 20–6: noche pura
          if (hour >= 20f || hour < 6f)
          {
              return 1f;
          }

          // 6–8: noche -> día (amanecer)
          if (hour >= 6f && hour < 8f)
          {
              float t = Mathf.InverseLerp(6f, 8f, hour); // 6->0, 8->1
              return 1f - t; // 6->1, 8->0
          }

          // 8–18: día puro
          return 0f;
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
      }*/
}