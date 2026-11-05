using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GoToScene : MonoBehaviour
{
    public string sceneName;
    [SerializeField] private Button _button;

    private void Start()
    {
        _button.onClick.AddListener(GoScene);
    }

    public void GoScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}
