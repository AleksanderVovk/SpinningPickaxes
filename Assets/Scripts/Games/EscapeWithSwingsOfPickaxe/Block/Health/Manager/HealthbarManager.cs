using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер пула healthbar'ов над блоками.
/// Показывает HP над блоками, переиспользует слайдеры при превышении лимита.
/// </summary>
public class HealthbarManager : MonoBehaviour
{
    [Header("Пул healthbar'ов")]

    [Tooltip("Префаб healthbar'а (Canvas с Slider в World Space).")]
    [SerializeField]
    private HealthbarView healthbarPrefab;

    [Tooltip("Количество слайдеров в пуле.")]
    [SerializeField]
    private int poolSize = 10;

    [Header("Настройки")]

    [Tooltip("На какой высоте над блоком держать healthbar.")]
    [SerializeField]
    private float yOffset = 1.2f;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private static HealthbarManager _instance;
    public static HealthbarManager Instance => _instance;

    // Пул свободных healthbar'ов
    private readonly Queue<HealthbarView> _freePool = new();

    // Активные healthbar'ы: блок -> healthbar
    private readonly Dictionary<BlockEntity, HealthbarView> _activeBars = new();

    // Список активных блоков в порядке появления (для LRU-вытеснения)
    private readonly List<BlockEntity> _activeBlocksOrder = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        CreatePool();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void CreatePool()
    {
        if (healthbarPrefab == null)
        {
            Debug.LogError("[HealthbarManager] Не задан healthbarPrefab.", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            HealthbarView bar = Instantiate(
                healthbarPrefab,
                Vector3.zero,
                Quaternion.identity,
                transform
            );

            bar.gameObject.name = $"Healthbar_{i}";
            _freePool.Enqueue(bar);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[HealthbarManager] Создан пул из {poolSize} healthbar'ов.", this);
        }
    }

    /// <summary>
    /// Показать/обновить healthbar над блоком.
    /// </summary>
    public void ShowHealthbar(BlockEntity block)
    {
        if (block == null)
        {
            Debug.LogWarning("[HealthbarManager] ShowHealthbar вызван с null блоком.", this);
            return;
        }

        // Если уже есть healthbar для этого блока — просто обновляем
        if (_activeBars.TryGetValue(block, out HealthbarView existingBar))
        {
            // Обновление происходит внутри HealthbarView в LateUpdate
            return;
        }

        // Если пул пуст — вытесняем самый старый блок
        if (_freePool.Count == 0)
        {
            ReuseOldestHealthbar(block);
            return;
        }

        // Берём свободный healthbar
        HealthbarView newBar = _freePool.Dequeue();
        AssignHealthbarToBlock(newBar, block);
    }

    /// <summary>
    /// Скрыть healthbar блока (например, при полном восстановлении).
    /// </summary>
    public void HideHealthbar(BlockEntity block)
    {
        if (block == null || !_activeBars.TryGetValue(block, out HealthbarView bar))
        {
            return;
        }

        bar.Unassign();
        _activeBars.Remove(block);
        _activeBlocksOrder.Remove(block);

        _freePool.Enqueue(bar);

        if (showDebugLogs)
        {
            Debug.Log($"[HealthbarManager] Healthbar блока {block.name} скрыт.", this);
        }
    }

    private void AssignHealthbarToBlock(HealthbarView bar, BlockEntity block)
    {
        bar.Assign(block);

        _activeBars[block] = bar;
        _activeBlocksOrder.Add(block);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[HealthbarManager] Healthbar назначен блоку {block.name}. " +
                $"Активных блоков: {_activeBars.Count}",
                this
            );
        }
    }

    private void ReuseOldestHealthbar(BlockEntity newBlock)
    {
        if (_activeBlocksOrder.Count == 0)
        {
            Debug.LogWarning("[HealthbarManager] Нечего вытеснять, пул пуст и активных блоков нет.", this);
            return;
        }

        // Берём самый старый блок (первый в списке)
        BlockEntity oldestBlock = _activeBlocksOrder[0];

        if (!_activeBars.TryGetValue(oldestBlock, out HealthbarView bar))
        {
            Debug.LogWarning("[HealthbarManager] Не найден healthbar для oldestBlock.", this);
            return;
        }

        // Отвязываем от старого блока
        bar.Unassign();
        _activeBars.Remove(oldestBlock);
        _activeBlocksOrder.RemoveAt(0);

        // Привязываем к новому
        AssignHealthbarToBlock(bar, newBlock);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[HealthbarManager] Healthbar переиспользован: " +
                $"{oldestBlock.name} -> {newBlock.name}",
                this
            );
        }
    }
}