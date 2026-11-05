using System.Collections.Generic;
using UnityEngine;

public class RotatingPickaxeRing : MonoBehaviour
{
    [Header("Центр кольца")]

    [Tooltip("Игрок или объект, за которым следует кольцо.")]
    [SerializeField]
    private Transform target;

    [Tooltip("Если Target не задан, используется Transform этого объекта.")]
    [SerializeField]
    private bool useThisTransformAsTarget = true;

    [Tooltip("Смещение центра кольца относительно игрока.")]
    [SerializeField]
    private Vector3 targetLocalOffset =
        new Vector3(0f, 1.2f, 0f);

    [Tooltip("Пустышка-центр орбиты. Если null — создаётся автоматически.")]
    [SerializeField]
    private Transform orbitCenter;

    [Header("Префабы кирок")]

    [Tooltip("Префаб, который будет использован, если ID не найден.")]
    [SerializeField]
    private GameObject defaultPickaxePrefab;

    [Tooltip("Максимальное количество визуальных кирок в кольце.")]
    [SerializeField]
    [Min(1)]
    private int maxPickaxeCount = 8;

    [Header("Расположение кирок")]

    [SerializeField]
    [Min(0.01f)]
    private float orbitRadius = 2f;

    [SerializeField]
    private float pickaxeHeight = 0f;

    [SerializeField]
    private float angleOffset = 0f;

    [Tooltip("Поправка поворота модели кирки.")]
    [SerializeField]
    private Vector3 pickaxeVisualRotationOffset =
        new Vector3(0f, 90f, 0f);

    [Header("Вращение")]

    [SerializeField]
    private bool rotateOrbit = true;

    [SerializeField]
    private float orbitRotationSpeed = 90f;

    [SerializeField]
    private float orbitRotationDirection = 1f;

    [Header("Дебаг")]

    [SerializeField]
    private bool showDebugLogs = false;

    private class SpawnedPickaxe
    {
        public Transform pivot;
        public Transform visual;
        public string pickaxeId;
    }

    private readonly List<SpawnedPickaxe> _pickaxes =
        new List<SpawnedPickaxe>();

    private float _orbitAngle;
    private string _equippedPickaxeId;

    public int CurrentPickaxeCount
    {
        get
        {
            CleanupDestroyedPickaxes();
            return _pickaxes.Count;
        }
    }

    public int MaxPickaxeCount => maxPickaxeCount;

    public string EquippedPickaxeId =>
        _equippedPickaxeId;

    private void Awake()
    {
        ResolveTarget();
        CreateOrbitCenterIfNeeded();
        UpdateOrbitCenterImmediately();
    }

    private void Start()
    {
        if (PlayerLevelSystem.Instance != null)
        {
            PlayerLevelSystem.Instance.SetPickaxeRing(
                this
            );
        }
    }

    private void Update()
    {
        UpdateOrbitCenter();
        RotateOrbit();
    }

    private void ResolveTarget()
    {
        if (target == null &&
            useThisTransformAsTarget)
        {
            target = transform;
        }
    }

    private void CreateOrbitCenterIfNeeded()
    {
        if (orbitCenter != null)
        {
            return;
        }

        GameObject centerObject =
            new GameObject("PickaxeOrbitCenter");

        orbitCenter = centerObject.transform;

        orbitCenter.position = transform.position;
        orbitCenter.rotation = Quaternion.identity;
    }

    private void UpdateOrbitCenter()
    {
        if (orbitCenter == null ||
            target == null)
        {
            return;
        }

        orbitCenter.position =
            target.position +
            target.TransformDirection(
                targetLocalOffset
            );

        orbitCenter.rotation =
            Quaternion.identity;
    }

    private void UpdateOrbitCenterImmediately()
    {
        UpdateOrbitCenter();
    }

    private void RotateOrbit()
    {
        if (!rotateOrbit ||
            _pickaxes.Count == 0)
        {
            return;
        }

        _orbitAngle +=
            orbitRotationSpeed *
            orbitRotationDirection *
            Time.deltaTime;

        _orbitAngle =
            Mathf.Repeat(
                _orbitAngle,
                360f
            );

        RebuildRing();
    }

