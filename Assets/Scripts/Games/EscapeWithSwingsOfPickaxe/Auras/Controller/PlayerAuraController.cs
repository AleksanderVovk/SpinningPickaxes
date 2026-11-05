using System;
using TMPro;
using UnityEngine;
using YG;

public class PlayerAuraController : MonoBehaviour
{
    public static PlayerAuraController Instance { get; private set; }

    [Header("Доступные ауры")]
    [SerializeField] private AuraData[] availableAuras;
    [SerializeField] private Transform auraAnchor;

    [Header("UI информации об ауре")]
    [SerializeField] private TMP_Text auraNameText;
    [SerializeField] private TMP_Text auraChanceText;
    [SerializeField] private TMP_Text auraMultiplierText;

    public event Action<AuraData, bool> OnAuraChanged;

    public AuraData CurrentAura { get; private set; }
    public bool AuraEnabled { get; private set; }
    public AuraData[] AvailableAuras => availableAuras;

    private ParticleSystem activeParticles;
    private bool initialized;

    public float CurrentAuraMultiplier =>
        CurrentAura != null && AuraEnabled
            ? Mathf.Max(0.01f, CurrentAura.damageMultiplier)
            : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("В сцене найдено несколько PlayerAuraController.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        RestoreSavedAura();
    }

    /// <summary>
    /// Восстанавливает единственную ауру из сохранения.
    /// Можно повторно вызвать после загрузки сохранения YG.
    /// </summary>
    public void RestoreSavedAura()
    {
        if (initialized)
            DestroyActiveParticles();

        initialized = true;

        string savedId = YG2.saves.playerData.equippedAuraId;
        bool savedEnabled = YG2.saves.playerData.isAuraEnabled;

        CurrentAura = FindAura(savedId);
        AuraEnabled = CurrentAura != null && savedEnabled;

        ApplyAura();
        RefreshAuraUI();
        NotifyAuraChanged();
    }

    /// <summary>
    /// Полностью заменяет предыдущую ауру новой и сохраняет результат.
    /// </summary>
    public void SetNewAura(AuraData newAura)
    {
        if (newAura == null)
        {
            Debug.LogWarning("Нельзя установить пустую ауру.", this);
            return;
        }

        CurrentAura = newAura;
        AuraEnabled = true;

        YG2.saves.playerData.equippedAuraId = newAura.id;
        YG2.saves.playerData.isAuraEnabled = true;
        YG2.SaveProgress();

        ApplyAura();
        RefreshAuraUI();
        NotifyAuraChanged();
    }

    public void ToggleAura()
    {
        if (CurrentAura == null)
            return;

        if (AuraEnabled)
            DisableAura();
        else
            EnableAura();
    }

    public void DisableAura()
    {
        if (CurrentAura == null)
            return;

        AuraEnabled = false;
        YG2.saves.playerData.isAuraEnabled = false;
        YG2.SaveProgress();

        ApplyAura();
        RefreshAuraUI();
        NotifyAuraChanged();
    }

    public void EnableAura()
    {
        if (CurrentAura == null)
            return;

        AuraEnabled = true;
        YG2.saves.playerData.isAuraEnabled = true;
        YG2.SaveProgress();

        ApplyAura();
        RefreshAuraUI();
        NotifyAuraChanged();
    }

    public AuraData FindAura(string id)
    {
        if (string.IsNullOrEmpty(id) || availableAuras == null)
            return null;

        foreach (AuraData aura in availableAuras)
        {
            if (aura != null && aura.id == id)
                return aura;
        }

        return null;
    }

    public float GetChancePercent(AuraData aura)
    {
        if (aura == null || availableAuras == null)
            return 0f;

        float totalWeight = 0f;

        foreach (AuraData item in availableAuras)
        {
            if (item != null)
                totalWeight += Mathf.Max(0f, item.dropWeight);
        }

        if (totalWeight <= 0f)
            return 0f;

        return Mathf.Max(0f, aura.dropWeight) / totalWeight * 100f;
    }

    private void ApplyAura()
    {
        DestroyActiveParticles();

        if (PlayerDamageSystem.Instance != null)
            PlayerDamageSystem.Instance.SetAuraMultiplier(CurrentAuraMultiplier);

        if (CurrentAura == null || !AuraEnabled || CurrentAura.particlePrefab == null)
            return;

        Transform parent = auraAnchor != null ? auraAnchor : transform;

        activeParticles = Instantiate(
            CurrentAura.particlePrefab,
            parent
        );

        Transform particleTransform = activeParticles.transform;
        particleTransform.localPosition = CurrentAura.particleOffset;
        particleTransform.localRotation = Quaternion.Euler(CurrentAura.particleRotation);
        particleTransform.localScale = Vector3.one * CurrentAura.particleScale;

        activeParticles.Play(true);
    }

    private void DestroyActiveParticles()
    {
        if (activeParticles == null)
            return;

        Destroy(activeParticles.gameObject);
        activeParticles = null;
    }

    private void RefreshAuraUI()
    {
        if (CurrentAura == null)
        {
            SetText(auraNameText, "Нет ауры");
            SetText(auraChanceText, "<color=yellow>Шанс выпадения: —</color>");
            SetText(auraMultiplierText, "Множитель урона: x1");
            return;
        }

        SetText(auraNameText, UTF.ReadLanguageDirectory(CurrentAura.displayName));
        ApplyGradient(auraNameText, CurrentAura.nameGradient);

        SetText(
            auraChanceText,
            $"Шанс выпадения: <color=yellow>{GetChancePercent(CurrentAura):0.##}%</color>"
        );

        SetText(
            auraMultiplierText,
            "Множитель урона: x" +
            CurrentAura.damageMultiplier.ToString(
                "0.##",
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    private void ApplyGradient(TMP_Text text, Gradient gradient)
    {
        if (text == null || gradient == null)
            return;

        Color top = gradient.Evaluate(1f);
        Color bottom = gradient.Evaluate(0f);

        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(top, top, bottom, bottom);
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void NotifyAuraChanged()
    {
        OnAuraChanged?.Invoke(CurrentAura, AuraEnabled);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}