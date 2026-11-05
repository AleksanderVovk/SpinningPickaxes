using UnityEngine;
using UnityEngine.UI;

public class RainbowImage : MonoBehaviour
{
    [Header("UI Image")]
    [SerializeField] private Image targetImage;

    [Header("Цвета радуги")]
    [SerializeField]
    private Color[] rainbowColors =
    {
        Color.red,
        new Color(1f, 0.5f, 0f), // Оранжевый
        Color.yellow,
        Color.green,
        Color.cyan,
        Color.blue,
        new Color(0.6f, 0f, 1f)  // Фиолетовый
    };

    [Header("Настройки анимации")]
    [SerializeField, Min(0f)] private float speed = 1f;

    [Tooltip("Сохранять прозрачность исходного Image")]
    [SerializeField] private bool preserveAlpha = true;

    private float timer;
    private Color originalColor;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (targetImage != null)
            originalColor = targetImage.color;
    }

    private void Update()
    {
        if (targetImage == null ||
            rainbowColors == null ||
            rainbowColors.Length == 0)
            return;

        if (rainbowColors.Length == 1)
        {
            ApplyColor(rainbowColors[0]);
            return;
        }

        // Переход между цветами по кругу.
        timer += Time.deltaTime * speed;

        float cycle = Mathf.Repeat(timer, rainbowColors.Length);
        int currentIndex = Mathf.FloorToInt(cycle);
        int nextIndex = (currentIndex + 1) % rainbowColors.Length;

        float t = cycle - currentIndex;

        Color color = Color.Lerp(
            rainbowColors[currentIndex],
            rainbowColors[nextIndex],
            t
        );

        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        if (preserveAlpha)
            color.a = originalColor.a;

        targetImage.color = color;
    }
}