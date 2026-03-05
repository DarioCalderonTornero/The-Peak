using UnityEngine;

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

    private void Start()
    {
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();

        Vector3 origin = transform.position + explosionOffset;

        foreach (Rigidbody rb in rbs)
        {
            if (rb == null) continue;

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