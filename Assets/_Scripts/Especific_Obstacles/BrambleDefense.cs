using System.Collections;
using UnityEngine;

public class BrambleDefense : BaseDefense
{
    [Header("Ralentización")]
    [SerializeField] private float slowFactor = 0.7f; // 30% más lento

    [Header("Aparición")]
    [SerializeField] private float spawnDuration = 0.25f;

    [Header("Vibración cuando hay escaladores dentro")]
    [SerializeField] private float shakeMagnitude = 0.05f;   // cuánto se mueve
    [SerializeField] private float shakeSpeed = 25f;         // qué rápido vibra

    private Vector3 originalLocalScale;
    private Vector3 originalLocalPosition;

    private Coroutine spawnRoutine;
    private Coroutine shakeRoutine;
    private int climbersInside = 0;

    private void Awake()
    {
        // Guardamos la escala y posición original del prefab
        originalLocalScale = transform.localScale;
        originalLocalPosition = transform.localPosition;

        // Aseguramos que el collider sea trigger
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    public override void Initialize()
    {
        base.Initialize();

        // Reiniciamos escala para animación de "salir del suelo"
        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(SpawnFromGround());
    }

    private IEnumerator SpawnFromGround()
    {
        // Empezamos desde escala 0 (invisible)
        transform.localScale = Vector3.zero;

        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);

            // Curva sencilla: acelera rápido al principio y se suaviza al final
            float eased = t * t * (3f - 2f * t); // easing tipo smoothstep

            transform.localScale = originalLocalScale * eased;
            yield return null;
        }

        transform.localScale = originalLocalScale;
        spawnRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // 🔹 Pasamos por el sistema de equipamiento
        var loadout = other.GetComponent<ClimberLoadout>();
        bool isImmuneToBramble = false;

        if (loadout != null)
        {
            // Notificamos a los equipos que hemos encontrado este obstáculo
            loadout.TryHandleObstacle(ObstacleType.Bramble);

            // Preguntamos si alguno puede manejarlo (tiene BrambleBreakerEquipment o similar)
            isImmuneToBramble = loadout.CanHandleObstacle(ObstacleType.Bramble);
        }

        // Si TIENE equipamiento que maneja este tipo de obstáculo → no le afecta la zarza
        if (isImmuneToBramble)
        {
            // Opcional: debug
            // Debug.Log("[BrambleDefense] Climber inmune a las zarzas.");
            return;
        }

        // Si NO es inmune → aplicamos la lógica original de zarzas
        climber.SetExternalSpeedMultiplier(slowFactor);
        Debug.Log($"[BrambleDefense] {other.name} ha entrado en zarza");

        climbersInside++;
        if (climbersInside == 1)
        {
            // Primer escalador dentro → empezamos a vibrar
            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeWhileActive());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var climber = other.GetComponent<ClimberMovement>();
        if (climber == null) return;

        // 🔹 Avisamos al equipamiento de que salimos del obstáculo (por si quiere reaccionar)
        var loadout = other.GetComponent<ClimberLoadout>();
        if (loadout != null)
        {
            loadout.TryHandleObstacleExit(ObstacleType.Bramble);
        }

        // Restauramos velocidad (si no se había ralentizado, no pasa nada)
        climber.SetExternalSpeedMultiplier(1f);
        Debug.Log($"[BrambleDefense] {other.name} ha salido de zarza");

        climbersInside = Mathf.Max(0, climbersInside - 1);
        if (climbersInside == 0)
        {
            // Ya no quedan escaladores → paramos vibración
            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);
            shakeRoutine = null;
            transform.localPosition = originalLocalPosition;
        }
    }

    private IEnumerator ShakeWhileActive()
    {
        float time = 0f;

        while (climbersInside > 0)
        {
            time += Time.deltaTime * shakeSpeed;

            // Pequeño movimiento aleatorio alrededor de la posición original
            Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
            transform.localPosition = originalLocalPosition + offset;

            yield return null;
        }

        // Aseguramos que vuelva a su sitio exacto
        transform.localPosition = originalLocalPosition;
        shakeRoutine = null;
    }
}
