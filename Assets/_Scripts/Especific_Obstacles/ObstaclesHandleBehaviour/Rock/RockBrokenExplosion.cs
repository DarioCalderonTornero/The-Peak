using UnityEngine;

public class RockBrokenExplosion : MonoBehaviour
{
    [SerializeField] private float explosionForce = 6.0f;
    [SerializeField] private float explosionRadious = 3.0f;
    [SerializeField] private float upwardsModifier = 0.4f;
    [SerializeField] private float randomImpulse = 1.5f;
    [SerializeField] private float lifeTime = 3f;

    private void Start()
    {
        var rbs = GetComponentsInChildren<Rigidbody>();

        Vector3 origin = transform.position;

        foreach (var rb in rbs)
        {
            if (rb == null) continue;

            rb.AddExplosionForce(explosionForce, origin, explosionRadious, upwardsModifier, ForceMode.Impulse);

            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y) * 0.3f;

            rb.AddForce(randomDir.normalized * randomImpulse, ForceMode.Impulse);
        }

        Destroy(gameObject, lifeTime);
    }
}
