using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PointsManager : MonoBehaviour
{
    public static PointsManager Instance { get; private set; }

    public int maxPoints = 10;
    private int currentPoints;

    public TextMeshProUGUI pointsText;
    public event Action<int> OnPointsChanged;

    [SerializeField] private AudioClip audioClip;

    public int GetCurrentPoints() => currentPoints;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        currentPoints = maxPoints;
        UpdateUI();
    }

    public bool CanAfford(int cost)
    {
        return currentPoints >= cost;
    }

    public bool SpendPoints(int cost)
    {
        if (CanAfford(cost))
        {
            currentPoints -= cost;
            UpdateUI();
            return true;
        }
        return false;
    }

    public void AddPoints(int amount)
    {
        //Temporal_Sound_Music.Instance.PlaySound(audioClip, 0.5f);
        currentPoints += amount;
        UpdateUI();
    }

    

    private void UpdateUI()
    {
        if (pointsText != null)
        {
            pointsText.text = $"Puntos: {currentPoints}";
        }

        OnPointsChanged?.Invoke(currentPoints);
    }
}
