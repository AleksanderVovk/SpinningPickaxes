using UnityEngine;

public class PlayerTrailController : MonoBehaviour
{
    public static PlayerTrailController Instance { get; private set; }

    [Header("След игрока")]
    [SerializeField] private TrailRenderer trailRenderer;

    [Header("Следы")]
    [SerializeField] private TrailData[] availableTrails;

    private string currentTrailId = string.Empty;

    public string CurrentTrailId => currentTrailId;
    public TrailRenderer Trail => trailRenderer;
    public TrailData[] AvailableTrails => availableTrails;

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
        string savedId = YG.YG2.saves.playerData.equippedTrailId;

        if (!string.IsNullOrEmpty(savedId))
            EquipTrail(savedId);
        else
            UnequipTrail(false);
    }

    public TrailData GetTrail(string id)
    {
        if (availableTrails == null)
            return null;

        foreach (TrailData trail in availableTrails)
        {
            if (trail != null && trail.id == id)
                return trail;
        }

        return null;
    }

    public void EquipTrail(string id)
    {
        TrailData data = GetTrail(id);

        if (data == null)
        {
            Debug.LogWarning($"След с ID '{id}' не найден.", this);
            return;
        }

        if (trailRenderer == null)
        {
            Debug.LogWarning("TrailRenderer не назначен в PlayerTrailController.", this);
            return;
        }

        var playerData = YG.YG2.saves.playerData;

        if (!playerData.boughtTrails.Contains(id))
        {
            Debug.LogWarning($"След '{id}' ещё не куплен.", this);
            return;
        }

        currentTrailId = id;

        ApplyTrailVisual(data);

        // Убираем старую линию, чтобы новый след начался чисто.
        trailRenderer.Clear();

        if (PlayerDamageSystem.Instance != null)
            PlayerDamageSystem.Instance.SetTrailMultiplier(data.damageMultiplier);

        playerData.equippedTrailId = id;
        YG.YG2.SaveProgress();
    }

    public void UnequipTrail(bool save = true)
    {
        currentTrailId = string.Empty;

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.enabled = false;
        }

        if (PlayerDamageSystem.Instance != null)
            PlayerDamageSystem.Instance.SetTrailMultiplier(1f);

        if (save && YG.YG2.saves != null &&
            YG.YG2.saves.playerData != null)
        {
            YG.YG2.saves.playerData.equippedTrailId = string.Empty;
            YG.YG2.SaveProgress();
        }
    }

    private void ApplyTrailVisual(TrailData data)
    {
        if (trailRenderer == null)
            return;

        trailRenderer.enabled = true;

        if (data.material != null)
        {
            trailRenderer.material = data.material;

            if (trailRenderer.material.HasProperty("_BaseColor"))
                trailRenderer.material.SetColor("_BaseColor", Color.white);

            if (trailRenderer.material.HasProperty("_Color"))
                trailRenderer.material.SetColor("_Color", Color.white);

            if (trailRenderer.material.HasProperty("_TintColor"))
                trailRenderer.material.SetColor("_TintColor", Color.white);
        }

        trailRenderer.colorGradient = CreateGradient(
            data.color1,
            data.color2,
            data.color3,
            data.color4
        );

        trailRenderer.Clear();
    }

    private Gradient CreateGradient(Color color1, Color color2, Color color3, Color color4)
    {
        color1.a = 1f;
        color2.a = 1f;
        color3.a = 1f;
        color4.a = 1f;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(color1, 0.00f),
                new GradientColorKey(color2, 0.33f),
                new GradientColorKey(color3, 0.66f),
                new GradientColorKey(color4, 1.00f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}