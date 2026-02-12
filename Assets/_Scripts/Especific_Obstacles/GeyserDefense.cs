using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

public class GeyserDefense : BaseDefense
{
    [Header("Trigger")]
    [SerializeField] private LayerMask climberLayer;

    [Header("Capture")]
    [SerializeField] private float captureDelay = 0.08f;   // cuanto más, más “se mete dentro” antes de pararse

    [Header("Hold + Shake (Geyser)")]
    [SerializeField] private float holdSeconds = 3f;

    [Tooltip("Magnitud del temblor al inicio del hold (suave).")]
    [SerializeField] private float shakeMagnitudeStart = 0.02f;

    [Tooltip("Magnitud del temblor al final del hold (fuerte, antes de salir volando).")]
    [SerializeField] private float shakeMagnitudeEnd = 0.10f;

    [SerializeField] private float shakeFrequency = 25f;

    [Tooltip("Cómo progresa el temblor de suave->fuerte. X=tiempo normalizado (0..1), Y=mezcla (0..1).")]
    [SerializeField] private AnimationCurve shakeRamp = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Hold + Shake (Climber)")]
    [SerializeField] private bool shakeClimber = true;

    [Tooltip("Multiplicador del temblor aplicado al climber respecto al géiser.")]
    [SerializeField] private float climberShakeMultiplier = 1.0f;

    [Header("Launch")]
    [SerializeField] private float launchForce = 18f;      // ↑ velocidad inicial (más alto = sube más rápido)
    [SerializeField] private float gravity = 25f;          // ↓ aceleración (más alto = cae más rápido)
    [SerializeField] private float noGravityTime = 0.0f;   // si quieres “0 gravedad” al inicio

    [Header("Air Spin")]
    [SerializeField] private float minSpinDegPerSec = 250f;
    [SerializeField] private float maxSpinDegPerSec = 650f;

    [Header("Destroy conditions")]
    [SerializeField] private float destroySecondsAfterLanding = 2f;
    [SerializeField] private float destroyMaxAirTime = 10f;

    [Header("Cooldown (turns)")]
    [SerializeField] private int cooldownTurns = 2;

    [Header("Ground detection")]
    [SerializeField] private LayerMask groundMask;

    // --- internal state ---
    private bool isBusy = false;
    private bool inCooldown = false;
    private int cooldownRemaining = 0;

    private ClimberMovement capturedClimber;
    private NavMeshAgent capturedAgent;

    private Vector3 originalPos;                 // posición base del géiser
    private Vector3 capturedClimberBasePos;      // posición base del climber mientras está “retenido”

    private Coroutine holdRoutine;
    private Coroutine captureRoutine;

    private readonly Dictionary<Transform, LaunchedData> launched = new();

    [Header("After landing")]
    [SerializeField] private bool returnToPathAfterLanding = true;
    [SerializeField] private float navmeshSnapRadius = 3f;

    // Guardar destino mientras está volando (por transform)
    private readonly Dictionary<Transform, Vector3> resumeDestination = new();

    [Header("Landing detection")]
    [SerializeField] private float landingGraceTime = 0.25f;     // ignora colisiones justo al despegar
    [SerializeField] private float minDownVelToCountLanding = 1.0f; // debe ir bajando al menos esto
    [SerializeField] private float minGroundNormalY = 0.55f;        // suelo “bastante” hacia arriba

    [Header("VFX")]
    [SerializeField] private VisualEffect geyserVFX;

    [SerializeField] private float eruptFadeSeconds = 2f;
    [SerializeField] private float eruptHoldSeconds = 3f; 

    // IDs (evita strings todo el rato)
    private static readonly int BubblingID = Shader.PropertyToID("Bubbling");
    private static readonly int EruptingPowerID = Shader.PropertyToID("EruptingPower");
    private static readonly int AlturaEspumaID = Shader.PropertyToID("AlturaEspuma");
    private static readonly int EspumaArribaID = Shader.PropertyToID("EspumaArriba");

    [SerializeField] private float espumaRampUpSeconds = 1.5f;
    [SerializeField] private float espumaMaxAltura = 1f;

    private Coroutine eruptFadeRoutine;

    private struct LaunchedData
    {
        public Transform t;
        public Rigidbody rb;

        public float launchedAtTime;

        public bool landed;
        public float landedAtTime;
    }

