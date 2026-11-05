using TMPro;
using UnityEngine;

public class GetValueTrigger : MonoBehaviour
{
    public Price price;
    [SerializeField] private TMP_Text _display;

    [Space(5f)]

    [Header("Settings")]
    [SerializeField] private bool _teleportInSpawn;
    [SerializeField] private ParticleType _particleType;
    [SerializeField] private AudioClip _clip;

    [Space(5f)]

    [Header("InApp")]
    [SerializeField] private bool _thisInApp;
    [SerializeField] private string _inAppId; // ID иннапа в консоли яндекс.»гр

    private void Start()
    {
        if (_display != null && price != null)
        {
            switch (price.valueType)
            {
                case ValueType.Wins:
                    _display.text =
                        "+" + price.amount + " " +
                        UTF.ReadLanguageDirectory(LangLIst.instance.win) + " " +
                        UTF.manager.TextToSprite("\U0001F600");
                    break;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var player = other.GetComponent<Player>();
        if (player == null || !player.thisRealPlayer)
            return;

        // ≈сли это иннап Ц вызываем Donate дл€ покупки, передава€ себ€
        if (_thisInApp)
        {
            if (string.IsNullOrEmpty(_inAppId))
            {
                Debug.LogError($"[GetValueTrigger] Ќе указан _inAppId на объекте {name}", this);
                return;
            }

            Donate.Buy(_inAppId, this);
            return;
        }

        // ќбычна€ логика (не иннап) Ц выдаЄм награду сразу при входе в триггер
        GiveReward(player);
    }

    #region Reward logic

    public void GiveReward(Player player)
    {
        if (price == null)
            return;

        int rewardAmount = price.amount;

        // x2 примен€етс€ ко всем наградам побед:
        // и к обычным триггерам, и к наградам после иннапа.
        if (price.valueType == ValueType.Wins && Donate.HasDoubleWins)
        {
            rewardAmount = Mathf.RoundToInt(rewardAmount * 2f);
        }

        switch (price.valueType)
        {
            case ValueType.Wins:
                NotificationManager.ShowNotification(
                    "+" + rewardAmount + " " +
                    UTF.ReadLanguageDirectory(LangLIst.instance.win) + "! " +
                    UTF.manager.TextToSprite("\U0001F600"),
                    NotificationType.Win
                );
                break;
        }

        ValueSystem.AddBalance(rewardAmount, price.valueType);

        if (_teleportInSpawn)
            player.TeleportToSpawn(_particleType);

        if (_clip != null && GlobalSound.instance != null)
            GlobalSound.instance.PlaySound_Custom(_clip, false);
    }

    #endregion
}