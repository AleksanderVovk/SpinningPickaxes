
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using YG;

public class BossFightZone : MonoBehaviour
{
    [Header("Босс")]
    [SerializeField] private BlockEntity boss;
    [SerializeField] private string bossId = "boss_01";
    [SerializeField] private string bossName = "Boss";
    [SerializeField] private string playerTag = "Player";

    [Header("Появление босса")]
    [Tooltip("Если включено, используется абсолютная координата Y. Если выключено — смещение вниз относительно исходной позиции.")]
    [SerializeField] private bool useAbsoluteStartY = false;

    [Tooltip("Абсолютная координата Y, с которой появляется босс.")]
    [SerializeField] private float bossStartY = -3f;

    [Tooltip("На сколько единиц ниже исходной позиции появляется босс.")]
    [SerializeField] private float bossStartYOffset = -4f;

    [Tooltip("Длительность выползания босса.")]
    [SerializeField, Min(0.01f)]
    private float bossEmergenceDuration = 2f;

    [Tooltip("Сила дрожания босса во время появления.")]
    [SerializeField, Min(0f)]
    private float bossShakeAmplitude = 0.15f;

    [Tooltip("Частота дрожания босса.")]
    [SerializeField, Min(0f)]
    private float bossShakeFrequency = 25f;

    [Header("UI")]
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text wallRecordText;

    [Header("Музыка интро")]
    [SerializeField] private AudioSource introAudioSource;
    [SerializeField] private AudioClip introMusic;
    [SerializeField, Range(0f, 1f)]
    private float introMusicVolume = 1f;

    [Header("Основная музыка")]
    [SerializeField] private AudioSource mainMusicAudioSource;

    [Header("Музыка боя с боссом")]
    [SerializeField] private AudioSource bossFightAudioSource;
    [SerializeField, Range(0f, 1f)]
    private float bossFightMusicVolume = 0.6f;
    [SerializeField, Min(0.01f)]
    private float musicFadeDuration = 1.5f;

    private Coroutine musicTransitionCoroutine;
    private bool bossMusicRequested;

    [Header("Партиклы интро")]
    [SerializeField] private ParticleSystem[] introParticles;

    [Header("Тряска камеры")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin cameraNoise;
    [SerializeField] private float shakeAmplitude = 2f;
    [SerializeField] private float shakeFrequency = 2f;
    [SerializeField] private float shakeDuration = 1.5f;
    [SerializeField] private bool fadeOutShake = true;

    [Header("Настройки")]
    [SerializeField] private bool hidePanelOnExit = true;
    [SerializeField] private bool showTimerWithMilliseconds = true;
    [SerializeField] private bool restartFightIfPlayerInside = true;

    private bool playerInside;
    private bool timerStarted;
    private bool victoryHandled;
    private bool introPlayed;

    private float startTime;
    private float finalTime;

    private Coroutine shakeCoroutine;
    private Coroutine bossEmergenceCoroutine;

    private float originalAmplitude;
    private float originalFrequency;

    private Vector3 bossInitialPosition;
    private bool bossInitialPositionCached;

    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        if (boss != null)
        {
            bossInitialPosition = boss.transform.position;
            bossInitialPositionCached = true;
        }

        if (bossPanel != null)
            bossPanel.SetActive(false);

        if (bossNameText != null)
            bossNameText.text = bossName;

        if (cameraNoise != null)
        {
            originalAmplitude = cameraNoise.AmplitudeGain;
            originalFrequency = cameraNoise.FrequencyGain;

            // При старте игры тряска выключена.
            cameraNoise.AmplitudeGain = 0f;
            cameraNoise.FrequencyGain = 0f;
        }

        if (bossFightAudioSource != null)
        {
            bossFightAudioSource.playOnAwake = false;
            bossFightAudioSource.loop = true;
            bossFightAudioSource.volume = 0f;
            bossFightAudioSource.Stop();
        }

        if (mainMusicAudioSource != null)
        {
            mainMusicAudioSource.playOnAwake = false;
            mainMusicAudioSource.volume = GetSavedMusicVolume();

            if (!mainMusicAudioSource.isPlaying)
                mainMusicAudioSource.Play();
        }

        UpdateRecordUI();
        UpdateHealthUI();
    }

