using UnityEngine;


[CreateAssetMenu(fileName = "DeathEffectConfig", menuName = "Game/DeathEffectConfig")]
public class DeathEffectConfigSO : ScriptableObject
{
    public DeathCause deathCause;
    public AudioClip deathAudioClip;
    //public GameObject deathEffectPrefab;
    public GameObject deathVisualPrefab;
    public GameObject deathVFX_Effect;
}