    private void OnEnable()
    {
        originalPos = transform.position;

        SetBubbling(false);
        SetEruptingPower(0f);
        SetEspumaActiva(false);
        SetAlturaEspuma(0f);

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd += OnClimberTurnEnd;
    }

    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnEnd -= OnClimberTurnEnd;
    }

    private void OnClimberTurnEnd()
    {
        if (!inCooldown) return;

        cooldownRemaining--;
        if (cooldownRemaining <= 0)
        {
            inCooldown = false;
            cooldownRemaining = 0;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBusy || inCooldown) return;

        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        if (climberLayer.value != 0)
        {
            int layerBit = 1 << other.gameObject.layer;
            if ((climberLayer.value & layerBit) == 0) return;
        }

        if (captureRoutine != null) StopCoroutine(captureRoutine);
        captureRoutine = StartCoroutine(CaptureAfterDelay(climber, captureDelay));
    }

    private IEnumerator CaptureAfterDelay(ClimberMovement climber, float delay)
    {
        float t = 0f;
        while (t < delay)
        {
            if (climber == null) yield break;
            if (isBusy || inCooldown) yield break;

            t += Time.deltaTime;
            yield return null;
        }

        if (isBusy || inCooldown) yield break;
        if (climber == null) yield break;

        capturedClimber = climber;
        capturedAgent = climber.GetComponent<NavMeshAgent>();

        // Guardamos la “base” del climber para poder temblarlo y luego restaurarlo bien
        capturedClimberBasePos = capturedClimber.transform.position;

        if (capturedAgent != null)
        {
            capturedAgent.isStopped = true;
            capturedAgent.updatePosition = false;
            capturedAgent.updateRotation = false;
        }

        capturedClimber.SetExternallyDoneThisTurn(true);

        isBusy = true;

        SetBubbling(true);

        if (holdRoutine != null) StopCoroutine(holdRoutine);
        holdRoutine = StartCoroutine(HoldThenLaunchRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        if (climber == capturedClimber)
            CancelHold();
    }

    private void CancelHold()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        // Reset géiser
        transform.position = originalPos;

        // Reset climber (por si se quedó con offset de temblor)
        if (capturedClimber != null)
        {
            capturedClimber.transform.position = capturedClimberBasePos;
            capturedClimber.SetExternallyDoneThisTurn(false);

            if (capturedAgent == null)
                capturedAgent = capturedClimber.GetComponent<NavMeshAgent>();

            if (capturedAgent != null)
            {
                capturedAgent.updatePosition = true;
                capturedAgent.updateRotation = true;
                capturedAgent.isStopped = false;
            }
        }

        capturedClimber = null;
        capturedAgent = null;
        isBusy = false;

        SetBubbling(false);
        SetEspumaActiva(false);
        SetAlturaEspuma(0f);
    }

    private void SetBubbling(bool value)
    {
        if (geyserVFX == null) return;
        geyserVFX.SetBool(BubblingID, value);
    }

    private void SetEruptingPower(float value)
    {
        if (geyserVFX == null) return;
        geyserVFX.SetFloat(EruptingPowerID, value);
    }

    private void TriggerEruptionFade()
    {
        if (geyserVFX == null) return;

        // al erupcionar: instant a 1 y baja a 0 en 2s
        SetEruptingPower(1f);

        if (eruptFadeRoutine != null) StopCoroutine(eruptFadeRoutine);
        eruptFadeRoutine = StartCoroutine(EruptFadeRoutine());
    }

    private IEnumerator EruptFadeRoutine()
    {
        // Activamos la espuma, pero empezamos en 0
        SetEspumaActiva(true);
        SetEruptingPower(0f);
        SetAlturaEspuma(0f);

        // 1) RAMP UP solo para la espuma, pero el chorro se pone instantáneo a 1
        SetEruptingPower(1f);  // El chorro va a 1 al instante
        SetAlturaEspuma(0f);   // Comienza la espuma en 0

        float ramp = Mathf.Max(0f, espumaRampUpSeconds); // Solo la espuma tarda en subir
        float tr = 0f;

        while (tr < ramp)
        {
            tr += Time.deltaTime;
            float n = (ramp <= 0.0001f) ? 1f : Mathf.Clamp01(tr / ramp);

            // Sincronizamos la espuma, pero el chorro ya está al máximo
            SetAlturaEspuma(n * espumaMaxAltura); // Solo afecta a la espuma

            yield return null;
        }

        // 2) Mantenemos el chorro al máximo y la espuma al máximo también
        SetEruptingPower(1f);  // Aseguramos que el chorro esté a tope
        SetAlturaEspuma(espumaMaxAltura); // Espuma al máximo

        float hold = Mathf.Max(0f, eruptHoldSeconds);
        float th = 0f;

        while (th < hold)
        {
            th += Time.deltaTime;
            yield return null;
        }

        // 3) FADE (1 -> 0) para chorro y espuma
        float fade = Mathf.Max(0.01f, eruptFadeSeconds);
        float tf = 0f;

        while (tf < fade)
        {
            tf += Time.deltaTime;
            float n = Mathf.Clamp01(tf / fade);

            float v = Mathf.Lerp(1f, 0f, n);
            SetEruptingPower(v); // disminuye el chorro
            SetAlturaEspuma(v * espumaMaxAltura); // disminuye la espuma

            yield return null;
        }

        // 4) Apagamos todo
        SetEruptingPower(0f);
        SetAlturaEspuma(0f);
        SetEspumaActiva(false);

        eruptFadeRoutine = null;
    }

    private IEnumerator HoldThenLaunchRoutine()
    {
        float t = 0f;

        while (t < holdSeconds)
        {
            if (capturedClimber == null)
            {
                CancelHold();
                yield break;
            }

            t += Time.deltaTime;

            // 1) Progreso 0..1 del hold
            float n = (holdSeconds <= 0.0001f) ? 1f : Mathf.Clamp01(t / holdSeconds);
            float mix = (shakeRamp != null) ? Mathf.Clamp01(shakeRamp.Evaluate(n)) : n;
            float mag = Mathf.Lerp(shakeMagnitudeStart, shakeMagnitudeEnd, mix);

            // 2) Normal del suelo bajo el géiser
            Vector3 groundNormal = Vector3.up;
            Vector3 rayOrigin = originalPos + Vector3.up * 1.0f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                groundNormal = hit.normal;

            // 3) Base tangente al plano (t1, t2)
            Vector3 t1 = Vector3.Cross(groundNormal, Vector3.up);
            if (t1.sqrMagnitude < 0.0001f)
                t1 = Vector3.Cross(groundNormal, Vector3.right);
            t1.Normalize();
            Vector3 t2 = Vector3.Cross(groundNormal, t1).normalized;

            // 4) Offset SOLO en el plano del suelo (no puede apuntar “hacia dentro”)
            float a = Time.time * shakeFrequency;
            Vector3 offset = (Mathf.Sin(a) * t1 + Mathf.Cos(a) * t2) * mag;

            // (Opcional) micro-separación del suelo para evitar cualquier “rozamiento visual”
            // Vector3 lift = groundNormal * 0.02f;

            // Aplicar temblor
            transform.position = originalPos + offset; // + lift

            if (shakeClimber && capturedClimber != null)
            {
                capturedClimber.transform.position = capturedClimberBasePos + (offset * climberShakeMultiplier); // + lift
            }

            yield return null; // <- CLAVE: mantiene el hold durante frames reales
        }

        // Reset posiciones antes de lanzar
        transform.position = originalPos;
        if (capturedClimber != null)
            capturedClimber.transform.position = capturedClimberBasePos;

        SetBubbling(false);
        TriggerEruptionFade();
        
        LaunchAndKillCaptured();


        inCooldown = true;
        cooldownRemaining = cooldownTurns;

        holdRoutine = null;
    }

    private void LaunchAndKillCaptured()
    {
        if (capturedClimber == null)
        {
            isBusy = false;
            return;
        }

        Transform tr = capturedClimber.transform;

        // 1) Guardar destino (para retomar ruta)
        NavMeshAgent agent = capturedClimber.GetComponent<NavMeshAgent>();
        Vector3 dest = (agent != null) ? agent.destination : capturedClimber.originalDestination;
        resumeDestination[tr] = dest;

        // 2) Parar lógica de turno + apagar NavMesh para que no pelee con física
        capturedClimber.SetExternallyDoneThisTurn(true);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        // 3) Física temporal SOLO durante el vuelo
        Rigidbody rb = tr.GetComponent<Rigidbody>();
        if (rb == null) rb = tr.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // limpiar velocidades
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 4) Impulso más “real” (más pop + opcional forward)
        Vector3 up = transform.up.normalized;
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (fwdOnPlane.sqrMagnitude < 0.0001f) fwdOnPlane = Vector3.zero;

        float forwardFactor = 0.15f; // 0 = vertical total, 0.10-0.25 queda bien
        Vector3 launchDir = (up + fwdOnPlane * forwardFactor).normalized;

        rb.linearVelocity = launchDir * launchForce;

        // spin
        Vector3 randomAxis = Random.onUnitSphere;
        float spin = Random.Range(minSpinDegPerSec, maxSpinDegPerSec);
        rb.angularVelocity = randomAxis * (spin * Mathf.Deg2Rad);

        // Reporter para marcar "landed"
        var rep = tr.GetComponent<GeyserLaunchedReporter>();
        if (rep == null) rep = tr.gameObject.AddComponent<GeyserLaunchedReporter>();
        rep.owner = this;
        rep.groundMask = groundMask;
        rep.launchedAtTime = Time.time;
        rep.rb = rb;

        launched[tr] = new LaunchedData
        {
            t = tr,
            rb = rb,
            launchedAtTime = Time.time,
            landed = false,
            landedAtTime = 0f
        };

        // 5) Caída más rápida sin tocar Physics.gravity global
        StartCoroutine(ExtraGravityWhileAirborne(tr, rb));

        // limpiar captura
        capturedClimber = null;
        capturedAgent = null;
        isBusy = false;
    }

    private IEnumerator ExtraGravityWhileAirborne(Transform tr, Rigidbody rb)
    {
        if (tr == null || rb == null) yield break;

        float extraG = Mathf.Max(0f, gravity); // usa tu "gravity" como gravedad extra (prueba 25-45)

        while (tr != null && rb != null)
        {
            if (launched.TryGetValue(tr, out var d) && d.landed)
                yield break;

            if (extraG > 0f)
                rb.AddForce(Vector3.down * extraG, ForceMode.Acceleration);

            yield return null;
        }
    }

    public void NotifyLanded(Transform tr)
    {
        if (tr == null) return;
        if (!launched.TryGetValue(tr, out var d)) return;
        if (d.landed) return;

        d.landed = true;
        d.landedAtTime = Time.time;
        launched[tr] = d;
    }


    private void Update()
    {
        if (launched.Count == 0) return;

        var keys = new List<Transform>(launched.Keys);

        foreach (var key in keys)
        {
            if (key == null)
            {
                launched.Remove(key);
                resumeDestination.Remove(key);
                continue;
            }

            var d = launched[key];

            if (Time.time - d.launchedAtTime >= destroyMaxAirTime)
            {
                var climber = key.GetComponent<ClimberMovement>();
                Vector3 pos = key.position;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
                    {
                        climber = climber,
                        position = pos,
                        cause = GameManager.DeathCause.Geyser 
                    });
                }

                Destroy(key.gameObject);
                launched.Remove(key);
                resumeDestination.Remove(key);
                continue;
            }


            // Aún no ha aterrizado
            if (!d.landed) continue;

            if (Time.time - d.landedAtTime >= destroySecondsAfterLanding)
            {
                var climber = key.GetComponent<ClimberMovement>();
                Vector3 pos = key.position;

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
                    {
                        climber = climber,
                        position = pos,
                        cause = GameManager.DeathCause.Geyser 
                    });
                }

                Destroy(key.gameObject);
                launched.Remove(key);
                resumeDestination.Remove(key);
            }

        }
    }


    private class GeyserLaunchedReporter : MonoBehaviour
    {
        public GeyserDefense owner;
        public LayerMask groundMask;

        public float launchedAtTime;
        public Rigidbody rb;

        private bool reported;

        private void OnCollisionEnter(Collision collision)
        {
            if (reported) return;
            if (owner == null) return;

            // 1) Ignorar colisiones justo al despegar
            if (Time.time - launchedAtTime < owner.landingGraceTime)
                return;

            // 2) Filtrar por máscara de suelo
            int layerBit = 1 << collision.gameObject.layer;
            if ((groundMask.value & layerBit) == 0) return;

            // 3) Debe ser un impacto “de aterrizaje”
            //    - o bien el contacto tiene normal hacia arriba (suelo)
            //    - y/o el cuerpo venía bajando con cierta velocidad
            float bestNormalY = -1f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                float ny = collision.GetContact(i).normal.y;
                if (ny > bestNormalY) bestNormalY = ny;
            }

            float vy = (rb != null) ? rb.linearVelocity.y : 0f;

            bool looksLikeGround = bestNormalY >= owner.minGroundNormalY;
            bool wasFalling = vy <= -owner.minDownVelToCountLanding;

            if (!looksLikeGround && !wasFalling)
                return;

            reported = true;
            owner.NotifyLanded(transform);
        }
    }

    private void SetAlturaEspuma(float value)
    {
        if (geyserVFX == null) return;
        geyserVFX.SetFloat(AlturaEspumaID, value);
    }

    private void SetEspumaActiva(bool value)
    {
        if (geyserVFX == null) return;
        geyserVFX.SetBool(EspumaArribaID, value);
    }

}
