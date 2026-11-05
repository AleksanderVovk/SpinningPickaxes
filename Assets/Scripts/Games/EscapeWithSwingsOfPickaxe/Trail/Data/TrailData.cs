using UnityEngine;

public enum TrailPurchaseType
{
    Wins,
    InApp
}

[CreateAssetMenu(fileName = "NewTrail", menuName = "Shop/Trail")]
public class TrailData : ScriptableObject
{
    [Header("ID и название")]
    public string id = "trail_orange";
    public LanguageDirectory displayName;

    [Header("Визуальный эффект")]
    public Material material;
    public Gradient gradient;

    public Color color1;
    public Color color2;
    public Color color3;
    public Color color4;

    [Header("Множитель урона")]
    [Min(0.01f)] public float damageMultiplier = 1f;

    [Header("Покупка")]
    public TrailPurchaseType purchaseType = TrailPurchaseType.Wins;
    [Min(0)] public int winsPrice = 10;
    public string inAppId;
}
