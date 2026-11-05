using UnityEngine;

namespace HayPileSystem
{
    public class HayShopSellerTarget : MonoBehaviour
    {
        [Header("Seller")]
        [SerializeField] private HayShopSeller seller;

        [Header("Outline")]
        [SerializeField] private Component outline;

        public HayShopSeller Seller => seller;
        public Component OutlineComponent => outline;

        private void Awake()
        {
            if (seller == null)
                seller = GetComponentInParent<HayShopSeller>();

            SetOutlineEnabled(false);
        }

        public void SetOutlineEnabled(bool value)
        {
            if (outline == null)
                return;

            Behaviour behaviour = outline as Behaviour;

            if (behaviour != null)
            {
                behaviour.enabled = value;
                return;
            }

            System.Reflection.FieldInfo field = outline.GetType().GetField("enabled");

            if (field != null)
            {
                field.SetValue(outline, value);
                return;
            }

            System.Reflection.PropertyInfo property = outline.GetType().GetProperty("enabled");

            if (property != null && property.CanWrite)
                property.SetValue(outline, value);
        }
    }
}