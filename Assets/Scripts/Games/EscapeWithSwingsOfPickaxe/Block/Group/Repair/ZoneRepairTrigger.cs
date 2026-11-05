using System.Collections;
using UnityEngine;

/// <summary>
/// Триггер-зона, которая при входе игрока запускает асинхронное
/// восстановление блоков во всех связанных BlockGroup.
/// Если восстановление уже идёт, повторный запуск игнорируется.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZoneRepairTrigger : MonoBehaviour
{
    [Header("Группы блоков")]

    [Tooltip("Все группы блоков, которые восстанавливаются в этой зоне.")]
    [SerializeField]
    private BlockGroup[] blockGroups;

    [Header("Настройки восстановления")]

    [Tooltip("Задержка перед началом восстановления после входа в триггер.")]
    [SerializeField]
    private float startDelay = 0.2f;

    [Tooltip("Задержка между восстановлением разных групп (в секундах).")]
    [SerializeField]
    private float groupInterval = 0.1f;

    [Tooltip("Восстанавливать только один раз за вход, или каждый раз.")]
    [SerializeField]
    private bool triggerOncePerEntry = true;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private bool _isPlayerInside;
    private Coroutine _repairCoroutine;

    // Флаг: идёт ли сейчас восстановление
    private bool IsRepairing => _repairCoroutine != null;

    public BossFightZone boss;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Простая проверка: по тегу Player или по компоненту CharacterController
        if (other.CompareTag("Player") ||
            other.GetComponent<CharacterController>() != null)
        {
            // Если уже восстанавливаемся — игнорируем вход
            if (IsRepairing)
            {
                if (showDebugLogs)
                {
                    Debug.Log(
                        "[ZoneRepairTrigger] Восстановление уже идёт, вход игнорируется.",
                        this
                    );
                }

                return;
            }

            if (_isPlayerInside && triggerOncePerEntry)
            {
                return;
            }

            _isPlayerInside = true;

            if (showDebugLogs)
            {
                Debug.Log("[ZoneRepairTrigger] Игрок вошёл в триггер.", this);
            }

            StartRepair();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") ||
            other.GetComponent<CharacterController>() != null)
        {
            _isPlayerInside = false;

            if (showDebugLogs)
            {
                Debug.Log("[ZoneRepairTrigger] Игрок вышел из триггера.", this);
            }
        }
    }

    /// <summary>
    /// Запустить асинхронное восстановление всех BlockGroup.
    /// Если восстановление уже идёт — ничего не делает.
    /// </summary>
    public void StartRepair()
    {
        if (IsRepairing)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "[ZoneRepairTrigger] StartRepair вызван, но восстановление уже идёт.",
                    this
                );
            }

            return;
        }

        if (blockGroups == null || blockGroups.Length == 0)
        {
            if (showDebugLogs)
            {
                Debug.Log("[ZoneRepairTrigger] Нет BlockGroup для восстановления.", this);
            }

            return;
        }

        _repairCoroutine = StartCoroutine(RepairAllGroupsRoutine());
    }

    /// <summary>
    /// Остановить текущее восстановление.
    /// </summary>
    public void StopRepair()
    {
        if (_repairCoroutine != null)
        {
            StopCoroutine(_repairCoroutine);
            _repairCoroutine = null;

            if (showDebugLogs)
            {
                Debug.Log("[ZoneRepairTrigger] Восстановление остановлено вручную.", this);
            }
        }
    }

    private IEnumerator RepairAllGroupsRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        boss.ReviveBoss();

        for (int i = 0; i < blockGroups.Length; i++)
        {
            BlockGroup group = blockGroups[i];

            if (group == null)
            {
                continue;
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[ZoneRepairTrigger] Запуск восстановления группы {group.name}",
                    this
                );
            }

            group.StartRepair();

            // Небольшая задержка между группами, чтобы не всё сразу
            if (groupInterval > 0f && i < blockGroups.Length - 1)
            {
                yield return new WaitForSeconds(groupInterval);
            }
        }

        _repairCoroutine = null;

        if (showDebugLogs)
        {
            Debug.Log("[ZoneRepairTrigger] Все группы восстановлены.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider>();
        if (col == null)
        {
            return;
        }

        Gizmos.color = new Color(0f, 1f, 0.6f, 0.25f);

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(
                transform.TransformPoint(box.center),
                transform.TransformVector(box.size)
            );
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(
                transform.TransformPoint(sphere.center),
                sphere.radius * Mathf.Max(
                    transform.lossyScale.x,
                    transform.lossyScale.y,
                    transform.lossyScale.z
                )
            );
        }
        else
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}