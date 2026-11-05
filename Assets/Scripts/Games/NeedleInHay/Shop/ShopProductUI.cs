using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

namespace ShopSystem
{
    public class ShopProductUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject productPanel;

        [Header("Objects hidden while product is open")]
        [SerializeField] private GameObject[] objectsToHideOnOpen;

        [Header("Animated blocks")]
        [SerializeField] private RectTransform header;
        [SerializeField] private RectTransform footer;

        [Header("Header animation")]
        [SerializeField] private float headerStartOffsetY = 200f;
        [SerializeField, Min(0.01f)] private float headerDuration = 0.35f;

        [Header("Footer animation")]
        [SerializeField] private float footerStartOffsetY = -200f;
        [SerializeField, Min(0.01f)] private float footerDuration = 0.35f;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text priceText;

        [Header("Buy")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;

        [Header("Donate Buy")]
        [SerializeField] private Button donateBuyButton;
        [SerializeField] private TMP_Text donateBuyButtonText;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera mainVirtualCamera;
        [SerializeField] private CinemachineBrain cameraBrain;

        [Header("Camera transition")]
        [SerializeField, Min(0f)] private float cameraBlendTime = 0.25f;

        private ShopProduct currentProduct;
        private ShopProduct previousProduct;

        private Vector2 headerRestPosition;
        private Vector2 footerRestPosition;

        private Coroutine headerCoroutine;
        private Coroutine footerCoroutine;

        public bool IsOpen => currentProduct != null;

        private void Awake()
        {
            SaveRestPositions();

            if (productPanel != null)
                productPanel.SetActive(false);

            if (buyButton != null)
                buyButton.onClick.AddListener(BuyCurrentProduct);

            if (donateBuyButton != null)
                donateBuyButton.onClick.AddListener(BuyCurrentProductWithDonate);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseProduct);

            SetupCameraBrain();
        }

        private void SaveRestPositions()
        {
            if (header != null)
                headerRestPosition = header.anchoredPosition;

            if (footer != null)
                footerRestPosition = footer.anchoredPosition;
        }

        private void SetupCameraBrain()
        {
            if (cameraBrain == null)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                    cameraBrain = mainCamera.GetComponent<CinemachineBrain>();
            }

