using System.Collections.Generic;
using UnityEngine;

public class ClimberListUI : MonoBehaviour
{
    public static ClimberListUI Instance { get; private set; }

    [SerializeField] private Transform content;
    [SerializeField] private GameObject climberEntryPrefab;

    private readonly Dictionary<ClimberMovement, ClimberEntryUI> entries = new();
    private ClimberMovement selectedClimber;

    [Header("Cámara")]
    [SerializeField] private LayerMask mountainLayer;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private float sideOffset = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (ClimberRegistry.Instance == null)
        {
            Debug.LogError("[ClimberListUI] No hay ClimberRegistry en escena.");
            return;
        }

        ClimberRegistry.Instance.OnClimberRegistered += HandleClimberRegistered;
        ClimberRegistry.Instance.OnClimberUnregistered += HandleClimberUnregistered;

        foreach (var climber in ClimberRegistry.Instance.Climbers)
            HandleClimberRegistered(climber);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnStart += HandleTurnChanged;
            TurnManager.Instance.OnPlayerTurnStart += HandleTurnChanged;
        }

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnClimberSelected += OnWorldClimberSelected;
            SelectionManager.Instance.OnClimberDeselected += OnWorldClimberDeselected;
        }
    }

    private void OnDestroy()
    {
        if (ClimberRegistry.Instance != null)
        {
            ClimberRegistry.Instance.OnClimberRegistered -= HandleClimberRegistered;
            ClimberRegistry.Instance.OnClimberUnregistered -= HandleClimberUnregistered;
        }

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnClimberTurnStart -= HandleTurnChanged;
            TurnManager.Instance.OnPlayerTurnStart -= HandleTurnChanged;
        }

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnClimberSelected -= OnWorldClimberSelected;
            SelectionManager.Instance.OnClimberDeselected -= OnWorldClimberDeselected;
        }
    }

    // ─── Registro ────────────────────────────────────────────────────────────

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

        RefreshList();
    }

    private void HandleClimberUnregistered(ClimberMovement climber)
    {
        if (!entries.TryGetValue(climber, out ClimberEntryUI entry)) return;
        entries.Remove(climber);

        if (climber == selectedClimber)
            selectedClimber = null;

        if (entry != null)
            Destroy(entry.gameObject);

        RefreshList();
    }

    // ─── Refresh ─────────────────────────────────────────────────────────────

    private void HandleTurnChanged()
    {
        RefreshList();
    }

    private void RefreshList()
    {
        if (entries.Count == 0) return;

        var sorted = new List<ClimberMovement>(entries.Keys);
        sorted.Sort((a, b) => b.GetAltitude().CompareTo(a.GetAltitude()));

        ClimberMovement mostDangerous = sorted.Count > 0 ? sorted[0] : null;

        for (int i = 0; i < sorted.Count; i++)
        {
            var climber = sorted[i];
            if (!entries.TryGetValue(climber, out ClimberEntryUI entry)) continue;

            entry.transform.SetSiblingIndex(i);
            //entry.SetUrgent(climber == mostDangerous);
            entry.RefreshState();
        }
    }

    // ─── Selección ────────────────────────────────────────────────────────────

    public void OnEntryClicked(ClimberMovement climber)
    {
        selectedClimber = climber;

        FreeCameraMovement cam = Object.FindFirstObjectByType<FreeCameraMovement>();
        if (cam == null) return;

        if (ClimberCameraUtils.TryCalculateCameraPosition(
            climber.transform,
            mountainLayer,
            cameraDistance,
            heightOffset,
            sideOffset,
            out Vector3 pos,
            out Quaternion rot))
        {
            cam.TeleportTo(pos, rot);
        }
    }

    public void OnWorldClimberSelected(ClimberMovement climber)
    {
        selectedClimber = climber;

        foreach (var kvp in entries)
        {
            bool isSelected = kvp.Key == climber;
            kvp.Value.SetSelected(isSelected);
        }
    }

    public void OnWorldClimberDeselected()
    {
        selectedClimber = null;
        foreach (var kvp in entries)
            kvp.Value.SetSelected(false);
    }
}