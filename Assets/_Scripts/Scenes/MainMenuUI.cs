using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button exitButton;

    float speed;

    Rigidbody rb;

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

    private void Update()
    {
        void Update()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(horizontal, 0, vertical);

            rb.MovePosition(transform.position + movement * speed * Time.deltaTime);
        }
    }
}