            if (cameraBrain != null)
            {
                CinemachineBlendDefinition blend = cameraBrain.DefaultBlend;
                blend.Time = cameraBlendTime;
                cameraBrain.DefaultBlend = blend;
            }
        }

        public void OpenProduct(ShopProduct product)
        {
            if (product == null)
                return;

            if (currentProduct != null && currentProduct != product)
            {
                CloseCurrentProductCamera();
                currentProduct.CloseProduct();
            }

            previousProduct = currentProduct;
            currentProduct = product;

            HideObjects();

            if (productPanel != null)
                productPanel.SetActive(true);

            RefreshProductUI();
            SwitchCamera(product);
            StartOpenAnimation();

            product.OpenProduct();
        }

        public void CloseProduct()
        {
            if (currentProduct == null)
                return;

            ShopProduct product = currentProduct;

            CloseCurrentProductCamera();

            currentProduct = null;
            previousProduct = null;

            RestoreMainCamera();
            StartCloseAnimation();

            ShowObjects();

            if (productPanel != null)
                StartCoroutine(DisablePanelAfterDelay(productPanel, 0.4f));

            product.CloseProduct();
        }

        private void HideObjects()
        {
            if (objectsToHideOnOpen == null)
                return;

            for (int i = 0; i < objectsToHideOnOpen.Length; i++)
            {
                if (objectsToHideOnOpen[i] != null)
                    objectsToHideOnOpen[i].SetActive(false);
            }
        }

        private void ShowObjects()
        {
            if (objectsToHideOnOpen == null)
                return;

            for (int i = 0; i < objectsToHideOnOpen.Length; i++)
            {
                if (objectsToHideOnOpen[i] != null)
                    objectsToHideOnOpen[i].SetActive(true);
            }
        }

        public void RefreshCurrentProductUI()
        {
            RefreshProductUI();
        }

        private void RefreshProductUI()
        {
            if (currentProduct == null)
                return;

            if (titleText != null)
                titleText.text = currentProduct.ProductTitle;

            if (descriptionText != null)
                descriptionText.text = currentProduct.ProductDescription;

            if (statusText != null)
                statusText.text = currentProduct.GetStatusText();

            if (priceText != null)
                priceText.text = currentProduct.GetPriceText();

            RefreshBuyButton();
            RefreshDonateBuyButton();
        }

        private void RefreshBuyButton()
        {
            if (buyButton == null)
                return;

            bool canBuy = currentProduct.CanBuy();

            buyButton.interactable = canBuy;

            if (buyButtonText == null)
                return;

            if (currentProduct.Type == ProductType.Upgrade && currentProduct.IsMaxLevel)
            {
                buyButtonText.text = "люйяхлсл";
                return;
            }

            if (currentProduct.Type == ProductType.SinglePurchase && currentProduct.IsPurchased())
            {
                buyButtonText.text = "йсокемн";
                return;
            }

            buyButtonText.text = "йСОХРЭ (" + currentProduct.GetPriceText() + ")";
        }

        private void RefreshDonateBuyButton()
        {
            if (donateBuyButton == null)
                return;

            bool canDonateBuy = currentProduct.CanBuyWithDonate();

            donateBuyButton.interactable = canDonateBuy;

            if (donateBuyButtonText == null)
                return;

            if (!canDonateBuy)
            {
                if (currentProduct.Type == ProductType.Upgrade && currentProduct.IsMaxLevel)
                    donateBuyButtonText.text = "люйяхлсл";
                else if (currentProduct.Type == ProductType.SinglePurchase && currentProduct.IsPurchased())
                    donateBuyButtonText.text = "йсокемн";
                else
                    donateBuyButtonText.text = "меднярсомн";

                return;
            }

            donateBuyButtonText.text = currentProduct.GetDonatePriceText();
        }

        private void BuyCurrentProduct()
        {
            if (currentProduct == null)
                return;

            if (currentProduct.TryPurchase())
                RefreshProductUI();
        }

        private void BuyCurrentProductWithDonate()
        {
            if (currentProduct == null)
                return;

            if (!currentProduct.CanBuyWithDonate())
                return;

            currentProduct.TryPurchaseWithDonate();
        }

        private void SwitchCamera(ShopProduct product)
        {
            if (previousProduct != null && previousProduct != product)
                DisableProductCamera(previousProduct);

            DisableAllOtherProductCameras(product);

            if (mainVirtualCamera != null)
                mainVirtualCamera.enabled = false;

            if (product.ProductVirtualCamera != null)
                product.ProductVirtualCamera.enabled = true;

            if (product.ProductCameraObject != null)
                product.ProductCameraObject.SetActive(true);
        }

        private void CloseCurrentProductCamera()
        {
            if (currentProduct == null)
                return;

            DisableProductCamera(currentProduct);
        }

        private void DisableProductCamera(ShopProduct product)
        {
            if (product == null)
                return;

            if (product.ProductVirtualCamera != null)
                product.ProductVirtualCamera.enabled = false;

            if (product.ProductCameraObject != null)
                product.ProductCameraObject.SetActive(false);
        }

        private void DisableAllOtherProductCameras(ShopProduct selectedProduct)
        {
            ShopProduct[] products = FindObjectsByType<ShopProduct>(FindObjectsSortMode.None);

            for (int i = 0; i < products.Length; i++)
            {
                if (products[i] == selectedProduct)
                    continue;

                DisableProductCamera(products[i]);
            }
        }

        private void RestoreMainCamera()
        {
            if (currentProduct != null)
                return;

            if (mainVirtualCamera != null)
                mainVirtualCamera.enabled = true;
        }

        private void StartOpenAnimation()
        {
            if (header != null)
            {
                Vector2 startPosition = headerRestPosition;
                startPosition.y += headerStartOffsetY;

                if (headerCoroutine != null)
                    StopCoroutine(headerCoroutine);

                headerCoroutine = StartCoroutine(
                    AnimateRectY(header, startPosition, headerRestPosition, headerDuration)
                );
            }

            if (footer != null)
            {
                Vector2 startPosition = footerRestPosition;
                startPosition.y += footerStartOffsetY;

                if (footerCoroutine != null)
                    StopCoroutine(footerCoroutine);

                footerCoroutine = StartCoroutine(
                    AnimateRectY(footer, startPosition, footerRestPosition, footerDuration)
                );
            }
        }

        private void StartCloseAnimation()
        {
            if (header != null)
            {
                Vector2 targetPosition = headerRestPosition;
                targetPosition.y += headerStartOffsetY;

                if (headerCoroutine != null)
                    StopCoroutine(headerCoroutine);

                headerCoroutine = StartCoroutine(
                    AnimateRectY(header, header.anchoredPosition, targetPosition, headerDuration)
                );
            }

            if (footer != null)
            {
                Vector2 targetPosition = footerRestPosition;
                targetPosition.y += footerStartOffsetY;

                if (footerCoroutine != null)
                    StopCoroutine(footerCoroutine);

                footerCoroutine = StartCoroutine(
                    AnimateRectY(footer, footer.anchoredPosition, targetPosition, footerDuration)
                );
            }
        }

        private IEnumerator AnimateRectY(RectTransform rect, Vector2 startPosition, Vector2 targetPosition, float duration)
        {
            rect.anchoredPosition = startPosition;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                rect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smooth);

                yield return null;
            }

            rect.anchoredPosition = targetPosition;
        }

        private IEnumerator DisablePanelAfterDelay(GameObject panel, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (panel != null)
                panel.SetActive(false);
        }
    }
}