    private void Start()
    {
        // Прячем босса под землёй до начала интро.
        // BlockEntity успевает сохранить исходную позицию
        // в своём Awake до перемещения в Start.
        if (boss != null &&
            !boss.IsDestroyed &&
            bossInitialPositionCached)
        {
            boss.transform.position =
                GetBossEmergenceStartPosition();
        }
    }

    private Vector3 GetBossEmergenceStartPosition()
    {
        Vector3 startPosition = bossInitialPosition;

        startPosition.y = useAbsoluteStartY
            ? bossStartY
            : bossInitialPosition.y + bossStartYOffset;

        return startPosition;
    }

    private void OnEnable()
    {
        if (boss != null)
        {
            boss.OnDamaged += HandleBossDamaged;
            boss.OnDestroyed += HandleBossDestroyed;
        }
    }

    private void OnDisable()
    {
        if (boss != null)
        {
            boss.OnDamaged -= HandleBossDamaged;
            boss.OnDestroyed -= HandleBossDestroyed;
        }

        if (musicTransitionCoroutine != null)
        {
            StopCoroutine(musicTransitionCoroutine);
            musicTransitionCoroutine = null;
        }

        if (bossEmergenceCoroutine != null)
        {
            StopCoroutine(bossEmergenceCoroutine);
            bossEmergenceCoroutine = null;
        }

        if (boss != null &&
            bossInitialPositionCached &&
            !boss.IsDestroyed)
        {
            boss.transform.position = bossInitialPosition;
        }

        if (bossFightAudioSource != null)
        {
            bossFightAudioSource.Stop();
            bossFightAudioSource.volume = 0f;
        }

        if (mainMusicAudioSource != null)
        {
            mainMusicAudioSource.volume = GetSavedMusicVolume();
        }

        StopCameraShake();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (victoryHandled || boss == null || boss.IsDestroyed)
            return;

        playerInside = true;

        if (bossPanel != null)
            bossPanel.SetActive(true);

        UpdateHealthUI();
        UpdateRecordUI();

        SetBossFightMusic(true);

        if (!timerStarted)
            StartNewFight();

        if (!introPlayed)
        {
            introPlayed = true;
            PlayBossIntro();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (IsAnotherPlayerColliderInside(other))
            return;

        playerInside = false;

        SetBossFightMusic(false);

        if (hidePanelOnExit && bossPanel != null)
            bossPanel.SetActive(false);
    }

    private bool IsAnotherPlayerColliderInside(Collider exitingCollider)
    {
        if (zoneCollider == null)
            return false;

        Collider[] overlaps = Physics.OverlapBox(
            zoneCollider.bounds.center,
            zoneCollider.bounds.extents,
            Quaternion.identity
        );

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap == exitingCollider)
                continue;

            if (overlap.CompareTag(playerTag))
                return true;
        }

