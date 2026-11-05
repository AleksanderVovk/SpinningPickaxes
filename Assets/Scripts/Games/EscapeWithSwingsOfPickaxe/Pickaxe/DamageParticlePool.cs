using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул из фиксированного количества эффектов попадания.
/// Если все эффекты заняты, повторно используется самый старый.
/// </summary>
public class DamageParticlePool : MonoBehaviour
{
    public static DamageParticlePool Instance { get; private set; }

    [Header("Настройки пула")]
    [SerializeField] private ParticleSystem particlePrefab;
    [SerializeField, Min(1)] private int poolSize = 10;

    [Tooltip("Родитель для созданных эффектов. Если не задан, используется этот объект.")]
    [SerializeField] private Transform effectsParent;

    private readonly List<ParticleSystem> _particles = new();
    private readonly Queue<ParticleSystem> _availableParticles = new();
    private readonly Queue<ParticleSystem> _activeOrder = new();

    private readonly Dictionary<ParticleSystem, Coroutine> _returnRoutines = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (effectsParent == null)
            effectsParent = transform;

        CreatePool();
    }

    private void CreatePool()
    {
        if (particlePrefab == null)
        {
            Debug.LogError("[DamageParticlePool] Не назначен Particle Prefab.", this);
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            ParticleSystem particle = Instantiate(
                particlePrefab,
                effectsParent
            );

            particle.name = $"DamageParticle_{i}";
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.gameObject.SetActive(false);

            _particles.Add(particle);
            _availableParticles.Enqueue(particle);
        }
    }

    /// <summary>
    /// Запускает эффект в указанной мировой позиции.
    /// </summary>
    public void PlayAt(Vector3 position)
    {
        if (_particles.Count == 0)
            return;

        ParticleSystem particle = GetParticle();

        if (particle == null)
            return;

        if (_returnRoutines.TryGetValue(particle, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            _returnRoutines.Remove(particle);
        }

        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Transform particleTransform = particle.transform;
        particleTransform.SetParent(effectsParent, true);
        particleTransform.position = position;

        // Для эффекта по умолчанию сохраняем поворот префаба.
        particle.gameObject.SetActive(true);
        particle.Play(true);

        _activeOrder.Enqueue(particle);
        _returnRoutines[particle] = StartCoroutine(ReturnWhenFinished(particle));
    }

    private ParticleSystem GetParticle()
    {
        // Убираем из очереди неактивные или уничтоженные ссылки.
        while (_availableParticles.Count > 0)
        {
            ParticleSystem available = _availableParticles.Dequeue();

            if (available != null)
                return available;
        }

        // Если свободных нет — забираем самый старый эффект.
        while (_activeOrder.Count > 0)
        {
            ParticleSystem oldest = _activeOrder.Dequeue();

            if (oldest != null)
                return oldest;
        }

        return null;
    }

    private IEnumerator ReturnWhenFinished(ParticleSystem particle)
    {
        // Ждём завершения корневой системы и всех дочерних систем.
        while (particle != null && particle.IsAlive(true))
            yield return null;

        if (particle == null)
            yield break;

        _returnRoutines.Remove(particle);

        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.gameObject.SetActive(false);

        _availableParticles.Enqueue(particle);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}