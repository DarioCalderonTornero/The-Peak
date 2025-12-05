using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        playButton.onClick.AddListener(() =>
        {
            SceneLoader.LoadScene(SceneLoader.Scene.DarioScene);
        });

        exitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }
}
