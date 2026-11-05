#define HIPM_DEBUG

using System.Collections.Generic;
using UnityEngine;

public sealed class HeadInfoProximityManager : MonoBehaviour
{
    [SerializeField] private float _radius = 18f;
    [SerializeField] private float _interval = 0.1f;
    [SerializeField] private LayerMask _layerMask;

    [Tooltip("Размер буфера результатов. Подбери под максимальное число HeadInfo рядом с игроком.")]
    [SerializeField] private int _bufferSize = 256;

    private Collider[] _hits;

    private readonly HashSet<HeadInfoCullerDOTween> _visible = new();
    private readonly HashSet<HeadInfoCullerDOTween> _next = new();

#if HIPM_DEBUG
    private int _tickIndex;
    private int _lastCount;
    private int _lastAdded;
    private int _lastShown;
    private int _lastHidden;
    private int _nullColliders;
    private int _noProxy;
    private int _noCuller;
    private int _destroyedCullers;
    private int _bufferFullTicks;
    private const int LOG_EVERY_N_TICKS = 20;
#endif

    private void Awake()
    {
        _hits = new Collider[_bufferSize];

#if HIPM_DEBUG
        Debug.unityLogger.logEnabled = Debug.isDebugBuild;
        Debug.Log($"[HIPM] Awake. bufferSize={_bufferSize}, radius={_radius}, interval={_interval}, layerMask={_layerMask.value}", this);
#endif
    }

    private void OnEnable() => InvokeRepeating(nameof(Tick), 0f, _interval);

    private void OnDisable()
    {
        CancelInvoke(nameof(Tick));
        _visible.Clear();
        _next.Clear();
    }

    private void Tick()
    {
        _next.Clear();

#if HIPM_DEBUG
        _tickIndex++;
        _nullColliders = 0;
        _noProxy = 0;
        _noCuller = 0;
        _destroyedCullers = 0;
        _lastAdded = 0;
        _lastShown = 0;
        _lastHidden = 0;
#endif

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            _radius,
            _hits,
            _layerMask
        ); // NonAlloc не аллоцирует и пишет в буфер [web:78]

#if HIPM_DEBUG
        _lastCount = count;
        if (count >= _hits.Length) _bufferFullTicks++;
#endif

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (col == null)
            {
#if HIPM_DEBUG
                _nullColliders++;
#endif
                continue;
            }

            if (!col.TryGetComponent(out HeadInfoProxy proxy))
            {
#if HIPM_DEBUG
                _noProxy++;
#endif
                continue;
            }

            var culler = proxy.Culler;
            if (!culler) // null ИЛИ destroyed (Unity-style)
            {
#if HIPM_DEBUG
                _noCuller++;
#endif
                continue;
            }

            if (_next.Add(culler))
            {
#if HIPM_DEBUG
                _lastAdded++;
#endif
            }
        }

        // Включить новых
        foreach (var c in _next)
        {
            if (!c)
            {
#if HIPM_DEBUG
                _destroyedCullers++;
#endif
                continue;
            }

            if (!_visible.Contains(c))
            {
                c.Show();
#if HIPM_DEBUG
                _lastShown++;
#endif
            }
        }

        // Выключить ушедших
        foreach (var c in _visible)
        {
            if (!c) // если объект уничтожен — просто пропускаем, иначе будет MissingReferenceException [web:66][web:60]
            {
#if HIPM_DEBUG
                _destroyedCullers++;
#endif
                continue;
            }

            if (!_next.Contains(c))
            {
                c.Hide();
#if HIPM_DEBUG
                _lastHidden++;
#endif
            }
        }

        // Обновить visible = next, без destroyed
        _visible.Clear();
        foreach (var c in _next)
        {
            if (!c)
            {
#if HIPM_DEBUG
                _destroyedCullers++;
#endif
                continue;
            }
            _visible.Add(c);
        }
    }
}
