using UnityEngine;
using YG;

[RequireComponent(typeof(Collider))]
public class DummyUnlockTrigger : MonoBehaviour
{
    [Header("Кукла")]
    [SerializeField]
    private TrainingDummy dummy;

    [Header("Покупка через YG")]
    [Tooltip("ID покупки из настроек YG.")]
    [SerializeField]
    private string paymentId = "";

    private bool _purchaseInProgress;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (dummy == null)
            return;

        if (!other.CompareTag("Player"))
            return;

        Player player = other.GetComponentInParent<Player>();

        if (player == null || !player.thisRealPlayer)
            return;

        if (dummy.IsUnlocked)
            return;

        // Кукла, открываемая за перерождения.
        if (!dummy.RequiresPurchase)
        {
            dummy.RefreshRebirthUnlock();

            if (!dummy.IsUnlocked)
            {
                NotificationManager.ShowNotification(
                    $"Для открытия куклы нужно перерождений: {dummy.RequiredRebirths}.",
                    NotificationType.Error
                );
            }

            return;
        }

        // Не запускаем несколько одинаковых покупок одновременно.
        if (_purchaseInProgress)
            return;

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            Debug.LogError(
                $"Для куклы {dummy.name} не указан Payment Id.",
                this
            );
            return;
        }

        if (Donate.manager == null)
        {
            Debug.LogError(
                "На сцене отсутствует объект с компонентом Donate.",
                this
            );
            return;
        }

        _purchaseInProgress = true;

        Donate.manager._BuyWithCallback(
            paymentId,
            onSuccess: () =>
            {
                if (dummy != null)
                    dummy.Unlock();

                _purchaseInProgress = false;
            }
        );
    }
}