
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private float deltaTime = 0f;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int fps = Mathf.RoundToInt(1f / deltaTime);

        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.green;
        style.fontStyle = FontStyle.Bold;

        GUI.Label(
            new Rect(20, 20, 300, 60),
            "FPS: " + fps,
            style
        );
    }
}
