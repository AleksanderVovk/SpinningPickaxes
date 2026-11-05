using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class HeadInfoCullerDOTween : MonoBehaviour
{
    [Header("Root (we disable it)")]
    [SerializeField] private GameObject _rootToToggle;

    [Header("What to fade")]
    [SerializeField] private TMP_Text[] _texts;

    [Header("Tween")]
    [SerializeField] private float _showTime = 0.18f;
    [SerializeField] private float _hideTime = 0.18f;
    [SerializeField] private Ease _easeShow = Ease.OutQuad;
    [SerializeField] private Ease _easeHide = Ease.InQuad;
    [SerializeField] private float _hiddenAlpha = 0f;
    [SerializeField] private Vector3 _hiddenScale = new Vector3(0.9f, 0.9f, 0.9f);

    [Header("Start state")]
    [SerializeField] private bool _startHidden = true;

    private bool _isShown;

    // Лучше int id (чуть дешевле чем string), и стабильный на объект
    private int _tweenId;

    private void Reset()
    {
        _rootToToggle = gameObject;
        _texts = GetComponentsInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        if (_rootToToggle == null) _rootToToggle = gameObject;
        if (_texts == null || _texts.Length == 0) _texts = GetComponentsInChildren<TMP_Text>(true);

        _tweenId = GetInstanceID();

        if (_startHidden)
        {
            _isShown = false;
            DOTween.Kill(_tweenId);          // на всякий случай прибить твины этого ID [web:53]
            ApplyHiddenInstant();             // выставить hidden scale/alpha
            if (_rootToToggle.activeSelf)
                _rootToToggle.SetActive(false);
        }
        else
        {
            _isShown = _rootToToggle.activeSelf;
            if (!_isShown)
                ApplyHiddenInstant();
        }
    }


    private void OnDestroy()
    {
        // На всякий случай убить твины этого объекта
        DOTween.Kill(_tweenId); // Kill по Id — рекомендуемый паттерн [web:20]
    }

    public void Show()
    {
        if (_isShown && _rootToToggle.activeSelf) return;
        _isShown = true;

        DOTween.Kill(_tweenId); // убрать hide-твины/переходы [web:20]

        if (!_rootToToggle.activeSelf)
            _rootToToggle.SetActive(true);

        // стартовая поза
        transform.localScale = _hiddenScale;
        SetTextsAlpha(_hiddenAlpha);

        transform.DOScale(Vector3.one, _showTime)
            .SetEase(_easeShow)
            .SetId(_tweenId);

        foreach (var t in _texts)
        {
            if (t == null) continue;
            t.DOFade(1f, _showTime)
             .SetEase(_easeShow)
             .SetId(_tweenId);
        }
    }

    public void Hide()
    {
        if (!_isShown) return;
        _isShown = false;

        DOTween.Kill(_tweenId); // убрать show-твины [web:20]

        transform.DOScale(_hiddenScale, _hideTime)
            .SetEase(_easeHide)
            .SetId(_tweenId);

        Tween last = null;
        int alive = 0;

        foreach (var t in _texts)
        {
            if (t == null) continue;
            alive++;
            last = t.DOFade(_hiddenAlpha, _hideTime)
                    .SetEase(_easeHide)
                    .SetId(_tweenId);
        }

        if (alive == 0)
        {
            DOVirtual.DelayedCall(_hideTime, TryDisableRoot)
                     .SetId(_tweenId);
            return;
        }

        // Выключаем после завершения твина [web:79]
        last.OnComplete(TryDisableRoot);
    }

    private void TryDisableRoot()
    {
        if (_isShown) return;
        if (_rootToToggle.activeSelf)
            _rootToToggle.SetActive(false);
    }

    private void ApplyHiddenInstant()
    {
        transform.localScale = _hiddenScale;
        SetTextsAlpha(_hiddenAlpha);
    }

    private void SetTextsAlpha(float a)
    {
        if (_texts == null) return;
        for (int i = 0; i < _texts.Length; i++)
        {
            var t = _texts[i];
            if (t == null) continue;
            var c = t.color;
            c.a = a;
            t.color = c;
        }
    }
}
