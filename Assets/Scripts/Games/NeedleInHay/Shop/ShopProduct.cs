using System;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using HayPileSystem;
using Unity.Cinemachine;

namespace ShopSystem
{
    public enum ProductType
    {
        SinglePurchase,
        Upgrade
    }

    public abstract class ShopProduct : MonoBehaviour
    {
        [Header("Product Info")]
        [SerializeField] private string productId;
        [SerializeField] private string productTitle = "Товар";

        [TextArea(2, 6)]
        [SerializeField] private string productDescription;

        [Header("Price")]
        [SerializeField] private float price = 10f;

        [Header("Donate")]
        [SerializeField] private string donateProductId;
        [SerializeField] private float donatePrice = 1.99f;

        [Header("Product kind")]
        [SerializeField] private ProductType productType = ProductType.SinglePurchase;

        [Header("Upgrade settings")]
        [SerializeField, Min(1)] private int maxLevel = 10;
        [SerializeField, Min(1f)] private float priceMultiplier = 1.5f;

        [Header("Money")]
        [SerializeField] private HayShopSeller moneySource;

        [Header("World product label")]
        [SerializeField] private GameObject worldLabelRoot;
        [SerializeField] private TMP_Text worldLabelTitleText;
        [SerializeField] private TMP_Text worldLabelPriceText;

        [Header("Camera")]
        [SerializeField] private GameObject productCameraObject;
        [SerializeField] private CinemachineCamera productVirtualCamera;

        [Header("Outline")]
        [SerializeField] private Component outline;

        [Header("Events")]
        public UnityEvent<ShopProduct> OnProductOpened;
        public UnityEvent<ShopProduct> OnProductClosed;
        public UnityEvent<ShopProduct> OnProductPurchased;

        public string ProductId => productId;
        public string ProductTitle => productTitle;
        public string ProductDescription => productDescription;
        public ProductType Type => productType;
        public int MaxLevel => maxLevel;
        public int CurrentLevel { get; protected set; }

        public string DonateProductId => donateProductId;
        public float DonatePrice => donatePrice;

        public virtual bool IsMaxLevel => CurrentLevel >= MaxLevel;

        public Component OutlineComponent => outline;

        public float CurrentPrice
        {
            get
            {
                if (productType == ProductType.SinglePurchase)
                    return price;

                return price * Mathf.Pow(priceMultiplier, CurrentLevel);
            }
        }

        public GameObject ProductCameraObject => productCameraObject;
        public CinemachineCamera ProductVirtualCamera => productVirtualCamera;

        protected virtual void Awake()
        {
            OnProductOpened.AddListener(OnProductOpenedHideWorldLabel);
            OnProductClosed.AddListener(OnProductClosedShowWorldLabel);

            RefreshWorldLabel();
            ShowWorldLabel();
        }

        protected virtual void OnDestroy()
        {
            OnProductOpened.RemoveListener(OnProductOpenedHideWorldLabel);
            OnProductClosed.RemoveListener(OnProductClosedShowWorldLabel);
        }

        private void OnProductOpenedHideWorldLabel(ShopProduct product)
        {
            HideWorldLabel();
        }

        private void OnProductClosedShowWorldLabel(ShopProduct product)
        {
            ShowWorldLabel();
        }

        public virtual bool CanBuy()
        {
            if (moneySource == null)
                return false;

            if (productType == ProductType.SinglePurchase && IsPurchased())
                return false;

            if (productType == ProductType.Upgrade && IsMaxLevel)
                return false;

            return moneySource.Balance >= CurrentPrice;
        }

        public virtual bool CanBuyWithDonate()
        {
            if (string.IsNullOrWhiteSpace(donateProductId))
                return false;

            if (productType == ProductType.SinglePurchase && IsPurchased())
                return false;

            if (productType == ProductType.Upgrade && IsMaxLevel)
                return false;

            return true;
        }

        public virtual bool IsPurchased()
        {
            return CurrentLevel > 0;
        }

        public virtual string GetStatusText()
        {
            if (productType == ProductType.Upgrade)
                return "Уровень: " + CurrentLevel + "/" + MaxLevel;

            if (IsPurchased())
                return "Куплено";

            return "Единичная покупка";
        }

        public virtual string GetPriceText()
        {
            if (productType == ProductType.Upgrade && IsMaxLevel)
                return "МАКСИМУМ";

            return CurrentPrice.ToString("0.00") + "$";
        }

        public virtual string GetDonatePriceText()
        {
            return DonatePrice.ToString("0.00") + "$";
        }

        public virtual bool TryPurchase()
        {
            if (!CanBuy())
            {
                GlobalSound.instance.PlaySound_BuyFaield(false);
                return false;
            }

            if (!TrySpendMoney(CurrentPrice))
                return false;

            CurrentLevel++;

            GlobalSound.instance.PlaySound_BuyTheme1(false);

            OnPurchased();
            OnProductPurchased?.Invoke(this);

            RefreshWorldLabel();

            return true;
        }

        public virtual bool TryPurchaseWithDonate()
        {
            if (!CanBuyWithDonate())
                return false;

            GlobalSound.instance.PlaySound_BuyTheme1(false);

            if (Donate.manager == null)
            {
                Debug.LogError("Donate.manager не найден в сцене.", this);
                return false;
            }

            Donate.manager.BuyShopProduct(this);

            return true;
        }

        public void CompleteDonatePurchase()
        {
            if (productType == ProductType.SinglePurchase && IsPurchased())
                return;

            if (productType == ProductType.Upgrade && IsMaxLevel)
                return;

            CurrentLevel++;

            OnPurchased();
            OnProductPurchased?.Invoke(this);

            RefreshWorldLabel();
        }

        protected virtual bool TrySpendMoney(float amount)
        {
            if (moneySource == null)
                return false;

            return moneySource.TrySpendBalance(amount);
        }

        protected virtual void OnPurchased()
        {
        }

        public virtual void OpenProduct()
        {
            OnProductOpened?.Invoke(this);
        }

        public virtual void CloseProduct()
        {
            OnProductClosed?.Invoke(this);
        }

        private void RefreshWorldLabel()
        {
            if (worldLabelTitleText != null)
                worldLabelTitleText.text = ProductTitle;

            if (worldLabelPriceText != null)
                worldLabelPriceText.text = GetPriceText();
        }

        private void ShowWorldLabel()
        {
            if (worldLabelRoot != null)
                worldLabelRoot.SetActive(true);

            RefreshWorldLabel();
        }

        private void HideWorldLabel()
        {
            if (worldLabelRoot != null)
                worldLabelRoot.SetActive(false);
        }
    }
}