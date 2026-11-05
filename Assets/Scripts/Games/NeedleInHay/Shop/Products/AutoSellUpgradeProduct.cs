using UnityEngine;
using HayPileSystem;

namespace ShopSystem
{
    public class AutoSellUpgradeProduct : ShopProduct
    {
        [Header("Auto Sell")]
        [SerializeField] private HayPileInteractor interactor;
        [SerializeField] private HayShopSeller seller;

        [Header("Settings")]
        [Tooltip("Задержка перед автоматической продажей после заполнения руки.")]
        [SerializeField, Min(0f)] private float autoSellDelay = 0.1f;

        [Tooltip("Пока рука заполнена, автоматически продавать сено.")]
        [SerializeField] private bool autoSellEnabled;

        private float autoSellTime;

        public bool AutoSellEnabled => autoSellEnabled;

        protected override void Awake()
        {
            base.Awake();

            autoSellEnabled = false;

            OnProductPurchased.AddListener(OnProductPurchasedEnableAutoSell);
        }

        protected override void OnDestroy()
        {
            OnProductPurchased.RemoveListener(OnProductPurchasedEnableAutoSell);

            base.OnDestroy();
        }

        private void OnProductPurchasedEnableAutoSell(ShopProduct product)
        {
            autoSellEnabled = true;
        }

        private void Update()
        {
            if (!autoSellEnabled)
                return;

            if (interactor == null || seller == null)
                return;

            if (seller.IsSelling)
                return;

            if (interactor.HayInHands < interactor.Capacity)
                return;

            if (autoSellDelay > 0f)
            {
                if (autoSellTime <= 0f)
                    autoSellTime = Time.time + autoSellDelay;

                if (Time.time < autoSellTime)
                    return;
            }

            autoSellTime = 0f;

            seller.SellHayFromButton();
        }

        public override string GetStatusText()
        {
            if (IsPurchased())
                return "Автопродажа включена";

            return "Автоматически продаёт сено, когда рука заполнена";
        }
    }
}