using UnityEngine;
using TMPro;
using HayPileSystem;

namespace ShopSystem
{
    public class BagUpgradeProduct : ShopProduct
    {
        [Header("Bag Upgrade")]
        [SerializeField] private HayPileInteractor interactor;

        [SerializeField, Min(1)] private int startingCapacity = 20;
        [SerializeField, Min(1)] private int capacityAddedPerLevel = 25;
        [SerializeField, Min(1)] private int bagMaxLevel = 5;

        [Header("Capacity UI")]
        [SerializeField] private GameObject capacityTextRoot;
        [SerializeField] private TMP_Text capacityText;

        public int CurrentCapacity
        {
            get
            {
                return startingCapacity + CurrentLevel * capacityAddedPerLevel;
            }
        }

        public int NextCapacity
        {
            get
            {
                int nextLevel = CurrentLevel + 1;

                if (nextLevel > bagMaxLevel)
                    return CurrentCapacity;

                return startingCapacity + nextLevel * capacityAddedPerLevel;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            OnProductOpened.AddListener(OnProductOpenedRefreshCapacity);
            OnProductClosed.AddListener(OnProductClosedHideCapacity);
            OnProductPurchased.AddListener(OnProductPurchasedRefreshCapacity);

            HideCapacityText();
        }

        protected override void OnDestroy()
        {
            OnProductOpened.RemoveListener(OnProductOpenedRefreshCapacity);
            OnProductClosed.RemoveListener(OnProductClosedHideCapacity);
            OnProductPurchased.RemoveListener(OnProductPurchasedRefreshCapacity);

            base.OnDestroy();
        }

        private void OnProductOpenedRefreshCapacity(ShopProduct product)
        {
            ShowCapacityText();
            RefreshCapacityText();
        }

        private void OnProductClosedHideCapacity(ShopProduct product)
        {
            HideCapacityText();
        }

        private void OnProductPurchasedRefreshCapacity(ShopProduct product)
        {
            RefreshCapacityText();
        }

        public override bool CanBuy()
        {
            if (interactor == null)
                return false;

            if (CurrentLevel >= bagMaxLevel)
                return false;

            return base.CanBuy();
        }

        public override bool IsMaxLevel
        {
            get
            {
                return CurrentLevel >= bagMaxLevel;
            }
        }

        public override string GetStatusText()
        {
            if (CurrentLevel >= bagMaxLevel)
            {
                return "Текущий уровень: " + CurrentLevel +
                       " (" + CurrentCapacity + " сена)\n" +
                       "МАКСИМУМ";
            }

            return "Текущий уровень: " + CurrentLevel +
                   " (" + CurrentCapacity + " сена)\n" +
                   "Следующий уровень: " + (CurrentLevel + 1) +
                   " (" + NextCapacity + " сена)";
        }

        private void ShowCapacityText()
        {
            if (capacityTextRoot != null)
                capacityTextRoot.SetActive(true);

            RefreshCapacityText();
        }

        private void HideCapacityText()
        {
            if (capacityTextRoot != null)
                capacityTextRoot.SetActive(false);
        }

        private void RefreshCapacityText()
        {
            if (capacityText == null)
                return;

            if (CurrentLevel >= bagMaxLevel)
            {
                capacityText.text = CurrentCapacity.ToString();
                return;
            }

            capacityText.text = CurrentCapacity + " -> " + NextCapacity;
        }

        protected override void OnPurchased()
        {
            if (interactor != null)
                interactor.SetCapacity(CurrentCapacity);

            RefreshCapacityText();
        }
    }
}