
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickaxeEntity : MonoBehaviour
{
    [Header("Урон")]
    [SerializeField, Min(0.01f)]
    private float baseDamage = 1f;

    [SerializeField, Min(0f)]
    private float hitCooldown = 0.25f;

    [SerializeField]
    private bool canDealDamage = true;

    [Header("Collider")]
    [SerializeField]
    private bool setColliderAsTrigger = true;

    [SerializeField]
    private bool addKinematicRigidbodyIfMissing = true;

    [SerializeField]
    private bool searchBlockInParent = true;

    [Header("Дебаг")]
    [SerializeField]
    private bool showDebugLogs = false;

    // Сохраняем дополнительный множитель урона кирки из YG.
    private float damageMultiplier = 1f;

    private Collider _hitCollider;
    private Rigidbody _rigidbody;

    private readonly Dictionary<BlockEntity, float> _lastHitTimes =
        new Dictionary<BlockEntity, float>();

    // Итоговый урон с учётом всех глобальных бонусов.
    public float Damage
    {
        get
        {
            float damageBeforeGlobalBonuses =
                Mathf.Max(0f, baseDamage) * damageMultiplier;

            if (PlayerDamageSystem.Instance != null)
            {
                return PlayerDamageSystem.Instance.CalculateDamage(
                    damageBeforeGlobalBonuses
                );
            }

            // Если система бонусов ещё не создана, наносим базовый урон
            // с учётом множителя, загруженного из сохранения.
            return damageBeforeGlobalBonuses;
        }
    }

    public float BaseDamage => baseDamage;
    public float DamageMultiplier => damageMultiplier;
    public float HitCooldown => hitCooldown;
    public bool CanDealDamage => canDealDamage;

    private void Awake()
    {
        SetupPhysics();
    }

    private void OnDisable()
    {
        _lastHitTimes.Clear();
    }

    private void SetupPhysics()
    {
        _hitCollider = GetComponent<Collider>();

        if (_hitCollider == null)
        {
            Debug.LogError(
                $"{nameof(PickaxeEntity)}: нужен Collider на объекте hitbox.",
                this
            );
            return;
        }

        if (setColliderAsTrigger)
            _hitCollider.isTrigger = true;

        _rigidbody = GetComponent<Rigidbody>();

        if (_rigidbody == null && addKinematicRigidbodyIfMissing)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamageBlock(other);
    }

    private void TryDamageBlock(Collider other)
    {
        if (!canDealDamage || other == null)
            return;

        BlockEntity block = other.GetComponent<BlockEntity>();

        if (block == null && searchBlockInParent)
            block = other.GetComponentInParent<BlockEntity>();

        if (block == null || !block.CanReceiveDamage)
            return;

        if (!CanHitBlockNow(block))
            return;

        _lastHitTimes[block] = Time.time;

        float oldHealth = block.CurrentHealth;

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // Damage автоматически учитывает перерождения, след и ауру.
        bool wasDestroyed = block.TakeDamage(Damage, this, hitPoint);

        float actualDamage = oldHealth - block.CurrentHealth;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PickaxeEntity] {name} → {block.name}. " +
                $"Урон: {actualDamage:0.##}; " +
                $"Общий множитель: " +
                $"{(PlayerDamageSystem.Instance != null ? PlayerDamageSystem.Instance.TotalDamageMultiplier : 1f):0.##}x; " +
                $"HP: {block.CurrentHealth:0.##}/{block.MaxHealth:0.##}",
                this
            );
        }

        if (wasDestroyed)
            _lastHitTimes.Remove(block);
    }

    private bool CanHitBlockNow(BlockEntity block)
    {
        if (hitCooldown <= 0f)
            return true;

        if (!_lastHitTimes.TryGetValue(block, out float lastHitTime))
            return true;

        return Time.time >= lastHitTime + hitCooldown;
    }

    public void SetDamage(float value)
    {
        baseDamage = Mathf.Max(0.01f, value);
    }

    public void SetHitCooldown(float value)
    {
        hitCooldown = Mathf.Max(0f, value);
    }

    public void SetCanDealDamage(bool value)
    {
        canDealDamage = value;
    }

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(1f, multiplier);
    }

    public void RefreshDamageMultiplier()
    {
        float multiplier = 1f;

        if (YG.YG2.saves != null &&
            YG.YG2.saves.playerData != null)
        {
            YG.YG2.saves.playerData.EnsureValidData();

            multiplier = YG.YG2.saves.playerData
                .pickaxeDamageMultiplier;
        }

        SetDamageMultiplier(multiplier);
    }

    private void OnDrawGizmosSelected()
    {
        Collider hitCollider = GetComponent<Collider>();

        if (hitCollider == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            hitCollider.bounds.center,
            hitCollider.bounds.size
        );
    }
}