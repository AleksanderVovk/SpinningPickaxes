using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DataInitialization : MonoBehaviour
{
    [SerializeField] private Settings _settings;

    private void Start()
    {
        Initialization();
    }

    private void Initialization()
    {
        if (_settings != null)
            _settings.InitializationSettings();
    }
}
