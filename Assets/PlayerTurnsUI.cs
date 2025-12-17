using TMPro;
using UnityEngine;

public class PlayerTurnsUI : MonoBehaviour
{

    [SerializeField] public TextMeshProUGUI currentTurns;

    private void Start()
    {
        currentTurns.text = "1";
    }
}
