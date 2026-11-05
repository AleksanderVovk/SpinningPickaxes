
using UnityEngine;

[DisallowMultipleComponent]
public class UIRobloxTextAnimation : MonoBehaviour
{
    [Header("Общие настройки")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Случайное начальное положение анимации.")]
    [SerializeField] private bool randomStartPhase = false;

    [Tooltip("Восстанавливать исходные поворот и размер при отключении.")]
    [SerializeField] private bool restoreOnDisable = true;

    [Header("Вращение Z")]
    [SerializeField] private bool enableRotation = true;

    [SerializeField] private float minRotation = -8f;
    [SerializeField] private float maxRotation = 8f;

    [Tooltip("Количество циклов в секунду.")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1f;

    [Tooltip("Добавляет пружинное покачивание.")]
    [SerializeField] private bool rotationSpring = false;

    [Tooltip("Сила дополнительного пружинного колебания.")]
    [SerializeField, Range(0f, 1f)]
    private float rotationSpringStrength = 0.2f;

    [Header("Размер")]
    [SerializeField] private bool enableScale = true;

    [Tooltip("Минимальный размер относительно исходного.")]
    [SerializeField, Min(0f)] private float minScale = 1f;

    [Tooltip("Максимальный размер относительно исходного.")]
    [SerializeField, Min(0f)] private float maxScale = 1.15f;

    [Tooltip("Количество циклов в секунду.")]
    [SerializeField, Min(0f)] private float scaleSpeed = 1.5f;

    [Tooltip("Добавляет пружинное изменение размера.")]
    [SerializeField] private bool scaleSpring = true;

    [Tooltip("Сила дополнительного пружинного колебания.")]
    [SerializeField, Range(0f, 1f)]
    private float scaleSpringStrength = 0.2f;

    [Header("Дополнительно")]
    [Tooltip("Сдвиг фазы размера относительно вращения, в градусах.")]
    [SerializeField] private float scalePhaseOffset = 90f;

    private RectTransform _rectTransform;

    private Quaternion _initialRotation;
    private Vector3 _initialScale;

    private float _rotationPhase;
    private float _scalePhase;

    private bool _isPlaying;
    private bool _initialized;

    private const float TwoPi = Mathf.PI * 2f;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (!_initialized)
            return;

        if (randomStartPhase)
        {
            _rotationPhase = Random.Range(0f, TwoPi);
            _scalePhase = Random.Range(0f, TwoPi);
        }
        else
        {
            _rotationPhase = 0f;
            _scalePhase = 0f;
        }

        _isPlaying = playOnEnable;

        if (_isPlaying)
            ApplyAnimation(0f);
    }

    private void OnDisable()
    {
        _isPlaying = false;

        if (restoreOnDisable && _initialized)
            RestoreTransform();
    }

    private void CacheReferences()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_rectTransform == null)
        {
            _initialized = false;
            return;
        }

        _initialRotation = _rectTransform.localRotation;
        _initialScale = _rectTransform.localScale;

        _initialized = true;
    }

    private void Update()
    {
        if (!_isPlaying || !_initialized)
            return;

        if (!enableRotation && !enableScale)
            return;

        float deltaTime = useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        ApplyAnimation(deltaTime);
    }

    private void ApplyAnimation(float deltaTime)
    {
        if (enableRotation)
        {
            _rotationPhase = Mathf.Repeat(
                _rotationPhase + rotationSpeed * TwoPi * deltaTime,
                TwoPi
            );
        }

        if (enableScale)
        {
            _scalePhase = Mathf.Repeat(
                _scalePhase + scaleSpeed * TwoPi * deltaTime,
                TwoPi
            );
        }

        // Вращение относительно исходного поворота.
        if (enableRotation)
        {
            float wave = GetWave(
                _rotationPhase,
                rotationSpring,
                rotationSpringStrength
            );

            float z = Mathf.Lerp(
                minRotation,
                maxRotation,
                (wave + 1f) * 0.5f
            );

            _rectTransform.localRotation =
                _initialRotation * Quaternion.Euler(0f, 0f, z);
        }

        // Размер относительно исходного масштаба.
        if (enableScale)
        {
            float phase = _scalePhase +
                scalePhaseOffset * Mathf.Deg2Rad;

            float wave = GetWave(
                phase,
                scaleSpring,
                scaleSpringStrength
            );

            float scale = Mathf.Lerp(
                minScale,
                maxScale,
                (wave + 1f) * 0.5f
            );

            _rectTransform.localScale = _initialScale * scale;
        }
    }

    private float GetWave(
        float phase,
        bool useSpring,
        float springStrength
    )
    {
        float wave = Mathf.Sin(phase);

        if (!useSpring || springStrength <= 0f)
            return wave;

        // Дополнительная гармоника создаёт эффект пружинного движения.
        float springWave = Mathf.Sin(phase * 3f);

        return Mathf.Clamp(
            (wave + springWave * springStrength) /
            (1f + springStrength),
            -1f,
            1f
        );
    }

    private void RestoreTransform()
    {
        _rectTransform.localRotation = _initialRotation;
        _rectTransform.localScale = _initialScale;
    }

    public void Play()
    {
        if (!_initialized)
            CacheReferences();

        if (!_initialized)
            return;

        _isPlaying = true;
    }

    public void Stop()
    {
        _isPlaying = false;

        if (restoreOnDisable && _initialized)
            RestoreTransform();
    }

    public void SetRotationEnabled(bool value)
    {
        enableRotation = value;

        if (!value && _initialized)
            _rectTransform.localRotation = _initialRotation;
    }

    public void SetScaleEnabled(bool value)
    {
        enableScale = value;

        if (!value && _initialized)
            _rectTransform.localScale = _initialScale;
    }

    public void SetScaleRange(float min, float max)
    {
        minScale = Mathf.Max(0f, min);
        maxScale = Mathf.Max(minScale, max);
    }

    public void SetRotationRange(float min, float max)
    {
        minRotation = min;
        maxRotation = Mathf.Max(min, max);
    }
}