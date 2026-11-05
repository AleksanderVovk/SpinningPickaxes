using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothRotation : MonoBehaviour
{
    public Vector3 rotationAxis = new Vector3(0, 1, 0); // Ось вращения (например, Y)
    public float rotationSpeed = 50f; // Скорость вращения

    void Update()
    {
        // Вращаем объект вокруг заданной оси
        RotateContinuously();
    }

    void RotateContinuously()
    {
        // Вычисляем вращение на основе времени и скорости
        float step = (rotationSpeed * 10) * Time.deltaTime;
        Quaternion rotation = Quaternion.Euler(rotationAxis * step);

        // Применяем вращение к объекту
        transform.rotation *= rotation;
    }
}
