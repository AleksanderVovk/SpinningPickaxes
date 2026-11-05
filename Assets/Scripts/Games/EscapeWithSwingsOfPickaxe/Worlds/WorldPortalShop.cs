
using UnityEngine;
using UnityEngine.SceneManagement;
using YG;

public class WorldPortalShop : MonoBehaviour
{
    public static WorldPortalShop Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        EnsurePlayerData();

        // Первый мир должен быть доступен бесплатно.
        // Бесплатные миры также сохраняются как открытые.
        WorldPortalView[] portals =
            FindObjectsByType<WorldPortalView>(
                FindObjectsSortMode.None
            );

        foreach (WorldPortalView portal in portals)
        {
            if (portal != null)
                RegisterFreeWorld(portal.WorldData);
        }

        RefreshAllPortals();
    }

    private bool EnsurePlayerData()
    {
        if (YG2.saves == null)
            return false;

        if (YG2.saves.playerData == null)
            YG2.saves.playerData = new PlayerData();

        YG2.saves.playerData.EnsureValidData();

        return YG2.saves.playerData.purchasedWorldIds != null;
    }

    public bool IsWorldUnlocked(WorldPortalData world)
    {
        if (world == null || !EnsurePlayerData())
            return false;

        if (world.freeByDefault)
            return true;

        return YG2.saves.playerData.purchasedWorldIds
            .Contains(world.worldId);
    }

    private void RegisterFreeWorld(WorldPortalData world)
    {
        if (world == null || !world.freeByDefault)
            return;

        if (!EnsurePlayerData())
            return;

        var purchased = YG2.saves.playerData.purchasedWorldIds;

        if (!purchased.Contains(world.worldId))
        {
            purchased.Add(world.worldId);
            YG2.SaveProgress();
        }
    }

    public bool TryPurchaseWorld(WorldPortalData world)
    {
        if (world == null || string.IsNullOrWhiteSpace(world.worldId))
            return false;

        if (!EnsurePlayerData())
            return false;

        if (IsWorldUnlocked(world))
            return true;

        long price = System.Math.Max(0L, world.priceInTrophies);

        if (ValueSystem.SubstractionBalance(price, ValueType.Wins))
    {      
            if (!YG2.saves.playerData.purchasedWorldIds.Contains(world.worldId))
                YG2.saves.playerData.purchasedWorldIds.Add(world.worldId);


            YG2.SaveProgress();

            RefreshAllPortals();

            GlobalSound.instance.PlaySound_BuyTheme1(false);

            Debug.Log($"Мир «{world.worldName}» куплен!");
        }
        return true;
    }

    public void EnterWorld(WorldPortalData world)
    {
        if (world == null)
            return;

        if (!IsWorldUnlocked(world))
        {
            Debug.LogWarning(
                $"Мир «{world.worldName}» ещё не куплен."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(world.sceneName))
        {
            Debug.LogError(
                $"У мира «{world.worldName}» не указано имя сцены."
            );
            return;
        }

        SceneManager.LoadScene(world.sceneName);
    }

    public void RefreshAllPortals()
    {
        WorldPortalView[] portals =
            FindObjectsByType<WorldPortalView>(
                FindObjectsSortMode.None
            );

        foreach (WorldPortalView portal in portals)
        {
            if (portal != null)
                portal.RefreshUI();
        }
    }
}