    public void SetPickaxeCount(
        int targetCount,
        string pickaxeId = null
    )
    {
        targetCount = Mathf.Clamp(
            targetCount,
            1,
            maxPickaxeCount
        );

        if (!string.IsNullOrEmpty(pickaxeId))
        {
            _equippedPickaxeId = pickaxeId;
        }

        CleanupDestroyedPickaxes();

        while (_pickaxes.Count > targetCount)
        {
            RemoveLastPickaxe();
        }

        while (_pickaxes.Count < targetCount)
        {
            SpawnOnePickaxe(
                _equippedPickaxeId
            );
        }

        RebuildRing();
    }

    public void SpawnOnePickaxe(
        string pickaxeId = null
    )
    {
        CleanupDestroyedPickaxes();

        if (_pickaxes.Count >= maxPickaxeCount)
        {
            return;
        }

        if (string.IsNullOrEmpty(pickaxeId))
        {
            pickaxeId = _equippedPickaxeId;
        }

        if (string.IsNullOrEmpty(pickaxeId))
        {
            pickaxeId = "pickaxe_01";
        }

        _equippedPickaxeId = pickaxeId;

        GameObject prefab =
            GetPickaxePrefabById(
                pickaxeId
            );

        if (prefab == null)
        {
            Debug.LogError(
                $"[{nameof(RotatingPickaxeRing)}] " +
                $"Не найден префаб кирки. ID: {pickaxeId}",
                this
            );

            return;
        }

        ResolveTarget();
        CreateOrbitCenterIfNeeded();
        UpdateOrbitCenterImmediately();

        GameObject pivotObject =
            new GameObject(
                $"PickaxePivot_{_pickaxes.Count + 1}"
            );

        Transform pivot =
            pivotObject.transform;

        pivot.SetParent(
            orbitCenter,
            false
        );

        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;

        GameObject pickaxeObject =
            Instantiate(
                prefab,
                pivot
            );

        pickaxeObject.name =
            $"Pickaxe_{_pickaxes.Count + 1}_{pickaxeId}";

        Transform visual =
            pickaxeObject.transform;

        visual.localPosition =
            new Vector3(
                orbitRadius,
                pickaxeHeight,
                0f
            );

        visual.localRotation =
            Quaternion.Euler(
                pickaxeVisualRotationOffset
            );

        ApplyPickaxeDamage(
            pickaxeObject,
            pickaxeId
        );

        _pickaxes.Add(
            new SpawnedPickaxe
            {
                pivot = pivot,
                visual = visual,
                pickaxeId = pickaxeId
            }
        );

        RebuildRing();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[RotatingPickaxeRing] Создана кирка: {pickaxeId}. " +
                $"Количество: {_pickaxes.Count}/{maxPickaxeCount}",
                this
            );
        }
    }

    public void EquipPickaxe(
        string pickaxeId
    )
    {
        if (string.IsNullOrEmpty(pickaxeId))
        {
            return;
        }

        _equippedPickaxeId = pickaxeId;

        int currentCount =
            Mathf.Max(
                1,
                CurrentPickaxeCount
            );

        ClearPickaxes();

        SetPickaxeCount(
            currentCount,
            _equippedPickaxeId
        );
    }

    public void ClearPickaxes()
    {
        for (int i = _pickaxes.Count - 1;
             i >= 0;
             i--)
        {
            SpawnedPickaxe pickaxe =
                _pickaxes[i];

            if (pickaxe != null &&
                pickaxe.pivot != null)
            {
                Destroy(
                    pickaxe.pivot.gameObject
                );
            }
        }

        _pickaxes.Clear();
    }

    public void RemoveLastPickaxe()
    {
        CleanupDestroyedPickaxes();

        if (_pickaxes.Count == 0)
        {
            return;
        }

        int lastIndex =
            _pickaxes.Count - 1;

        SpawnedPickaxe lastPickaxe =
            _pickaxes[lastIndex];

        if (lastPickaxe != null &&
            lastPickaxe.pivot != null)
        {
            Destroy(
                lastPickaxe.pivot.gameObject
            );
        }

        _pickaxes.RemoveAt(
            lastIndex
        );
    }

    public void RebuildRing()
    {
        CleanupDestroyedPickaxes();

        int count = _pickaxes.Count;

        if (count == 0)
        {
            return;
        }

        float angleStep =
            360f / count;

        for (int i = 0; i < count; i++)
        {
            SpawnedPickaxe data =
                _pickaxes[i];

            if (data == null ||
                data.pivot == null ||
                data.visual == null)
            {
                continue;
            }

            float angle =
                angleOffset +
                _orbitAngle +
                angleStep * i;

            data.pivot.localPosition =
                Vector3.zero;

            data.pivot.localRotation =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f
                );

            data.visual.localPosition =
                new Vector3(
                    orbitRadius,
                    pickaxeHeight,
                    0f
                );

            data.visual.localRotation =
                Quaternion.Euler(
                    pickaxeVisualRotationOffset
                );
        }
    }

    public void SetTarget(
        Transform newTarget
    )
    {
        target = newTarget;
        UpdateOrbitCenterImmediately();
    }

    public void SetRotationEnabled(
        bool value
    )
    {
        rotateOrbit = value;
    }

    public void SetOrbitRadius(
        float newRadius
    )
    {
        orbitRadius = Mathf.Max(
            0.01f,
            newRadius
        );

        RebuildRing();
    }

    public void SetMaxPickaxeCount(
        int newMaxCount
    )
    {
        maxPickaxeCount = Mathf.Max(
            1,
            newMaxCount
        );

        if (CurrentPickaxeCount >
            maxPickaxeCount)
        {
            SetPickaxeCount(
                maxPickaxeCount,
                _equippedPickaxeId
            );
        }
    }


    private void ApplyPickaxeDamage(
        GameObject pickaxeObject,
        string pickaxeId
    )
    {
        if (pickaxeObject == null)
            return;

        PickaxeEntity entity =
            pickaxeObject.GetComponentInChildren<PickaxeEntity>();

        if (entity == null)
        {
            Debug.LogWarning(
                $"У префаба кирки {pickaxeId} " +
                "не найден компонент PickaxeEntity.",
                pickaxeObject
            );
            return;
        }

        PickaxeData data = GetPickaxeDataById(pickaxeId);

        if (data == null)
        {
            Debug.LogWarning(
                $"Не найдены данные кирки с ID: {pickaxeId}",
                this
            );
            return;
        }

        // Передаём только базовый урон конкретной кирки.
        // Общие множители применяются внутри PickaxeEntity.Damage.
        entity.SetDamage(data.damage);

        if (showDebugLogs)
        {
            Debug.Log(
                $"Кирка {pickaxeId}: базовый урон = {data.damage:0.##}, " +
                $"итоговый урон = {entity.Damage:0.##}",
                entity
            );
        }
    }

    private GameObject GetPickaxePrefabById(
        string pickaxeId
    )
    {
        PickaxeData data =
            GetPickaxeDataById(
                pickaxeId
            );

        if (data != null &&
            data.pickaxePrefab != null)
        {
            return data.pickaxePrefab;
        }

        return defaultPickaxePrefab;
    }

    private PickaxeData GetPickaxeDataById(
        string pickaxeId
    )
    {
        if (string.IsNullOrEmpty(pickaxeId))
        {
            return null;
        }

        if (PickaxeDatabase.Instance == null)
        {
            return null;
        }

        return PickaxeDatabase.Instance.GetPickaxeById(
            pickaxeId
        );
    }

    private void CleanupDestroyedPickaxes()
    {
        for (int i = _pickaxes.Count - 1;
             i >= 0;
             i--)
        {
            SpawnedPickaxe pickaxe =
                _pickaxes[i];

            if (pickaxe == null ||
                pickaxe.pivot == null ||
                pickaxe.visual == null)
            {
                _pickaxes.RemoveAt(i);
            }
        }
    }
    public void ResetToDefaultPickaxe()
    {
        const string defaultPickaxeId = "pickaxe_01";

        // Сбрасываем ID экипированной кирки.
        _equippedPickaxeId = defaultPickaxeId;

        // Удаляем все кирки из кольца.
        ClearPickaxes();

        // Создаём одну дефолтную кирку.
        SetPickaxeCount(1, defaultPickaxeId);

        if (showDebugLogs)
        {
            Debug.Log(
                "[RotatingPickaxeRing] " +
                "После перерождения установлена дефолтная кирка.",
                this
            );
        }
    }


    private void OnDrawGizmosSelected()
    {
        Transform gizmoTarget =
            target != null
                ? target
                : transform;

        if (gizmoTarget == null)
        {
            return;
        }

        Vector3 centerPosition =
            gizmoTarget.position +
            gizmoTarget.TransformDirection(
                targetLocalOffset
            );

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(
            centerPosition,
            0.12f
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            centerPosition,
            orbitRadius
        );

        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            gizmoTarget.position,
            centerPosition
        );
    }
}