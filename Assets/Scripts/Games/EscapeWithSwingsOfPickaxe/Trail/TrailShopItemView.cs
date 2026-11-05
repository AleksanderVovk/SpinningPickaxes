using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class TrailShopItemView : MonoBehaviour
{
    [Header("Тексты")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text winsPriceText;
    [SerializeField] private TMP_Text inAppPriceText;
    [SerializeField] private TMP_Text interactButtonText;

    [Header("Кнопки")]
    [SerializeField] private Button buyWinsButton;
    [SerializeField] private Button buyInAppButton;
    [SerializeField] private Button interactButton;

    private TrailData trail;
    private Action onChanged;

    [SerializeField] private HorizontalGradientOverlay _gradientTrail;

    public void Initialize(TrailData data, Action refreshCallback)
    {
        trail = data;
        onChanged = refreshCallback;

        buyWinsButton.onClick.RemoveAllListeners();
        buyInAppButton.onClick.RemoveAllListeners();
        interactButton.onClick.RemoveAllListeners();

        buyWinsButton.onClick.AddListener(BuyWithWins);
        buyInAppButton.onClick.AddListener(BuyWithInApp);
        interactButton.onClick.AddListener(Interact);

        _gradientTrail.ApplyGradient(data.color1, data.color2, data.color3, data.color4);

        Refresh();
    }

    public void Refresh()
    {
        if (trail == null || YG2.saves == null ||
            YG2.saves.playerData == null)
            return;

        var data = YG2.saves.playerData;

        bool bought = data.boughtTrails.Contains(trail.id);
        bool equipped = data.equippedTrailId == trail.id;

        if (nameText != null)
            nameText.text = UTF.ReadLanguageDirectory(trail.displayName);

        if (multiplierText != null)
            multiplierText.text = "x" + trail.damageMultiplier.ToString("0.##");

        if (winsPriceText != null)
            winsPriceText.text = UTF.manager.TextToSprite("\U0001F600") + trail.winsPrice.ToString();

        if (inAppPriceText != null)
            inAppPriceText.text = GetInAppPriceText();

        // Покупки доступны только для ещё не купленного следа.
        SetButtonVisible(buyWinsButton,
            !bought && trail.winsPrice > 0);

        SetButtonVisible(buyInAppButton,
            !bought && !string.IsNullOrWhiteSpace(trail.inAppId));

        // Если оба способа покупки недоступны и след не куплен,
        // карточка не должна показывать кнопку надевания.
        SetButtonVisible(interactButton, bought);

        if (interactButtonText != null)
        {
            interactButtonText.text = equipped
                ? Translate("Снять", "Unequip", "Çıkar")
                : Translate("Надеть", "Equip", "Tak");
        }
    }

    private void BuyWithWins()
    {
        if (trail == null || trail.winsPrice <= 0)
            return;

        var data = YG2.saves.playerData;

        if (data.boughtTrails.Contains(trail.id))
        {
            Refresh();
            return;
        }

        if (!ValueSystem.SubstractionBalance(trail.winsPrice, ValueType.Wins))
            return;

        data.boughtTrails.Add(trail.id);
        YG2.SaveProgress();

        GlobalSound.instance.PlaySound_BuyTheme1(false);

        Refresh();
        onChanged?.Invoke();
    }

    private void BuyWithInApp()
    {
        if (trail == null || string.IsNullOrWhiteSpace(trail.inAppId))
            return;

        var data = YG2.saves.playerData;

        if (data.boughtTrails.Contains(trail.id))
        {
            Refresh();
            return;
        }

        TrailInAppPurchase.Buy(trail.inAppId, trail.id);
    }

    private void Interact()
    {
        if (trail == null)
            return;

        var data = YG2.saves.playerData;

        if (!data.boughtTrails.Contains(trail.id))
            return;

        PlayerTrailController controller = PlayerTrailController.Instance;

        if (controller == null)
        {
            Debug.LogError("PlayerTrailController не найден.");
            return;
        }

        GlobalSound.instance.PlaySound_ShortPop(true);

        if (data.equippedTrailId == trail.id)
            controller.UnequipTrail();
        else
            controller.EquipTrail(trail.id);

        Refresh();
        onChanged?.Invoke();
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(visible);
        button.interactable = visible;
    }

    private string GetInAppPriceText()
    {
        // Реальная цена отображается, если ты укажешь её в данных карточки
        // или добавишь получение цены из каталога YG2.
        return Translate("0.99$", "0.99$", "0.99$");
    }

    private string Translate(string ru, string en, string tr)
    {
        return UTF.ReadLanguageDirectory(new LanguageDirectory(ru, en, tr));
    }
}
