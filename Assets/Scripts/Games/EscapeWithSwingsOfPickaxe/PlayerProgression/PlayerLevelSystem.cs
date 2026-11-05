
using System;
using UnityEngine;
using YG;

public class PlayerLevelSystem : MonoBehaviour
{
    public static PlayerLevelSystem Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private RotatingPickaxeRing pickaxeRing;

    [Header("Настройки уровней")]
    [Tooltip("Минимальный возможный уровень игрока.")]
    [SerializeField, Min(1)] private int minimumLevel = 1;

    private static readonly int[] RequiredExperienceByLevel =
    {
        10, 20, 30, 40, 50, 70, 375, 1100, 1200, 1800,
        2700, 4500, 5500, 6500, 7500, 8500, 9500, 12500,
        14500, 16500, 18700, 20200, 21700, 23500, 25200,
        27200, 29200, 31500, 33700, 36200, 39000, 41700,
        44700, 48000, 51500, 55200, 59200, 63500, 68000,
        72700, 77700, 83000
    };

    [Header("Настройки опыта")]
    [SerializeField, Range(1.01f, 1.20f)]
    private float experienceGrowthAfterTable = 1.04f;

    [SerializeField]
    private bool roundExperienceAfterTable = true;

    [Header("Настройки кирок")]
    [Tooltip("Новая кирка открывается на уровнях 10, 20, 30 и т. д.")]
    [SerializeField, Min(1)]
    private int levelsPerNewPickaxe = 10;

    [Tooltip("Количество кирок на начальном уровне.")]
    [SerializeField, Min(1)]
    private int initialPickaxeCount = 1;

    [Header("Автосохранение")]
    [SerializeField, Min(0.1f)]
    private float autoSaveInterval = 5f;

    [SerializeField]
    private bool saveImmediatelyOnLevelUp = true;

    [SerializeField]
    private bool saveOnDisable = true;

    [Header("Дебаг")]
    [SerializeField]
    private bool showDebugLogs = false;

    [Header("Визуальные эффекты")]
    [SerializeField] private XPPopupSpawner _xpPoupSpawner;
    [SerializeField] private AudioClip[] _pickaxeHit;
    [SerializeField] private ParticleSystem _levelUp;

    private int _currentLevel;
    private float _currentExperience;
    private float _totalExperience;

    private float _autoSaveTimer;
    private bool _hasUnsavedChanges;
    private bool _isInitialized;
    private bool _isSaving;

    public int CurrentLevel => _currentLevel;
    public float CurrentExperience => _currentExperience;
    public float TotalExperience => _totalExperience;
    public int MinimumLevel => minimumLevel;

    public float RequiredExperienceForCurrentLevel =>
        GetRequiredExperienceForLevel(_currentLevel);

    public float CurrentExperienceProgress
    {
        get
        {
            float required = RequiredExperienceForCurrentLevel;

            return required <= 0f
                ? 1f
                : Mathf.Clamp01(_currentExperience / required);
        }
    }

    public int UnlockedPickaxeCount =>
        GetRequiredPickaxeCountForLevel(_currentLevel);

    public event Action<int, float, float> OnExperienceChanged;
    public event Action<int> OnLevelChanged;
    public event Action<int> OnPickaxeCountChanged;
    public event Action OnProgressLoaded;

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
        LoadProgressFromSave();

        _isInitialized = true;

        ApplyPickaxeCountForCurrentLevel();
        NotifyAllUI();

        if (PickaxeStandsController.Instance != null)
        {
            PickaxeStandsController.Instance
                .ApplyDamageMultiplierToEquippedPickaxe();
        }

        if (RebirthSystem.Instance != null)
        {
            RebirthSystem.Instance.RefreshAvailableImage();
        }

