using System.Collections.Generic; // 1. AÑADIDO: Necesario para usar HashSet
using UnityEngine;

public class ClimberDeathPointsManager : MonoBehaviour
{
    public static ClimberDeathPointsManager Instance { get; private set; }

    private const string RECORD_KEY = "CLIMBER_DEATH_RECORD";

    private int climbersDeath;
    private int recordDeaths;
    private int roundDeaths = 0; //

    // 2. AÑADIDO: Conjunto para registrar qué escaladores ya hemos contado en esta ronda
    private HashSet<ClimberMovement> countedClimbers = new HashSet<ClimberMovement>();

    private void Awake() //
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        recordDeaths = PlayerPrefs.GetInt(RECORD_KEY, 0);
    }

    private void Start() //
    {
        climbersDeath = 0;
    }

    private void OnDestroy() //
    {
        /* if (TurnManager.Instance != null)
            TurnManager.Instance.OnClimberTurnStart -= ResetRoundDeaths;*/
    }

    // 3. MODIFICADO: Ahora acepta opcionalmente al escalador para evitar duplicados
    public void AddClimberDeathPoints(ClimberMovement climber = null)
    {
        if (climber != null)
        {
            // Si este escalador ya fue contado en esta ronda, ignoramos la llamada para no duplicar puntos
            if (countedClimbers.Contains(climber))
            {
                Debug.Log($"[ClimberDeathPointsManager] {climber.name} ya fue contabilizado. Evitando duplicado.");
                return;
            }
            countedClimbers.Add(climber);
        }

        climbersDeath++;
        roundDeaths++; //
        Debug.Log($"[ClimberDeathPointsManager] AddClimberDeathPoints | roundDeaths={roundDeaths}"); //

        if (climbersDeath > recordDeaths) //
        {
            recordDeaths = climbersDeath;
            PlayerPrefs.SetInt(RECORD_KEY, recordDeaths);
            PlayerPrefs.Save();
        }
    }

    public int GetRoundDeaths() => roundDeaths; //

    public void ResetRoundDeaths() //
    {
        Debug.Log($"[ClimberDeathPointsManager] ResetRoundDeaths | roundDeaths antes={roundDeaths}"); //
        roundDeaths = 0; //

        // 4. AÑADIDO: Limpiamos el registro para que en la siguiente ronda se puedan volver a contar nuevos escaladores
        countedClimbers.Clear();
    }

    public int GetTotalClimberDeathPoints() => climbersDeath; //
    public int GetRecordDeaths() => recordDeaths; //

    public void ResetRunDeaths() //
    {
        climbersDeath = 0; //
    }
}