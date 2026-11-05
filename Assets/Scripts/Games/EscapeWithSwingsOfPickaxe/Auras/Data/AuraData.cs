
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewAura",
    menuName = "Game/Auras/Aura"
)]
public class AuraData : ScriptableObject
{
    [Header("Основное")]
    public string id = "aura_default";
    public LanguageDirectory displayName;

    [Header("Бонусы")]
    [Min(0.01f)]
    public float damageMultiplier = 1.25f;

    [Tooltip("Относительный вес выпадения. Чем меньше, тем реже.")]
    [Min(0.001f)]
    public float dropWeight = 50f;

    [Header("Оформление названия")]
    public Gradient nameGradient = new Gradient();

    [Header("Эффект игрока")]
    [Tooltip("Префаб Particle System, прикрепляемый к игроку.")]
    public ParticleSystem particlePrefab;

    [Tooltip("Локальное смещение частиц относительно игрока.")]
    public Vector3 particleOffset = Vector3.zero;

    [Tooltip("Локальный поворот эффекта.")]
    public Vector3 particleRotation = Vector3.zero;

    [Tooltip("Масштаб эффекта.")]
    [Min(0.01f)]
    public float particleScale = 1f;
}