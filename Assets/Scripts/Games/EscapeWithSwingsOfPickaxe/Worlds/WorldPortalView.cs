
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldPortalView : MonoBehaviour
{
    [Header("Мир")]
    [SerializeField] private WorldPortalData worldData;

    [Header("UI карточки")]
    [SerializeField] private TMP_Text worldNameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private Button actionButton;

    [SerializeField] private Image _buttonImage;

    [SerializeField] private Sprite _buy;
    [SerializeField] private Sprite _action;

    public WorldPortalData WorldData => worldData;

    private void OnEnable()
    {
        RefreshUI();
    }

    private void Start()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (worldData == null)
        {
            if (actionButton != null)
                actionButton.interactable = false;

            Debug.LogWarning(
                $"На объекте {name} не назначен WorldPortalData.",
                this
            );
            return;
        }

        if (worldNameText != null)
            worldNameText.text = UTF.ReadLanguageDirectory(worldData.worldName);

        bool unlocked =
            WorldPortalShop.Instance != null &&
            WorldPortalShop.Instance.IsWorldUnlocked(worldData);

        if (priceText != null)
        {
            if (unlocked)
            {
                priceText.text = worldData.freeByDefault
                    ? "Бесплатно"
                    : "Куплено";

                _buttonImage.sprite = _action;
            }
            else
            {
                priceText.text =
                    UTF.manager.TextToSprite("\U0001F600") + worldData.priceInTrophies;

                _buttonImage.sprite = _buy;
            }
        }

        if (buttonText != null)
            buttonText.text = unlocked ? "ПЕРЕХОД" : "КУПИТЬ";

        if (actionButton != null)
            actionButton.interactable =
                WorldPortalShop.Instance != null;
    }

    // Назначь этот метод в Button -> On Click().
    public void OnActionButtonClicked()
    {
        if (worldData == null || WorldPortalShop.Instance == null)
            return;

        bool unlocked =
            WorldPortalShop.Instance.IsWorldUnlocked(worldData);

        if (!unlocked)
        {
            bool purchased =
                WorldPortalShop.Instance.TryPurchaseWorld(worldData);

            RefreshUI();

            if (!purchased)
            {
                Debug.Log("Не удалось купить мир. Проверь баланс кубков.");
                return;
            }

            // После покупки кнопка меняется на «ПЕРЕХОД».
            // Переход выполняется следующим нажатием.
            return;
        }

        WorldPortalShop.Instance.EnterWorld(worldData);
    }
}