using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Application.targetFrameRate = 60; // Set the target frame rate to 60 FPS
    }

}
