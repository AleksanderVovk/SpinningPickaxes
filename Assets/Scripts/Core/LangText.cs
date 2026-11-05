using TMPro;
using UnityEngine;
using YG;

public class LangText : MonoBehaviour
{
    public string ru, en, tr;

    private TMP_Text textComponent;

    public bool addStrengthSprite;

    private void Awake()
    {
        if (addStrengthSprite == false)
        {
            textComponent = GetComponent<TMP_Text>();
            SwitchLanguage(YG2.envir.language);
        }
    }

    private void Start()
    {
        if (addStrengthSprite == true)
        {
            textComponent = GetComponent<TMP_Text>();
            SwitchLanguage(YG2.envir.language);
        }
    }

    public void SwitchLanguage(string lang)
    {
        string _text = "";
        _text = ru;
        return;
        switch (lang)
        {
            case "ru":
                _text = ru;
                break;
            case "tr":
                _text = tr;
                break;
            default:
                _text = en;
                break;
        }

        if (addStrengthSprite)
            textComponent.text = ru + UTF.manager.TextToSprite("\U0001F602");
        else
            textComponent.text = _text;
    }
}
