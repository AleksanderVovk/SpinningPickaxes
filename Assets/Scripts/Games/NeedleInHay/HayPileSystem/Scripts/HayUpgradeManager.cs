using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HayPileSystem
{
    public class HayUpgradeManager : MonoBehaviour
    {
        public enum UpgradeType
        {
            HandHold,
            HandSpeed,
            HandAmount,
            PitchforkHold,
            PitchforkSpeed,
            PitchforkAmount
        }

        [Serializable]
        public class UpgradeDisplay
        {
            [Header("Upgrade")]
            public UpgradeType type;

            [Header("Selection")]
            public Button selectButton;
            public Image selectedVisual;

            [Header("Display")]
            public TMP_Text titleLabel;
            public TMP_Text levelLabel;
            public TMP_Text priceLabel;
            public TMP_Text effectLabel;

            [Header("Level slider")]
            public Slider levelSlider;
        }

        [Serializable]
        public class UpgradeSettings
        {
            public UpgradeType type;

            [Header("Info")]
            public string title;

            [TextArea(2, 6)]
            public string description;

            [Header("Progression")]
            [Min(0f)] public float startingPrice = 0.25f;
            [Min(1)] public int maxLevel = 10;
            [Min(1f)] public float priceMultiplier = 2f;

            [Header("Speed: seconds removed per level")]
            [Min(0f)] public float cooldownReductionPerLevel = 0.08f;
            [Min(0.01f)] public float minimumCooldown = 0.12f;

            [Header("Amount: extra straws per level")]
            [Min(1)] public int amountAddedPerLevel = 1;
        }

        [Header("References")]
        [SerializeField] private HayPileInteractor interactor;
        [SerializeField] private HayShopSeller moneySource;

        [Header("Starting values")]
        [SerializeField, Min(1)] private int startingHandAmount = 1;
        [SerializeField, Min(1)] private int startingPitchforkAmount = 3;
        [SerializeField, Min(0.01f)] private float startingHandCooldown = 0.6f;
        [SerializeField, Min(0.01f)] private float startingPitchforkCooldown = 0.6f;
        [SerializeField] private bool handHoldUnlockedAtStart;
        [SerializeField] private bool pitchforkHoldUnlockedAtStart;

        [Header("Upgrade settings")]
        [SerializeField] private UpgradeSettings[] settings;

        [Header("Upgrade displays")]
        [SerializeField] private UpgradeDisplay[] displays;

        [Header("Selection colors")]
        [SerializeField] private Color defaultCardColor = Color.white;
        [SerializeField] private Color selectedCardColor = new Color(0.2f, 0.8f, 0.4f, 1f);

        [Header("Global selected upgrade description")]
        [SerializeField] private GameObject descriptionRoot;
        [SerializeField] private TMP_Text descriptionTitleLabel;
        [SerializeField] private TMP_Text descriptionTextLabel;

        [Header("Global buy button")]
        [SerializeField] private GameObject buyButtonRoot;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonLabel;

        private readonly Dictionary<UpgradeType, UpgradeSettings> settingsByType =
            new Dictionary<UpgradeType, UpgradeSettings>();

        private readonly Dictionary<UpgradeType, UpgradeDisplay> displaysByType =
            new Dictionary<UpgradeType, UpgradeDisplay>();

        private readonly Dictionary<UpgradeType, int> levels =
            new Dictionary<UpgradeType, int>();

        private UpgradeType? selectedUpgrade;

        public int GetLevel(UpgradeType type)
        {
            int level;

            if (levels.TryGetValue(type, out level))
                return level;

            return 0;
        }

        private void Awake()
        {
            settingsByType.Clear();
            displaysByType.Clear();
            levels.Clear();

            if (settings != null)
            {
                for (int i = 0; i < settings.Length; i++)
                {
                    UpgradeSettings setting = settings[i];

                    if (setting == null)
                        continue;

                    if (IsHoldUpgrade(setting.type))
                        setting.maxLevel = 1;

                    settingsByType[setting.type] = setting;
                    levels[setting.type] = 0;
                }
            }

            if (displays != null)
            {
                for (int i = 0; i < displays.Length; i++)
                {
                    UpgradeDisplay display = displays[i];

                    if (display == null)
                        continue;

                    displaysByType[display.type] = display;

                    if (display.levelSlider != null)
                    {
                        display.levelSlider.minValue = 0f;
                        display.levelSlider.maxValue = 1f;
                        display.levelSlider.value = 0f;
                        display.levelSlider.interactable = false;
                    }

                    if (display.selectButton != null)
                    {
                        UpgradeType type = display.type;
                        display.selectButton.onClick.AddListener(delegate
                        {
                            SelectUpgrade(type);
                        });
                    }
                }
            }

            if (buyButton != null)
                buyButton.onClick.AddListener(BuySelectedUpgrade);

            HideDescription();
            HideBuyButton();
        }

        private void Start()
        {
            if (interactor == null)
            {
                RefreshAllDisplays();
                return;
            }

            interactor.SetToolCollectionCooldown(
                HayPileInteractor.ToolType.Hand,
                startingHandCooldown
            );

            interactor.SetToolCollectionCooldown(
                HayPileInteractor.ToolType.Pitchfork,
                startingPitchforkCooldown
            );

            interactor.SetStrawsPerAction(
                HayPileInteractor.ToolType.Hand,
                startingHandAmount
            );

            interactor.SetStrawsPerAction(
                HayPileInteractor.ToolType.Pitchfork,
                startingPitchforkAmount
            );

            interactor.EnableHoldToCollect(
                HayPileInteractor.ToolType.Hand,
                handHoldUnlockedAtStart
            );

            interactor.EnableHoldToCollect(
                HayPileInteractor.ToolType.Pitchfork,
                pitchforkHoldUnlockedAtStart
            );

            if (handHoldUnlockedAtStart && levels.ContainsKey(UpgradeType.HandHold))
                levels[UpgradeType.HandHold] = 1;

            if (pitchforkHoldUnlockedAtStart && levels.ContainsKey(UpgradeType.PitchforkHold))
                levels[UpgradeType.PitchforkHold] = 1;

            RefreshAllDisplays();
        }

        public void SelectUpgrade(UpgradeType type)
        {
            if (!settingsByType.ContainsKey(type))
                return;

            GlobalSound.instance.PlaySound_ShortPop(true);

            selectedUpgrade = type;

            RefreshSelectionColors();
            RefreshDisplay(type);
            ShowDescription(type);
            ShowBuyButton();
            RefreshBuyButton();
        }

        public void BuySelectedUpgrade()
        {
            if (!selectedUpgrade.HasValue)
            {
                return;
            }

            BuyUpgrade(selectedUpgrade.Value);
        }

        public void BuyUpgrade(UpgradeType type)
        {
            if (interactor == null || moneySource == null)
                return;

            if (!settingsByType.TryGetValue(type, out UpgradeSettings setting))
                return;

            int level = GetLevel(type);

            if (level >= setting.maxLevel)
            {
                RefreshDisplay(type);
                RefreshBuyButton();
                return;
            }

            float price = GetPrice(setting, level);

            if (!moneySource.TrySpendBalance(price))
                return;

            level++;
            levels[type] = level;

            GlobalSound.instance.PlaySound_BuyTheme1(false);

            ApplyUpgrade(type, setting, level);

            RefreshDisplay(type);
            RefreshBuyButton();
        }

        private void ApplyUpgrade(UpgradeType type, UpgradeSettings setting, int level)
        {
            switch (type)
            {
                case UpgradeType.HandHold:
                    interactor.EnableHoldToCollect(HayPileInteractor.ToolType.Hand, true);
                    break;

                case UpgradeType.PitchforkHold:
                    interactor.EnableHoldToCollect(HayPileInteractor.ToolType.Pitchfork, true);
                    break;

                case UpgradeType.HandSpeed:
                    {
                        float cooldown = GetHandCooldown(setting, level);
                        interactor.SetToolCollectionCooldown(HayPileInteractor.ToolType.Hand, cooldown);
                        break;
                    }

                case UpgradeType.PitchforkSpeed:
                    {
                        float cooldown = GetPitchforkCooldown(setting, level);
                        interactor.SetToolCollectionCooldown(HayPileInteractor.ToolType.Pitchfork, cooldown);
                        break;
                    }

                case UpgradeType.HandAmount:
                    {
                        int amount = GetHandAmount(setting, level);
                        interactor.SetStrawsPerAction(HayPileInteractor.ToolType.Hand, amount);
                        break;
                    }

                case UpgradeType.PitchforkAmount:
                    {
                        int amount = GetPitchforkAmount(setting, level);
                        interactor.SetStrawsPerAction(HayPileInteractor.ToolType.Pitchfork, amount);
                        break;
                    }
            }
        }

        private void RefreshAllDisplays()
        {
            RefreshSelectionColors();

            foreach (UpgradeType type in displaysByType.Keys)
                RefreshDisplay(type);

            RefreshBuyButton();
        }

        private void RefreshSelectionColors()
        {
            foreach (KeyValuePair<UpgradeType, UpgradeDisplay> pair in displaysByType)
            {
                UpgradeDisplay display = pair.Value;

                if (display.selectedVisual == null)
                    continue;

                bool selected = selectedUpgrade.HasValue && selectedUpgrade.Value == pair.Key;

                if (selected)
                    display.selectedVisual.color = selectedCardColor;
                else
                    display.selectedVisual.color = defaultCardColor;
            }
        }

        private void RefreshDisplay(UpgradeType type)
        {
            if (!displaysByType.TryGetValue(type, out UpgradeDisplay display))
                return;

            if (!settingsByType.TryGetValue(type, out UpgradeSettings setting))
                return;

            int level = GetLevel(type);
            bool maxed = level >= setting.maxLevel;
            float price = GetPrice(setting, level);

            if (display.titleLabel != null)
                display.titleLabel.text = GetTitle(type, setting);

            if (display.levelLabel != null)
            {
                if (maxed)
                    display.levelLabel.text = "Уровень: " + setting.maxLevel + "/" + setting.maxLevel + " (МАКСИМУМ)";
                else
                    display.levelLabel.text = "Уровень: " + level + "/" + setting.maxLevel;
            }

            if (display.priceLabel != null)
            {
                if (maxed)
                    display.priceLabel.text = "МАКСИМУМ";
                else
                    display.priceLabel.text = price.ToString("0.00") + "$";
            }

            if (display.levelSlider != null)
            {
                display.levelSlider.maxValue = setting.maxLevel;
                display.levelSlider.value = level;
            }

            if (display.effectLabel != null)
                display.effectLabel.text = GetEffectText(type, setting, level);
        }

        private void ShowBuyButton()
        {
            if (buyButtonRoot != null)
                buyButtonRoot.SetActive(true);
        }

        private void HideBuyButton()
        {
            if (buyButtonRoot != null)
                buyButtonRoot.SetActive(false);
        }

        private void RefreshBuyButton()
        {
            if (buyButton == null)
                return;

            if (!selectedUpgrade.HasValue)
            {
                buyButton.interactable = false;

                if (buyButtonLabel != null)
                    buyButtonLabel.text = "Выбери улучшение";

                return;
            }

            UpgradeType type = selectedUpgrade.Value;

            if (!settingsByType.TryGetValue(type, out UpgradeSettings setting))
            {
                buyButton.interactable = false;

                if (buyButtonLabel != null)
                    buyButtonLabel.text = "Выбери улучшение";

                return;
            }

            int level = GetLevel(type);
            bool maxed = level >= setting.maxLevel;

            if (maxed)
            {
                buyButton.interactable = false;

                if (buyButtonLabel != null)
                    buyButtonLabel.text = "Максимум";

                return;
            }

            float price = GetPrice(setting, level);

            buyButton.interactable = true;

            if (buyButtonLabel != null)
                buyButtonLabel.text = "Купить (" + price.ToString("0.00") + "$)";
        }

        private void ShowDescription(UpgradeType type)
        {
            if (!settingsByType.TryGetValue(type, out UpgradeSettings setting))
                return;

            if (descriptionRoot != null)
                descriptionRoot.SetActive(true);

            if (descriptionTitleLabel != null)
                descriptionTitleLabel.text = GetTitle(type, setting);

            if (descriptionTextLabel != null)
                descriptionTextLabel.text = setting.description;
        }

        private void HideDescription()
        {
            if (descriptionRoot != null)
                descriptionRoot.SetActive(false);

            if (descriptionTitleLabel != null)
                descriptionTitleLabel.text = string.Empty;

            if (descriptionTextLabel != null)
                descriptionTextLabel.text = string.Empty;
        }

        private string GetEffectText(UpgradeType type, UpgradeSettings setting, int level)
        {
            switch (type)
            {
                case UpgradeType.HandHold:
                case UpgradeType.PitchforkHold:
                    if (level > 0)
                        return "Автосбор при удержании ЛКМ: открыт";
                    else
                        return "Зажми ЛКМ для автоматического сбора";

                case UpgradeType.HandSpeed:
                    {
                        float cooldown = GetHandCooldown(setting, level);
                        return "Перезарядка: " + cooldown.ToString("0.00") + " сек";
                    }

                case UpgradeType.PitchforkSpeed:
                    {
                        float cooldown = GetPitchforkCooldown(setting, level);
                        return "Перезарядка: " + cooldown.ToString("0.00") + " сек";
                    }

                case UpgradeType.HandAmount:
                    {
                        int amount = GetHandAmount(setting, level);
                        return "За сбор: " + amount + " шт.";
                    }

                case UpgradeType.PitchforkAmount:
                    {
                        int amount = GetPitchforkAmount(setting, level);
                        return "За сбор: " + amount + " шт.";
                    }

                default:
                    return string.Empty;
            }
        }

        private float GetHandCooldown(UpgradeSettings setting, int level)
        {
            float cooldown = startingHandCooldown - level * setting.cooldownReductionPerLevel;
            return Mathf.Max(setting.minimumCooldown, cooldown);
        }

        private float GetPitchforkCooldown(UpgradeSettings setting, int level)
        {
            float cooldown = startingPitchforkCooldown - level * setting.cooldownReductionPerLevel;
            return Mathf.Max(setting.minimumCooldown, cooldown);
        }

        private int GetHandAmount(UpgradeSettings setting, int level)
        {
            return startingHandAmount + level * setting.amountAddedPerLevel;
        }

        private int GetPitchforkAmount(UpgradeSettings setting, int level)
        {
            return startingPitchforkAmount + level * setting.amountAddedPerLevel;
        }

        private string GetTitle(UpgradeType type, UpgradeSettings setting)
        {
            if (!string.IsNullOrWhiteSpace(setting.title))
                return setting.title;

            switch (type)
            {
                case UpgradeType.HandHold:
                    return "Рука — Удерживание";

                case UpgradeType.HandSpeed:
                    return "Рука — Скорость подбора";

                case UpgradeType.HandAmount:
                    return "Рука — Количество подбора";

                case UpgradeType.PitchforkHold:
                    return "Вилы — Удерживание";

                case UpgradeType.PitchforkSpeed:
                    return "Вилы — Скорость подбора";

                case UpgradeType.PitchforkAmount:
                    return "Вилы — Количество подбора";

                default:
                    return "Улучшение";
            }
        }

        private static bool IsHoldUpgrade(UpgradeType type)
        {
            return type == UpgradeType.HandHold || type == UpgradeType.PitchforkHold;
        }

        private static float GetPrice(UpgradeSettings setting, int currentLevel)
        {
            return setting.startingPrice * Mathf.Pow(setting.priceMultiplier, currentLevel);
        }
    }
}