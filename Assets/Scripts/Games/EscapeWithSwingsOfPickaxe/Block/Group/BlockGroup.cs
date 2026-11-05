using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Группа блоков, относящихся к одной зоне.
/// Отвечает за их восстановление (возврат наверх + лечение).
/// </summary>
public class BlockGroup : MonoBehaviour
{
    [Header("Блоки зоны")]

    public LanguageDirectory nameZone;
    public long healthZone;

    [SerializeField] private TMP_Text _nameZoneDisplay;
    [SerializeField] private TMP_Text _healthDisplay;

    [Tooltip("Все блоки, которые относятся к этой зоне.")]
    [SerializeField]
    private BlockEntity[] blocks;

    [Header("Настройки восстановления")]

    [Tooltip("С какой задержкой после старта начинать восстановление.")]
    [SerializeField]
    private float startDelay = 0.1f;

    [Tooltip("Задержка между восстановлением соседних блоков (в секундах).")]
    [SerializeField]
    private float blockInterval = 0.05f;

    [Header("Возврат наверх")]

    [Tooltip("Возвращать блоки на их исходную позицию по Y.")]
    [SerializeField]
    private bool resetPositionY = true;

    [Tooltip("На сколько выше поднимать блок относительно его текущей позиции.")]
    [SerializeField]
    private float riseOffset = 0f;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    public BlockEntity[] Blocks => blocks;

    private Coroutine _repairCoroutine;

    private void Start()
    {
        if (_nameZoneDisplay != null) _nameZoneDisplay.text = UTF.ReadLanguageDirectory(nameZone);
        if (_healthDisplay != null) _healthDisplay.text = healthZone + " " + UTF.ReadLanguageDirectory(LangLIst.instance.hp);
    }

    /// <summary>
    /// Очистить список и добавить все BlockEntity из дочерних объектов.
    /// </summary>
    /// 
    [ContextMenu("Перезаписать блоки из детей")]
    public void RefreshBlocksFromChildren()
    {
        blocks = GetComponentsInChildren<BlockEntity>(true);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        if (showDebugLogs)
        {
            Debug.Log($"[BlockGroup] Обновлено блоков из дочерних объектов: {blocks.Length}", this);
        }
    }

    /// <summary>
    /// Запустить асинхронное восстановление всех блоков группы.
    /// </summary>
    public void StartRepair()
    {
        if (blocks == null || blocks.Length == 0)
        {
            if (showDebugLogs)
            {
                Debug.Log("[BlockGroup] Нет блоков для восстановления.", this);
            }

            return;
        }

        if (_repairCoroutine != null)
        {
            StopCoroutine(_repairCoroutine);
        }

        _repairCoroutine = StartCoroutine(RepairRoutine());
    }

    /// <summary>
    /// Остановить восстановление, если оно идёт.
    /// </summary>
    public void StopRepair()
    {
        if (_repairCoroutine != null)
        {
            StopCoroutine(_repairCoroutine);
            _repairCoroutine = null;
        }
    }

    private IEnumerator RepairRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            BlockEntity block = blocks[i];

            if (block == null || block.gameObject.activeSelf == false)
            {
                continue;
            }

            if (block.IsDestroyed)
            {
                block.Revive();
            }
            else
            {
                block.SetHealth(block.MaxHealth);
            }

            if (resetPositionY)
            {
                ResetBlockPositionY(block);
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[BlockGroup] Блок {block.name} восстановлен. " +
                    $"HP: {block.CurrentHealth}/{block.MaxHealth}",
                    this
                );
            }

            if (blockInterval > 0f && i < blocks.Length - 1)
            {
                yield return new WaitForSeconds(blockInterval);
            }
        }

        HideAllHealthbarsInGroup();

        _repairCoroutine = null;

        if (showDebugLogs)
        {
            Debug.Log("[BlockGroup] Восстановление группы завершено.", this);
        }
    }

    private void ResetBlockPositionY(BlockEntity block)
    {
        Transform t = block.transform;
        Vector3 pos = t.position;
        pos.y += riseOffset;
        t.position = pos;
    }

    private void HideAllHealthbarsInGroup()
    {
        if (blocks == null)
        {
            return;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            BlockEntity block = blocks[i];

            if (block == null)
            {
                continue;
            }

            if (HealthbarManager.Instance != null)
            {
                HealthbarManager.Instance.HideHealthbar(block);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[BlockGroup] Скрыто healthbar'ов: {blocks.Length}", this);
        }
    }
}