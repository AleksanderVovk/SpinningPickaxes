using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BlockEntity : MonoBehaviour
{
    [Header("Данные блока")]
    [SerializeField] private string blockId = "Stone";
    [SerializeField, Min(0.01f)] protected float maxHealth = 10f;
    [SerializeField, Min(0f)] protected float currentHealth = 10f;
    [Tooltip("Может ли блок получать урон.")]
    [SerializeField] private bool canReceiveDamage = true;

    [Header("Опыт за урон")]
    [SerializeField] private bool giveExperienceForDamage = true;
    [SerializeField, Min(0f)] private float experienceMultiplier = 1f;
    protected virtual float ExperienceMultiplier => experienceMultiplier;
    [SerializeField, Min(0f)] private float bonusExperienceOnDestroy = 0f;

    [Header("Визуальная модель")]
    [Tooltip("Дочерний объект визуальной модели. Если не задан, используется корень.")]
    [SerializeField] private Transform visualTransform;
    [Tooltip("Объект, который опускается при разрушении. Если не задан, используется корень.")]
    [SerializeField] private Transform sinkTransform;

    [Header("Squash при ударе")]
    [SerializeField] private bool playSquashOnDamage = true;
    [SerializeField, Range(1f, 2f)] private float squashHorizontalMultiplier = 1.12f;
    [SerializeField, Range(0.1f, 1f)] private float squashVerticalMultiplier = 0.82f;
    [SerializeField, Min(0.01f)] private float squashDownDuration = 0.045f;
    [SerializeField, Min(0.01f)] private float squashReturnDuration = 0.1f;
    [SerializeField] private bool restartSquashOnEveryHit = true;

    [Header("Разрушение: проваливание вниз")]
    [SerializeField] private bool sinkIntoGroundOnDestroy = true;
    [SerializeField, Min(0f)] private float sinkDistance = 3f;
    [SerializeField, Min(0.01f)] private float sinkDuration = 0.45f;
    [SerializeField, Min(0f)] private float sinkStartDelay = 0f;
    [SerializeField] private bool disableRenderersAfterSinking = true;
    [SerializeField] private bool disableObjectAfterSinking = false;
    [Tooltip("Для возрождаемых боссов оставь выключенным: уничтоженный GameObject восстановить нельзя.")]
    [SerializeField] private bool destroyGameObjectAfterSinking = false;

    [Header("Реакция на разрушение")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private GameObject dropPrefab;
    [SerializeField, Min(0)] private int dropCount = 0;
    [SerializeField, Min(0f)] private float dropSpreadRadius = 0.4f;

    [Header("Эффекты попадания")]
    [SerializeField] private ParticleSystem damageParticle;
    [SerializeField] private Renderer[] blockRenderers;
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField, Min(0f)] private float flashDuration = 0.08f;

    [Header("Дебаг")]
    [SerializeField] private bool showDebugLogs = false;

    public string BlockId => blockId;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDestroyed => _isDestroyed;
    public virtual bool CanReceiveDamage => canReceiveDamage && !_isDestroyed;
    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    public event Action<BlockEntity, float, PickaxeEntity> OnDamaged;
    public event Action<BlockEntity, PickaxeEntity> OnDestroyed;

    private bool _isDestroyed;
    private bool _canReceiveDamageBeforeDestroy = true;
    private Collider _blockCollider;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine _flashCoroutine;
    private Coroutine _squashCoroutine;
    private Coroutine _sinkCoroutine;
    private Vector3 _visualInitialScale;
    private Vector3 _visualInitialLocalPosition;
    private Vector3 _rootInitialPosition;
    private Quaternion _rootInitialRotation;
    private Vector3 _rootInitialScale;
    private Vector3 _sinkInitialPosition;
    private bool _initialStateCached;

    private static readonly int DamageColorId = Shader.PropertyToID("_DamageColor");
    private static readonly int DamageFlashId = Shader.PropertyToID("_DamageFlash");
    protected virtual bool PreventHealthReduction => false;

    private void Reset()
    {
        currentHealth = maxHealth;
        damageFlashColor = Color.red;
    }

    protected virtual void Awake()
    {
        CacheReferencesAndInitialState();
        currentHealth = currentHealth <= 0f || currentHealth > maxHealth ? maxHealth : currentHealth;
        ResetDamageFlash();
    }

    private void CacheReferencesAndInitialState()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
        _blockCollider = GetComponent<Collider>();
        if (visualTransform == null) visualTransform = transform;
        if (sinkTransform == null) sinkTransform = transform;
        RefreshRendererCache();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

        _visualInitialScale = visualTransform.localScale;
        _visualInitialLocalPosition = visualTransform.localPosition;
        _rootInitialPosition = transform.position;
        _rootInitialRotation = transform.rotation;
        _rootInitialScale = transform.localScale;
        _sinkInitialPosition = sinkTransform.position;
        _initialStateCached = true;
        _canReceiveDamageBeforeDestroy = canReceiveDamage;
    }

    private void RefreshRendererCache()
    {
        // Обновляем список каждый раз, когда восстанавливаем блок: дочерние Renderer
        // могут быть добавлены/активированы уже после Awake.
        blockRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnDisable() => StopAllBlockCoroutines();

    public virtual bool TakeDamage(float damage, PickaxeEntity attacker = null, Vector3 hitPoint = default)
    {
        if (!CanReceiveDamage || damage <= 0f) return false;
        float actualDamage = Mathf.Min(damage, currentHealth);
        if (!PreventHealthReduction) currentHealth = Mathf.Max(0f, currentHealth - actualDamage);

        GiveExperienceForDamage(actualDamage);
        Vector3 particlePosition = hitPoint == default ? transform.position : hitPoint;
        if (damageParticle != null)
        {
            damageParticle.transform.position = particlePosition;
            damageParticle.Play();
        }
        if (DamageParticlePool.Instance != null) DamageParticlePool.Instance.PlayAt(particlePosition);
        if (flashOnDamage) FlashDamageColor();
        if (playSquashOnDamage && currentHealth > 0f) PlaySquashAnimation();

        OnDamaged?.Invoke(this, actualDamage, attacker);
        if (PlayerLevelSystem.Instance != null) PlayerLevelSystem.Instance.TrueDamageBlock(transform.position);
        if (HealthbarManager.Instance != null) HealthbarManager.Instance.ShowHealthbar(this);

        if (showDebugLogs)
        {
            string attackerName = attacker != null ? attacker.name : "Unknown";
            Debug.Log($"[BlockEntity] {name} получил {actualDamage:0.##} урона от {attackerName}. HP: {currentHealth:0.##}/{maxHealth:0.##}", this);
        }
        if (currentHealth > 0f) return false;
        DestroyBlock(attacker);
        return true;
    }

    public void Heal(float value)
    {
        if (_isDestroyed || value <= 0f) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + value);
    }

    public void SetHealth(float value)
    {
        if (_isDestroyed) return;
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        if (currentHealth <= 0f) { DestroyBlock(null); return; }
        if (currentHealth >= maxHealth && HealthbarManager.Instance != null) HealthbarManager.Instance.HideHealthbar(this);
    }

    public void SetCanReceiveDamage(bool value) => canReceiveDamage = value;

    private void GiveExperienceForDamage(float actualDamage)
    {
        if (!giveExperienceForDamage || actualDamage <= 0f || PlayerLevelSystem.Instance == null) return;
        float amount = actualDamage * ExperienceMultiplier;
        if (amount > 0f) PlayerLevelSystem.Instance.AddExperience(amount);
    }

    private void DestroyBlock(PickaxeEntity attacker)
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        _canReceiveDamageBeforeDestroy = canReceiveDamage;
        canReceiveDamage = false;
        if (_blockCollider != null) _blockCollider.enabled = false;
        StopCoroutineIfRunning(ref _flashCoroutine);
        StopCoroutineIfRunning(ref _squashCoroutine);
        ResetDamageFlash();

        if (playSquashOnDamage && visualTransform != null)
            visualTransform.localScale = GetSquashScale(squashHorizontalMultiplier + 0.04f, Mathf.Max(0.1f, squashVerticalMultiplier - 0.06f));
        if (bonusExperienceOnDestroy > 0f && PlayerLevelSystem.Instance != null)
            PlayerLevelSystem.Instance.AddExperience(bonusExperienceOnDestroy);

        OnDestroyed?.Invoke(this, attacker);
        if (HealthbarManager.Instance != null) HealthbarManager.Instance.HideHealthbar(this);
        if (showDebugLogs) Debug.Log($"[BlockEntity] Блок разрушен: {name}.", this);
        if (destroyEffectPrefab != null) Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        SpawnDrops();

        if (sinkIntoGroundOnDestroy) _sinkCoroutine = StartCoroutine(SinkIntoGroundRoutine());
        else FinishDestroyState();
    }

    private void SpawnDrops()
    {
        if (dropPrefab == null || dropCount <= 0) return;
        for (int i = 0; i < dropCount; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * dropSpreadRadius;
            Vector3 position = transform.position + new Vector3(offset.x, 0.2f, offset.y);
            Instantiate(dropPrefab, position, UnityEngine.Random.rotation);
        }
    }

    private void PlaySquashAnimation()
    {
        if (visualTransform == null || _isDestroyed) return;
        if (_squashCoroutine != null)
        {
            if (!restartSquashOnEveryHit) return;
            StopCoroutine(_squashCoroutine);
        }
        _squashCoroutine = StartCoroutine(SquashRoutine());
    }

    private IEnumerator SquashRoutine()
    {
        Vector3 startScale = visualTransform.localScale;
        Vector3 squashScale = GetSquashScale(squashHorizontalMultiplier, squashVerticalMultiplier);
        yield return AnimateVisualScale(startScale, squashScale, squashDownDuration);
        if (visualTransform != null) yield return AnimateVisualScale(visualTransform.localScale, _visualInitialScale, squashReturnDuration);
        if (visualTransform != null) visualTransform.localScale = _visualInitialScale;
        _squashCoroutine = null;
    }

    private Vector3 GetSquashScale(float horizontal, float vertical)
        => new Vector3(_visualInitialScale.x * horizontal, _visualInitialScale.y * vertical, _visualInitialScale.z * horizontal);

    private IEnumerator AnimateVisualScale(Vector3 from, Vector3 to, float duration)
    {
        if (visualTransform == null) yield break;
        if (duration <= 0f) { visualTransform.localScale = to; yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (visualTransform == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            visualTransform.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
        if (visualTransform != null) visualTransform.localScale = to;
    }

    private IEnumerator SinkIntoGroundRoutine()
    {
        if (sinkTransform == null) { FinishDestroyState(); yield break; }
        if (sinkStartDelay > 0f) yield return new WaitForSeconds(sinkStartDelay);
        Vector3 start = sinkTransform.position;
        Vector3 target = start + Vector3.down * sinkDistance;
        float elapsed = 0f;
        while (elapsed < sinkDuration)
        {
            if (sinkTransform == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, sinkDuration)));
            sinkTransform.position = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }
        if (sinkTransform != null) sinkTransform.position = target;
        _sinkCoroutine = null;
        FinishDestroyState();
    }

    private void FinishDestroyState()
    {
        if (disableRenderersAfterSinking) SetRenderersEnabled(false);
        if (destroyGameObjectAfterSinking) { Destroy(gameObject); return; }
        if (disableObjectAfterSinking) gameObject.SetActive(false);
    }

    private void SetRenderersEnabled(bool value)
    {
        if (value) RefreshRendererCache();
        if (blockRenderers == null) return;
        foreach (Renderer renderer in blockRenderers)
            if (renderer != null) renderer.enabled = value;
    }

    private void FlashDamageColor()
    {
        if (blockRenderers == null || blockRenderers.Length == 0) RefreshRendererCache();
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashDamageRoutine());
    }

    private IEnumerator FlashDamageRoutine()
    {
        SetDamageFlash(damageFlashColor, 1f);
        yield return new WaitForSeconds(flashDuration);
        ResetDamageFlash();
        _flashCoroutine = null;
    }

    private void SetDamageFlash(Color color, float amount)
    {
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
        if (blockRenderers == null) return;
        foreach (Renderer renderer in blockRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(DamageColorId, color);
            _propertyBlock.SetFloat(DamageFlashId, amount);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void ResetDamageFlash()
    {
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
        if (blockRenderers == null) return;
        foreach (Renderer renderer in blockRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(DamageFlashId, 0f);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void StopCoroutineIfRunning(ref Coroutine coroutine)
    {
        if (coroutine == null) return;
        StopCoroutine(coroutine);
        coroutine = null;
    }

    private void StopAllBlockCoroutines()
    {
        StopCoroutineIfRunning(ref _flashCoroutine);
        StopCoroutineIfRunning(ref _squashCoroutine);
        StopCoroutineIfRunning(ref _sinkCoroutine);
    }

    /// <summary>Полностью восстанавливает блок, включая объект, визуальную модель, позицию, коллайдер и здоровье.</summary>
    public void Revive()
    {
        // Если объект был выключен после проваливания, включаем его первым делом.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopAllBlockCoroutines();

        if (!_initialStateCached) CacheReferencesAndInitialState();
        if (_blockCollider == null) _blockCollider = GetComponent<Collider>();
        if (_blockCollider != null) _blockCollider.enabled = true;

        _isDestroyed = false;
        canReceiveDamage = true;
        currentHealth = maxHealth;

        // Возвращаем корень в исходное состояние. BossFightZone затем может
        // переместить его вниз для анимации появления.
        transform.position = _rootInitialPosition;
        transform.rotation = _rootInitialRotation;
        transform.localScale = _rootInitialScale;
        if (sinkTransform != null) sinkTransform.position = _sinkInitialPosition;
        if (visualTransform != null)
        {
            visualTransform.localScale = _visualInitialScale;
            visualTransform.localPosition = _visualInitialLocalPosition;
        }

        RefreshRendererCache();
        foreach (Renderer renderer in blockRenderers)
        {
            if (renderer == null) continue;
            renderer.enabled = true;
        }
        ResetDamageFlash();

        if (HealthbarManager.Instance != null) HealthbarManager.Instance.HideHealthbar(this);
        if (showDebugLogs) Debug.Log($"[BlockEntity] Блок {name} оживлён. HP восстановлено: {currentHealth:0.##}/{maxHealth:0.##}.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Collider blockCollider = GetComponent<Collider>();
        if (blockCollider == null) return;
        float healthPercent = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        Gizmos.color = _isDestroyed ? Color.gray : Color.Lerp(Color.red, Color.green, healthPercent);
        Gizmos.DrawWireCube(blockCollider.bounds.center, blockCollider.bounds.size);
    }
}
