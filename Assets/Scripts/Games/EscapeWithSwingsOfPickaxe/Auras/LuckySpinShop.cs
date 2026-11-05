using UnityEngine;
using YG;

public class LuckySpinShop : MonoBehaviour
{
    [Header("ID иннапов из панели YG")]
    [SerializeField] private string oneSpinId = "lucky_spin_1";
    [SerializeField] private string fiveSpinsId = "lucky_spin_5";
    [SerializeField] private string tenSpinsId = "lucky_spin_10";
    [SerializeField] private string twentyFiveSpinsId = "lucky_spin_25";
    [SerializeField] private string fiftySpinsId = "lucky_spin_50";

    [Header("Система вращений")]
    [SerializeField] private AuraSpinSystem auraSpinSystem;

    public void Buy1Spin()
    {
        BuySpins(oneSpinId, 1);
    }

    public void Buy5Spins()
    {
        BuySpins(fiveSpinsId, 5);
    }

    public void Buy10Spins()
    {
        BuySpins(tenSpinsId, 10);
    }

    public void Buy25Spins()
    {
        BuySpins(twentyFiveSpinsId, 25);
    }

    public void Buy50Spins()
    {
        BuySpins(fiftySpinsId, 50);
    }

    private void BuySpins(string inAppId, int amount)
    {
        if (string.IsNullOrWhiteSpace(inAppId))
        {
            Debug.LogError(
                "LuckySpinShop: не указан ID покупки."
            );
            return;
        }

        if (Donate.manager == null)
        {
            Debug.LogError(
                "LuckySpinShop: объект Donate не найден в сцене."
            );
            return;
        }

        Donate.manager._BuyWithCallback(inAppId, () =>
        {
            GrantSpins(amount);
        });
    }

    private void GrantSpins(int amount)
    {
        if (amount <= 0)
            return;

        if (YG2.saves == null ||
            YG2.saves.playerData == null)
        {
            Debug.LogError(
                "LuckySpinShop: данные игрока недоступны. " +
                "Не удалось начислить счастливые вращения."
            );
            return;
        }

        PlayerData data = YG2.saves.playerData;
        data.EnsureValidData();

        // Защита от переполнения int.
        data.luckySpins = (int)Mathf.Min(
            (long)data.luckySpins + amount,
            int.MaxValue
        );

        YG2.SaveProgress();

        if (auraSpinSystem != null)
            auraSpinSystem.RefreshLuckySpinsUI();

        Debug.Log(
            "Куплено счастливых вращений: " + amount +
            ". Всего: " + data.luckySpins
        );
    }
}
