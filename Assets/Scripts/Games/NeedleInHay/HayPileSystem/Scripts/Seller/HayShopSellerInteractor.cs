using UnityEngine;
using HayPileSystem;

public class HayShopSellerInteractor : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private LayerMask sellerMask = ~0;

    [Header("UI")]
    [SerializeField] private GameObject sellHayButton;
    [SerializeField] private GameObject pickupHayButton;

    private HayShopSellerTarget currentSellerTarget;
    private Component currentOutline;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (sellHayButton != null)
            sellHayButton.SetActive(false);

        UpdatePickupButton(false);
    }

    private void Update()
    {
        DetectSeller();
    }

    private void DetectSeller()
    {
        if (playerCamera == null)
        {
            if (Camera.main != null)
                playerCamera = Camera.main;
            else
                return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, sellerMask,
                QueryTriggerInteraction.Collide))
        {
            HayShopSellerTarget sellerTarget = hit.collider.GetComponentInParent<HayShopSellerTarget>();

            if (sellerTarget != null)
            {
                SetCurrentSeller(sellerTarget);
                return;
            }
        }

        SetCurrentSeller(null);
    }

    private void SetCurrentSeller(HayShopSellerTarget sellerTarget)
    {
        if (currentSellerTarget == sellerTarget)
            return;

        DisableCurrentOutline();

        currentSellerTarget = sellerTarget;
        currentOutline = null;

        if (currentSellerTarget != null)
        {
            currentOutline = currentSellerTarget.OutlineComponent;
            EnableCurrentOutline();
        }

        bool buttonActive = sellerTarget != null;

        if (sellHayButton != null)
            sellHayButton.SetActive(buttonActive);

        UpdatePickupButton(buttonActive);
    }

    private void UpdatePickupButton(bool sellButtonActive)
    {
        if (pickupHayButton == null)
            return;

        pickupHayButton.SetActive(!sellButtonActive);
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

        System.Reflection.FieldInfo field = currentOutline.GetType().GetField("enabled");

        if (field != null)
        {
            field.SetValue(currentOutline, value);
            return;
        }

        System.Reflection.PropertyInfo property = currentOutline.GetType().GetProperty("enabled");

        if (property != null && property.CanWrite)
            property.SetValue(currentOutline, value);
    }

    /// <summary>
    /// Вызывается с UI-кнопки «Продать сено».
    /// </summary>
    public void SellCurrentHay()
    {
        if (currentSellerTarget == null || currentSellerTarget.Seller == null)
            return;

        currentSellerTarget.Seller.SellHayFromButton();
    }
}