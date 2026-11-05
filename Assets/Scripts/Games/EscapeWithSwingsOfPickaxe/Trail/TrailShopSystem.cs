using System.Collections.Generic;
using UnityEngine;
using YG;

public class TrailShopView : MonoBehaviour
{
    [Header("Данные магазина")]
    [SerializeField] private List<TrailData> trails = new List<TrailData>();

    [Header("UI")]
    [SerializeField] private TrailShopItemView itemPrefab;
    [SerializeField] private Transform contentParent;

    private readonly List<TrailShopItemView> spawnedItems = new();

    private void OnEnable()
    {
        ValueSystem.OnBalanceChanged += RefreshAll;
        TrailInAppPurchase.OnTrailPurchased += RefreshAll;
    }

    private void Start()
    {
        SpawnItems();
        RefreshAll();
    }

    private void OnDisable()
    {
        ValueSystem.OnBalanceChanged -= RefreshAll;
        TrailInAppPurchase.OnTrailPurchased -= RefreshAll;
    }

    public void SpawnItems()
    {
        foreach (TrailShopItemView item in spawnedItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        spawnedItems.Clear();

        if (itemPrefab == null || contentParent == null)
        {
            Debug.LogError("TrailShopView: не назначен Item Prefab или Content Parent.", this);
            return;
        }

        foreach (TrailData trail in trails)
        {
            if (trail == null)
                continue;

            TrailShopItemView item = Instantiate(itemPrefab, contentParent);
            item.Initialize(trail, RefreshAll);
            spawnedItems.Add(item);
        }
    }

    public void RefreshAll()
    {
        foreach (TrailShopItemView item in spawnedItems)
        {
            if (item != null)
                item.Refresh();
        }
    }
}
