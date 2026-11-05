
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class RebirthSystem : MonoBehaviour
{
    public static RebirthSystem Instance { get; private set; }

    [Header("Настройки перерождения")]

    [Tooltip("Через сколько уровней доступно каждое перерождение.")]
    [SerializeField, Min(1)]
    private int levelsPerRebirth = 5;

    [Tooltip("Множитель урона при отсутствии перерождений.")]
    [SerializeField, Min(1f)]
    private float startDamageMultiplier = 1f;

    [Tooltip("Прибавка к множителю после каждого перерождения.")]
    [SerializeField, Min(0.01f)]
    private float damageMultiplierIncrease = 0.5f;

    [Header("Кирка после перерождения")]

    [Tooltip("ID кирки, которая будет выдана после перерождения на этой сцене. Должен совпадать с PickaxeId в PickaxeStand.")]
    [SerializeField]
    private string defaultPickaxeId = "pickaxe_01";

    [Header("UI доступности")]

    [SerializeField]
    private Image rebirthAvailableImage;

    [SerializeField]
    private Button rebirthButton;

    [Header("UI множителя урона")]

    [SerializeField]
    private TMP_Text damageMultiplierBeforeText;

    [SerializeField]
    private TMP_Text damageMultiplierAfterText;

    [Header("UI информации о перерождении")]

    [SerializeField]
    private TMP_Text rebirthMultiplierInfoText;

    [Header("UI уровня")]

    [SerializeField]
    private TMP_Text levelAfterRebirthText;

    [SerializeField]
    private TMP_Text levelProgressText;

    [SerializeField]
    private Slider levelProgressSlider;

    [SerializeField]
    private TMP_Text rebirthButtonText;

    [Header("Ссылки")]

    [SerializeField]
    private PlayerLevelSystem playerLevelSystem;

    [SerializeField]
    private PickaxeStandsController pickaxeStandsController;

    [SerializeField]
    private RotatingPickaxeRing rotatingPickaxeRing;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private bool _isInitialized;
    private string _lastLanguage;

    private readonly LanguageDirectory _damageBeforeTitle =
        new LanguageDirectory(
            "МНОЖИТЕЛЬ УРОНА ДО",
            "DAMAGE MULTIPLIER BEFORE",
            "HASAR ÇARPANI ÖNCE"
        );

    private readonly LanguageDirectory _damageAfterTitle =
        new LanguageDirectory(
            "МНОЖИТЕЛЬ УРОНА ПОСЛЕ",
            "DAMAGE MULTIPLIER AFTER",
            "HASAR ÇARPANI SONRA"
        );

    private readonly LanguageDirectory _currentLevelTitle =
        new LanguageDirectory(
            "ТЕКУЩИЙ УРОВЕНЬ",
            "CURRENT LEVEL",
            "MEVCUT SEVİYE"
        );

    private readonly LanguageDirectory _nextLevelTitle =
        new LanguageDirectory(
            "ПОСЛЕ ПЕРЕРОЖДЕНИЯ",
            "AFTER REBIRTH",
            "YENİDEN DOĞUŞTAN SONRA"
        );

    private readonly LanguageDirectory _rebirthButtonTitle =
        new LanguageDirectory(
            "ВОЗРОЖДЕНИЕ",
            "REBIRTH",
            "YENİDEN DOĞUŞ"
        );

    private readonly LanguageDirectory _levelTitle =
        new LanguageDirectory(
            "УРОВЕНЬ",
            "LEVEL",
            "SEVİYE"
        );

    private readonly LanguageDirectory _multiplierInfoTitle =
        new LanguageDirectory(
            "Множитель",
            "Multiplier",
            "Carpan"
        );

    private readonly LanguageDirectory _rebirthInfoTitle =
        new LanguageDirectory(
            "Перерождение",
            "Rebirth",
            "Yeniden Dogus"
        );

    public int RebirthCount
    {
        get
        {
            EnsureSaveDataExists();

            if (YG2.saves == null ||
                YG2.saves.playerData == null)
                return 0;

            return Mathf.Max(
                0,
                YG2.saves.playerData.rebirthCount
            );
        }
    }

    public float DamageMultiplier
    {
        get
        {
            EnsureSaveDataExists();

            if (YG2.saves == null ||
                YG2.saves.playerData == null)
                return startDamageMultiplier;

            return Mathf.Max(
                startDamageMultiplier,
                YG2.saves.playerData.pickaxeDamageMultiplier
            );
        }
    }

    public float DamageMultiplierAfterRebirth
    {
        get
        {
            return GetMultiplierForRebirthCount(
                RebirthCount + 1
            );
        }
    }

    public int CurrentRequiredLevel
    {
        get
        {
            int safeLevelsPerRebirth =
                Mathf.Max(1, levelsPerRebirth);

            return (RebirthCount + 1) *
                   safeLevelsPerRebirth;
        }
    }

    public int LevelAfterRebirth
    {
        get { return 0; }
    }

    public bool CanRebirth
    {
        get
        {
            if (playerLevelSystem == null)
                return false;

            return playerLevelSystem.CurrentLevel >=
                   CurrentRequiredLevel;
        }
    }

    public event Action<bool> OnRebirthAvailabilityChanged;
    public event Action<int, float> OnRebirthCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (playerLevelSystem == null)
            playerLevelSystem = PlayerLevelSystem.Instance;

        if (pickaxeStandsController == null)
            pickaxeStandsController =
                PickaxeStandsController.Instance;

        if (rotatingPickaxeRing == null)
            rotatingPickaxeRing =
                FindFirstObjectByType<RotatingPickaxeRing>();

        if (rebirthButton != null)
            rebirthButton.onClick.AddListener(TryRebirth);

        if (playerLevelSystem != null)
        {
            playerLevelSystem.OnLevelChanged +=
                OnPlayerLevelChanged;

            playerLevelSystem.OnExperienceChanged +=
                OnExperienceChanged;
        }

        EnsureSaveDataExists();
        ApplyCorrectMultiplierToSave();
        ApplyDamageMultiplierToCurrentPickaxe();

        _isInitialized = true;
        _lastLanguage = GetCurrentLanguage();

        RefreshAllUI();
        RefreshRebirthMultiplierInfoText();
    }

    private void Update()
    {
        if (!_isInitialized)
            return;

        string currentLanguage = GetCurrentLanguage();

        if (_lastLanguage != currentLanguage)
        {
            _lastLanguage = currentLanguage;

            RefreshAllUI();
            RefreshRebirthMultiplierInfoText();
        }
    }

    private void OnDestroy()
    {
        if (rebirthButton != null)
            rebirthButton.onClick.RemoveListener(TryRebirth);

        if (playerLevelSystem != null)
        {
            playerLevelSystem.OnLevelChanged -=
                OnPlayerLevelChanged;

            playerLevelSystem.OnExperienceChanged -=
                OnExperienceChanged;
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnPlayerLevelChanged(int level)
    {
        RefreshAllUI();
    }

    private void OnExperienceChanged(
        int level,
        float currentExperience,
        float requiredExperience)
    {
        RefreshAllUI();
    }

    public void TryRebirth()
    {
        if (!_isInitialized)
            return;

        if (!CanRebirth)
        {
            RefreshAllUI();
            return;
        }

        PerformRebirthInternal();
    }

    public void PerformRebirth()
    {
        if (!_isInitialized)
            return;

        if (!CanRebirth)
        {
            RefreshAllUI();
            return;
        }

        PerformRebirthInternal();
    }

    private void PerformRebirthInternal()
    {
        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        PlayerData data = YG2.saves.playerData;

        float oldMultiplier = DamageMultiplier;

        // Подготавливаем ID кирки для текущей сцены.
        string selectedPickaxeId = GetDefaultPickaxeId();

        // Начисляем одно перерождение.
        data.rebirthCount++;

        // Обновляем множитель урона.
        data.pickaxeDamageMultiplier =
            GetMultiplierForRebirthCount(data.rebirthCount);

        // Сбрасываем купленные кирки,
        // оставляя выбранную кирку доступной.
        ResetBoughtPickaxes(data, selectedPickaxeId);

        // Сбрасываем прогресс игрока.
        data.playerLevel = 1;
        data.levelExperience = 0f;
        data.totalExperience = 0f;
        data.unlockedPickaxeCount = 1;

        // Сохраняем ID выбранной кирки.
        data.equippedPickaxeId = selectedPickaxeId;

        if (playerLevelSystem == null)
            playerLevelSystem = PlayerLevelSystem.Instance;

        if (playerLevelSystem != null)
            playerLevelSystem.ResetLevelProgress();

        if (pickaxeStandsController == null)
        {
            pickaxeStandsController =
                PickaxeStandsController.Instance;
        }

        if (rotatingPickaxeRing == null)
        {
            rotatingPickaxeRing =
                FindFirstObjectByType<RotatingPickaxeRing>();
        }

        // Перезагружаем стенды и экипируем
        // выбранную кирку из сохранённых данных.
        if (pickaxeStandsController != null)
        {
            pickaxeStandsController.ReloadAfterRebirth();
        }
        else if (rotatingPickaxeRing != null)
        {
            // Резервный вариант, если контроллера нет.
            rotatingPickaxeRing.EquipPickaxe(
                selectedPickaxeId
            );
        }
        else
        {
            Debug.LogWarning(
                "[RebirthSystem] Не найдено кольцо кирок.",
                this
            );
        }

        // Применяем новый множитель урона.
        ApplyDamageMultiplierToCurrentPickaxe();

        if (GlobalSound.instance != null)
            GlobalSound.instance.PlaySound_RebirthComplete(false);

        // Сохраняем результат перерождения.
        YG2.SaveProgress();

        // Обновляем разблокировки тренировочных кукол.
        TrainingDummy[] dummies =
            FindObjectsByType<TrainingDummy>(
                FindObjectsSortMode.None
            );

        foreach (TrainingDummy dummy in dummies)
        {
            if (dummy != null)
                dummy.RefreshRebirthUnlock();
        }

        RefreshAllUI();
        RefreshRebirthMultiplierInfoText();

        OnRebirthCompleted?.Invoke(
            data.rebirthCount,
            data.pickaxeDamageMultiplier
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "[RebirthSystem] Перерождение выполнено. " +
                "Количество: " + data.rebirthCount +
                "; Кирка: " + selectedPickaxeId +
                "; Старый множитель: x" +
                oldMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture
                ) +
                "; Новый множитель: x" +
                data.pickaxeDamageMultiplier.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture
                ),
                this
            );
        }
    }

    /// <summary>
    /// Возвращает настроенный ID кирки.
    /// Если поле пустое, используется pickaxe_01.
    /// </summary>
    private string GetDefaultPickaxeId()
    {
        if (string.IsNullOrWhiteSpace(defaultPickaxeId))
            return "pickaxe_01";

        return defaultPickaxeId.Trim();
    }

    /// <summary>
    /// Сбрасывает покупки, оставляя выбранную кирку.
    /// </summary>
    private void ResetBoughtPickaxes(
        PlayerData data,
        string selectedPickaxeId)
    {
        data.boughtPickaxes.Clear();

        if (string.IsNullOrWhiteSpace(selectedPickaxeId))
            selectedPickaxeId = "pickaxe_01";

        data.boughtPickaxes.Add(selectedPickaxeId);
    }

    private float GetMultiplierForRebirthCount(
        int rebirthCount)
    {
        rebirthCount = Mathf.Max(0, rebirthCount);

        return startDamageMultiplier +
               rebirthCount * damageMultiplierIncrease;
    }

    private void ApplyCorrectMultiplierToSave()
    {
        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        YG2.saves.playerData.pickaxeDamageMultiplier =
            GetMultiplierForRebirthCount(
                YG2.saves.playerData.rebirthCount
            );
    }

    public void RefreshAllUI()
    {
        RefreshTexts();
        RefreshSlider();
        RefreshAvailableImage();
    }

    private void RefreshTexts()
    {
        if (playerLevelSystem == null)
            return;

        int currentLevel = playerLevelSystem.CurrentLevel;
        int levelAfterRebirth = LevelAfterRebirth;

        float currentMultiplier = DamageMultiplier;
        float nextMultiplier = DamageMultiplierAfterRebirth;

        string levelText =
            UTF.ReadLanguageDirectory(_levelTitle);

        string rebirthButtonTitle =
            UTF.ReadLanguageDirectory(_rebirthButtonTitle);

        if (damageMultiplierBeforeText != null)
        {
            damageMultiplierBeforeText.text =
                "x" + currentMultiplier.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                );
        }

        if (damageMultiplierAfterText != null)
        {
            damageMultiplierAfterText.text =
                "x" + nextMultiplier.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture
                );
        }

        if (levelAfterRebirthText != null)
        {
            levelAfterRebirthText.text =
                levelText + " " + levelAfterRebirth;
        }

        if (levelProgressText != null)
        {
            levelProgressText.text =
                currentLevel + "/" + CurrentRequiredLevel;
        }

        if (rebirthButtonText != null)
            rebirthButtonText.text = rebirthButtonTitle;
    }

    private void RefreshRebirthMultiplierInfoText()
    {
        if (rebirthMultiplierInfoText == null)
            return;

        string multiplierTitle =
            UTF.ReadLanguageDirectory(_multiplierInfoTitle);

        string rebirthTitle =
            UTF.ReadLanguageDirectory(_rebirthInfoTitle);

        string multiplier = DamageMultiplier.ToString(
            "0.##",
            CultureInfo.InvariantCulture
        );

        rebirthMultiplierInfoText.text =
            multiplierTitle +
            " x" +
            multiplier +
            " (" +
            rebirthTitle +
            ")";
    }

    private void RefreshSlider()
    {
        if (levelProgressSlider == null ||
            playerLevelSystem == null)
            return;

        int currentLevel = playerLevelSystem.CurrentLevel;
        int maxLevel = CurrentRequiredLevel;

        levelProgressSlider.minValue = 0f;
        levelProgressSlider.maxValue = Mathf.Max(1, maxLevel);

        levelProgressSlider.value = Mathf.Clamp(
            currentLevel,
            0,
            maxLevel
        );
    }

    public void RefreshAvailableImage()
    {
        bool canRebirth = CanRebirth;

        if (rebirthAvailableImage != null)
        {
            rebirthAvailableImage.gameObject.SetActive(
                canRebirth
            );
        }

        if (rebirthButton != null)
            rebirthButton.interactable = canRebirth;

        OnRebirthAvailabilityChanged?.Invoke(canRebirth);
    }

    private void ApplyDamageMultiplierToCurrentPickaxe()
    {
        if (pickaxeStandsController == null)
        {
            pickaxeStandsController =
                PickaxeStandsController.Instance;
        }

        if (pickaxeStandsController != null)
        {
            pickaxeStandsController
                .ApplyDamageMultiplierToEquippedPickaxe();
        }
    }

    private string GetCurrentLanguage()
    {
        if (YG2.envir == null)
            return "ru";

        return YG2.envir.language;
    }

    /// <summary>
    /// Бесплатное перерождение без проверки уровня.
    /// Вызывай после подтверждённой награды за рекламу.
    /// </summary>
    public void PerformFreeRebirth()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning(
                "[RebirthSystem] Система ещё не инициализирована.",
                this
            );

            return;
        }

        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
        {
            Debug.LogWarning(
                "[RebirthSystem] Данные игрока не загружены.",
                this
            );

            return;
        }

        PerformRebirthInternal();
    }

    /// <summary>
    /// Перерождение после успешной оплаты.
    /// Не требует достижения нужного уровня.
    /// </summary>
    public void PerformPaidRebirth()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning(
                "[RebirthSystem] Система ещё не инициализирована.",
                this
            );

            return;
        }

        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
        {
            Debug.LogWarning(
                "[RebirthSystem] Данные игрока не загружены.",
                this
            );

            return;
        }

        PerformRebirthInternal();
    }

    private void EnsureSaveDataExists()
    {
        if (YG2.saves == null)
            return;

        if (YG2.saves.playerData == null)
            YG2.saves.playerData = new PlayerData();

        YG2.saves.playerData.EnsureValidData();

        if (YG2.saves.playerData.rebirthCount < 0)
            YG2.saves.playerData.rebirthCount = 0;

        float correctMultiplier =
            GetMultiplierForRebirthCount(
                YG2.saves.playerData.rebirthCount
            );

        if (YG2.saves.playerData.pickaxeDamageMultiplier <
            correctMultiplier)
        {
            YG2.saves.playerData.pickaxeDamageMultiplier =
                correctMultiplier;
        }
    }
}