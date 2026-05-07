using UnityEngine;

public class CloudDefensePreview : MonoBehaviour
{
    [SerializeField] private Transform visualChild;
    [SerializeField] private Transform rainObject;
    [SerializeField] private float cloudHeight = 3f;
    [SerializeField] private Vector3 cloudOffset = new Vector3(0.5f, 0f, 0f);

    private float currentYaw = 0f;
    public float GetCurrentYaw() => currentYaw;

    private bool isPreview = true;
    public void StopPreview() { isPreview = false; }


    private void Update()
    {
        if (!isPreview) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentYaw += 90f;
            if (currentYaw >= 360f) currentYaw = 0f;
        }

        // Rotar el offset según el yaw actual
        Vector3 rotatedOffset = Quaternion.Euler(0f, currentYaw, 0f) * cloudOffset;
        Vector3 cloudPosition = transform.position + Vector3.up * cloudHeight + rotatedOffset;

        if (visualChild != null)
        {
            visualChild.position = cloudPosition;
            visualChild.rotation = Quaternion.Euler(-90f, currentYaw, 0f);
        }

        if (rainObject != null)
        {
            rainObject.position = cloudPosition;
            rainObject.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }
    }
}