using UnityEngine;
using UnityEngine.UI;

public class TurnsStateMachine : MonoBehaviour
{
    public static TurnsStateMachine Instance { get; private set; }

    [SerializeField] private float maxClimberTime = 10f;
    [SerializeField] private Button nexTurnButton;
    [SerializeField] private float time;

    private void Awake()
    {
        Instance = this;


        nexTurnButton.onClick.AddListener(() =>
        {
            state = GameState.ClimberTurn;
        });
    }

    public enum GameState
    {
        Idle,
        PlayerTurn,
        ClimberTurn,
        GameOverState,
    }

    public GameState state;

    private void Start()
    {
        state = GameState.PlayerTurn;
    }

    private void Update()
    {
        switch (state)
        {
            case GameState.Idle:
                Debug.Log("Idle");
                break;
            case GameState.PlayerTurn:

                Debug.Log("PlayerTurn");
                break;
            case GameState.ClimberTurn:

               
                time += Time.deltaTime;

                if (time >= maxClimberTime)
                {
                    time = 0f;
                    state = GameState.PlayerTurn;
                    Debug.Log("ClimberTurn");
                }

                break;
            case GameState.GameOverState:
                Debug.Log("GameOver");
                break;

            default:
                break;
        }

        Debug.Log(state);
    }
}
