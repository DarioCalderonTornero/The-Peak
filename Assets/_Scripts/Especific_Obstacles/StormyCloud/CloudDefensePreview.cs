using UnityEngine;

public class CloudDefensePreview : MonoBehaviour
{
    [SerializeField] private Transform visualChild;
    [SerializeField] private GameObject rainObject;
    [SerializeField] private float cloudHeight = 3f;
    [SerializeField] private Vector3 cloudOffset = new Vector3(0.5f, 0f, 0f);

    private void Update()
    {
        Vector3 cloudPosition = transform.position + Vector3.up * cloudHeight + cloudOffset;

        if (visualChild != null)
        {
            visualChild.position = cloudPosition;
            visualChild.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        if (rainObject != null)
        {
            rainObject.transform.position = cloudPosition;
            rainObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
}