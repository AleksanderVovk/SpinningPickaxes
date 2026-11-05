using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Спавнит XP Popup в случайной точке внутри RectTransform spawnParent.
/// Случайными являются и X, и Y. Оптимизировано: область считается 1 раз.
/// </summary>
public class XPPopupSpawner : MonoBehaviour
{
    [Header("Ссылки")]

    [Tooltip("Префаб всплывашки.")]
    [SerializeField]
    private GameObject xpPopupPrefab;

    [Tooltip("RectTransform области, внутри которой можно спавнить popup.")]
    [SerializeField]
    private RectTransform spawnParent;

    [Header("Отступ от краёв")]

    [Tooltip("Минимальный отступ от левого/правого края.")]
    [SerializeField]
    private float horizontalPadding = 50f;

    [Tooltip("Минимальный отступ от верхнего/нижнего края.")]
    [SerializeField]
    private float verticalPadding = 50f;

    [Header("Настройки кирки")]

    [SerializeField]
    private float minAngle = -45f;

    [SerializeField]
    private float maxAngle = 45f;

    [Header("Настройки времени жизни")]

    [SerializeField]
    private float lifetime = 1.2f;

    [Tooltip("Насколько ниже стартует popup относительно выбранной случайной точки.")]
    [SerializeField]
    private float startYOffset = -30f;

    [Tooltip("Конечное смещение относительно случайной точки.")]
    [SerializeField]
    private float endYOffset = 0f;

    [SerializeField]
    private float riseDuration = 0.4f;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private PlayerLevelSystem _levelSystem;

    // Предвычисленная область спавна (в локальных координатах spawnParent)
    private float _minX;
    private float _maxX;
    private float _minY;
    private float _maxY;

    private void Start()
    {
        _levelSystem = PlayerLevelSystem.Instance;

        if (_levelSystem == null)
        {
            Debug.LogWarning(
                "[XPPopupSpawner] Не найден PlayerLevelSystem.",
                this
            );
        }
        else
        {
            _levelSystem.OnExperienceChanged += HandleExperienceChanged;
        }

        if (spawnParent == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();

            if (canvas != null)
            {
                spawnParent = canvas.GetComponent<RectTransform>();
            }
            else
            {
                Debug.LogWarning(
                    "[XPPopupSpawner] Не задан spawnParent и не найден Canvas.",
                    this
                );
            }
        }

        CalculateSpawnArea();
    }

    private void OnDestroy()
    {
        if (_levelSystem != null)
        {
            _levelSystem.OnExperienceChanged -= HandleExperienceChanged;
        }
    }

    private void HandleExperienceChanged(
        int level,
        float currentExperience,
        float requiredExperience)
    {
        // Здесь ничего не делаем.
    }

    private void CalculateSpawnArea()
    {
        if (spawnParent == null)
        {
            Debug.LogWarning("[XPPopupSpawner] spawnParent == null в CalculateSpawnArea.", this);
            _minX = _maxX = _minY = _maxY = 0f;
            return;
        }

        Rect parentRect = spawnParent.rect;

        float halfWidth = parentRect.width * 0.5f;
        float halfHeight = parentRect.height * 0.5f;

        // Просто отступаем от краёв родителя, не завися от размера попупа
        _minX = -halfWidth + horizontalPadding;
        _maxX = halfWidth - horizontalPadding;

        _minY = -halfHeight + verticalPadding;
        _maxY = halfHeight - verticalPadding;

        if (_minX > _maxX)
        {
            float c = (_minX + _maxX) * 0.5f;
            _minX = _maxX = c;
        }

        if (_minY > _maxY)
        {
            float c = (_minY + _maxY) * 0.5f;
            _minY = _maxY = c;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[XPPopupSpawner] SpawnArea: X[{_minX:0.##}, {_maxX:0.##}], " +
                $"Y[{_minY:0.##}, {_maxY:0.##}]",
                this
            );
        }
    }

    public void SpawnXPPopup(float amount)
    {
        if (xpPopupPrefab == null)
        {
            Debug.LogError("[XPPopupSpawner] Не задан xpPopupPrefab.", this);
            return;
        }

        if (spawnParent == null)
        {
            Debug.LogError("[XPPopupSpawner] Не задан spawnParent.", this);
            return;
        }

        GameObject popupObj = Instantiate(xpPopupPrefab, spawnParent, false);
        popupObj.SetActive(true);

        RectTransform rt = popupObj.GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogError("[XPPopupSpawner] У префаба нет RectTransform.", popupObj);
            Destroy(popupObj);
            return;
        }

        // Фиксируем anchors/pivot
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Случайная позиция внутри预先вычисленной области
        float randomX = Random.Range(_minX, _maxX);
        float randomY = Random.Range(_minY, _maxY);

        rt.anchoredPosition = new Vector2(randomX, randomY + startYOffset);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[XPPopupSpawner] Spawn: X={randomX:F1}, Y={randomY:F1}, " +
                $"anchored={rt.anchoredPosition}",
                popupObj
            );
        }

        // Текст XP
        TMP_Text textXP = popupObj.GetComponentInChildren<TMP_Text>(true);
        if (textXP != null)
        {
            textXP.text = $"+{amount}";
        }
        else
        {
            Debug.LogWarning("[XPPopupSpawner] Не найден TMP_Text.", popupObj);
        }

        // Кирка: случайный угол
        Image pickaxeImage = popupObj.GetComponentInChildren<Image>(true);
        if (pickaxeImage != null)
        {
            float randomAngle = Random.Range(minAngle, maxAngle);
            pickaxeImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, randomAngle);
        }
        else
        {
            Debug.LogWarning("[XPPopupSpawner] Не найдена Image для кирки.", popupObj);
        }

        // Анимация
        XPPopup popup = popupObj.GetComponent<XPPopup>();
        if (popup == null)
        {
            popup = popupObj.AddComponent<XPPopup>();
        }

        popup.Init(
            startYOffset,
            endYOffset,
            riseDuration,
            lifetime,
            showDebugLogs
        );
    }
}