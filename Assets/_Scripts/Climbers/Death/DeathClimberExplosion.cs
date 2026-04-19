using UnityEngine;

/// <summary>
/// Se añade al prefab visual de muerte junto a DeathAnimation.
/// Escucha OnAnimationComplete y lanza la explosión cuando la animación termina.
/// </summary>
public class DeathClimberExplosion : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 10f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float upwardsModifier = 0.5f;

    [Header("Variance")]
    [SerializeField] private float randomImpulse = 2f;
    [Tooltip("Fuerza de rotación para que los trozos giren en el aire.")]
    [SerializeField] private float randomSpin = 10f;
    [Tooltip("Desfase para que la explosión salga del pecho y no de los pies.")]
    [SerializeField] private Vector3 explosionOffset = new Vector3(0f, 1f, 0f);

    [Header("Cleanup")]
    [SerializeField] private float lifeTime = 3f;

    private DeathAnimation deathAnimation;

    private void Awake()
    {
        deathAnimation = GetComponent<DeathAnimation>();

        // Congelar física hasta que explote
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
        }
    }

    private void OnEnable()
    {
        if (deathAnimation != null)
            deathAnimation.OnAnimationComplete += HandleAnimationComplete;
    }

    private void OnDisable()
    {
        if (deathAnimation != null)
            deathAnimation.OnAnimationComplete -= HandleAnimationComplete;
    }

    private void HandleAnimationComplete()
    {
        ClimberExplosion();
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

            Vector3 randomTorque = Random.onUnitSphere * randomSpin;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
        }

        Destroy(gameObject, lifeTime);
    }
}
