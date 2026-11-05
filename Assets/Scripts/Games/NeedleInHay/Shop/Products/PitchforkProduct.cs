using UnityEngine;
using HayPileSystem;

namespace ShopSystem
{
    public class PitchforkProduct : ShopProduct
    {
        [Header("Pitchfork")]
        [SerializeField] private HayPileInteractor interactor;

        public override bool CanBuy()
        {
            if (interactor == null)
                return false;

            if (interactor.IsPitchforkUnlocked())
                return false;

            return base.CanBuy();
        }

        public override bool IsPurchased()
        {
            if (interactor != null && interactor.IsPitchforkUnlocked())
                return true;

            return base.IsPurchased();
        }

        public override string GetStatusText()
        {
            if (interactor != null && interactor.IsPitchforkUnlocked())
                return "Куплено";

            return "Единичная покупка";
        }

        public override string GetPriceText()
        {
            if (interactor != null && interactor.IsPitchforkUnlocked())
                return "КУПЛЕНО";

            return base.GetPriceText();
        }

        protected override void OnPurchased()
        {
            if (interactor != null)
                interactor.UnlockPitchfork();
        }
    }
}