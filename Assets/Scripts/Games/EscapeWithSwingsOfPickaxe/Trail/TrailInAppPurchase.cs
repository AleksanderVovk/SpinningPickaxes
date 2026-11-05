using System;
using UnityEngine;
using YG;

public static class TrailInAppPurchase
{
    public static event Action OnTrailPurchased;
    public static void Buy(string inAppId, string trailId)
    {
        Donate.manager._BuyWithCallback(inAppId, () =>
        {
            if (YG2.saves == null || YG2.saves.playerData == null)
                return;

            var data = YG2.saves.playerData;

            if (!data.boughtTrails.Contains(trailId))
                data.boughtTrails.Add(trailId);

            data.equippedTrailId = trailId;
            YG2.SaveProgress();

            PlayerTrailController controller = PlayerTrailController.Instance;

            if (controller != null)
                controller.EquipTrail(trailId);

            YG2.SaveProgress();

            OnTrailPurchased?.Invoke();

            Debug.Log($"След '{trailId}' успешно куплен за иннап.");
        });
    }
}
