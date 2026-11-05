using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

public class Donate : MonoBehaviour
{
    public static Donate manager;

    [Header("ID иннап-покупок")]
    [SerializeField] private string doubleWinsInAppId = "double_wins";
    [SerializeField] private string doubleDamageInAppId = "double_damage";
    [SerializeField] private string rebirthInAppId = "rebirth";

    [Header("x2 победы — объекты UI")]
    [SerializeField] private GameObject doubleWinsSalePanel;
    [SerializeField] private GameObject gameObject1;

    [Header("x2 урона — объекты UI")]
    [SerializeField] private GameObject doubleDamageSalePanel;
    [SerializeField] private GameObject gameObject2;

    private static readonly Dictionary<string, Queue<GetValueTrigger>> _pendingTriggers = new();
    private static readonly Dictionary<string, Queue<Action>> _pendingActions = new();

    public static bool HasDoubleWins =>
        YG2.saves != null &&
        YG2.saves.playerData != null &&
        YG2.saves.playerData.hasDoubleWins;

    public static bool HasDoubleDamage =>
        YG2.saves != null &&
        YG2.saves.playerData != null &&
        YG2.saves.playerData.hasDoubleDamage;

    private void Awake()
    {
        if (manager != null && manager != this)
        {
            Debug.LogError("В сцене найдено несколько Donate.", this);
            Destroy(gameObject);
            return;
        }

        manager = this;

        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
    }

    private void Start()
    {
        RefreshPurchaseUI();
    }

    private void OnDestroy()
    {
        if (manager == this)
            manager = null;
    }

    public void BuyRebirth()
    {
        if (string.IsNullOrWhiteSpace(rebirthInAppId))
        {
            Debug.LogError("Не указан ID иннапа для перерождения.", this);
            return;
        }

        _BuyWithCallback(rebirthInAppId, () =>
        {
            if (RebirthSystem.Instance == null)
            {
                Debug.LogError("RebirthSystem не найден в сцене.");
                return;
            }

            RebirthSystem.Instance.PerformPaidRebirth();
        });
    }

    public void BuyDoubleWins()
    {
        if (HasDoubleWins)
        {
            RefreshPurchaseUI();
            return;
        }

        _BuyWithCallback(doubleWinsInAppId, () =>
        {
            if (YG2.saves?.playerData == null)
                return;

            YG2.saves.playerData.hasDoubleWins = true;
            YG2.SaveProgress();

            RefreshPurchaseUI();

            Debug.Log("Покупка x2 побед активирована.");
        });
    }

    public void BuyDoubleDamage()
    {
        if (HasDoubleDamage)
        {
            RefreshPurchaseUI();
            return;
        }

        _BuyWithCallback(doubleDamageInAppId, () =>
        {
            if (YG2.saves?.playerData == null)
                return;

            YG2.saves.playerData.hasDoubleDamage = true;
            YG2.SaveProgress();

            RefreshPurchaseUI();

            if (PlayerDamageSystem.Instance != null)
                PlayerDamageSystem.Instance.RefreshUI();

            Debug.Log("Покупка x2 урона активирована.");
        });
    }

    public void BuyShopProduct(ShopSystem.ShopProduct product)
    {
        if (product == null)
            return;

        if (!product.CanBuyWithDonate())
            return;

        string donateId = product.DonateProductId;

        if (string.IsNullOrWhiteSpace(donateId))
        {
            Debug.LogError("У товара не указан Donate Product ID: " + product.ProductTitle, product);
            return;
        }

        _BuyWithCallback(donateId, () =>
        {
            product.CompleteDonatePurchase();
            RefreshShopProductUI();
        });
    }

    private void RefreshShopProductUI()
    {
        ShopSystem.ShopProductUI productUI =
            FindFirstObjectByType<ShopSystem.ShopProductUI>();

        if (productUI != null && productUI.IsOpen)
            productUI.RefreshCurrentProductUI();
    }

    public void RefreshPurchaseUI()
    {
        if (doubleWinsSalePanel != null)
            doubleWinsSalePanel.SetActive(!HasDoubleWins);

        if (gameObject1 != null)
            gameObject1.SetActive(HasDoubleWins);

        if (doubleDamageSalePanel != null)
            doubleDamageSalePanel.SetActive(!HasDoubleDamage);

        if (gameObject2 != null)
            gameObject2.SetActive(HasDoubleDamage);
    }

    public static void Buy(string id, GetValueTrigger trigger)
    {
        if (manager == null)
        {
            Debug.LogError("Donate manager не найден в сцене.");
            return;
        }

        manager._Buy(id, trigger);
    }

    public void _Buy(string id, GetValueTrigger trigger)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("Пустой ID иннапа.");
            return;
        }

        if (!_pendingTriggers.ContainsKey(id))
            _pendingTriggers[id] = new Queue<GetValueTrigger>();

        _pendingTriggers[id].Enqueue(trigger);

        ProcessInternalPurchase(id);
    }

    public void _BuyWithCallback(string id, Action onSuccess)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("Пустой ID покупки.");
            return;
        }

        if (!_pendingActions.ContainsKey(id))
            _pendingActions[id] = new Queue<Action>();

        _pendingActions[id].Enqueue(onSuccess);

        ProcessInternalPurchase(id);
    }

    private void ProcessInternalPurchase(string id)
    {
        Debug.Log($"[Donate] Внутренняя покупка активирована: {id}");

        SuccessPurchased(id);
    }

    private void SuccessPurchased(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (_pendingTriggers.TryGetValue(id, out var triggerQueue) && triggerQueue.Count > 0)
        {
            var trigger = triggerQueue.Dequeue();
            var player = FindLocalPlayer();

            if (player != null && player.thisRealPlayer && trigger != null)
                trigger.GiveReward(player);
        }

        if (_pendingActions.TryGetValue(id, out var actionQueue) && actionQueue.Count > 0)
        {
            Action action = actionQueue.Dequeue();
            action?.Invoke();
        }

        RefreshPurchaseUI();
        YG2.SaveProgress();
    }

    private void FailedPurchased(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        if (_pendingTriggers.TryGetValue(id, out var queue) && queue.Count > 0)
            queue.Dequeue();

        if (_pendingActions.TryGetValue(id, out var actionQueue) && actionQueue.Count > 0)
            actionQueue.Dequeue();

        LanguageDirectory messageNotification = new LanguageDirectory(
            "Покупка не была завершена",
            "The purchase was not completed",
            "Satin alma islemi yapilmadi"
        );

        NotificationManager.ShowNotification(
            UTF.ReadLanguageDirectory(messageNotification),
            NotificationType.Error
        );
    }

    private Player FindLocalPlayer()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go != null ? go.GetComponent<Player>() : null;
    }
}