using UnityEngine;

public class MobileFrameRateManager : MonoBehaviour
{
    public enum MobileFrameRateMode
    {
        Native,
        Fps120,
        Fps90,
        Fps60,
        Fps30
    }

    [Header("Frame Rate")]
    [SerializeField] private MobileFrameRateMode frameRateMode = MobileFrameRateMode.Fps60;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog;

    private void Awake()
    {
        ApplyFrameRate();
    }

    private void OnEnable()
    {
        // На некоторых Android-устройствах значение может сбрасываться после загрузки сцены.
        ApplyFrameRate();
    }

    public void ApplyFrameRate()
    {
        int targetFps = GetTargetFrameRate();

        Application.targetFrameRate = targetFps;

        if (showDebugLog)
            Debug.Log($"[MobileFrameRateManager] Target FPS: {targetFps}. " +
                      $"Display: {GetDisplayRefreshRate():0.#} Hz.", this);
    }

    public void SetFrameRateMode(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex > (int)MobileFrameRateMode.Fps30)
            return;

        frameRateMode = (MobileFrameRateMode)modeIndex;
        ApplyFrameRate();
    }

    public void SetFrameRateMode(MobileFrameRateMode mode)
    {
        frameRateMode = mode;
        ApplyFrameRate();
    }

    public void SetFps60()
    {
        SetFrameRateMode(MobileFrameRateMode.Fps60);
    }

    public void SetFps30()
    {
        SetFrameRateMode(MobileFrameRateMode.Fps30);
    }

    public void SetNativeFps()
    {
        SetFrameRateMode(MobileFrameRateMode.Native);
    }

    private int GetTargetFrameRate()
    {
        int displayFps = GetDisplayRefreshRate();

        switch (frameRateMode)
        {
            case MobileFrameRateMode.Native:
                return displayFps;

            case MobileFrameRateMode.Fps120:
                return GetBestSupportedFps(120, displayFps);

            case MobileFrameRateMode.Fps90:
                return GetBestSupportedFps(90, displayFps);

            case MobileFrameRateMode.Fps60:
                return GetBestSupportedFps(60, displayFps);

            case MobileFrameRateMode.Fps30:
                return GetBestSupportedFps(30, displayFps);

            default:
                return 60;
        }
    }

    private int GetBestSupportedFps(int desiredFps, int displayFps)
    {
        // На 60 Гц нельзя честно получить 90 или 120 FPS.
        return Mathf.Min(desiredFps, displayFps);
    }

    private int GetDisplayRefreshRate()
    {
        int fps = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);

        if (fps < 30)
            fps = 60;

        return fps;
    }
}