        return false;
    }

    private void Update()
    {
        if (timerStarted && !victoryHandled)
        {
            float elapsed = Time.time - startTime;
            UpdateTimerUI(elapsed);
        }

        // Запасная проверка: если событие OnDestroyed не дошло до этого компонента,
        // всё равно обработаем победу, когда BlockEntity пометил босса уничтоженным.
        if (!victoryHandled && boss != null && boss.IsDestroyed)
        {
            HandleBossDestroyed(boss, null);
        }
    }

    private void StartNewFight()
    {
        if (boss == null || boss.IsDestroyed || victoryHandled)
            return;

        timerStarted = true;
        startTime = Time.time;
        finalTime = 0f;

        if (playerInside)
            SetBossFightMusic(true);

        UpdateHealthUI();
        UpdateRecordUI();
    }

    private void UpdateTimerUI(float elapsed)
    {
        if (timerText == null)
            return;

        TimeSpan time = TimeSpan.FromSeconds(elapsed);

        if (showTimerWithMilliseconds)
        {
            timerText.text =
                $"{(int)time.TotalMinutes:00}:{time.Seconds:00}:{time.Milliseconds:000}";
        }
        else
        {
            timerText.text =
                $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
        }
    }

    private void HandleBossDamaged(
        BlockEntity damagedBoss,
        float damage,
        PickaxeEntity attacker)
    {
        if (damagedBoss != boss)
            return;

        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (boss == null)
            return;

        float currentHealth = boss.CurrentHealth;
        float maxHealth = boss.MaxHealth;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
            healthText.text = $"{currentHealth:0} / {maxHealth:0}";
    }

    private void HandleBossDestroyed(
        BlockEntity destroyedBoss,
        PickaxeEntity attacker)
    {
        if (destroyedBoss == null || destroyedBoss != boss || victoryHandled)
            return;

        finalTime = timerStarted
            ? Mathf.Max(0f, Time.time - startTime)
            : Mathf.Max(0f, finalTime);

        victoryHandled = true;
        timerStarted = false;
        playerInside = false;

        // Сразу закрываем интерфейс босса после победы.
        if (bossPanel != null)
            bossPanel.SetActive(false);

        // Оставляем итоговое время на таймере, если он назначен.
        if (timerText != null)
            timerText.text = FormatTime(finalTime);

        SetBossFightMusic(false);
        StopCameraShake();

        if (bossEmergenceCoroutine != null)
        {
            StopCoroutine(bossEmergenceCoroutine);
            bossEmergenceCoroutine = null;
        }

        if (introAudioSource != null)
            introAudioSource.Stop();

        if (introParticles != null)
        {
            foreach (ParticleSystem particles in introParticles)
            {
                if (particles != null)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        UpdateHealthUI();

        bool recordSaved = SaveBestRecord();
        UpdateRecordUI();

        string killedTime = FormatTime(finalTime);
        string message = recordSaved
            ? $"Вы убили босса за: {killedTime}. Новый лучший рекорд записан!"
            : $"Вы убили босса за: {killedTime}. Рекорд не записан. Лучший рекорд: {GetBestRecordText()}";

        // Используем ShowMessage — именно этот метод указан в проекте.
        // Выводим сообщение до выдачи награды, чтобы ошибка в коде награды
        // не помешала показать победу.
        NotificationManager.ShowNotification(message);

        GiveReward();
    }

    private void GiveReward()
    {
        // Существующая награда за победу.
        if (PlayersManager.instance != null &&
            PlayersManager.instance.realPlayer != null)
        {
            PlayersManager.instance.realPlayer.TeleportToSpawn(ParticleType.Win);
        }

        ValueSystem.AddBalance(5000, ValueType.Wins);
    }

    /// <summary>
    /// Сохраняет время, только если это первое прохождение
    /// или время лучше предыдущего (меньше).
    /// Возвращает true, если новый рекорд записан.
    /// </summary>
    private bool SaveBestRecord()
    {
        if (YG2.saves == null)
            return false;

        if (YG2.saves.playerData == null)
            YG2.saves.playerData = new PlayerData();

        PlayerData data = YG2.saves.playerData;
        if (data.bossRecords == null)
            data.bossRecords = new System.Collections.Generic.List<BossRecord>();

        BossRecord record = data.bossRecords.Find(r => r != null && r.bossId == bossId);

        // Первое прохождение — сразу создаём рекорд.
        if (record == null)
        {
            data.bossRecords.Add(new BossRecord(bossId, finalTime));
            YG2.SaveProgress();
            return true;
        }

        // Сохраняем только более быстрое прохождение.
        if (!record.defeated || record.bestTime <= 0f || finalTime < record.bestTime)
        {
            record.bestTime = finalTime;
            record.defeated = true;
            YG2.SaveProgress();
            return true;
        }

        // Босс побеждён, но лучшее время не улучшено.
        record.defeated = true;
        return false;
    }

    private void UpdateRecordUI()
    {
        if (wallRecordText == null)
            return;

        wallRecordText.text = $"Рекорд: {GetBestRecordText()}";
    }

    private string GetBestRecordText()
    {
        if (YG2.saves == null ||
            YG2.saves.playerData == null ||
            YG2.saves.playerData.bossRecords == null)
        {
            return "не установлен";
        }

        BossRecord record = YG2.saves.playerData.bossRecords.Find(
            r => r != null && r.bossId == bossId && r.defeated
        );

        if (record == null || record.bestTime <= 0f)
            return "не установлен";

        return FormatTime(record.bestTime);
    }

    private string FormatTime(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));

        if (showTimerWithMilliseconds)
        {
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}:{time.Milliseconds:000}";
        }

        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private void PlayBossIntro()
    {
        if (introAudioSource != null && introMusic != null)
        {
            introAudioSource.clip = introMusic;
            introAudioSource.volume = introMusicVolume;
            introAudioSource.Play();
        }

        if (introParticles != null)
        {
            foreach (ParticleSystem particles in introParticles)
            {
                if (particles != null)
                    particles.Play();
            }
        }

        StartBossEmergence();
        StartCameraShake();
    }

    private void StartBossEmergence()
    {
        if (boss == null ||
            boss.IsDestroyed ||
            !bossInitialPositionCached)
        {
            return;
        }

        if (bossEmergenceCoroutine != null)
        {
            StopCoroutine(bossEmergenceCoroutine);
            bossEmergenceCoroutine = null;
        }

        bossEmergenceCoroutine =
            StartCoroutine(BossEmergenceCoroutine());
    }

    private IEnumerator BossEmergenceCoroutine()
    {
        Transform bossTransform = boss.transform;

        Vector3 startPosition =
            GetBossEmergenceStartPosition();

        Vector3 targetPosition =
            bossInitialPosition;

        // Начинаем ниже исходной позиции.
        bossTransform.position = startPosition;

        float duration =
            Mathf.Max(0.01f, bossEmergenceDuration);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (boss == null || boss.IsDestroyed)
            {
                bossEmergenceCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Плавное выползание с мягким торможением.
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                smoothT
            );

            // Дрожание постепенно уменьшается к концу анимации.
            float shakeFade = 1f - smoothT;

            float phase =
                elapsed * bossShakeFrequency * Mathf.PI * 2f;

            float shakeX =
                Mathf.Sin(phase) * bossShakeAmplitude;

            float shakeY =
                Mathf.Sin(phase * 1.7f) *
                bossShakeAmplitude * 0.5f;

            float shakeZ =
                Mathf.Cos(phase * 1.3f) *
                bossShakeAmplitude;

            position += new Vector3(
                shakeX,
                shakeY,
                shakeZ
            ) * shakeFade;

            bossTransform.position = position;

            yield return null;
        }

        // Точно возвращаем босса в исходную позицию,
        // чтобы дрожание не оставило смещение.
        if (boss != null && !boss.IsDestroyed)
            bossTransform.position = targetPosition;

        bossEmergenceCoroutine = null;
    }

    private void StartCameraShake()
    {
        if (cameraNoise == null)
            return;

        StopCameraShake();
        shakeCoroutine = StartCoroutine(CameraShakeCoroutine());
    }

    private IEnumerator CameraShakeCoroutine()
    {
        cameraNoise.AmplitudeGain = shakeAmplitude;
        cameraNoise.FrequencyGain = shakeFrequency;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shakeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (fadeOutShake)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                cameraNoise.AmplitudeGain =
                    Mathf.Lerp(shakeAmplitude, 0f, t);

                cameraNoise.FrequencyGain =
                    Mathf.Lerp(shakeFrequency, originalFrequency, t);
            }

            yield return null;
        }

        RestoreCameraNoise();
        shakeCoroutine = null;
    }

    private void StopCameraShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        RestoreCameraNoise();
    }

    private void RestoreCameraNoise()
    {
        if (cameraNoise == null)
            return;

        // Не восстанавливаем исходную амплитуду:
        // она могла быть ненулевой и вызывать постоянную тряску.
        cameraNoise.AmplitudeGain = 0f;
        cameraNoise.FrequencyGain = originalFrequency;
    }

    private float GetSavedMusicVolume()
    {
        if (YG2.saves == null)
            return 1f;

        return Mathf.Clamp01(YG2.saves.settings.musicVolume);
    }

    private void SetBossFightMusic(bool enabled)
    {
        if (enabled &&
            (victoryHandled || boss == null || boss.IsDestroyed))
        {
            return;
        }

        bossMusicRequested = enabled;

        if (musicTransitionCoroutine != null)
        {
            StopCoroutine(musicTransitionCoroutine);
            musicTransitionCoroutine = null;
        }

        musicTransitionCoroutine =
            StartCoroutine(TransitionMusicCoroutine(enabled));
    }

    private IEnumerator TransitionMusicCoroutine(bool enableBossMusic)
    {
        float duration = Mathf.Max(0.01f, musicFadeDuration);

        if (enableBossMusic)
        {
            // Плавно убираем основную музыку.
            if (mainMusicAudioSource != null)
            {
                yield return FadeAudioSource(
                    mainMusicAudioSource,
                    0f,
                    duration
                );

                if (!bossMusicRequested)
                {
                    musicTransitionCoroutine = null;
                    yield break;
                }

                mainMusicAudioSource.Stop();
            }

            // Включаем музыку боя с боссом.
            if (bossFightAudioSource != null &&
                bossMusicRequested &&
                !victoryHandled &&
                boss != null &&
                !boss.IsDestroyed)
            {
                if (!bossFightAudioSource.isPlaying)
                {
                    bossFightAudioSource.volume = 0f;
                    bossFightAudioSource.Play();
                }

                yield return FadeAudioSource(
                    bossFightAudioSource,
                    bossFightMusicVolume,
                    duration
                );
            }
        }
        else
        {
            // Сначала затухает музыка боя.
            if (bossFightAudioSource != null)
            {
                yield return FadeAudioSource(
                    bossFightAudioSource,
                    0f,
                    duration
                );

                bossFightAudioSource.Stop();
            }

            if (bossMusicRequested)
            {
                musicTransitionCoroutine = null;
                yield break;
            }

            // Возвращаем основную музыку.
            if (mainMusicAudioSource != null)
            {
                if (!mainMusicAudioSource.isPlaying)
                {
                    mainMusicAudioSource.volume = 0f;
                    mainMusicAudioSource.Play();
                }

                yield return FadeAudioSource(
                    mainMusicAudioSource,
                    GetSavedMusicVolume(),
                    duration
                );
            }
        }

        musicTransitionCoroutine = null;
    }

    private IEnumerator FadeAudioSource(
        AudioSource source,
        float targetVolume,
        float duration)
    {
        if (source == null)
            yield break;

        float startVolume = source.volume;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            if (source == null)
                yield break;

            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            source.volume = Mathf.Lerp(startVolume, targetVolume, t);

            yield return null;
        }

        if (source != null)
            source.volume = targetVolume;
    }

    public void ReviveBoss()
    {
        if (boss == null)
            return;

        // Нельзя оживить объект, который уже был Destroy(gameObject).
        // Для босса в Inspector должно быть выключено Destroy GameObject After Sinking.
        if (boss == null)
            return;

        // BlockEntity может выключить весь объект после анимации проваливания.
        // Включаем его до восстановления здоровья, коллайдера и Renderer.
        if (!boss.gameObject.activeSelf)
            boss.gameObject.SetActive(true);

        if (bossEmergenceCoroutine != null)
        {
            StopCoroutine(bossEmergenceCoroutine);
            bossEmergenceCoroutine = null;
        }

        SetBossFightMusic(false);
        StopCameraShake();

        if (introAudioSource != null)
            introAudioSource.Stop();

        if (introParticles != null)
        {
            foreach (ParticleSystem particles in introParticles)
            {
                if (particles != null)
                {
                    particles.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear
                    );
                }
            }
        }

        boss.Revive();

        // Повторно активируем объект на случай, если Revive был вызван
        // сразу после завершения анимации проваливания.
        if (!boss.gameObject.activeSelf)
            boss.gameObject.SetActive(true);

        victoryHandled = false;
        timerStarted = false;
        introPlayed = false;
        finalTime = 0f;

        // BlockEntity.Revive() восстанавливает собственную исходную позицию.
        // Здесь синхронизируем её с позицией, которую сохранила зона боя.
        if (bossInitialPositionCached)
            boss.transform.position = bossInitialPosition;

        if (bossPanel != null)
            bossPanel.SetActive(playerInside);

        UpdateHealthUI();
        UpdateRecordUI();

        if (playerInside && restartFightIfPlayerInside)
        {
            StartNewFight();

            introPlayed = true;
            PlayBossIntro();
        }
        else
        {
            // Прячем босса до следующего входа игрока.
            if (bossInitialPositionCached)
            {
                boss.transform.position =
                    GetBossEmergenceStartPosition();
            }
        }
    }
}