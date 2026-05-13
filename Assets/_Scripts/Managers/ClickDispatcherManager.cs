using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ClickDispatcherManager : MonoBehaviour
{
    [SerializeField] private LayerMask climberLayer;
    [SerializeField] private LayerMask tentLayer;

    [SerializeField] private AudioClip clickTentAudioClip;

    private void Start()
    {
        InputManager.Instance.OnClimberClickRoute += OnClick;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnClimberClickRoute -= OnClick;
    }

    private void OnClick(object sender, System.EventArgs e)
    {
        if (IsPointerOverUI()) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // ¿Tienda?
        if (Physics.Raycast(ray, out RaycastHit tentHit, 1000f, tentLayer))
        {
            TentInteraction.Instance?.HandleCampClick(tentHit.collider.gameObject);
            Temporal_Sound_Music.Instance.Play2DSound(clickTentAudioClip, 1f);
            return;
        }

        // ¿Escalador?
        if (Physics.Raycast(ray, out RaycastHit climberHit, 1000f, climberLayer))
        {
            var climber = climberHit.collider.GetComponentInParent<ClimberMovement>();
            if (climber != null && !climber.IsInsideTent)
            {
                SelectionManager.Instance?.HandleClimberClicked(climber);
                Temporal_Sound_Music.Instance.Play2DSound(clickTentAudioClip, 1f);
                return;
            }
        }

        // ¿Nada?
        SelectionManager.Instance?.Deselect();
        TentInteraction.Instance?.ClearAll();
    }

    private bool IsPointerOverUI()
    {
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results.Count > 0;
    }
}
