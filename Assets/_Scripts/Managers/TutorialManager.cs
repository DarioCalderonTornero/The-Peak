using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System;

public class TutorialManager : MonoBehaviour
{
    public event EventHandler OnShowClimberText;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera climberTutorialVCam;
    [SerializeField] private CinemachineCamera followClimberTutorialVCam;

    [Header("Images")]
    [SerializeField] private Image fadeImage;

    [SerializeField] private Animator tutorialAnimator;

    [SerializeField] private AudioClip climberTutorialAudioClip;


    private bool isFadeOutComplete = false;

    private void Awake()
    {
        CinemachineBlendManager.Instance.SetBlendCut();
    }

    private void Start()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private void Update()
    {

    }

    private IEnumerator FadeOutRoutine()
    {
        yield return new WaitForSeconds(1f);

        float duration = 3f;
        float time = 0f;

        Color color = fadeImage.color;

        while (time < duration)
        {
            time += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, time / duration);
            fadeImage.color = new Color(color.r, color.g, color.b, alpha);
            Temporal_Sound_Music.Instance.PlaySound(climberTutorialAudioClip, 1f);
            yield return null;
        }

        OnShowClimberText?.Invoke(this, EventArgs.Empty);
        fadeImage.color = new Color(color.r, color.g, color.b, 0f);
    }

    public void CameraBlends()
    {
        CinemachineBlendManager.Instance.SetBlendEaseInOut(3f);
        climberTutorialVCam.Priority = 0;
        followClimberTutorialVCam.Priority = 100;
    }
}
