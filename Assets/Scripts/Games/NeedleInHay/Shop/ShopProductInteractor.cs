using UnityEngine;

namespace ShopSystem
{
    public class ShopProductInteractor : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactionDistance = 4f;
        [SerializeField] private LayerMask productMask = ~0;

        [Header("UI")]
        [SerializeField] private GameObject openProductButton;
        [SerializeField] private GameObject objectToHideWhenButtonActive;
        [SerializeField] private ShopProductUI productUI;

        private ShopProduct currentProduct;
        private Component currentOutline;

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (openProductButton != null)
                openProductButton.SetActive(false);

            UpdateHiddenObject(false);
        }

        private void Update()
        {
            DetectProduct();
        }

        private void DetectProduct()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, productMask,
                    QueryTriggerInteraction.Ignore))
            {
                ShopProduct product = hit.collider.GetComponentInParent<ShopProduct>();

                if (product != null)
                {
                    SetCurrentProduct(product);
                    return;
                }
            }

            SetCurrentProduct(null);
        }

        private void SetCurrentProduct(ShopProduct product)
        {
            if (currentProduct == product)
                return;

            DisableCurrentOutline();

            currentProduct = product;
            currentOutline = null;

            if (currentProduct != null)
            {
                currentOutline = currentProduct.OutlineComponent;
                EnableCurrentOutline();
            }

            bool buttonActive = product != null;

            if (openProductButton != null)
                openProductButton.SetActive(buttonActive);

            UpdateHiddenObject(buttonActive);
        }

        private void UpdateHiddenObject(bool buttonActive)
        {
            if (objectToHideWhenButtonActive == null)
                return;

            objectToHideWhenButtonActive.SetActive(!buttonActive);
        }

        private void EnableCurrentOutline()
        {
            SetOutlineEnabled(true);
        }

        private void DisableCurrentOutline()
        {
            SetOutlineEnabled(false);
        }

        private void SetOutlineEnabled(bool value)
        {
            if (currentOutline == null)
                return;

            Behaviour behaviour = currentOutline as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = value;
                return;
            }

            System.Reflection.FieldInfo field =
                currentOutline.GetType().GetField("enabled");

            if (field != null)
            {
                field.SetValue(currentOutline, value);
                return;
            }

            System.Reflection.PropertyInfo property =
                currentOutline.GetType().GetProperty("enabled");

            if (property != null && property.CanWrite)
            {
                property.SetValue(currentOutline, value);
            }
        }

        public void OpenCurrentProduct()
        {
            if (currentProduct == null)
                return;

            if (productUI != null)
                productUI.OpenProduct(currentProduct);
        }
    }
}