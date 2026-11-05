using System.Globalization;
using TMPro;
using UnityEngine;
using YG;

public class PlayerDamageSystem : MonoBehaviour
{
    public static PlayerDamageSystem Instance { get; private set; }

    [SerializeField] private RebirthSystem rebirthSystem;

    [Header("Множители")]
    [SerializeField, Min(0.01f)] private float trailMultiplier = 1f;

    [Header("UI")]
    [SerializeField] private TMP_Text totalDamageMultiplierText;

    [Tooltip("Текст формата: Множитель x12 (След x3 + Аура x4)")]
    [SerializeField] private TMP_Text trailAuraMultiplierText;

    public float RebirthMultiplier
    {
        get
        {
            if (rebirthSystem == null)
                rebirthSystem = RebirthSystem.Instance;

            return rebirthSystem != null
                ? Mathf.Max(0.01f, rebirthSystem.DamageMultiplier)
                : 1f;
        }
    }

    public float TrailMultiplier => Mathf.Max(0.01f, trailMultiplier);

    public float AuraMultiplier =>
        PlayerAuraController.Instance != null
            ? PlayerAuraController.Instance.CurrentAuraMultiplier
            : 1f;

    // Множитель купленного иннапа x2 урона.
    public float InAppDamageMultiplier
    {
        get
        {
            if (YG2.saves == null || YG2.saves.playerData == null)
                return 1f;

            YG2.saves.playerData.EnsureValidData();

            return YG2.saves.playerData.hasDoubleDamage ? 2f : 1f;
        }
    }

    // Все множители урона перемножаются.
    public float TotalDamageMultiplier =>
        RebirthMultiplier *
        TrailMultiplier *
        AuraMultiplier *
        InAppDamageMultiplier;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "В сцене найдено несколько PlayerDamageSystem.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (rebirthSystem == null)
            rebirthSystem = RebirthSystem.Instance;

        RefreshUI();
    }

    private void OnEnable()
    {
        if (PlayerAuraController.Instance != null)
        {
            PlayerAuraController.Instance.OnAuraChanged += OnAuraChanged;
        }
    }

    private void OnDisable()
    {
        if (PlayerAuraController.Instance != null)
        {
            PlayerAuraController.Instance.OnAuraChanged -= OnAuraChanged;
        }
    }

    private void Update()
    {
        RefreshUI();
    }

    private void OnAuraChanged(AuraData aura, bool enabled)
    {
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetTrailMultiplier(float multiplier)
    {
        trailMultiplier = Mathf.Max(0.01f, multiplier);
        RefreshUI();
    }

    // Оставлено для совместимости с существующим кодом.
    public void SetAuraMultiplier(float multiplier)
    {
        RefreshUI();
    }

    public float CalculateDamage(float baseDamage)
    {
        return Mathf.Max(0f, baseDamage) * TotalDamageMultiplier;
    }

    public void RefreshUI()
    {
        string format = "0.##";

        if (totalDamageMultiplierText != null)
        {
            totalDamageMultiplierText.text =
                "x" + TotalDamageMultiplier.ToString(
                    format,
                    CultureInfo.InvariantCulture
                );
        }

        if (trailAuraMultiplierText != null)
        {
            float trail = TrailMultiplier;
            float aura = AuraMultiplier;
            float combined = trail * aura;

            trailAuraMultiplierText.text =
                "Множитель x" +
                combined.ToString(format, CultureInfo.InvariantCulture) +
                " (След и Аура)";
        }
    }
}
