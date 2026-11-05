using UnityEngine;
using YG;

public class TrainingDummy : BlockEntity
{
    private const float DummyHealth = 9999999f;

    [Header("Множители")]

    [Tooltip("Множитель урона по этой кукле. Например, 5 = урон x5.")]
    [SerializeField, Min(0.01f)]
    private float damageMultiplier = 1f;

    [Tooltip("Дополнительный множитель опыта. Например, 2 = опыт x2.")]
    [SerializeField, Min(0f)]
    private float experienceMultiplierForDummy = 1f;

    // Базовый множитель опыта из BlockEntity умножается
    // на дополнительный множитель этой куклы.
    protected override float ExperienceMultiplier =>
        base.ExperienceMultiplier * experienceMultiplierForDummy;

    [Header("Разблокировка куклы")]

    [Tooltip("Если включено, кукла доступна всем с самого начала.")]
    [SerializeField]
    private bool unlockedByDefault = true;

    [Tooltip("Уникальный ID. Не повторяй его у других кукол.")]
    [SerializeField]
    private string unlockId = "TrainingDummy";

    [Tooltip("Сколько перерождений нужно для разблокировки.")]
    [SerializeField, Min(0)]
    private int requiredRebirths = 0;

    [Tooltip("Если включено, кукла открывается только покупкой через YG.")]
    [SerializeField]
    private bool requiresPurchase = false;

    private bool _isUnlocked;

    public bool IsUnlocked => _isUnlocked;
    public bool RequiresPurchase => requiresPurchase;
    public string UnlockId => unlockId;
    public int RequiredRebirths => requiredRebirths;

    public float DamageMultiplier => damageMultiplier;
    public float ExperienceMultiplierForDummy => experienceMultiplierForDummy;

    // Кукла получает расчётный урон, но не теряет HP.
    protected override bool PreventHealthReduction => true;

    public override bool CanReceiveDamage =>
        base.CanReceiveDamage && _isUnlocked;

    protected override void Awake()
    {
        maxHealth = DummyHealth;
        currentHealth = DummyHealth;

        base.Awake();

        LoadUnlockState();
    }

    private void Start()
    {
        RefreshRebirthUnlock();
    }

    /// <summary>
    /// Умножает входящий урон по этой кукле.
    /// Базовый BlockEntity рассчитывает опыт по умноженному урону.
    /// </summary>
    public override bool TakeDamage(
        float damage,
        PickaxeEntity attacker = null,
        Vector3 hitPoint = default)
    {
        if (!CanReceiveDamage || damage <= 0f)
            return false;

        float finalDamage = damage * Mathf.Max(0.01f, damageMultiplier);

        return base.TakeDamage(finalDamage, attacker, hitPoint);
    }

    private void LoadUnlockState()
    {
        if (unlockedByDefault)
        {
            _isUnlocked = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(unlockId))
        {
            Debug.LogError(
                $"У куклы {name} не задан уникальный Unlock Id.",
                this
            );

            _isUnlocked = false;
            return;
        }

        PlayerData data = GetPlayerData();

        _isUnlocked =
            data != null &&
            data.unlockedDummies.Contains(unlockId);

        if (!requiresPurchase)
            RefreshRebirthUnlock();
    }

    /// <summary>
    /// Проверяет перерождения и открывает куклу,
    /// если требуемое количество достигнуто.
    /// </summary>
    public void RefreshRebirthUnlock()
    {
        if (_isUnlocked || unlockedByDefault || requiresPurchase)
            return;

        PlayerData data = GetPlayerData();

        if (data == null)
            return;

        if (data.rebirthCount >= requiredRebirths)
            Unlock();
    }

    public bool CanUnlockWithRebirths()
    {
        if (unlockedByDefault || requiresPurchase)
            return false;

        PlayerData data = GetPlayerData();

        return data != null &&
               data.rebirthCount >= requiredRebirths;
    }

    /// <summary>
    /// Разблокирует куклу и сохраняет ID в YG PlayerData.
    /// </summary>
    public void Unlock()
    {
        if (_isUnlocked)
            return;

        if (string.IsNullOrWhiteSpace(unlockId))
        {
            Debug.LogError(
                $"Нельзя сохранить разблокировку куклы {name}: пустой Unlock Id.",
                this
            );
            return;
        }

        PlayerData data = GetPlayerData();

        if (data == null)
        {
            Debug.LogError(
                "Данные YG ещё не доступны. Кукла не разблокирована.",
                this
            );
            return;
        }

        if (data.unlockedDummies == null)
        {
            data.unlockedDummies =
                new System.Collections.Generic.List<string>();
        }

        if (!data.unlockedDummies.Contains(unlockId))
            data.unlockedDummies.Add(unlockId);

        _isUnlocked = true;

        YG2.SaveProgress();

        Debug.Log(
            $"Кукла {name} разблокирована и сохранена через YG.",
            this
        );
    }

    private PlayerData GetPlayerData()
    {
        if (YG2.saves == null)
            return null;

        if (YG2.saves.playerData == null)
            return null;

        YG2.saves.playerData.EnsureValidData();

        return YG2.saves.playerData;
    }
}
