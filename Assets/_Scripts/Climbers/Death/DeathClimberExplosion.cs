using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeathClimberExplosion : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float upwardsModifier = 0.5f;
    [SerializeField] private float randomImpulse = 2f;
    [Tooltip("Fuerza de rotación para que los trozos giren en el aire.")]
    [SerializeField] private float randomSpin = 10f;
    [Tooltip("Desfase para que la explosión salga del pecho y no de los pies.")]
    [SerializeField] private Vector3 explosionOffset = new Vector3(0f, 1f, 0f);

    [Header("Melt Settings")]
    [SerializeField] private float meltDuration = 3f;
    [SerializeField] private string targetMaterialName = "MountainClimberMat_Death";
    //[SerializeField] private float meltXZExpand = 2f;
    //[SerializeField] private float meltYSpeedMultiplier = 1.5f;

    [Header("Cleanup")]
    [SerializeField] private float lifeTime = 3f;

    private DeathAnimation deathAnimation;
    private DeathCause deathCause;

    private void Awake()
    {
        deathAnimation = GetComponent<DeathAnimation>();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            rb.isKinematic = true;
    }

    private void OnEnable()
    {
        if (deathAnimation != null)
            deathAnimation.OnReadyForExplosion += HandleAnimationComplete;
    }

    private void OnDisable()
    {
        if (deathAnimation != null)
            deathAnimation.OnReadyForExplosion -= HandleAnimationComplete;
    }

    public void SetDeathCause(DeathCause cause)
    {
        deathCause = cause;
    }

    private void HandleAnimationComplete()
    {
        switch (deathCause)
        {
            case DeathCause.Mud:    
                StartCoroutine(MeltRoutine());
                break;
            case DeathCause.Stamina:
                ClimberExplosion(); break;
            case DeathCause.Bramble:
                ClimberBrambleCut(); break;
            case DeathCause.BadBerry:
                ClimberExplosion(); break;
            case DeathCause.Geyser:
                ClimberExplosion(); break;
            case DeathCause.Snow:
                Debug.Log("Snow Death Explosion");
                ClimberExplosion(); break;
            case DeathCause.StormyCloud:
                ClimberToBlackColor(); break;
            default:
                ClimberExplosion();
                break;
        }
    }


    private void ClimberExplosion()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        Vector3 origin = transform.position + explosionOffset;

        foreach (Rigidbody rb in rbs)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddExplosionForce(explosionForce, origin, explosionRadius, upwardsModifier, ForceMode.Impulse);

            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y) * 0.3f;
            rb.AddForce(randomDir.normalized * randomImpulse, ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * randomSpin, ForceMode.Impulse);
        }

        Destroy(gameObject, lifeTime);
    }

    private void ClimberBrambleCut()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rbs)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        Destroy(gameObject, lifeTime);
    }
    private void ClimberToBlackColor()
    {
        float animDuration = 0.0f;

        //StartCoroutine(deathAnimation.ScaleRoutine(transform.localScale * 1.5f, animDuration));
        //StartCoroutine(deathAnimation.ShakeRoutine(animDuration, shakeMagnitude));
        StartCoroutine(deathAnimation.BodyColorToRedRoutine(Color.black, animDuration));

        StartCoroutine(BlackColorSequence(animDuration));
    }

    private IEnumerator BlackColorSequence(float animDuration)
    {
        yield return new WaitForSeconds(animDuration);
        deathAnimation.NotifyComplete();
        ClimberExplosion();
    }

    private IEnumerator MeltRoutine()
    {
        var pieceData = new List<(Rigidbody rb, List<Material> mats, float alphaSpeed, Vector3 originalScale)>();

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            if (rb == null) continue;

            rb.isKinematic = true;
            rb.useGravity = false;

            var mats = new List<Material>();
            foreach (var r in rb.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mats.Add(mat);
                }
            }

            float alphaSpeed = Random.Range(0.9f, 1.1f);
            pieceData.Add((rb, mats, alphaSpeed, rb.transform.localScale));
        }

        float shakeDuration = 0.2f;
        float shakeElapsed = 0f;
        float shakeMag = 0.01f;
        var originalPositions = new Dictionary<Rigidbody, Vector3>();
        foreach (var (rb, _, _, _) in pieceData)
            if (rb != null) originalPositions[rb] = rb.transform.localPosition;

        while (shakeElapsed < shakeDuration)
        {
            shakeElapsed += Time.deltaTime;

            foreach (var (rb, _, _, _) in pieceData)
            {
                if (rb == null) continue;
                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)
                ) * shakeMag;
                rb.transform.localPosition = originalPositions[rb] + offset;
            }

            yield return null;
        }

        // Restaurar posiciones tras el shake
        foreach (var (rb, _, _, _) in pieceData)
            if (rb != null && originalPositions.ContainsKey(rb))
                rb.transform.localPosition = originalPositions[rb];

        //  Fase 2: Fade + aplastamiento + caída
        foreach (var (rb, _, _, _) in pieceData)
        {
            if (rb == null) continue;
            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 drift = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.1f, 0.4f),
                Random.Range(-0.5f, 0.5f)
            );
            rb.linearVelocity = drift;
        }

        float duration = Random.Range(1f, 2.5f); float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            foreach (var (rb, mats, alphaSpeed, originalScale) in pieceData)
            {
                if (rb == null) continue;

                float t = Mathf.Clamp01((elapsed / duration) * alphaSpeed);

                // Alpha agresivo desde el inicio
                float alpha = Mathf.Lerp(1f, 0f, Mathf.Pow(t, 0.2f));
                // Aplastamiento en Y
                float scaleY = Mathf.Lerp(originalScale.y, originalScale.y * 0.1f, t);
                rb.transform.localScale = new Vector3(
                    originalScale.x,
                    scaleY,
                    originalScale.z
                );

                foreach (var mat in mats)
                {
                    if (mat == null) continue;
                    Color c = mat.GetColor("_BaseColor");
                    c.a = alpha;
                    mat.SetColor("_BaseColor", c);
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}