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

    [Header("Air Spin")]
    [SerializeField] private float minSpinDegPerSec = 250f;
    [SerializeField] private float maxSpinDegPerSec = 650f;

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

    [Header("VFX")]
    [SerializeField] private VisualEffect geyserVFX;
    [SerializeField] private float eruptFadeSeconds = 2f;
    [SerializeField] private float eruptHoldSeconds = 3f;

    // IDs
    private static readonly int BubblingID = Shader.PropertyToID("Bubbling");
    private static readonly int EruptingPowerID = Shader.PropertyToID("EruptingPower");
    private static readonly int AlturaEspumaID = Shader.PropertyToID("AlturaEspuma");
    private static readonly int EspumaArribaID = Shader.PropertyToID("EspumaArriba");

    [SerializeField] private float espumaRampUpSeconds = 1.5f;
    [SerializeField] private float espumaMaxAltura = 1f;

    private Coroutine eruptFadeRoutine;

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

        // --- ¡EL CHIVATAZO TEMPRANO! ---
        // Avisamos al Manager AHORA para que la cámara venga a ver el temblor y el despegue
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyClimberDied(new GameManager.DeathInfo
            {
                climber = capturedClimber,
                position = transform.position,
                cause = DeathCause.Geyser
            });

            // Repartimos los puntos directamente aquí
            if (ClimberDeathPointsManager.Instance != null) ClimberDeathPointsManager.Instance.AddClimberDeathPoints();
            if (PointsManager.Instance != null) PointsManager.Instance.AddPoints(10);
        }
        // -------------------------------

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

    private IEnumerator HoldThenLaunchRoutine()
    {
        float t = 0f;

        while (t < holdSeconds)
        {
            // El manager podría haber destruido al escalador si el jugador saltó la cinemática
            if (capturedClimber == null)
            {
                CancelHold();
                yield break;
            }

            t += Time.deltaTime;

            // Progreso 0..1 del hold
            float n = (holdSeconds <= 0.0001f) ? 1f : Mathf.Clamp01(t / holdSeconds);
            float mix = (shakeRamp != null) ? Mathf.Clamp01(shakeRamp.Evaluate(n)) : n;
            float mag = Mathf.Lerp(shakeMagnitudeStart, shakeMagnitudeEnd, mix);

            // Normal del suelo bajo el géiser
            Vector3 groundNormal = Vector3.up;
            Vector3 rayOrigin = originalPos + Vector3.up * 1.0f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
                groundNormal = hit.normal;

            // Base tangente al plano (t1, t2)
            Vector3 t1 = Vector3.Cross(groundNormal, Vector3.up);
            if (t1.sqrMagnitude < 0.0001f)
                t1 = Vector3.Cross(groundNormal, Vector3.right);
            t1.Normalize();
            Vector3 t2 = Vector3.Cross(groundNormal, t1).normalized;

            // Offset SOLO en el plano del suelo
            float a = Time.time * shakeFrequency;
            Vector3 offset = (Mathf.Sin(a) * t1 + Mathf.Cos(a) * t2) * mag;

            // Aplicar temblor
            transform.position = originalPos + offset;

            if (shakeClimber && capturedClimber != null)
            {
                capturedClimber.transform.position = capturedClimberBasePos + (offset * climberShakeMultiplier);
            }

            yield return null;
        }

        // Reset posiciones antes de lanzar
        transform.position = originalPos;
        if (capturedClimber != null)
            capturedClimber.transform.position = capturedClimberBasePos;

        SetBubbling(false);
        TriggerEruptionFade();

        LaunchCaptured();

        inCooldown = true;
        cooldownRemaining = cooldownTurns;

        holdRoutine = null;
    }

    private void LaunchCaptured()
    {
        if (capturedClimber == null)
        {
            isBusy = false;
            return;
        }

        Transform tr = capturedClimber.transform;

        // Apagar NavMesh para que no pelee con la física
        if (capturedAgent != null)
        {
            capturedAgent.isStopped = true;
            capturedAgent.enabled = false;
        }

        // Física temporal SOLO durante el vuelo
        Rigidbody rb = tr.GetComponent<Rigidbody>();
        if (rb == null) rb = tr.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Impulso direccional
        Vector3 up = transform.up.normalized;
        Vector3 fwdOnPlane = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (fwdOnPlane.sqrMagnitude < 0.0001f) fwdOnPlane = Vector3.zero;

        float forwardFactor = 0.15f;
        Vector3 launchDir = (up + fwdOnPlane * forwardFactor).normalized;

        rb.linearVelocity = launchDir * launchForce;

        // Spin
        Vector3 randomAxis = Random.onUnitSphere;
        float spin = Random.Range(minSpinDegPerSec, maxSpinDegPerSec);
        rb.angularVelocity = randomAxis * (spin * Mathf.Deg2Rad);

        // Caída más rápida (el bucle se destruirá solo cuando el Manager elimine el GameObject)
        StartCoroutine(ExtraGravityWhileAirborne(tr, rb));

        // Limpiar captura (el Géiser se desentiende)
        capturedClimber = null;
        capturedAgent = null;
        isBusy = false;
    }

    private IEnumerator ExtraGravityWhileAirborne(Transform tr, Rigidbody rb)
    {
        float extraG = Mathf.Max(0f, gravity);

        // Como el Manager se encargará de destruirlo, este bucle correrá hasta que el objeto desaparezca
        while (tr != null && rb != null)
        {
            if (extraG > 0f)
                rb.AddForce(Vector3.down * extraG, ForceMode.Acceleration);

            // Usamos FixedUpdate porque estamos aplicando fuerzas físicas
            yield return new WaitForFixedUpdate();
        }
    }

    // --- MÉTODOS DE VFX INTACTOS ---
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

    private void TriggerEruptionFade()
    {
        if (geyserVFX == null) return;
        SetEruptingPower(1f);

        if (eruptFadeRoutine != null) StopCoroutine(eruptFadeRoutine);
        eruptFadeRoutine = StartCoroutine(EruptFadeRoutine());
    }

    private IEnumerator EruptFadeRoutine()
    {
        SetEspumaActiva(true);
        SetEruptingPower(0f);
        SetAlturaEspuma(0f);

        SetEruptingPower(1f);
        SetAlturaEspuma(0f);

        float ramp = Mathf.Max(0f, espumaRampUpSeconds);
        float tr = 0f;

        while (tr < ramp)
        {
            tr += Time.deltaTime;
            float n = (ramp <= 0.0001f) ? 1f : Mathf.Clamp01(tr / ramp);
            SetAlturaEspuma(n * espumaMaxAltura);
            yield return null;
        }

        SetEruptingPower(1f);
        SetAlturaEspuma(espumaMaxAltura);

        float hold = Mathf.Max(0f, eruptHoldSeconds);
        float th = 0f;

        while (th < hold)
        {
            th += Time.deltaTime;
            yield return null;
        }

        float fade = Mathf.Max(0.01f, eruptFadeSeconds);
        float tf = 0f;

        while (tf < fade)
        {
            tf += Time.deltaTime;
            float n = Mathf.Clamp01(tf / fade);

            float v = Mathf.Lerp(1f, 0f, n);
            SetEruptingPower(v);
            SetAlturaEspuma(v * espumaMaxAltura);

            yield return null;
        }

        SetEruptingPower(0f);
        SetAlturaEspuma(0f);
        SetEspumaActiva(false);

        eruptFadeRoutine = null;
    }
}