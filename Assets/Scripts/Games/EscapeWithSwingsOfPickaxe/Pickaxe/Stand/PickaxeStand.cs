using TMPro;
using UnityEngine;

public class PickaxeStand : MonoBehaviour
{
    [Header("Pickaxe")]
    [SerializeField] private PickaxeData pickaxeData;

    [Header("Settings")]
    [SerializeField] private bool _isFirstPickaxe;

    [SerializeField] private Transform _pickaxePrefabPoint;
    [SerializeField] private GameObject _displayParent;

    [SerializeField] private TMP_Text _pickaxePowerDisplay;
    [SerializeField] private TMP_Text _stateBuyDisplay;

    [Header("Purchase Trigger")]
    [SerializeField] private Collider _buyTrigger;

    private GameObject _pickaxePrefab;

    public PickaxeData PickaxeData => pickaxeData;

    [SerializeField] private DynamicTilingProperty _standMaterial;

    public string PickaxeId
    {
        get
        {
            if (pickaxeData == null)
            {
                return string.Empty;
            }

            return pickaxeData.id;
        }
    }

    public bool IsFirstPickaxe => _isFirstPickaxe;

    private void Awake()
    {
        SetupTrigger();
    }

    private void Start()
    {
        InitializationStand();
    }

    #region Initialization

    private void InitializationStand()
    {
        if (pickaxeData == null)
        {
            Debug.LogError($"У стенда {name} не назначен PickaxeData.", this);
            return;
        }

        CreatePrefab();
        UpdatePowerDisplay();
        RefreshStand();
    }

    private void CreatePrefab()
    {
        if (_pickaxePrefabPoint == null)
        {
            Debug.LogError($"У стенда {name} не назначена точка _pickaxePrefabPoint.", this);
            return;
        }

        if (pickaxeData.pickaxePrefab == null)
        {
            Debug.LogError($"У кирки {PickaxeId} не назначен pickaxePrefab.", this);
            return;
        }

        if (_pickaxePrefab != null)
        {
            Destroy(_pickaxePrefab);
        }

        _pickaxePrefab = Instantiate(
            pickaxeData.pickaxePrefab,
            _pickaxePrefabPoint.position,
            _pickaxePrefabPoint.rotation,
            _pickaxePrefabPoint
        );
    }

    private void UpdatePowerDisplay()
    {
        if (_pickaxePowerDisplay == null || pickaxeData == null)
        {
            return;
        }

        _pickaxePowerDisplay.text =
            "+ " +
            pickaxeData.damage.ToString() +
            " " +
            UTF.ReadLanguageDirectory(LangLIst.instance.power_b);
    }

    #endregion

    #region Stand State

    public void RefreshStand()
    {
        if (pickaxeData == null)
        {
            return;
        }

        bool isBought =
            PickaxeStandsController.Instance != null &&
            PickaxeStandsController.Instance.IsPickaxeBought(PickaxeId);

        bool isAvailable =
            PickaxeStandsController.Instance != null &&
            PickaxeStandsController.Instance.CanBuyPickaxe(this);

        bool isEquipped =
            PickaxeStandsController.Instance != null &&
            PickaxeStandsController.Instance.IsPickaxeEquipped(PickaxeId);

        // Показываем текущую кирку только если она доступна.
        // Первая кирка доступна сразу.
        if (_displayParent != null)
        {
            _displayParent.SetActive(isBought || isAvailable);
        }

        UpdateStateBuy(isBought, isAvailable, isEquipped);
    }

    public void UpdateStateBuy()
    {
        RefreshStand();
    }

    private void UpdateStateBuy(
    bool isBought,
    bool isAvailable,
    bool isEquipped)
    {
        if (_stateBuyDisplay == null)
        {
            return;
        }

        Color blueColor = new Color(0f, 0.6f, 1f, 1f);
        Color orangeColor = new Color(1f, 0.5f, 0f, 1f);
        Color greenColor = Color.green;
        Color redColor = Color.red;

        if (isEquipped)
        {
            _stateBuyDisplay.text =
                UTF.ReadLanguageDirectory(LangLIst.Equipped).ToUpper();

            _stateBuyDisplay.color = greenColor;
            _standMaterial.SetColor(greenColor);
            return;
        }

        if (isBought)
        {
            _stateBuyDisplay.text =
                UTF.ReadLanguageDirectory(LangLIst.Bought).ToUpper();

            _stateBuyDisplay.color = orangeColor;
            _standMaterial.SetColor(orangeColor);
            return;
        }

        // Проверяем предыдущую кирку.
        PickaxeStand previousStand =
            PickaxeStandsController.Instance != null
                ? PickaxeStandsController.Instance.GetPreviousStand(this)
                : null;

        // Нельзя купить, пока предыдущая кирка не куплена.
        if (!_isFirstPickaxe &&
            previousStand != null &&
            !PickaxeStandsController.Instance.IsPickaxeBought(
                previousStand.PickaxeId))
        {
            _standMaterial.SetColor(redColor);
            return;
        }

        // Кирка доступна для покупки.
        _stateBuyDisplay.text =
            LangLIst.RequiredWinsText(pickaxeData.price.amount);

        _stateBuyDisplay.color = blueColor;
        _standMaterial.SetColor(blueColor);
    }


    #endregion

    #region Trigger

    private void SetupTrigger()
    {
        if (_buyTrigger == null)
        {
            _buyTrigger = GetComponent<Collider>();
        }

        if (_buyTrigger != null)
        {
            _buyTrigger.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();

            if (player != null && player.thisRealPlayer)
            {
                TryInteract();
            }
        }
    }

    public void TryInteract()
    {
        if (PickaxeStandsController.Instance == null)
        {
            Debug.LogError("На сцене отсутствует PickaxeStandsController.");
            return;
        }

        bool isBought =
            PickaxeStandsController.Instance.IsPickaxeBought(PickaxeId);

        if (isBought)
        {
            PickaxeStandsController.Instance.EquipPickaxe(this);
            GlobalSound.instance.PlaySound_ShortPop(true);
            return;
        }

        PickaxeStandsController.Instance.TryBuyPickaxe(this);
    }

    #endregion
}