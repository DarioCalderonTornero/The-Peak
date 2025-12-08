using UnityEngine;

public class Pickaxe : MonoBehaviour
{
    [SerializeField] private GameObject pickaxe;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        pickaxe.gameObject.SetActive(true);
    }

    public void Hide()
    {
        pickaxe.gameObject.SetActive(false);
    }
}
