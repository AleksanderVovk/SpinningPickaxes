
using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class AuraSpinSystem : MonoBehaviour
{
    [Header("Стоимость")]
    [SerializeField, Min(0)] private int spinCost = 1500;

    [Header("Камеры")]
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private GameObject spinCamera;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera fovCamera;
    [SerializeField] private CinemachineBasicMultiChannelPerlin cameraNoise;

    [Header("Прокрутка - время")]
    [SerializeField, Min(0.5f)] private float spinDuration = 4f;
    [SerializeField, Min(0.01f)] private float startNameChangeInterval = 0.4f;
    [SerializeField, Min(0.01f)] private float endNameChangeInterval = 0.06f;
    [SerializeField, Min(0.1f)] private float accelerationPower = 1.8f;

    [Header("Прокрутка - FOV")]
    [Tooltip("Насколько увеличить FOV к концу прокрутки")]
    [SerializeField, Min(0f)] private float spinFovBuildUp = 8f;

    [Tooltip("Сила мелкой пульсации FOV во время прокрутки")]
    [SerializeField, Min(0f)] private float spinFovPulse = 1.2f;

    [Header("Прокрутка - тряска камеры")]
    [SerializeField, Min(0f)] private float spinNoiseStart = 0.15f;
    [SerializeField, Min(0f)] private float spinNoiseEnd = 1.5f;
    [SerializeField, Min(0f)] private float spinNoiseFrequency = 1.5f;
    [SerializeField, Min(0f)] private float spinNoiseEndFrequency = 4f;

    [Header("Выпадение ауры - эффект")]
    [Tooltip("Положительное значение увеличивает FOV")]
    [SerializeField] private float resultFovIncrease = 12f;

    [SerializeField, Min(0.01f)] private float resultFovInDuration = 0.18f;
    [SerializeField, Min(0f)] private float resultShakeAmplitude = 5f;
    [SerializeField, Min(0f)] private float resultShakeFrequency = 8f;

    [Tooltip("Сколько держится сильная тряска после выпадения")]
    [SerializeField, Min(0f)] private float resultShakeHoldDuration = 0.65f;

    [Tooltip("Время плавного возврата камеры в норму")]
    [SerializeField, Min(0.01f)] private float resultRecoveryDuration = 1.1f;

    [Header("Пауза после выпадения")]
    [SerializeField, Min(0f)] private float resultPause = 3f;

    [Header("UI прокрутки")]
    [SerializeField] private GameObject auraPanel;
    [SerializeField] private GameObject spinPanel;
    [SerializeField] private TMP_Text rollingAuraText;
    [SerializeField] private Button spinButton;
    [SerializeField] private Button toggleAuraButton;
    [SerializeField] private TMP_Text toggleAuraButtonText;

    [Header("Объекты на время спина")]
    [SerializeField] private GameObject[] objectsToHideDuringSpin;

    [Header("Анимация текста")]
    [SerializeField, Min(0f)] private float scalePulse = 0.2f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.12f;

    [Header("Звуки")]
    [SerializeField] private AudioClip _rareDrop;

    private Coroutine spinCoroutine;
    private Coroutine textAnimationCoroutine;

    private bool isSpinning;
    private bool[] previousObjectsStates;

    private bool previousMainCameraState;
    private bool previousSpinCameraState;
    private bool cameraStatesSaved;

    private Vector3 originalTextScale;
    private Color originalTextColor;

    private float originalFov;
    private bool originalFovSaved;

    private float originalNoiseAmplitude;
    private float originalNoiseFrequency;
    private bool originalNoiseSaved;

    private PlayerAuraController AuraPlayer =>
        PlayerAuraController.Instance;

    [SerializeField] private AudioClip _reseval;

    [Header("Счастливые вращения")]
    [SerializeField] private Button luckySpinButton;
    [SerializeField] private TMP_Text luckySpinsText;

    private void Awake()
    {
        if (spinPanel != null)
            spinPanel.SetActive(false);

        if (spinCamera != null)
            spinCamera.SetActive(false);

        if (rollingAuraText != null)
        {
            originalTextScale = rollingAuraText.rectTransform.localScale;
            originalTextColor = rollingAuraText.color;
        }

        InitializeCameraEffects();
    }

    private void Start()
    {
        if (spinButton != null)
            spinButton.onClick.AddListener(StartSpin);

        if (toggleAuraButton != null)
            toggleAuraButton.onClick.AddListener(ToggleAura);

        if (luckySpinButton != null)
            luckySpinButton.onClick.AddListener(StartLuckySpin);

        RefreshUI();
    }


    private void OnDestroy()
    {
        if (spinButton != null)
            spinButton.onClick.RemoveListener(StartSpin);

        if (toggleAuraButton != null)
            toggleAuraButton.onClick.RemoveListener(ToggleAura);

        if (textAnimationCoroutine != null)
            StopCoroutine(textAnimationCoroutine);

        RestoreCameraEffects();
        RestoreCameras();
        RestoreObjectsAfterSpin();
    }

    // --------------------------------------------------
    // МЕНЮ
    // --------------------------------------------------

    public void OpenAuraMenu()
    {
        if (auraPanel != null)
            auraPanel.SetActive(true);

        RefreshUI();
    }

    public void CloseAuraMenu()
    {
        if (isSpinning)
            return;

        if (auraPanel != null)
            auraPanel.SetActive(false);
    }

    // --------------------------------------------------
    // ЗАПУСК СПИНА
    // --------------------------------------------------

// Обычное вращение за кубки.
    public void StartSpin()
    {
        TryStartSpin(false);
    }

    // Счастливое вращение из накопленного запаса.
    public void StartLuckySpin()
    {
        TryStartSpin(true);
    }

    private void TryStartSpin(bool useLuckySpin)
    {
        if (isSpinning)
            return;

        if (AuraPlayer == null)
        {
            Debug.LogWarning(
                "AuraSpinSystem: PlayerAuraController не найден."
            );
            return;
        }

        if (AuraPlayer.AvailableAuras == null ||
            AuraPlayer.AvailableAuras.Length == 0)
        {
            Debug.LogWarning(
                "AuraSpinSystem: список доступных аур пуст."
            );
            return;
        }

        if (!HasValidWeights())
        {
            Debug.LogWarning(
                "AuraSpinSystem: нет аур с положительным dropWeight."
            );
            return;
        }

        if (useLuckySpin)
        {
            if (YG2.saves == null ||
                YG2.saves.playerData == null)
            {
                Debug.LogWarning(
                    "AuraSpinSystem: данные игрока ещё не загружены."
                );
                return;
            }

            YG2.saves.playerData.EnsureValidData();

            if (YG2.saves.playerData.luckySpins <= 0)
            {
                RefreshUI();
                return;
            }

            // Списываем вращение только после всех проверок.
            YG2.saves.playerData.luckySpins--;
            YG2.SaveProgress();
        }
        else
        {
            // Обычный спин по-прежнему стоит кубки.
            if (!ValueSystem.SubstractionBalance(
                    spinCost, ValueType.Wins))
            {
                RefreshUI();
                return;
            }
        }

        RefreshUI();
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    public void RefreshLuckySpinsUI()
    {
        int count = 0;

        if (YG2.saves != null &&
            YG2.saves.playerData != null)
        {
            YG2.saves.playerData.EnsureValidData();
            count = Mathf.Max(
                0,
                YG2.saves.playerData.luckySpins
            );
        }

        if (luckySpinsText != null)
            luckySpinsText.text = "(У вас есть " + count + ")";

        if (luckySpinButton != null)
            luckySpinButton.interactable = !isSpinning && count > 0;
    }


    private IEnumerator SpinRoutine()
    {
        isSpinning = true;

        GlobalSound.instance.PlaySound_Custom(_reseval, false);

        HideObjectsForSpin();
        SaveAndSwitchCameras();

        if (spinPanel != null)
            spinPanel.SetActive(true);

        if (rollingAuraText != null)
        {
            rollingAuraText.gameObject.SetActive(true);
            rollingAuraText.rectTransform.localScale = originalTextScale;
        }

        // Обновляем исходные значения после переключения камеры.
        CaptureCameraEffects();

        RefreshUI();

        float elapsed = 0f;
        float nextNameChange = 0f;

        // Прокрутка постепенно ускоряется.
        while (elapsed < spinDuration)
        {
            float progress = Mathf.Clamp01(elapsed / spinDuration);

            // Чем ближе конец, тем быстрее смена названий.
            float acceleratedProgress = Mathf.Pow(
                progress,
                Mathf.Max(0.1f, accelerationPower)
            );

            float currentInterval = Mathf.Lerp(
                startNameChangeInterval,
                endNameChangeInterval,
                acceleratedProgress
            );

            if (elapsed >= nextNameChange)
            {
                AuraData randomAura = GetRandomWeightedAura();

                if (randomAura != null)
                {
                    SetAnimatedAuraText(randomAura);
                    PlaySpinSound();
                }

                nextNameChange = elapsed + currentInterval;
            }

            // Плавное приближение по мере прокрутки.
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            float pulse = Mathf.Sin(elapsed * 12f) * spinFovPulse;

            float currentFov =
                originalFov +
                spinFovBuildUp * smoothProgress +
                pulse * smoothProgress;

            SetCinemachineFov(currentFov);

            // Тряска постепенно усиливается.
            float currentAmplitude = Mathf.Lerp(
                spinNoiseStart,
                spinNoiseEnd,
                smoothProgress
            );

            float currentFrequency = Mathf.Lerp(
                spinNoiseFrequency,
                spinNoiseEndFrequency,
                smoothProgress
            );

            SetCameraNoise(currentAmplitude, currentFrequency);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Фиксируем усиление FOV перед выпадением результата.
        SetCinemachineFov(originalFov + spinFovBuildUp);

        // Финальный звук.
        PlayRareDropSound();

        // Выбираем итоговую ауру.
        AuraData result = GetRandomWeightedAura();

        if (result != null)
        {
            AuraPlayer.SetNewAura(result);
            SetAnimatedAuraText(result);

            // Сильный удар камеры и увеличение FOV.
            yield return StartCoroutine(PlayResultImpact());
        }

        // Держим результат на экране.
        yield return new WaitForSecondsRealtime(resultPause);

        FinishSpin();
    }

    // --------------------------------------------------
    // ФИНАЛЬНЫЙ УДАР КАМЕРЫ
    // --------------------------------------------------

    private IEnumerator PlayResultImpact()
    {
        float startFov = fovCamera != null
            ? fovCamera.Lens.FieldOfView
            : originalFov;

        float targetFov = Mathf.Clamp(
            originalFov + resultFovIncrease,
            1f,
            179f
        );

        float startAmplitude = spinNoiseEnd;
        float startFrequency = spinNoiseEndFrequency;

        // Быстро, но плавно усиливаем приближение и тряску.
        float elapsed = 0f;
        float inDuration = Mathf.Max(0.01f, resultFovInDuration);

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / inDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetCinemachineFov(
                Mathf.Lerp(startFov, targetFov, smoothT)
            );

            SetCameraNoise(
                Mathf.Lerp(startAmplitude, resultShakeAmplitude, smoothT),
                Mathf.Lerp(startFrequency, resultShakeFrequency, smoothT)
            );

            yield return null;
        }

        SetCinemachineFov(targetFov);
        SetCameraNoise(resultShakeAmplitude, resultShakeFrequency);

        // Небольшая сильная тряска в момент получения ауры.
        if (resultShakeHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(resultShakeHoldDuration);

        // Плавно возвращаем камеру в исходное состояние.
        elapsed = 0f;

        float recoveryStartFov = fovCamera != null
            ? fovCamera.Lens.FieldOfView
            : targetFov;

        while (elapsed < resultRecoveryDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / resultRecoveryDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetCinemachineFov(
                Mathf.Lerp(recoveryStartFov, originalFov, smoothT)
            );

            SetCameraNoise(
                Mathf.Lerp(resultShakeAmplitude, originalNoiseAmplitude, smoothT),
                Mathf.Lerp(resultShakeFrequency, originalNoiseFrequency, smoothT)
            );

            yield return null;
        }

        RestoreCameraEffects();
    }

    // --------------------------------------------------
    // НАСТРОЙКА CINEMACHINE
    // --------------------------------------------------

    private void InitializeCameraEffects()
    {
        // Сначала ищем камеру спина.
        if (fovCamera == null && spinCamera != null)
        {
            fovCamera =
                spinCamera.GetComponentInChildren<CinemachineCamera>(true);
        }

        // Затем основную виртуальную камеру.
        if (fovCamera == null && mainCamera != null)
        {
            fovCamera =
                mainCamera.GetComponentInChildren<CinemachineCamera>(true);
        }

        if (fovCamera == null)
        {
            Debug.LogError(
                "AuraSpinSystem: CinemachineCamera не найдена. " +
                "Назначь Fov Camera в Inspector."
            );

            return;
        }

        if (cameraNoise == null)
        {
            cameraNoise =
                fovCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        originalFov = fovCamera.Lens.FieldOfView;
        originalFovSaved = true;

        if (cameraNoise != null)
        {
            originalNoiseAmplitude = cameraNoise.AmplitudeGain;
            originalNoiseFrequency = cameraNoise.FrequencyGain;
            originalNoiseSaved = true;
        }
        else
        {
            Debug.LogWarning(
                "AuraSpinSystem: на Fov Camera нет " +
                "CinemachineBasicMultiChannelPerlin. " +
                "Добавь Noise extension для тряски."
            );
        }
    }

    private void CaptureCameraEffects()
    {
        if (fovCamera != null)
        {
            originalFov = fovCamera.Lens.FieldOfView;
            originalFovSaved = true;
        }

        if (cameraNoise != null)
        {
            originalNoiseAmplitude = cameraNoise.AmplitudeGain;
            originalNoiseFrequency = cameraNoise.FrequencyGain;
            originalNoiseSaved = true;
        }
    }

    private void SetCinemachineFov(float value)
    {
        if (fovCamera == null)
            return;

        var lens = fovCamera.Lens;
        lens.FieldOfView = Mathf.Clamp(value, 1f, 179f);
        fovCamera.Lens = lens;
    }

    private void SetCameraNoise(float amplitude, float frequency)
    {
        if (cameraNoise == null)
            return;

        cameraNoise.AmplitudeGain = amplitude;
        cameraNoise.FrequencyGain = frequency;
    }

    private void RestoreCameraEffects()
    {
        if (fovCamera != null && originalFovSaved)
            SetCinemachineFov(originalFov);

        if (cameraNoise != null && originalNoiseSaved)
        {
            cameraNoise.AmplitudeGain = originalNoiseAmplitude;
            cameraNoise.FrequencyGain = originalNoiseFrequency;
        }
    }

    // --------------------------------------------------
    // ЗВУКИ
    // --------------------------------------------------

    private void PlaySpinSound()
    {
        if (GlobalSound.instance != null)
            GlobalSound.instance.PlaySound_ShortPop(false);
    }

    private void PlayRareDropSound()
    {
        if (GlobalSound.instance != null && _rareDrop != null)
            GlobalSound.instance.PlaySound_Custom(_rareDrop, false);
    }

    // --------------------------------------------------
    // АНИМАЦИЯ НАЗВАНИЯ АУРЫ
    // --------------------------------------------------

    private void SetAnimatedAuraText(AuraData aura)
    {
        if (rollingAuraText == null || aura == null)
            return;

        if (textAnimationCoroutine != null)
        {
            StopCoroutine(textAnimationCoroutine);
            textAnimationCoroutine = null;
        }

        rollingAuraText.text =
            UTF.ReadLanguageDirectory(aura.displayName);

        ApplyGradient(rollingAuraText, aura.nameGradient);

        textAnimationCoroutine =
            StartCoroutine(AnimateTextChange());
    }

    private IEnumerator AnimateTextChange()
    {
        RectTransform rect = rollingAuraText.rectTransform;

        float elapsed = 0f;

        Vector3 startScale =
            originalTextScale * (1f + scalePulse);

        Color startColor = originalTextColor;
        startColor.a = 0f;

        rollingAuraText.color = startColor;
        rect.localScale = startScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / animationDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            rect.localScale = Vector3.Lerp(
                startScale,
                originalTextScale,
                smoothT
            );

            rollingAuraText.color = Color.Lerp(
                startColor,
                originalTextColor,
                smoothT
            );

            yield return null;
        }

        rect.localScale = originalTextScale;
        rollingAuraText.color = originalTextColor;
        textAnimationCoroutine = null;
    }

    // --------------------------------------------------
    // ЗАВЕРШЕНИЕ СПИНА
    // --------------------------------------------------

    private void FinishSpin()
    {
        if (textAnimationCoroutine != null)
        {
            StopCoroutine(textAnimationCoroutine);
            textAnimationCoroutine = null;
        }

        if (rollingAuraText != null)
        {
            rollingAuraText.rectTransform.localScale = originalTextScale;
            rollingAuraText.color = originalTextColor;
        }

        if (spinPanel != null)
            spinPanel.SetActive(false);

        RestoreCameraEffects();
        RestoreCameras();
        RestoreObjectsAfterSpin();

        isSpinning = false;
        spinCoroutine = null;

        RefreshUI();
    }

    // --------------------------------------------------
    // ОБЪЕКТЫ НА ВРЕМЯ СПИНА
    // --------------------------------------------------

    private void HideObjectsForSpin()
    {
        if (objectsToHideDuringSpin == null)
            return;

        previousObjectsStates =
            new bool[objectsToHideDuringSpin.Length];

        for (int i = 0; i < objectsToHideDuringSpin.Length; i++)
        {
            GameObject obj = objectsToHideDuringSpin[i];

            if (obj == null)
                continue;

            previousObjectsStates[i] = obj.activeSelf;
            obj.SetActive(false);
        }
    }

    private void RestoreObjectsAfterSpin()
    {
        if (objectsToHideDuringSpin == null ||
            previousObjectsStates == null)
            return;

        for (int i = 0; i < objectsToHideDuringSpin.Length; i++)
        {
            GameObject obj = objectsToHideDuringSpin[i];

            if (obj == null)
                continue;

            obj.SetActive(previousObjectsStates[i]);
        }

        previousObjectsStates = null;
    }

    // --------------------------------------------------
    // ПЕРЕКЛЮЧЕНИЕ КАМЕР
    // --------------------------------------------------

    private void SaveAndSwitchCameras()
    {
        if (cameraStatesSaved)
            return;

        previousMainCameraState =
            mainCamera != null && mainCamera.activeSelf;

        previousSpinCameraState =
            spinCamera != null && spinCamera.activeSelf;

        cameraStatesSaved = true;

        if (mainCamera != null)
            mainCamera.SetActive(false);

        if (spinCamera != null)
            spinCamera.SetActive(true);
    }

    private void RestoreCameras()
    {
        if (!cameraStatesSaved)
            return;

        if (mainCamera != null)
            mainCamera.SetActive(previousMainCameraState);

        if (spinCamera != null)
            spinCamera.SetActive(previousSpinCameraState);

        cameraStatesSaved = false;
    }

    // --------------------------------------------------
    // ВЫБОР АУРЫ ПО ВЕСАМ
    // --------------------------------------------------

    private AuraData GetRandomWeightedAura()
    {
        AuraData[] auras =
            AuraPlayer != null ? AuraPlayer.AvailableAuras : null;

        if (auras == null || auras.Length == 0)
            return null;

        float totalWeight = 0f;

        foreach (AuraData aura in auras)
        {
            if (aura != null)
                totalWeight += Mathf.Max(0f, aura.dropWeight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);

        foreach (AuraData aura in auras)
        {
            if (aura == null || aura.dropWeight <= 0f)
                continue;

            roll -= aura.dropWeight;

            if (roll <= 0f)
                return aura;
        }

        for (int i = auras.Length - 1; i >= 0; i--)
        {
            if (auras[i] != null && auras[i].dropWeight > 0f)
                return auras[i];
        }

        return null;
    }

    private bool HasValidWeights()
    {
        if (AuraPlayer == null || AuraPlayer.AvailableAuras == null)
            return false;

        foreach (AuraData aura in AuraPlayer.AvailableAuras)
        {
            if (aura != null && aura.dropWeight > 0f)
                return true;
        }

        return false;
    }

    // --------------------------------------------------
    // ВКЛЮЧЕНИЕ / ОТКЛЮЧЕНИЕ АУРЫ
    // --------------------------------------------------

    public void ToggleAura()
    {
        if (isSpinning ||
            AuraPlayer == null ||
            AuraPlayer.CurrentAura == null)
            return;

        AuraPlayer.ToggleAura();
        RefreshUI();
    }

    // --------------------------------------------------
    // UI
    // --------------------------------------------------

    private void RefreshUI()
    {
        if (rollingAuraText != null && !isSpinning)
        {
            if (AuraPlayer != null && AuraPlayer.CurrentAura != null)
            {
                rollingAuraText.text =
                    UTF.ReadLanguageDirectory(
                        AuraPlayer.CurrentAura.displayName
                    );

                ApplyGradient(
                    rollingAuraText,
                    AuraPlayer.CurrentAura.nameGradient
                );
            }
            else
            {
                rollingAuraText.text = "Нет ауры";
            }
        }

        if (toggleAuraButtonText != null)
        {
            if (AuraPlayer == null || AuraPlayer.CurrentAura == null)
            {
                toggleAuraButtonText.text = "Нет ауры";
            }
            else
            {
                toggleAuraButtonText.text = AuraPlayer.AuraEnabled
                    ? "Отключить ауру"
                    : "Включить ауру";
            }
        }

        if (toggleAuraButton != null)
        {
            toggleAuraButton.interactable =
                !isSpinning &&
                AuraPlayer != null &&
                AuraPlayer.CurrentAura != null;
        }

        RefreshSpinButton();
        RefreshLuckySpinsUI();
    }

    private void RefreshSpinButton()
    {
        if (spinButton != null)
            spinButton.interactable = !isSpinning;

        RefreshLuckySpinsUI();
    }

    // --------------------------------------------------
    // ГРАДИЕНТ ТЕКСТА
    // --------------------------------------------------

    private void ApplyGradient(TMP_Text text, Gradient gradient)
    {
        if (text == null || gradient == null)
            return;

        Color top = gradient.Evaluate(1f);
        Color bottom = gradient.Evaluate(0f);

        text.enableVertexGradient = true;

        text.colorGradient = new VertexGradient(
            top,
            top,
            bottom,
            bottom
        );
    }
}