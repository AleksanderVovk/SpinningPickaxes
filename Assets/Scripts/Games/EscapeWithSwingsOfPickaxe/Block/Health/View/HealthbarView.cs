using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Представление одного healthbar'а (один слайдер над блоком).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class HealthbarView : MonoBehaviour
{
    [Header("Ссылки")]

    [Tooltip("Slider внутри этого Canvas.")]
    [SerializeField]
    private Slider slider;

    [Tooltip("Насколько выше блока держать healthbar.")]
    [SerializeField]
    private float yOffset = 1.2f;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private Canvas _canvas;
    private BlockEntity _currentBlock;

    public bool IsAssigned => _currentBlock != null;
    public BlockEntity CurrentBlock => _currentBlock;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();

        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>(true);
        }

        // Изначально скрываем
        Hide();
    }

    /// <summary>
    /// Привязать healthbar к блоку.
    /// </summary>
    public void Assign(BlockEntity block)
    {
        if (block == null)
        {
            Debug.LogWarning("[HealthbarView] Assign вызван с null блоком.", this);
            return;
        }

        _currentBlock = block;

        // Подписываемся на события блока
        _currentBlock.OnDamaged += OnBlockDamaged;
        _currentBlock.OnDestroyed += OnBlockDestroyed;

        UpdateSliderValues();
        Show();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[HealthbarView] Привязан к блоку {block.name}. " +
                $"HP: {block.CurrentHealth}/{block.MaxHealth}",
                this
            );
        }
    }

    /// <summary>
    /// Отвязать healthbar от текущего блока.
    /// </summary>
    public void Unassign()
    {
        if (_currentBlock == null)
        {
            return;
        }

        _currentBlock.OnDamaged -= OnBlockDamaged;
        _currentBlock.OnDestroyed -= OnBlockDestroyed;

        _currentBlock = null;

        Hide();

        if (showDebugLogs)
        {
            Debug.Log("[HealthbarView] Отвязан от блока.", this);
        }
    }

    private void LateUpdate()
    {
        if (_currentBlock == null)
        {
            return;
        }

        // Держим healthbar над блоком
        Vector3 blockPos = _currentBlock.transform.position;
        transform.position = blockPos + Vector3.up * yOffset;

        // Поворачиваем лицом к камере (опционально)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward;
        }

        UpdateSliderValues();
    }

    private void UpdateSliderValues()
    {
        if (_currentBlock == null || slider == null)
        {
            return;
        }

        slider.maxValue = _currentBlock.MaxHealth;
        slider.value = _currentBlock.CurrentHealth;
    }

    private void OnBlockDamaged(BlockEntity block, float damage, PickaxeEntity attacker)
    {
        UpdateSliderValues();
        Show();
    }

    private void OnBlockDestroyed(BlockEntity block, PickaxeEntity attacker)
    {
        // Можно либо скрывать сразу, либо оставлять до исчезновения блока
        UpdateSliderValues();
    }

    private void Show()
    {
        if (_canvas != null)
        {
            _canvas.enabled = true;
        }

        gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_currentBlock != null)
        {
            _currentBlock.OnDamaged -= OnBlockDamaged;
            _currentBlock.OnDestroyed -= OnBlockDestroyed;
        }
    }
}