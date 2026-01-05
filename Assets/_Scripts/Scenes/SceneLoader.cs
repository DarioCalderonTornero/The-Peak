using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public enum Scene
    {
        MenuScene,
        DarioScene,
        MRScene,
        AlexScene,
        JuanScene,
        TutorialScene
    }

    public static void LoadScene(Scene scene)
    {
        SceneManager.LoadScene(scene.ToString());
    }

    public static void RemoveScene(Scene scene)
    {
        string sceneName = scene.ToString();    

        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }

        else
        {
            Debug.LogWarning($"Scene {sceneName} was not loaded");
        }
    }
}
