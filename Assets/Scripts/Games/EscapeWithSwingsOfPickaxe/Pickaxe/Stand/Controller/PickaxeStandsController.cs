
using System.Collections.Generic;
using UnityEngine;
using YG;

public class PickaxeStandsController : MonoBehaviour
{
    public static PickaxeStandsController Instance
    {
        get;
        private set;
    }

    [Header("Стенды")]
    [SerializeField]
    private List<PickaxeStand> _stands =
        new List<PickaxeStand>();

    [Header("Кольцо кирок")]
    [SerializeField]
    private RotatingPickaxeRing pickaxeRing;

    [Header("Дебаг")]
    [SerializeField]
    private bool showDebugLogs = false;

    private readonly HashSet<string> _boughtPickaxes =
        new HashSet<string>();

    private string _equippedPickaxeId;

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
        LoadData();
        EquipSavedPickaxeToRing();
        RefreshAllStands();
    }

    private void LoadData()
    {
        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        _boughtPickaxes.Clear();

        foreach (string pickaxeId in
                 YG2.saves.playerData.boughtPickaxes)
        {
            if (!string.IsNullOrEmpty(pickaxeId))
                _boughtPickaxes.Add(pickaxeId);
        }

        _equippedPickaxeId =
            YG2.saves.playerData.equippedPickaxeId;

        if (string.IsNullOrEmpty(_equippedPickaxeId))
        {
            _equippedPickaxeId = "pickaxe_01";

            YG2.saves.playerData.equippedPickaxeId =
                _equippedPickaxeId;
        }

        // Экипированная кирка должна считаться купленной.
        if (!_boughtPickaxes.Contains(_equippedPickaxeId))
        {
            _boughtPickaxes.Add(_equippedPickaxeId);

            if (!YG2.saves.playerData.boughtPickaxes.Contains(
                    _equippedPickaxeId))
            {
                YG2.saves.playerData.boughtPickaxes.Add(
                    _equippedPickaxeId
                );
            }
        }
    }


    private void EquipSavedPickaxeToRing()
    {
        if (pickaxeRing == null)
        {
            Debug.LogError(
                "[PickaxeStandsController] Кольцо кирок не назначено!",
                this
            );
            return;
        }

        // Сначала экипируем сохранённую кирку.
        pickaxeRing.EquipPickaxe(_equippedPickaxeId);

        // Обновляем систему уровня.
        if (PlayerLevelSystem.Instance != null)
        {
            PlayerLevelSystem.Instance.SetPickaxeRing(pickaxeRing);
            PlayerLevelSystem.Instance.ForceRefresh();
        }

        // Повторно устанавливаем нужную кирку,
        // чтобы ForceRefresh не оставил старую.
        pickaxeRing.EquipPickaxe(_equippedPickaxeId);

        ApplyDamageMultiplierToEquippedPickaxe();

        if (showDebugLogs)
        {
            Debug.Log(
                "[PickaxeStandsController] После обновления экипирована: " +
                _equippedPickaxeId,
                this
            );
        }
    }

    public void RefreshAllStands()
    {
        for (int i = 0; i < _stands.Count; i++)
        {
            if (_stands[i] != null)
                _stands[i].RefreshStand();
        }
    }

    public bool IsPickaxeBought(string pickaxeId)
    {
        if (string.IsNullOrEmpty(pickaxeId))
            return false;

        return _boughtPickaxes.Contains(pickaxeId);
    }

    public bool IsPickaxeEquipped(string pickaxeId)
    {
        if (string.IsNullOrEmpty(pickaxeId))
            return false;

        return _equippedPickaxeId == pickaxeId;
    }

    public bool CanBuyPickaxe(PickaxeStand stand)
    {
        if (stand == null || stand.PickaxeData == null)
            return false;

        if (IsPickaxeBought(stand.PickaxeId))
            return false;

        if (stand.IsFirstPickaxe)
            return true;

        PickaxeStand previousStand = GetPreviousStand(stand);

        if (previousStand == null)
            return false;

        return IsPickaxeBought(previousStand.PickaxeId);
    }

    public bool TryBuyPickaxe(PickaxeStand stand)
    {
        if (stand == null || stand.PickaxeData == null)
            return false;

        if (!CanBuyPickaxe(stand))
            return false;

        bool successfullySubtracted =
            ValueSystem.SubstractionBalance(
                stand.PickaxeData.price.amount,
                stand.PickaxeData.price.valueType
            );

        if (!successfullySubtracted)
            return false;

        if (GlobalSound.instance != null)
            GlobalSound.instance.PlaySound_BuyTheme1(false);

        AddBoughtPickaxe(stand.PickaxeId);
        EquipPickaxe(stand);

        SaveData();
        RefreshAllStands();

        return true;
    }

    public void EquipPickaxe(PickaxeStand stand)
    {
        if (stand == null || stand.PickaxeData == null)
            return;

        EquipPickaxeById(stand.PickaxeId);
    }

    public void EquipPickaxeById(string pickaxeId)
    {
        if (string.IsNullOrEmpty(pickaxeId))
            return;

        if (!IsPickaxeBought(pickaxeId))
            return;

        _equippedPickaxeId = pickaxeId;

        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        YG2.saves.playerData.equippedPickaxeId =
            _equippedPickaxeId;

        if (pickaxeRing != null)
        {
            pickaxeRing.EquipPickaxe(_equippedPickaxeId);
            ApplyDamageMultiplierToEquippedPickaxe();
        }

        if (PlayerLevelSystem.Instance != null)
            PlayerLevelSystem.Instance.ForceRefresh();

        SaveData();
        RefreshAllStands();

        if (showDebugLogs)
        {
            Debug.Log(
                "[PickaxeStandsController] Экипирована кирка: " +
                _equippedPickaxeId,
                this
            );
        }
    }

    private void AddBoughtPickaxe(string pickaxeId)
    {
        if (string.IsNullOrEmpty(pickaxeId))
            return;

        EnsureSaveDataExists();

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        _boughtPickaxes.Add(pickaxeId);

        if (!YG2.saves.playerData.boughtPickaxes.Contains(
                pickaxeId))
        {
            YG2.saves.playerData.boughtPickaxes.Add(pickaxeId);
        }
    }

    public PickaxeStand GetPreviousStand(PickaxeStand currentStand)
    {
        if (currentStand == null)
            return null;

        int index = _stands.IndexOf(currentStand);

        if (index <= 0)
            return null;

        return _stands[index - 1];
    }

    public IReadOnlyList<PickaxeStand> GetStands()
    {
        return _stands;
    }

    public void ReloadAfterRebirth()
    {
        LoadData();

        EquipSavedPickaxeToRing();

        RefreshAllStands();

        ApplyDamageMultiplierToEquippedPickaxe();
    }

    public void ApplyDamageMultiplierToEquippedPickaxe()
    {
        float multiplier = 1f;

        EnsureSaveDataExists();

        if (YG2.saves != null &&
            YG2.saves.playerData != null)
        {
            multiplier = Mathf.Max(
                1f,
                YG2.saves.playerData.pickaxeDamageMultiplier
            );
        }

        PickaxeEntity[] pickaxes = GetAllPickaxeEntities();

        for (int i = 0; i < pickaxes.Length; i++)
        {
            if (pickaxes[i] == null)
                continue;

            pickaxes[i].SetDamageMultiplier(multiplier);
        }
    }

    private PickaxeEntity[] GetAllPickaxeEntities()
    {
        if (pickaxeRing == null)
            return new PickaxeEntity[0];

        return pickaxeRing.GetComponentsInChildren<PickaxeEntity>(
            true
        );
    }

    private void EnsureSaveDataExists()
    {
        if (YG2.saves == null)
            return;

        if (YG2.saves.playerData == null)
            YG2.saves.playerData = new PlayerData();

        YG2.saves.playerData.EnsureValidData();
    }

    private void SaveData()
    {
        YG2.SaveProgress();
    }
}