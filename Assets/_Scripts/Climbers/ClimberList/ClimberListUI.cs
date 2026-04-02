using System.Collections.Generic;
using UnityEngine;

public class ClimberListUI : MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private GameObject climberEntryPrefab;

    private readonly Dictionary<ClimberMovement, ClimberEntryUI> entries = new();

    private void Start()
    {
        if (ClimberRegistry.Instance == null)
        {
            Debug.LogError("[ClimberListUI] No hay ClimberRegistry en escena.");
            return;
        }

        ClimberRegistry.Instance.OnClimberRegistered += HandleClimberRegistered;
        ClimberRegistry.Instance.OnClimberUnregistered += HandleClimberUnregistered;

        // Por si ya había escaladores al activarse el UI
        foreach (var climber in ClimberRegistry.Instance.Climbers)
            HandleClimberRegistered(climber);
    }

    private void OnDestroy()
    {
        if (ClimberRegistry.Instance == null) return;
        ClimberRegistry.Instance.OnClimberRegistered -= HandleClimberRegistered;
        ClimberRegistry.Instance.OnClimberUnregistered -= HandleClimberUnregistered;
    }

    private void HandleClimberRegistered(ClimberMovement climber)
    {
        if (climberEntryPrefab == null || content == null) return;
        if (entries.ContainsKey(climber)) return;

        GameObject go = Instantiate(climberEntryPrefab, content);
        ClimberEntryUI entry = go.GetComponent<ClimberEntryUI>();

        if (entry == null)
        {
            Debug.LogError("[ClimberListUI] El prefab no tiene ClimberEntryUI.");
            Destroy(go);
            return;
        }

        entry.Setup(climber);
        entries[climber] = entry;
    }

    private void HandleClimberUnregistered(ClimberMovement climber)
    {
        if (!entries.TryGetValue(climber, out ClimberEntryUI entry)) return;
        entries.Remove(climber);

        if (entry != null)
            Destroy(entry.gameObject);
    }
}