using UnityEngine;

/// <summary>
/// Компонент на префабе всплывашки.
/// Анимирует popup вверх относительно его текущей стартовой позиции
/// и уничтожает объект после окончания lifetime.
/// </summary>
public class XPPopup : MonoBehaviour
{
    private float _startYOffset;
    private float _endYOffset;
    private float _duration;
    private float _lifetime;
    private bool _debug;

    private float _timer;

    private RectTransform _rectTransform;

    // Стартовая позиция, которую выставил Spawner
    private Vector2 _startPosition;

    public void Init(
        float startYOffset,
        float endYOffset,
        float riseDuration,
        float lifetime,
        bool debug
    )
    {
        _startYOffset = startYOffset;
        _endYOffset = endYOffset;
        _duration = riseDuration;
        _lifetime = lifetime;
        _debug = debug;

        _rectTransform = GetComponent<RectTransform>();

        _timer = 0f;

        // Запоминаем стартовую позицию, которую уже выставил спавнер
        _startPosition = _rectTransform.anchoredPosition;

        if (_debug)
        {
            Debug.Log(
                $"[XPPopup] StartPosition={_startPosition}",
                this
            );
        }
    }

    private void Update()
    {
        if (_rectTransform == null)
            return;

        _timer += Time.unscaledDeltaTime;

        // Анимация всплытия
        if (_timer < _duration)
        {
            float t = _timer / _duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float currentYOffset =
                Mathf.Lerp(_startYOffset, _endYOffset, t);

            // X оставляем как есть, меняем только Y относительно стартовой позиции
            _rectTransform.anchoredPosition =
                new Vector2(_startPosition.x, _startPosition.y + (currentYOffset - _startYOffset));
        }
        else
        {
            // Фиксируем конечную позицию
            _rectTransform.anchoredPosition =
                new Vector2(_startPosition.x, _startPosition.y + (_endYOffset - _startYOffset));
        }

        // Уничтожение
        if (_timer >= _lifetime)
        {
            if (_debug)
            {
                Debug.Log("[XPPopup] Уничтожение popup", this);
            }

            Destroy(gameObject);
        }
    }
}