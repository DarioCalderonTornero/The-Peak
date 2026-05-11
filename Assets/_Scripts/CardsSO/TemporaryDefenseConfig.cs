using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "TemporaryDefenseConfig", menuName = "TowerDefense/TemporaryDefenseConfig")]
public class TemporaryDefenseConfig : ScriptableObject
{
    public static TemporaryDefenseConfig Instance { get; private set; }

    [Header("Indicador de turnos")]
    public TMP_FontAsset numberFont;
    public Color numberColor = Color.white;
    public float floatHeight = 2f;
    public float showDuration = 1.5f;
    public float fadeDuration = 0.4f;
    public float numberScale = 0.015f;

    private void OnEnable()
    {
        Instance = this;
    }
}