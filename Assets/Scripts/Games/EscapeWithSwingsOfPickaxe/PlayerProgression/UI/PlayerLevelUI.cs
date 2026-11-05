using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLevelUI : MonoBehaviour
{
    [Header("Ссылки")]

    [SerializeField]
    private PlayerLevelSystem levelSystem;

    [SerializeField]
    private Slider experienceSlider;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private TMP_Text experienceText;

    [SerializeField]
    private TMP_Text pickaxeCountText;

    [SerializeField]
    private TMP_Text nextPickaxeText;

    [Header("Анимация Slider")]

    [Tooltip("Чем больше значение, тем быстрее Slider доходит до нового XP.")]
    [SerializeField]
    [Min(0.01f)]
    private float sliderFillSpeed = 2.5f;

    [Tooltip("Плавность сглаживания. Меньше = быстрее; 0.1–0.2 обычно хорошо.")]
    [SerializeField]
    [Range(0.01f, 1f)]
    private float sliderSmoothTime = 0.15f;

    [Tooltip("При level up сначала показывать полную шкалу, потом переходить к новому уровню.")]
    [SerializeField]
    private bool animateFullSliderOnLevelUp = true;

    [Tooltip("Сколько времени удерживать Slider полностью заполненным перед новым уровнем.")]
    [SerializeField]
    [Min(0f)]
    private float fullSliderHoldTime = 0.12f;

    private float _targetSliderValue;
    private float _sliderVelocity;

    private bool _isLevelUpAnimationActive;
    private float _levelUpHoldTimer;

    private int _lastKnownLevel;
    private float _lastKnownExperience;
    private float _lastKnownRequiredExperience;

    private void Awake()
    {
        if (experienceSlider == null)
        {
            return;
        }

        experienceSlider.minValue = 0f;
        experienceSlider.maxValue = 1f;
        experienceSlider.wholeNumbers = false;
    }

    private void Start()
    {
        if (levelSystem == null)
        {
            levelSystem =
                PlayerLevelSystem.Instance;
        }

        if (levelSystem == null)
        {
            Debug.LogError(
                "[PlayerLevelUI] Не найден PlayerLevelSystem.",
                this
            );

            return;
        }

        levelSystem.OnExperienceChanged +=
            HandleExperienceChanged;

        levelSystem.OnLevelChanged +=
            HandleLevelChanged;

        levelSystem.OnPickaxeCountChanged +=
            HandlePickaxeCountChanged;

        RefreshImmediately();
    }

    private void OnDestroy()
    {
        if (levelSystem == null)
        {
            return;
        }

        levelSystem.OnExperienceChanged -=
            HandleExperienceChanged;

        levelSystem.OnLevelChanged -=
            HandleLevelChanged;

        levelSystem.OnPickaxeCountChanged -=
            HandlePickaxeCountChanged;
    }

    private void Update()
    {
        AnimateSlider();
    }

    private void AnimateSlider()
    {
        if (experienceSlider == null)
        {
            return;
        }

        if (_isLevelUpAnimationActive)
        {
            experienceSlider.value =
                Mathf.MoveTowards(
                    experienceSlider.value,
                    1f,
                    sliderFillSpeed *
                    Time.unscaledDeltaTime
                );

            if (Mathf.Abs(
                    experienceSlider.value - 1f
                ) <= 0.001f)
            {
                _levelUpHoldTimer -=
                    Time.unscaledDeltaTime;

                if (_levelUpHoldTimer <= 0f)
                {
                    _isLevelUpAnimationActive = false;
                    _sliderVelocity = 0f;
                }
            }

            return;
        }

        experienceSlider.value =
            Mathf.SmoothDamp(
                experienceSlider.value,
                _targetSliderValue,
                ref _sliderVelocity,
                sliderSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
    }

    private void HandleExperienceChanged(
        int level,
        float currentExperience,
        float requiredExperience
    )
    {
        bool levelChanged =
            level != _lastKnownLevel;

        _lastKnownLevel = level;
        _lastKnownExperience = currentExperience;
        _lastKnownRequiredExperience = requiredExperience;

        _targetSliderValue =
            requiredExperience <= 0f
                ? 1f
                : Mathf.Clamp01(
                    currentExperience /
                    requiredExperience
                );

        // Текст опыта меняется сразу, без анимации.
        RefreshAllTexts();

        if (animateFullSliderOnLevelUp &&
            levelChanged &&
            experienceSlider != null &&
            experienceSlider.value > 0.01f)
        {
            StartLevelUpSliderAnimation();
        }
    }

    private void HandleLevelChanged(
        int newLevel
    )
    {
        _lastKnownLevel = newLevel;

        RefreshAllTexts();
    }

    private void HandlePickaxeCountChanged(
        int pickaxeCount
    )
    {
        RefreshAllTexts();
    }

    private void StartLevelUpSliderAnimation()
    {
        _isLevelUpAnimationActive = true;

        _levelUpHoldTimer =
            fullSliderHoldTime;

        _sliderVelocity = 0f;
    }

    private void RefreshImmediately()
    {
        if (levelSystem == null)
        {
            return;
        }

        _lastKnownLevel =
            levelSystem.CurrentLevel;

        _lastKnownExperience =
            levelSystem.CurrentExperience;

        _lastKnownRequiredExperience =
            levelSystem.RequiredExperienceForCurrentLevel;

        _targetSliderValue =
            levelSystem.CurrentExperienceProgress;

        if (experienceSlider != null)
        {
            experienceSlider.value =
                _targetSliderValue;
        }

        RefreshAllTexts();
    }

    private void RefreshAllTexts()
    {
        if (levelSystem == null)
        {
            return;
        }

        RefreshLevelText();
        RefreshExperienceText();
        RefreshPickaxeText();
        RefreshNextPickaxeText();
    }

    private void RefreshLevelText()
    {
        if (levelText == null)
        {
            return;
        }

        levelText.text =
            $"УРОВЕНЬ {levelSystem.CurrentLevel}";
    }

    private void RefreshExperienceText()
    {
        if (experienceText == null)
        {
            return;
        }

        // Значение текста обновляется сразу.
        experienceText.text =
            $"{FormatNumber(levelSystem.CurrentExperience)}/" +
            $"{FormatNumber(levelSystem.RequiredExperienceForCurrentLevel)}";
    }

    private void RefreshPickaxeText()
    {
        if (pickaxeCountText == null)
        {
            return;
        }

        pickaxeCountText.text =
            $"Всего кирок: " +
            $"{levelSystem.UnlockedPickaxeCount}";
    }

    private void RefreshNextPickaxeText()
    {
        if (nextPickaxeText == null)
        {
            return;
        }

        int nextPickaxeLevel =
            levelSystem.GetNextPickaxeLevel();

        if (nextPickaxeLevel < 0)
        {
            nextPickaxeText.text =
                "Все кирки открыты";

            return;
        }

        nextPickaxeText.text =
            $"Следующая кирка: " +
            $"Уровень {nextPickaxeLevel}";
    }

    private string FormatNumber(float value)
    {
        if (value >= 1_000_000_000f)
        {
            return
                $"{value / 1_000_000_000f:0.##}B";
        }

        if (value >= 1_000_000f)
        {
            return
                $"{value / 1_000_000f:0.##}M";
        }

        if (value >= 1_000f)
        {
            return
                $"{value / 1_000f:0.##}K";
        }

        return value.ToString("0.#");
    }
}