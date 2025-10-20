using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PointsManager : MonoBehaviour
{
    public static PointsManager Instance { get; private set; }

    public int startingPoints = 10;
    private int currentPoints;

    public TextMeshProUGUI pointsText;
    public event Action<int> OnPointsChanged;

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
        currentPoints = startingPoints;
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
