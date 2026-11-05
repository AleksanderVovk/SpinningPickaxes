using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class Settings : MonoBehaviour
{
    public float SENSITIVITY;
    public float VOLUME;
    public float MUSICVOLUME;

    public Slider sensitivity_slider;
    public Slider volume_slider;
    public Slider musicVolume_slider;

    [SerializeField] private PlayerCharacteristic _playerCharacteristics;
    [SerializeField] private AudioSource _musicSource;

    public void InitializationSettings()
    {
        SENSITIVITY = YG2.saves.settings.sensitivity;
        VOLUME = YG2.saves.settings.volume;
        MUSICVOLUME = YG2.saves.settings.musicVolume;

        InitializationSliders();

        // SET
        SetNewSettings();
    }

    public void InitializationSliders()
    {
        sensitivity_slider.value = YG2.saves.settings.sensitivity;
        volume_slider.value = YG2.saves.settings.volume;
        musicVolume_slider.value = YG2.saves.settings.musicVolume;
    }

    public void SaveSettings()
    {
        WriteNewSettings();
        SetNewSettings();

        YG2.saves.settings.sensitivity = SENSITIVITY;
        YG2.saves.settings.volume = VOLUME;
        YG2.saves.settings.musicVolume = MUSICVOLUME;

        YG2.SaveProgress();
    }

    public void SetNewSettings()
    {
        _playerCharacteristics.CAMERA_SENSITIVITY = SENSITIVITY;

        AudioListener.volume = VOLUME;

        if (_musicSource != null)
            _musicSource.volume = MUSICVOLUME;
    }

    public void WriteNewSettings()
    {
        SENSITIVITY = sensitivity_slider.value;
        VOLUME = volume_slider.value;
        MUSICVOLUME = musicVolume_slider.value;
    }
}
