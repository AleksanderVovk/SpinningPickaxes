using UnityEngine;

public class LangLIst : MonoBehaviour
{
    public static LangLIst instance;

    public LanguageDirectory power;
    public LanguageDirectory power_b;

    public LanguageDirectory newLvl;
    public LanguageDirectory win;
    public LanguageDirectory hp;

    public static readonly LanguageDirectory NotEnoughFunds =
        new LanguageDirectory(
            "Недостаточно средств!",
            "Insufficient funds!",
            "Yetersiz bakiye!"
        );

    #region pickaxe
    public static LanguageDirectory Bought =
       new LanguageDirectory(
           "Куплено",
           "Bought",
           "Satin alindi"
       );

    public static LanguageDirectory Equipped =
        new LanguageDirectory(
            "Экипировано",
            "Equipped",
            "Kusanildi"
        );

    public static LanguageDirectory Buy =
        new LanguageDirectory(
            "Купить",
            "Buy",
            "Satin al"
        );

    public static LanguageDirectory RequiredWins(long wins)
    {
        return new LanguageDirectory(
            $"Требуется {wins} побед",
            $"Requires {wins} wins",
            $"{wins} galibiyet gerekli"
        );
    }

    // Возвращает сразу строку на нужном языке
    public static string RequiredWinsText(long wins)
    {
        return UTF.ReadLanguageDirectory(RequiredWins(wins));
    }
    #endregion

    private void Awake()
    {
        instance = this;
    }
}