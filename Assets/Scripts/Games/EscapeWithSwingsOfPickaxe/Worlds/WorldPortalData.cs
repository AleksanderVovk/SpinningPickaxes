
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldPortal",
    menuName = "Game/World Portal"
)]
public class WorldPortalData : ScriptableObject
{
    [Header("Основные настройки")]
    public string worldId = "world_01";
    public LanguageDirectory worldName;

    [Header("Покупка")]
    [Min(0)] public long priceInTrophies = 0;
    public bool freeByDefault = false;

    [Header("Переход")]
    [Tooltip("Название сцены из Build Settings")]
    public string sceneName = "World1";
}