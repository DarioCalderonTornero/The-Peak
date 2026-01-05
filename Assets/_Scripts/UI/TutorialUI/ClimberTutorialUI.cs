using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClimberTutorialUI : MonoBehaviour
{
    
    [SerializeField] private TutorialManager tutorialManager;

    [SerializeField] private Animator tutorialAnimator;

    [SerializeField] private Button continueClimberTutorialButton;

    private void Awake()
    {

        continueClimberTutorialButton.onClick.AddListener(() =>
        {
            tutorialAnimator.SetBool("HideClimberText", true);
            tutorialManager.CameraBlends();
        });
    }

    private void Start()
    {
        tutorialManager.OnShowClimberText += TutorialManager_OnShowClimberText;
        continueClimberTutorialButton.gameObject.SetActive(false);
    }

    private void TutorialManager_OnShowClimberText(object sender, System.EventArgs e)
    {
        tutorialAnimator.SetBool("ShowClimberText", true);
    }

    public void ShowcontinueClimberTutorialButton()
    {
        continueClimberTutorialButton.gameObject.SetActive(true);
    }
}