        OnProgressLoaded?.Invoke();
    }

    private void Update()
    {
        if (_isInitialized)
        {
            AutoSaveTick();
        }
    }

    private void OnDisable()
    {
        if (saveOnDisable)
        {
            SaveNowIfNeeded();
        }
    }

    private void OnApplicationQuit()
    {
        SaveNowIfNeeded();
    }

    public void AddExperience(float amount)
    {
        if (!_isInitialized || amount <= 0f)
        {
            return;
        }

        _currentExperience += amount;
        _totalExperience += amount;
        _hasUnsavedChanges = true;

        bool leveledUp = ProcessLevelUps();

        NotifyExperienceChanged();

        if (leveledUp && saveImmediatelyOnLevelUp)
        {
            SaveProgressNow();
        }

        if (_xpPoupSpawner != null)
        {
            _xpPoupSpawner.SpawnXPPopup(amount);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerLevelSystem] +{amount:0.##} XP | " +
                $"Уровень: {_currentLevel} | " +
                $"XP: {_currentExperience:0.##}/" +
                $"{RequiredExperienceForCurrentLevel:0.##} | " +
                $"Кирок по уровню: " +
                $"{GetRequiredPickaxeCountForLevel(_currentLevel)}",
                this
            );
        }
    }

    private void AutoSaveTick()
    {
        if (!_hasUnsavedChanges)
        {
            return;
        }

        _autoSaveTimer += Time.unscaledDeltaTime;

        if (_autoSaveTimer >= autoSaveInterval)
        {
            SaveProgressNow();
        }
    }

    private bool ProcessLevelUps()
    {
        bool levelChanged = false;
        int oldPickaxeCount = GetCurrentRingPickaxeCount();
        int safetyCounter = 0;

        while (_currentExperience >= RequiredExperienceForCurrentLevel
               && safetyCounter < 1000)
        {
            _currentExperience -= RequiredExperienceForCurrentLevel;

            int previousLevel = _currentLevel;
            _currentLevel++;

            levelChanged = true;
            safetyCounter++;

            if (_levelUp != null)
            {
                _levelUp.Play();
            }

            if (GlobalSound.instance != null)
            {
                GlobalSound.instance.PlaySound_LevelUp(false);
            }

            if (LangLIst.instance != null)
            {
                NotificationManager.ShowNotification(
                    UTF.ReadLanguageDirectory(LangLIst.instance.newLvl) +
                    "! " + previousLevel + " -> " + _currentLevel,
                    NotificationType.Success
                );
            }

            int requiredPickaxes =
                GetRequiredPickaxeCountForLevel(_currentLevel);

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[PlayerLevelSystem] Уровень: {_currentLevel}, " +
                    $"Требуемое количество кирок: {requiredPickaxes}",
                    this
                );
            }
        }

        if (!levelChanged)
        {
            return false;
        }

        // Обновляем кольцо сразу после изменения уровня.
        ApplyPickaxeCountForCurrentLevel();

        OnLevelChanged?.Invoke(_currentLevel);

        int newPickaxeCount = GetCurrentRingPickaxeCount();

        if (newPickaxeCount != oldPickaxeCount)
        {
            OnPickaxeCountChanged?.Invoke(newPickaxeCount);
        }

        return true;
    }

    public float GetRequiredExperienceForLevel(int level)
    {
        level = Mathf.Max(minimumLevel, level);

        int levelIndex = level - minimumLevel;

        if (levelIndex < RequiredExperienceByLevel.Length)
        {
            return RequiredExperienceByLevel[levelIndex];
        }

        int extraLevels =
            levelIndex - (RequiredExperienceByLevel.Length - 1);

        float lastExperience =
            RequiredExperienceByLevel[RequiredExperienceByLevel.Length - 1];

        float required =
            lastExperience *
            Mathf.Pow(experienceGrowthAfterTable, extraLevels);

        if (roundExperienceAfterTable)
        {
            required = Mathf.Round(required / 1000f) * 1000f;
        }

        return Mathf.Max(lastExperience, required);
    }

    /// <summary>
    /// Первая кирка доступна с начала игры.
    /// При шаге 10 новая кирка добавляется на уровнях 10, 20, 30...
    /// Уровни 1-9: 1 кирка.
    /// Уровни 10-19: 2 кирки.
    /// Уровни 20-29: 3 кирки.
    /// </summary>
    public int GetRequiredPickaxeCountForLevel(int level)
    {
        level = Mathf.Max(minimumLevel, level);

        int step = Mathf.Max(1, levelsPerNewPickaxe);

        // ВАЖНО: не вычитаем 1.
        // Уровень 10 при шаге 10 должен дать +1 кирку.
        int extraPickaxes = level / step;

        int result = Mathf.Max(1, initialPickaxeCount) + extraPickaxes;

        if (pickaxeRing != null)
        {
            result = Mathf.Min(result, pickaxeRing.MaxPickaxeCount);
        }

        return Mathf.Max(1, result);
    }

    /// <summary>
    /// Возвращает следующий уровень открытия кирки.
    /// Например: уровень 9 -> 10, уровень 10 -> 20.
    /// Возвращает -1, если все доступные кирки уже открыты.
    /// </summary>
    public int GetNextPickaxeLevel()
    {
        int step = Mathf.Max(1, levelsPerNewPickaxe);

        int currentCount =
            GetRequiredPickaxeCountForLevel(_currentLevel);

        if (pickaxeRing != null &&
            currentCount >= pickaxeRing.MaxPickaxeCount)
        {
            return -1;
        }

        int currentLevel = Mathf.Max(minimumLevel, _currentLevel);

        return (currentLevel / step + 1) * step;
    }

    private void ApplyPickaxeCountForCurrentLevel()
    {
        if (pickaxeRing == null)
        {
            return;
        }

        int count = GetRequiredPickaxeCountForLevel(_currentLevel);
        string equippedId = GetEquippedPickaxeIdFromSave();

        // SetPickaxeCount создаёт или удаляет кирки до нужного количества.
        pickaxeRing.SetPickaxeCount(count, equippedId);

        if (YG2.saves != null && YG2.saves.playerData != null)
        {
            YG2.saves.playerData.unlockedPickaxeCount = count;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerLevelSystem] Синхронизация кольца: " +
                $"уровень {_currentLevel}, " +
                $"кирки в кольце {pickaxeRing.CurrentPickaxeCount}, " +
                $"расчётное количество {count}",
                this
            );
        }
    }

    private int GetCurrentRingPickaxeCount()
    {
        return pickaxeRing != null
            ? pickaxeRing.CurrentPickaxeCount
            : 0;
    }

    private void LoadProgressFromSave()
    {
        EnsureSaveDataExists();

        if (YG2.saves == null || YG2.saves.playerData == null)
        {
            _currentLevel = minimumLevel;
            _currentExperience = 0f;
            _totalExperience = 0f;
            return;
        }

        PlayerData data = YG2.saves.playerData;

        _currentLevel = Mathf.Max(minimumLevel, data.playerLevel);
        _currentExperience = Mathf.Max(0f, data.levelExperience);
        _totalExperience = Mathf.Max(0f, data.totalExperience);

        ProcessSavedExperienceLevelUps();
        WriteCurrentProgressToSave();

        _hasUnsavedChanges = false;
        _autoSaveTimer = 0f;
    }

    private void ProcessSavedExperienceLevelUps()
    {
        int safetyCounter = 0;

        while (_currentExperience >= RequiredExperienceForCurrentLevel
               && safetyCounter < 1000)
        {
            _currentExperience -= RequiredExperienceForCurrentLevel;
            _currentLevel++;
            safetyCounter++;
        }
    }

    private void SaveNowIfNeeded()
    {
        if (_isInitialized && _hasUnsavedChanges)
        {
            SaveProgressNow();
        }
    }

    public void SaveProgressNow()
    {
        if (_isSaving)
        {
            return;
        }

        EnsureSaveDataExists();

        if (YG2.saves == null || YG2.saves.playerData == null)
        {
            return;
        }

        _isSaving = true;

        WriteCurrentProgressToSave();
        YG2.SaveProgress();

        _hasUnsavedChanges = false;
        _autoSaveTimer = 0f;
        _isSaving = false;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerLevelSystem] Сохранён уровень {_currentLevel}, " +
                $"XP {_currentExperience:0.##}",
                this
            );
        }
    }

    private void WriteCurrentProgressToSave()
    {
        if (YG2.saves == null || YG2.saves.playerData == null)
        {
            return;
        }

        PlayerData data = YG2.saves.playerData;

        data.playerLevel = _currentLevel;
        data.levelExperience = _currentExperience;
        data.totalExperience = _totalExperience;

        data.unlockedPickaxeCount =
            GetRequiredPickaxeCountForLevel(_currentLevel);
    }

    private string GetEquippedPickaxeIdFromSave()
    {
        EnsureSaveDataExists();

        if (YG2.saves == null || YG2.saves.playerData == null)
        {
            return "pickaxe_01";
        }

        string pickaxeId = YG2.saves.playerData.equippedPickaxeId;

        if (string.IsNullOrEmpty(pickaxeId))
        {
            pickaxeId = "pickaxe_01";
            YG2.saves.playerData.equippedPickaxeId = pickaxeId;
            _hasUnsavedChanges = true;
        }

        return pickaxeId;
    }

    private void EnsureSaveDataExists()
    {
        if (YG2.saves == null)
        {
            return;
        }

        if (YG2.saves.playerData == null)
        {
            YG2.saves.playerData = new PlayerData();
        }

        YG2.saves.playerData.EnsureValidData();
    }

    private void NotifyAllUI()
    {
        NotifyExperienceChanged();
        OnLevelChanged?.Invoke(_currentLevel);
        OnPickaxeCountChanged?.Invoke(GetCurrentRingPickaxeCount());
    }

    private void NotifyExperienceChanged()
    {
        OnExperienceChanged?.Invoke(
            _currentLevel,
            _currentExperience,
            RequiredExperienceForCurrentLevel
        );
    }

    public void SetPickaxeRing(RotatingPickaxeRing newPickaxeRing)
    {
        pickaxeRing = newPickaxeRing;

        if (_isInitialized)
        {
            ApplyPickaxeCountForCurrentLevel();
            NotifyAllUI();
        }
    }

    public void ForceRefresh()
    {
        if (!_isInitialized)
        {
            return;
        }

        ApplyPickaxeCountForCurrentLevel();
        NotifyAllUI();
    }

    public void ResetLevelProgress()
    {
        _currentLevel = minimumLevel;
        _currentExperience = 0f;
        _totalExperience = 0f;

        EnsureSaveDataExists();

        if (YG2.saves != null && YG2.saves.playerData != null)
        {
            YG2.saves.playerData.playerLevel = minimumLevel;
            YG2.saves.playerData.levelExperience = 0f;
            YG2.saves.playerData.totalExperience = 0f;
            YG2.saves.playerData.unlockedPickaxeCount =
                Mathf.Max(1, initialPickaxeCount);
        }

        _hasUnsavedChanges = true;

        ApplyPickaxeCountForCurrentLevel();
        NotifyAllUI();
        SaveProgressNow();

        if (RebirthSystem.Instance != null)
        {
            RebirthSystem.Instance.RefreshAvailableImage();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG: Добавить 100 XP")]
    private void DebugAdd100Experience()
    {
        if (Application.isPlaying)
        {
            AddExperience(100f);
        }
    }

    [ContextMenu("DEBUG: Сохранить сейчас")]
    private void DebugSaveNow()
    {
        if (Application.isPlaying)
        {
            SaveProgressNow();
        }
    }

    [ContextMenu("DEBUG: Сбросить прогресс")]
    private void DebugResetProgress()
    {
        if (Application.isPlaying)
        {
            ResetLevelProgress();
        }
    }
#endif

    public void TrueDamageBlock(Vector3 position)
    {
        if (_pickaxeHit == null || _pickaxeHit.Length == 0)
        {
            return;
        }

        if (GlobalSound.instance != null)
        {
            GlobalSound.instance.PlaySound_Custom(
                _pickaxeHit[
                    UnityEngine.Random.Range(0, _pickaxeHit.Length)
                ],
                true
            );
        }
    }
}