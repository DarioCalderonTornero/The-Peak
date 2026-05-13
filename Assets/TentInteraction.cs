using System.Collections.Generic;
using UnityEngine;

public class TentInteraction : MonoBehaviour
{
    public static TentInteraction Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private CampGraphBuilder campGraph;


    private List<ClimberMovement> currentlySelectedClimbers = new List<ClimberMovement>();
    private CampGraphBuilder.CampNode lastClickedNode;
    private int cycleIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (campGraph == null)
            campGraph = Object.FindFirstObjectByType<CampGraphBuilder>();
    }

    public void HandleCampClick(GameObject clickedObject)
    {
        CampGraphBuilder.CampNode clickedNode = GetNodeFromClickedObject(clickedObject);
        if (clickedNode != null && clickedNode.HasTent)
        {
            HandleCampClickInternal(clickedNode);
        }
    }

    private void HandleCampClickInternal(CampGraphBuilder.CampNode node)
    {
        int climberCount = node.presentClimbers != null ? node.presentClimbers.Count : 0;
        if (climberCount == 0) return;

        if (lastClickedNode != node)
        {
            lastClickedNode = node;
            cycleIndex = 0;
            ShowClimberAtIndex(node, cycleIndex);
            return;
        }

        int nextIndex = cycleIndex + 1;

        if (nextIndex >= climberCount)
        {
            ClearAll();
            return;
        }

        cycleIndex = nextIndex;
        ShowClimberAtIndex(node, cycleIndex);
    }

    private void ShowClimberAtIndex(CampGraphBuilder.CampNode node, int index)
    {
        ClearLines();

        ClimberMovement climber = node.presentClimbers[index];
        if (climber == null) return;

        climber.SetSelected(true);
        currentlySelectedClimbers.Add(climber);

        ClimberListUI.Instance?.OnWorldClimberSelected(climber);
    }

    private CampGraphBuilder.CampNode GetNodeFromClickedObject(GameObject clickedObject)
    {
        if (campGraph == null || campGraph.nodes == null) return null;

        foreach (var node in campGraph.nodes)
        {
            if (node.instantiatedTent != null &&
               (clickedObject == node.instantiatedTent ||
                clickedObject.transform.IsChildOf(node.instantiatedTent.transform)))
                return node;

            if (clickedObject.name.Contains("Camp_INVISIBLE") &&
                Vector3.Distance(clickedObject.transform.position, node.position) < 0.1f)
                return node;
        }

        return null;
    }

    private void ClearLines()
    {
        foreach (var climber in currentlySelectedClimbers)
        {
            if (climber != null)
                climber.SetSelected(false);
        }
        currentlySelectedClimbers.Clear();
    }

    public void ClearAll()
    {
        ClearLines();
        lastClickedNode = null;
        cycleIndex = -1;
        ClimberListUI.Instance?.OnWorldClimberDeselected();
    }
}