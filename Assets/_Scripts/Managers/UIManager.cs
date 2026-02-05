using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Dictionary<UICanvasType, UICanvas> canvases = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(ShowAlexCanvas());
    }

    private IEnumerator ShowAlexCanvas()
    {
        yield return new WaitForSeconds(0.5f);
        ShowOnly(UICanvasType.Alex);
    }

    public void RegisterCanvas(UICanvas canvas)
    {
        canvases[canvas.canvasType] = canvas;
    }

    public void UnregisterCanvas(UICanvas canvas)
    {
        if (canvases.ContainsKey(canvas.canvasType))
            canvases.Remove(canvas.canvasType);
    }

    public void ShowCanvas(UICanvasType type)
    {
        if (canvases.TryGetValue(type, out var canvas))
            canvas.Show();
    }

    public void HideCanvas(UICanvasType type)
    {
        if (canvases.TryGetValue(type, out var canvas))
            canvas.Hide();
    }

    public void HideAll()
    {
        foreach (var canvas in canvases.Values)
            canvas.Hide();
    }

    public void ShowMultiple(params UICanvasType[] types)
    {
        foreach (var type in types)
        {
            ShowCanvas(type);
        }
    }

    public void HideMultiple(params UICanvasType[] types)
    {
        foreach (var type in types)
        {
            HideCanvas(type);
        }
    }

    public void ShowOnly(UICanvasType type)
    {
        HideAll();
        ShowCanvas(type);
    }
}
