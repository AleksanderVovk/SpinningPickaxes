using DG.Tweening;
using UnityEngine;

public class Page : MonoBehaviour
{
    public string pageName;

    [Header("Parts")]
    [SerializeField] private GameObject background;     // не анимируем
    [SerializeField] private RectTransform body;        // анимируем
    [SerializeField] private CanvasGroup bodyGroup;     // fade + input block

    [Header("Anim")]
    [SerializeField] private float openDuration = 0.18f;
    [SerializeField] private float closeDuration = 0.12f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Other objects")]
    [SerializeField] private GameObject[] _closeAndOpenObject;

    private Tween _openTween;
    private Tween _closeTween;

    private void Reset()
    {
        // удобство: если забыли назначить
        body = transform.Find("Body") as RectTransform;
        background = transform.Find("Background")?.gameObject;
        if (body != null) bodyGroup = body.GetComponent<CanvasGroup>();
    }

    public void Open()
    {
        CloseAndOpenOtherObjects(false);

        gameObject.SetActive(true);

        if (background != null) background.SetActive(true);
        if (body != null) body.gameObject.SetActive(true);

        KillTweens();

        // начальные значения
        if (body != null) body.localScale = Vector3.one * 0.85f;

        if (bodyGroup != null)
        {
            bodyGroup.alpha = 0f;
            bodyGroup.interactable = false;
            bodyGroup.blocksRaycasts = false;
        }

        // анимация body
        var seq = DOTween.Sequence().SetTarget(this);

        if (body != null)
            seq.Join(body.DOScale(1f, openDuration).SetEase(openEase));

        if (bodyGroup != null)
            seq.Join(bodyGroup.DOFade(1f, openDuration));

        _openTween = seq.OnComplete(() =>
        {
            if (bodyGroup != null)
            {
                bodyGroup.interactable = true;
                bodyGroup.blocksRaycasts = true;
            }
        });
    }

    public void Close()
    {
        CloseAndOpenOtherObjects(true);

        if (!gameObject.activeSelf) return;

        KillTweens();

        // сразу выключаем интеракции
        if (bodyGroup != null)
        {
            bodyGroup.interactable = false;
            bodyGroup.blocksRaycasts = false;
        }

        var seq = DOTween.Sequence().SetTarget(this);

        if (body != null)
            seq.Join(body.DOScale(0.85f, closeDuration).SetEase(closeEase));

        if (bodyGroup != null)
            seq.Join(bodyGroup.DOFade(0f, closeDuration));

        _closeTween = seq.OnComplete(() =>
        {
            // фон тоже выключаем при закрытии страницы
            if (background != null) background.SetActive(false);
            gameObject.SetActive(false);
        });
    }

    private void KillTweens()
    {
        // убиваем только твины этой Page, чтобы не задевать другие
        DOTween.Kill(this); // если все seq/tween создаём с SetTarget(this) [web:101]
        _openTween = null;
        _closeTween = null;
    }

    private void CloseAndOpenOtherObjects(bool value)
    {
        if (_closeAndOpenObject == null || _closeAndOpenObject.Length == 0)
            return;

        for (int i = 0; i < _closeAndOpenObject.Length; i++)
        {
            var go = _closeAndOpenObject[i];
            if (go != null)
                go.SetActive(value);
        }
    }
}
