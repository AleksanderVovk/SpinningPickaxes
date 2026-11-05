using HayPileSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Unity.Profiling;

namespace HayPileSystem
{
    public enum HayMeshAxis
    {
        X,
        Y,
        Z
    }

    public class HayPileGenerator : MonoBehaviour
    {
        // ============================================================
        // SOURCE
        // ============================================================

        [Header("SOURCE")]

        private static readonly ProfilerMarker ReserveMarker =
    new ProfilerMarker("Hay/ActivateReserve");

        private static readonly ProfilerMarker CollapseMarker =
            new ProfilerMarker("Hay/SingleCollapse");

        private static readonly ProfilerMarker HoleFillMarker =
            new ProfilerMarker("Hay/HoleFilling");

        private static readonly ProfilerMarker ColliderMarker =
            new ProfilerMarker("Hay/ColliderRebuild");

        [SerializeField]
        private HayStraw strawPrefab;

        [SerializeField]
        private Transform generatedRoot;

        // ============================================================
        // PILE SHAPE
        // ============================================================

        [Header("PILE SHAPE")]

        [Min(1)]
        [SerializeField]
        private int strawCount = 2200;

        [Min(0.1f)]
        [SerializeField]
        private float radius = 6f;

        [Min(0.1f)]
        [SerializeField]
        private float height = 4.2f;

        [SerializeField]
        private float groundY = 0f;

        // ============================================================
        // HAY DENSITY
        // ============================================================

        [Header("HAY DENSITY")]

        [Min(1)]
        [SerializeField]
        private int surfaceLayers = 5;

        [Min(0.01f)]
        [SerializeField]
        private float shellLayerSpacing = 0.14f;

        [Min(0f)]
        [SerializeField]
        private float shellLayerJitter = 0.04f;

        [Range(0f, 1f)]
        [SerializeField]
        private float innerFill = 0.38f;

        [Min(0f)]
        [SerializeField]
        private float surfaceJitter = 0.045f;

        // ============================================================
        // RESERVE STRAWS
        // ============================================================

        [Header("RESERVE STRAWS")]

        [Min(0)]
        [SerializeField]
        private int reserveStrawCount = 1800;

        [Min(0)]
        [SerializeField]
        private int reservePerDig = 8;

        [Range(0.1f, 0.95f)]
        [SerializeField]
        private float reserveInnerRadiusFactor = 0.78f;

        [Min(0.1f)]
        [SerializeField]
        private float reserveSearchRadius = 3.5f;

        // ============================================================
        // PROFILE
        // ============================================================

        [Header("PILE PROFILE")]

        [Min(0.01f)]
        [SerializeField]
        private float pileProfileExponent = 0.58f;

        [Min(0f)]
        [SerializeField]
        private float surfaceDepth = 0.28f;

        // ============================================================
        // SCALE
        // ============================================================

        [Header("SCALE")]

        [SerializeField]
        private Vector2 scaleRange =
            new Vector2(0.90f, 1.10f);

        // ============================================================
        // ROTATION
        // ============================================================

        [Header("ROTATION")]

        [SerializeField]
        private HayMeshAxis meshLongAxis =
            HayMeshAxis.X;

        [Range(0f, 180f)]
        [SerializeField]
        private float slopeRandomAngle = 55f;

        [Range(0f, 30f)]
        [SerializeField]
        private float surfaceTilt = 9f;

        [Range(0f, 180f)]
        [SerializeField]
        private float rollRandomAngle = 180f;

        [SerializeField]
        private Vector3 baseRotation = Vector3.zero;

        [Range(0f, 1f)]
        [SerializeField]
        private float innerRandomness = 0.72f;

        // ============================================================
        // RANDOMIZATION
        // ============================================================

        [Header("RANDOMIZATION")]

        [SerializeField]
        private int seed = 12345;

        [SerializeField]
        private bool randomSeedOnGenerate = false;

        [Range(0f, 0.5f)]
        [SerializeField]
        private float positionJitter = 0.055f;

        // ============================================================
        // LOCAL DIG
        // ============================================================

        [Header("LOCAL DIG")]

        [Min(0.1f)]
        [SerializeField]
        private float digRadius = 0.78f;

        [Min(0f)]
        [SerializeField]
        private float digDepth = 0.45f;

        [Min(0f)]
        [SerializeField]
        private float digPull = 0.18f;

        [Min(0.1f)]
        [SerializeField]
        private float digFalloff = 1.55f;

        [Min(0.01f)]
        [SerializeField]
        private float digVerticalRange = 1.15f;

        // ============================================================
        // LOCAL COLLAPSE
        // ============================================================

        [Header("LOCAL COLLAPSE")]

        [Min(0.1f)]
        [SerializeField]
        private float collapseRadius = 1.8f;

        [Min(0.1f)]
        [SerializeField]
        private float collapseHeight = 3.2f;

        [Min(0f)]
        [SerializeField]
        private float collapseDepth = 0.62f;

        [Min(0f)]
        [SerializeField]
        private float collapsePull = 0.32f;

        [Range(0f, 1f)]
        [SerializeField]
        private float collapseStrength = 0.95f;

        [Range(0f, 1f)]
        [SerializeField]
        private float collapseLateralStrength = 0.85f;

        [Range(0f, 1f)]
        [SerializeField]
        private float collapseRandomness = 0.18f;

        // ============================================================
        // SECONDARY COLLAPSE
        // ============================================================

        [Header("SECONDARY COLLAPSE")]

        [Min(0f)]
        [SerializeField]
        private float secondaryCollapseDepth = 0.24f;

        [Min(0.1f)]
        [SerializeField]
        private float secondaryCollapseRadius = 2.5f;

        [Min(0.1f)]
        [SerializeField]
        private float secondaryCollapseHeight = 4f;

        [Range(0f, 1f)]
        [SerializeField]
        private float secondaryCollapseStrength = 0.38f;

        // ============================================================
        // HOLE FILLING
        // ============================================================

        [Header("HOLE FILLING")]

        [Tooltip(
            "Р Р°РґРёСѓСЃ РѕР±Р»Р°СЃС‚Рё, РёР· РєРѕС‚РѕСЂРѕР№ СЃРѕР»РѕРјРёРЅРєРё РјРѕРіСѓС‚ " +
            "РїРѕРґС‚СЏРіРёРІР°С‚СЊСЃСЏ РґР»СЏ Р·Р°РєСЂС‹С‚РёСЏ РґС‹СЂРєРё."
        )]
        [Min(0.1f)]
        [SerializeField]
        private float holeFillRadius = 1.85f;

        [Tooltip(
            "РњР°РєСЃРёРјР°Р»СЊРЅР°СЏ РіР»СѓР±РёРЅР° РІРµСЂС‚РёРєР°Р»СЊРЅРѕРіРѕ РїР°РґРµРЅРёСЏ."
        )]
        [Min(0f)]
        [SerializeField]
        private float holeFillDepth = 0.82f;

        [Tooltip(
            "РћР±С‰Р°СЏ СЃРёР»Р° Р·Р°РїРѕР»РЅРµРЅРёСЏ."
        )]
        [Range(0f, 2f)]
        [SerializeField]
        private float holeFillStrength = 1.15f;

        [Tooltip(
            "РњР°РєСЃРёРјР°Р»СЊРЅР°СЏ РІС‹СЃРѕС‚Р°, РЅР° РєРѕС‚РѕСЂРѕР№ РёС‰СѓС‚СЃСЏ СЃРѕР»РѕРјРёРЅРєРё."
        )]
        [Min(0.1f)]
        [SerializeField]
        private float holeFillHeight = 4.5f;

        [Tooltip(
            "РњРёРЅРёРјР°Р»СЊРЅР°СЏ РІС‹СЃРѕС‚Р° СЃРѕР»РѕРјРёРЅРєРё РЅР°Рґ С†РµРЅС‚СЂРѕРј РѕС‚РІРµСЂСЃС‚РёСЏ."
        )]
        [Min(0f)]
        [SerializeField]
        private float holeFillMinHeight = 0.08f;

        [Tooltip(
            "РЎРёР»Р° РіРѕСЂРёР·РѕРЅС‚Р°Р»СЊРЅРѕРіРѕ РїРѕРґС‚СЏРіРёРІР°РЅРёСЏ Рє С†РµРЅС‚СЂСѓ."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float holeFillPull = 0.34f;

        [Tooltip(
            "Р Р°РґРёР°Р»СЊРЅР°СЏ СЃС‚РµРїРµРЅСЊ РІР»РёСЏРЅРёСЏ."
        )]
        [Range(0.5f, 4f)]
        [SerializeField]
        private float holeFillRadialPower = 1.35f;

        [Tooltip(
            "Р’РµСЃ РІРµСЂС…РЅРµР№ С‡Р°СЃС‚Рё Р»РѕРєР°Р»СЊРЅРѕРіРѕ СЃР»РѕСЏ."
        )]
        [Range(0f, 2f)]
        [SerializeField]
        private float holeFillUpperWeight = 1.25f;

        [Tooltip(
            "Р’РµСЃ РЅРёР¶РЅРµР№ С‡Р°СЃС‚Рё Р»РѕРєР°Р»СЊРЅРѕРіРѕ СЃР»РѕСЏ."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float holeFillLowerWeight = 0.35f;

        [Tooltip(
            "РњР°РєСЃРёРјР°Р»СЊРЅРѕРµ РіРѕСЂРёР·РѕРЅС‚Р°Р»СЊРЅРѕРµ РґРІРёР¶РµРЅРёРµ " +
            "РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ РІРµСЂС‚РёРєР°Р»СЊРЅРѕРіРѕ РїР°РґРµРЅРёСЏ."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float holeFillMaxLateralRatio = 0.72f;

        [Tooltip(
            "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕРµ РїРµСЂРµРєСЂС‹С‚РёРµ РѕС‚РІРµСЂСЃС‚РёСЏ. " +
            "РџРѕР·РІРѕР»СЏРµС‚ СЃРѕР»РѕРјРёРЅРєР°Рј РЅРµРјРЅРѕРіРѕ Р·Р°С…РѕРґРёС‚СЊ РґСЂСѓРі РЅР° РґСЂСѓРіР°."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float holeFillOverlap = 0.35f;

        [Tooltip(
            "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅР°СЏ СЃРёР»Р° Р·Р°РІР°Р»Р° РєСЂР°СЏ РѕС‚РІРµСЂСЃС‚РёСЏ."
        )]
        [Range(0f, 2f)]
        [SerializeField]
        private float holeFillEdgePull = 1.35f;

        [Tooltip(
            "РќР°СЃРєРѕР»СЊРєРѕ СЃРёР»СЊРЅРѕ СЃРѕР»РѕРјРёРЅРєРё РѕРєРѕР»Рѕ С†РµРЅС‚СЂР° " +
            "РґРѕР»Р¶РЅС‹ РїРµСЂРµРјРµС‰Р°С‚СЊСЃСЏ РЅРµРїРѕСЃСЂРµРґСЃС‚РІРµРЅРЅРѕ РІ РѕР±Р»Р°СЃС‚СЊ РѕС‚РІРµСЂСЃС‚РёСЏ."
        )]
        [Range(0f, 2f)]
        [SerializeField]
        private float holeFillCenterBias = 1.25f;

        [Tooltip(
            "РњРёРЅРёРјР°Р»СЊРЅР°СЏ РґРѕР»СЏ РїРѕРєСЂС‹С‚РёСЏ РѕС‚РІРµСЂСЃС‚РёСЏ, " +
            "РїРѕСЃР»Рµ РєРѕС‚РѕСЂРѕР№ СЃР»РѕР№ СЃС‡РёС‚Р°РµС‚СЃСЏ РґРѕСЃС‚Р°С‚РѕС‡РЅС‹Рј."
        )]
        [Range(0.25f, 2f)]
        [SerializeField]
        private float holeFillCoverageTarget = 0.82f;

        // ============================================================
        // ROOF SETTLEMENT
        // ============================================================

        [Header("ROOF SETTLEMENT")]

        [Tooltip("Насколько верхнее сено проседает при каждом копке под ним.")]
        [Min(0f)]
        [SerializeField]
        private float roofSettlementDepth = 0.12f;

        [Tooltip("Радиус области сверху, которая проседает над местом копания.")]
        [Min(0.1f)]
        [SerializeField]
        private float roofSettlementRadius = 1.45f;

        [Tooltip("Насколько сильно проседание затрагивает сено прямо над ямой.")]
        [Range(0.1f, 4f)]
        [SerializeField]
        private float roofSettlementFalloff = 1.35f;

        // ============================================================
        // LOCAL LAYER COLLAPSE
        // ============================================================

        [Header("LOCAL LAYER COLLAPSE")]

        [Tooltip(
            "РўРѕР»С‰РёРЅР° РѕРґРЅРѕРіРѕ Р»РѕРєР°Р»СЊРЅРѕРіРѕ СЃР»РѕСЏ."
        )]
        [Min(0.01f)]
        [SerializeField]
        private float localCollapseLayerHeight = 0.32f;

        [Tooltip(
            "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Р№ РґРѕРїСѓСЃРє РїСЂРё РѕРїСЂРµРґРµР»РµРЅРёРё Р»РѕРєР°Р»СЊРЅРѕРіРѕ СЃР»РѕСЏ."
        )]
        [Min(0f)]
        [SerializeField]
        private float localCollapseLayerTolerance = 0.08f;

        [Tooltip(
            "РњР°РєСЃРёРјР°Р»СЊРЅРѕРµ РєРѕР»РёС‡РµСЃС‚РІРѕ Р»РѕРєР°Р»СЊРЅС‹С… СЃР»РѕС‘РІ."
        )]
        [Min(1)]
        [SerializeField]
        private int maxHoleFillLayers = 5;

        [Tooltip(
            "РњРЅРѕР¶РёС‚РµР»СЊ РѕС†РµРЅРєРё РїРѕРєСЂС‹С‚РёСЏ."
        )]
        [Range(0.1f, 3f)]
        [SerializeField]
        private float localLayerCoverageMultiplier = 0.95f;

        [Tooltip(
            "РњРёРЅРёРјР°Р»СЊРЅРѕРµ РєРѕР»РёС‡РµСЃС‚РІРѕ СЃРѕР»РѕРјРёРЅРѕРє РІ РїРµСЂРІРѕРј СЃР»РѕРµ."
        )]
        [Min(1)]
        [SerializeField]
        private int minimumLocalLayerStraws = 7;

        [Tooltip(
            "РћСЃР»Р°Р±Р»РµРЅРёРµ РєР°Р¶РґРѕРіРѕ СЃР»РµРґСѓСЋС‰РµРіРѕ СЃР»РѕСЏ."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float nextLayerStrength = 0.68f;

        [Tooltip(
            "РќР°СЃРєРѕР»СЊРєРѕ СЃРёР»СЊРЅРѕ РєСЂР°Р№ Р»РѕРєР°Р»СЊРЅРѕРіРѕ СЃР»РѕСЏ РїР°РґР°РµС‚ РІРЅСѓС‚СЂСЊ."
        )]
        [Range(0f, 2f)]
        [SerializeField]
        private float edgeCollapseStrength = 1.25f;

        // ============================================================
        // STABILITY
        // ============================================================

        [Header("STABILITY")]

        [Min(0f)]
        [SerializeField]
        private float maxAccumulatedDeformation = 1.55f;

        [Min(0f)]
        [SerializeField]
        private float minimumSupportHeight = 0.05f;

        [Range(0f, 1f)]
        [SerializeField]
        private float lowerLayerWeight = 0.25f;

        [Range(0f, 2f)]
        [SerializeField]
        private float upperLayerWeight = 1.0f;

        // ============================================================
        // LOCAL MEMORY
        // ============================================================

        [Header("LOCAL DIG MEMORY")]

        [Min(1)]
        [SerializeField]
        private int maxDigPoints = 30;

        [SerializeField]
        private bool debugDrawDigPoints = false;

        // ============================================================
        // SINGLE DYNAMIC COLLIDER
        // ============================================================

        [Header("SINGLE DYNAMIC COLLIDER")]

        [SerializeField]
        private bool useSingleInteractionCollider = true;

        [Tooltip(
            "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Р№ padding РёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ СЃР°РјРёРј " +
            "HayPileInteractionCollider РґР»СЏ С„РѕСЂРјС‹ СЃС‚РѕРіР°."
        )]
        [Min(0f)]
        [SerializeField]
        private float interactionColliderPadding = 0.18f;

        [Tooltip(
            "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Р№ СЂР°Р·РјРµСЂ bounds СЃРѕР»РѕРјРёРЅРєРё РїСЂРё РІС‹Р±РѕСЂРµ Р»СѓС‡РѕРј."
        )]
        [Min(0f)]
        [SerializeField]
        private float interactionStrawBoundsPadding = 0.02f;

        [Tooltip(
            "РќРµР±РѕР»СЊС€РѕР№ Р·Р°РїР°СЃ РіР»СѓР±РёРЅС‹ РјРµР¶РґСѓ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊСЋ collider " +
            "Рё С‚РѕС‡РєРѕР№ Р»СѓС‡Р°."
        )]
        [Min(0f)]
        [SerializeField]
        private float interactionPickDepthTolerance = 0.18f;

        [SerializeField]
        private bool interactionColliderDebugLogs = false;

        // ============================================================
        // DEBUG
        // ============================================================

        [SerializeField]
        private bool debugLogs = false;

        // ============================================================
        // INTERNAL
        // ============================================================

        private readonly List<HayStraw> straws =
            new List<HayStraw>();

        private readonly List<HayStraw> reserveStraws =
            new List<HayStraw>();

        private readonly List<DigPoint> digPoints =
            new List<DigPoint>();

        private readonly Dictionary<HayStraw, Vector3>
            accumulatedOffsets =
                new Dictionary<HayStraw, Vector3>();

        private readonly Dictionary<HayStraw, Quaternion>
            accumulatedRotations =
                new Dictionary<HayStraw, Quaternion>();

        private readonly List<LayerCandidate>
            layerCandidates =
                new List<LayerCandidate>();

        private int collapseCandidateCount;
        private int collapseAppliedCount;
        private int holeCandidateCount;
        private int holeAppliedCount;

        // ============================================================
        // DEFORMATION SPATIAL GRID
        // ============================================================

        [Header("DEFORMATION SPATIAL GRID")]

        [Tooltip("Р Р°Р·РјРµСЂ СЏС‡РµР№РєРё 3D-СЃРµС‚РєРё РґР»СЏ РїРѕРёСЃРєР° СЃРѕР»РѕРјРёРЅРѕРє РїСЂРё РґРµС„РѕСЂРјР°С†РёРё. 2.5 РѕР±С‹С‡РЅРѕ С…РѕСЂРѕС€Рѕ РїРѕРґС…РѕРґРёС‚ РґР»СЏ СЃС‚РѕРіР° 15K+.")]
        [Min(0.5f)]
        [SerializeField]
        private float deformationGridCellSize = 2.5f;

        [Tooltip("Р—Р°РїР°СЃ СЂР°РґРёСѓСЃР° РїРѕРёСЃРєР° СѓС‡РёС‚С‹РІР°РµС‚ СѓР¶Рµ РЅР°РєРѕРїР»РµРЅРЅРѕРµ РїРµСЂРµРјРµС‰РµРЅРёРµ СЃРѕР»РѕРјРёРЅРєРё.")]
        [Min(0f)]
        [SerializeField]
        private float deformationGridExtraPadding = 0.05f;

        private readonly Dictionary<Vector3Int, List<HayStraw>>
            deformationGrid =
                new Dictionary<Vector3Int, List<HayStraw>>(256);

        private readonly List<HayStraw>
            deformationCandidates =
                new List<HayStraw>(2048);

        private Transform root;
        private Transform reserveRoot;

        private HayPileInteractionCollider interactionCollider;

        private Vector3 originalPrefabScale =
            Vector3.one;

        private int removalCount;
        private int totalPhysicalStrawCount;

        // ============================================================
        // PUBLIC
        // ============================================================

        public IReadOnlyList<HayStraw> Straws =>
            straws;

        public int StrawCount =>
            straws.Count;

        public int DigPointCount =>
            digPoints.Count;

        public int TotalPhysicalStrawCount =>
            totalPhysicalStrawCount;

        public int RemovedStrawCount =>
            removalCount;

        public int RemainingStrawCount =>
            Mathf.Max(
                0,
                totalPhysicalStrawCount -
                removalCount);

        public float RemainingRatio
        {
            get
            {
                if (totalPhysicalStrawCount <= 0)
                    return 0f;

                return Mathf.Clamp01(
                    RemainingStrawCount /
                    (float)totalPhysicalStrawCount);
            }
        }

        public float Radius =>
            radius;

        public float Height =>
            height;

        public float GroundY =>
            groundY;

        // ============================================================
        // DIG POINT
        // ============================================================

        [Serializable]
        private class DigPoint
        {
            public Vector3 localPosition;
            public float radius;

            public DigPoint(
                Vector3 position,
                float radius)
            {
                localPosition = position;
                this.radius = radius;
            }
        }

        // ============================================================
        // LAYER CANDIDATE
        // ============================================================

        private class LayerCandidate
        {
            public int layerIndex;
            public float centerHeight;

            public readonly List<HayStraw> straws =
                new List<HayStraw>();

            public float score;

            public LayerCandidate(
                int layerIndex,
                float centerHeight)
            {
                this.layerIndex = layerIndex;
                this.centerHeight = centerHeight;
            }
        }

        // ============================================================
        // UNITY
        // ============================================================

        private void Start()
        {
            Generate();
        }

        // ============================================================
        // GENERATE
        // ============================================================

        [ContextMenu("Generate Hay Pile")]
        public void Generate()
        {
            if (strawPrefab == null)
            {
                Debug.LogError(
                    "[HayPileGenerator] Straw Prefab РЅРµ РЅР°Р·РЅР°С‡РµРЅ.",
                    this);

                return;
            }

            Clear();

            int usedSeed =
                randomSeedOnGenerate
                    ? Random.Range(
                        int.MinValue,
                        int.MaxValue)
                    : seed;

            Random.InitState(
                usedSeed);

            root =
                CreateGeneratedRoot();

            // --------------------------------------------------------
            // CREATE SINGLE COLLIDER
            // --------------------------------------------------------

            CreateInteractionCollider();

            originalPrefabScale =
                strawPrefab.transform.localScale;

            removalCount = 0;

            totalPhysicalStrawCount =
                strawCount +
                reserveStrawCount;

            // --------------------------------------------------------
            // REAL STRAWS
            // --------------------------------------------------------

            for (int i = 0;
                 i < strawCount;
                 i++)
            {
                GenerateSingleStraw(i);
            }

            // --------------------------------------------------------
            // RESERVE
            // --------------------------------------------------------

            GenerateReserveStraws();

            // --------------------------------------------------------
            // BUILD DEFORMATION GRID
            // --------------------------------------------------------

            BuildDeformationGrid();

            // --------------------------------------------------------
            // FIRST COLLIDER BUILD
            // --------------------------------------------------------

            if (interactionCollider != null)
            {
                interactionCollider.Rebuild(
                    straws);
            }

            if (debugLogs)
            {
                Debug.Log(
                    $"[HayPileGenerator] " +
                    $"Visible={strawCount}, " +
                    $"Reserve={reserveStrawCount}, " +
                    $"Total={totalPhysicalStrawCount}",
                    this);
            }
        }

        // ============================================================
        // ROOT
        // ============================================================

        private Transform CreateGeneratedRoot()
        {
            Transform parent =
                generatedRoot != null
                    ? generatedRoot
                    : transform;

            GameObject rootObject =
                new GameObject(
                    "Generated_Hay");

            Transform newRoot =
                rootObject.transform;

            newRoot.SetParent(
                parent,
                false);

            return newRoot;
        }

        // ============================================================
        // SINGLE COLLIDER
        // ============================================================

        private void CreateInteractionCollider()
        {
            interactionCollider = null;

            if (!useSingleInteractionCollider ||
                root == null)
            {
                return;
            }

            GameObject colliderObject =
                new GameObject(
                    "Hay_InteractionCollider");

            Transform colliderTransform =
                colliderObject.transform;

            colliderTransform.SetParent(
                root,
                false);

            colliderTransform.localPosition =
                Vector3.zero;

            colliderTransform.localRotation =
                Quaternion.identity;

            colliderTransform.localScale =
                Vector3.one;

            // Р’РђР–РќРћ:
            // collider РїРѕР»СѓС‡Р°РµС‚ С‚РѕС‚ Р¶Рµ Layer,
            // С‡С‚Рѕ Рё prefab СЃРѕР»РѕРјРёРЅРєРё.
            colliderObject.layer =
                strawPrefab.gameObject.layer;

            interactionCollider =
                colliderObject.AddComponent<
                    HayPileInteractionCollider>();

            interactionCollider.Initialize(
                root,
                strawPrefab,
                groundY,
                interactionStrawBoundsPadding,
                interactionPickDepthTolerance,
                interactionColliderDebugLogs);
        }

        // ============================================================
        // REQUEST COLLIDER UPDATE
        // ============================================================

        private void RequestInteractionColliderRebuild()
        {
            if (!useSingleInteractionCollider)
                return;

            if (interactionCollider == null)
                return;

            interactionCollider.RequestRebuild(
                straws);
        }

        // ============================================================
        // REAL STRAW
        // ============================================================

        private void GenerateSingleStraw(
            int index)
        {
            bool isSurface =
                Random.value >=
                innerFill;

            Vector3 position;
            Vector3 normal;

            if (isSurface)
            {
                position =
                    GetShellPoint(
                        out normal);
            }
            else
            {
                position =
                    GetInnerPoint(
                        out normal);
            }

            position +=
                Random.insideUnitSphere *
                positionJitter;

            position.y =
                Mathf.Max(
                    position.y,
                    groundY + 0.025f);

            HayStraw straw =
                Instantiate(
                    strawPrefab,
                    root);

            straw.name =
                $"HayStraw_{index:0000}";

            float scale =
                Random.Range(
                    scaleRange.x,
                    scaleRange.y);

            straw.transform.localScale =
                originalPrefabScale *
                scale;

            Quaternion rotation =
                isSurface
                    ? CreateSurfaceRotation(
                        position,
                        normal)
                    : CreateInnerRotation(
                        position,
                        normal);

            rotation *=
                Quaternion.Euler(
                    baseRotation);

            straw.Initialize(
                position,
                rotation,
                isSurface,
                Random.Range(
                    0.88f,
                    1.12f),
                Random.Range(
                    0.72f,
                    1.28f));

            // Collider РєР°Р¶РґРѕР№ РѕС‚РґРµР»СЊРЅРѕР№ СЃРѕР»РѕРјРёРЅРєРё РІС‹РєР»СЋС‡РµРЅ.
            straw.SetInteractionEnabled(false);

            straws.Add(straw);

            accumulatedOffsets[straw] =
                Vector3.zero;

            accumulatedRotations[straw] =
                rotation;
        }

        // ============================================================
        // RESERVE
        // ============================================================

        private void GenerateReserveStraws()
        {
            if (reserveStrawCount <= 0)
                return;

            GameObject reserveObject =
                new GameObject(
                    "Hay_ReserveStraws");

            reserveRoot =
                reserveObject.transform;

            reserveRoot.SetParent(
                root,
                false);

            for (int i = 0;
                 i < reserveStrawCount;
                 i++)
            {
                float y01 =
                    Random.Range(
                        0.08f,
                        0.90f);

                float profile =
                    Mathf.Pow(
                        Mathf.Clamp01(
                            1f - y01),
                        pileProfileExponent);

                float maxRadius =
                    Mathf.Max(
                        0.15f,
                        radius *
                        profile *
                        reserveInnerRadiusFactor);

                float radial =
                    Mathf.Sqrt(
                        Random.value) *
                    maxRadius;

                float angle =
                    Random.Range(
                        0f,
                        Mathf.PI * 2f);

                Vector3 position =
                    new Vector3(
                        Mathf.Cos(angle) *
                        radial,
                        groundY +
                        y01 * height,
                        Mathf.Sin(angle) *
                        radial);

                HayStraw straw =
                    Instantiate(
                        strawPrefab,
                        reserveRoot);

                straw.name =
                    $"ReserveHay_{i:0000}";

                float scale =
                    Random.Range(
                        scaleRange.x,
                        scaleRange.y);

                straw.transform.localScale =
                    originalPrefabScale *
                    scale;

                Quaternion rotation =
                    CreateInnerRotation(
                        position,
                        Vector3.up);

                rotation *=
                    Quaternion.Euler(
                        baseRotation);

                straw.Initialize(
                    position,
                    rotation,
                    false,
                    Random.Range(
                        0.90f,
                        1.10f),
                    Random.Range(
                        0.75f,
                        1.25f));

                straw.SetInteractionEnabled(false);

                straw.gameObject.SetActive(false);

                reserveStraws.Add(straw);

                accumulatedOffsets[straw] =
                    Vector3.zero;

                accumulatedRotations[straw] =
                    rotation;
            }
        }

        // ============================================================
        // DIG
        // ============================================================

        public void DeformAt(
            Vector3 worldPosition,
            HayStraw removedStraw = null)
        {
            if (root == null)
                return;

            if (RemainingStrawCount <= 0)
                return;

            Vector3 localPosition =
                root.InverseTransformPoint(
                    worldPosition);

            // --------------------------------------------------------
            // MEMORY
            // --------------------------------------------------------

            digPoints.Add(
                new DigPoint(
                    localPosition,
                    digRadius));

            while (digPoints.Count >
                   maxDigPoints)
            {
                digPoints.RemoveAt(0);
            }

            // --------------------------------------------------------
            // REMOVE ONE PHYSICAL STRAW
            // --------------------------------------------------------

            removalCount =
                Mathf.Min(
                    removalCount + 1,
                    totalPhysicalStrawCount);

            // --------------------------------------------------------
            // OPEN INNER HAY
            // --------------------------------------------------------

            // Открываем внутреннюю солому
            using (ReserveMarker.Auto())
            {
                ActivateReserveStraws(localPosition);
            }

            // Основное обрушение
            using (CollapseMarker.Auto())
            {
                ApplySingleCollapse(localPosition, removedStraw);
            }

            // Заполнение ямки
            using (HoleFillMarker.Auto())
            {
                ApplyHoleFilling(localPosition, removedStraw);
            }

            // Верхний слой постепенно оседает в образовавшуюся яму.
            ApplyRoofSettlement(localPosition, removedStraw);

            // Обновление коллайдера
            using (ColliderMarker.Auto())
            {
                RequestInteractionColliderRebuild();
            }

            if (debugLogs)
            {
                Debug.Log(
                    $"[HayPileGenerator] " +
                    $"Dig at {localPosition}, " +
                    $"remaining={RemainingStrawCount}",
                    this);
            }
        }

        // ============================================================
        // RESERVE ACTIVATION
        // ============================================================

        private void ActivateReserveStraws(
            Vector3 digPosition)
        {
            if (reservePerDig <= 0)
                return;

            int activated = 0;

            while (activated < reservePerDig)
            {
                HayStraw best = null;

                float bestScore =
                    float.MaxValue;

                for (int i = 0;
                     i < reserveStraws.Count;
                     i++)
                {
                    HayStraw straw =
                        reserveStraws[i];

                    if (straw == null)
                        continue;

                    if (straw.gameObject.activeSelf)
                        continue;

                    Vector3 reservePosition =
                        straw.BaseLocalPosition;

                    float horizontalDistance =
                        Vector2.Distance(
                            new Vector2(
                                reservePosition.x,
                                reservePosition.z),
                            new Vector2(
                                digPosition.x,
                                digPosition.z));

                    if (horizontalDistance >
                        reserveSearchRadius)
                    {
                        continue;
                    }

                    float verticalDistance =
                        Mathf.Abs(
                            reservePosition.y -
                            digPosition.y);

                    float score =
                        horizontalDistance * 0.75f +
                        verticalDistance * 0.16f;

                    if (horizontalDistance <=
                        digRadius)
                    {
                        score *=
                            0.35f;
                    }

                    if (score < bestScore)
                    {
                        bestScore =
                            score;

                        best =
                            straw;
                    }
                }

                if (best == null)
                    break;

                ActivateReserveStraw(
                    best);

                activated++;
            }

            if (debugLogs)
            {
                Debug.Log(
                    $"Collapse: {collapseCandidateCount} candidates, " +
                    $"{collapseAppliedCount} applied | " +
                    $"Hole fill: {holeCandidateCount} candidates, " +
                    $"{holeAppliedCount} applied",
                    this);
            }
        }

        // ============================================================
        // ACTIVATE ONE RESERVE STRAW
        // ============================================================

        private void ActivateReserveStraw(
            HayStraw straw)
        {
            if (straw == null)
                return;

            Vector3 target =
                straw.BaseLocalPosition;

            Quaternion rotation =
                straw.BaseLocalRotation;

            straw.gameObject.SetActive(true);

            straw.SetInteractionEnabled(false);

            straw.ActivateAsInnerStraw(
                target,
                rotation);

            // Reserve straw СѓР¶Рµ РµСЃС‚СЊ РІ deformationGrid.
            // Р”РѕР±Р°РІР»СЏРµРј РІ Р°РєС‚РёРІРЅС‹Р№ СЃРїРёСЃРѕРє РЅР°РїСЂСЏРјСѓСЋ: Contains() Р·РґРµСЃСЊ Р±С‹Р»
            // O(N) Рё РїСЂРё 15K+ СЃРѕР»РѕРјРёРЅРѕРє СЃРѕР·РґР°РІР°Р» Р»РёС€РЅРёР№ РјРёРєСЂРѕ-С„СЂРёР·.
            straws.Add(straw);

            accumulatedOffsets[straw] =
                Vector3.zero;

            accumulatedRotations[straw] =
                rotation;

            // Register the newly activated reserve straw incrementally.
            // This prevents a full render rebuild of the entire hay pile.
            HayPileRenderChunks renderChunks =
                GetComponent<HayPileRenderChunks>();

            if (renderChunks != null)
            {
                renderChunks.RegisterActivatedStraw(straw);
            }
        }

        // ============================================================
        // NORMAL COLLAPSE
        // ============================================================

        private void ApplySingleCollapse(
            Vector3 digPosition,
            HayStraw ignoredStraw)
        {
            CollectDeformationCandidates(
                digPosition,
                secondaryCollapseRadius +
                maxAccumulatedDeformation +
                deformationGridExtraPadding,
                secondaryCollapseHeight +
                maxAccumulatedDeformation +
                deformationGridExtraPadding);

            collapseCandidateCount = deformationCandidates.Count;
            collapseAppliedCount = 0;

            for (int i = 0;
                 i < deformationCandidates.Count;
                 i++)
            {
                HayStraw straw =
                    deformationCandidates[i];

                if (!CanDeform(
                        straw,
                        ignoredStraw))
                {
                    continue;
                }

                Vector3 currentPosition =
                    GetCurrentLocalPosition(
                        straw);

                Vector3 delta =
                    CalculateSingleCollapse(
                        currentPosition,
                        straw.DeformationRandom,
                        digPosition);

                if (delta.sqrMagnitude <=
                    0.000001f)
                {
                    continue;
                }

                ApplyAccumulatedDelta(
                    straw,
                    delta);
            }
        }

        // ============================================================
        // HOLE FILLING
        // ============================================================

        private void ApplyHoleFilling(
            Vector3 digPosition,
            HayStraw ignoredStraw)
        {
            if (holeFillStrength <= 0f)
                return;

            float effectiveRadius =
                Mathf.Max(
                    holeFillRadius,
                    digRadius);

            layerCandidates.Clear();

            // --------------------------------------------------------
            // COLLECT CANDIDATES
            // --------------------------------------------------------

            CollectDeformationCandidates(
                digPosition,
                effectiveRadius +
                maxAccumulatedDeformation +
                deformationGridExtraPadding,
                holeFillHeight +
                maxAccumulatedDeformation +
                deformationGridExtraPadding);

            holeCandidateCount = deformationCandidates.Count;
            holeAppliedCount = 0;

            for (int i = 0;
                 i < deformationCandidates.Count;
                 i++)
            {
                HayStraw straw =
                    deformationCandidates[i];

                if (!CanDeform(
                        straw,
                        ignoredStraw))
                {
                    continue;
                }

                Vector3 current =
                    GetCurrentLocalPosition(
                        straw);

                float heightAbove =
                    current.y -
                    digPosition.y;

                if (heightAbove <
                    holeFillMinHeight)
                {
                    continue;
                }

                if (heightAbove >
                    holeFillHeight)
                {
                    continue;
                }

                Vector2 horizontal =
                    new Vector2(
                        current.x -
                        digPosition.x,
                        current.z -
                        digPosition.z);

                float distance =
                    horizontal.magnitude;

                if (distance >
                    effectiveRadius)
                {
                    continue;
                }

                int layerIndex =
                    Mathf.FloorToInt(
                        (heightAbove -
                         holeFillMinHeight) /
                        Mathf.Max(
                            localCollapseLayerHeight,
                            0.001f));

                layerIndex =
                    Mathf.Max(
                        0,
                        layerIndex);

                if (layerIndex >=
                    maxHoleFillLayers)
                {
                    continue;
                }

                LayerCandidate layer =
                    GetOrCreateLayer(
                        layerIndex,
                        heightAbove);

                layer.straws.Add(
                    straw);

                float radial01 =
                    1f -
                    Mathf.Clamp01(
                        distance /
                        effectiveRadius);

                float radialWeight =
                    Mathf.Pow(
                        radial01,
                        holeFillRadialPower);

                layer.score +=
                    radialWeight;
            }

            if (layerCandidates.Count == 0)
                return;

            // --------------------------------------------------------
            // SORT
            // --------------------------------------------------------

            layerCandidates.Sort(
                (a, b) =>
                    a.layerIndex.CompareTo(
                        b.layerIndex));

            // --------------------------------------------------------
            // COVERAGE
            // --------------------------------------------------------

            float requiredCoverage =
                Mathf.PI *
                digRadius *
                digRadius *
                holeFillCoverageTarget;

            float accumulatedCoverage =
                0f;

            int layersToUse =
                0;

            for (int i = 0;
                 i < layerCandidates.Count;
                 i++)
            {
                LayerCandidate layer =
                    layerCandidates[i];

                float layerCoverage =
                    EstimateLayerCoverage(
                        layer,
                        digPosition,
                        effectiveRadius);

                accumulatedCoverage +=
                    layerCoverage;

                layersToUse++;

                if (accumulatedCoverage >=
                    requiredCoverage *
                    localLayerCoverageMultiplier)
                {
                    break;
                }

                if (i == 0 &&
                    layer.straws.Count >=
                    minimumLocalLayerStraws)
                {
                    break;
                }
            }

            // --------------------------------------------------------
            // APPLY
            // --------------------------------------------------------

            for (int i = 0;
                 i < layersToUse;
                 i++)
            {
                LayerCandidate layer =
                    layerCandidates[i];

                float layerStrength =
                    i == 0
                        ? 1f
                        : Mathf.Pow(
                            nextLayerStrength,
                            i);

                ApplyHoleFillLayer(
                    layer,
                    digPosition,
                    effectiveRadius,
                    layerStrength);
            }
        }

        // ============================================================
        // GET / CREATE LAYER
        // ============================================================

        private LayerCandidate GetOrCreateLayer(
            int layerIndex,
            float height)
        {
            for (int i = 0;
                 i < layerCandidates.Count;
                 i++)
            {
                if (layerCandidates[i].layerIndex ==
                    layerIndex)
                {
                    return layerCandidates[i];
                }
            }

            LayerCandidate created =
                new LayerCandidate(
                    layerIndex,
                    height);

            layerCandidates.Add(
                created);

            return created;
        }

        // ============================================================
        // ESTIMATE COVERAGE
        // ============================================================

        private float EstimateLayerCoverage(
            LayerCandidate layer,
            Vector3 digPosition,
            float effectiveRadius)
        {
            if (layer == null ||
                layer.straws.Count == 0)
            {
                return 0f;
            }

            float coverage =
                0f;

            float holeArea =
                Mathf.PI *
                digRadius *
                digRadius;

            float averageStrawArea =
                holeArea /
                Mathf.Max(
                    minimumLocalLayerStraws,
                    1);

            averageStrawArea *=
                1f +
                holeFillOverlap;

            for (int i = 0;
                 i < layer.straws.Count;
                 i++)
            {
                HayStraw straw =
                    layer.straws[i];

                if (straw == null)
                    continue;

                Vector3 position =
                    GetCurrentLocalPosition(
                        straw);

                Vector2 offset =
                    new Vector2(
                        position.x -
                        digPosition.x,
                        position.z -
                        digPosition.z);

                float distance =
                    offset.magnitude;

                float radial01 =
                    1f -
                    Mathf.Clamp01(
                        distance /
                        effectiveRadius);

                float radialWeight =
                    Mathf.Pow(
                        radial01,
                        1.05f);

                float centerBoost =
                    distance <= digRadius
                        ? 1.35f
                        : 1f;

                coverage +=
                    averageStrawArea *
                    Mathf.Lerp(
                        0.45f,
                        1.15f,
                        radialWeight) *
                    centerBoost;
            }

            return coverage;
        }

        // ============================================================
        // APPLY ONE LOCAL LAYER
        // ============================================================

        private void ApplyHoleFillLayer(
            LayerCandidate layer,
            Vector3 digPosition,
            float effectiveRadius,
            float layerStrength)
        {
            if (layer == null)
                return;

            if (layerStrength <= 0.001f)
                return;

            for (int i = 0;
                 i < layer.straws.Count;
                 i++)
            {
                HayStraw straw =
                    layer.straws[i];

                if (straw == null)
                    continue;

                if (!CanDeform(
                        straw,
                        null))
                {
                    continue;
                }

                Vector3 current =
                    GetCurrentLocalPosition(
                        straw);

                Vector2 horizontal =
                    new Vector2(
                        current.x -
                        digPosition.x,
                        current.z -
                        digPosition.z);

                float distance =
                    horizontal.magnitude;

                if (distance >
                    effectiveRadius)
                {
                    continue;
                }

                // ----------------------------------------------------
                // RADIAL
                // ----------------------------------------------------

                float radial01 =
                    1f -
                    Mathf.Clamp01(
                        distance /
                        effectiveRadius);

                float radialWeight =
                    Mathf.Pow(
                        radial01,
                        holeFillRadialPower);

                // ----------------------------------------------------
                // HEIGHT
                // ----------------------------------------------------

                float layerStart =
                    digPosition.y +
                    holeFillMinHeight +
                    layer.layerIndex *
                    localCollapseLayerHeight;

                float localHeight01 =
                    Mathf.InverseLerp(
                        layerStart,
                        layerStart +
                        localCollapseLayerHeight +
                        localCollapseLayerTolerance,
                        current.y);

                float heightWeight =
                    Mathf.SmoothStep(
                        holeFillLowerWeight,
                        holeFillUpperWeight,
                        localHeight01);

                // ----------------------------------------------------
                // EDGE
                // ----------------------------------------------------

                float edge01 =
                    Mathf.Clamp01(
                        distance /
                        Mathf.Max(
                            digRadius,
                            0.001f));

                float edgeWeight =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        edge01);

                // ----------------------------------------------------
                // CENTER
                // ----------------------------------------------------

                float center01 =
                    1f -
                    Mathf.Clamp01(
                        distance /
                        Mathf.Max(
                            digRadius,
                            0.001f));

                float centerWeight =
                    Mathf.Pow(
                        center01,
                        1.15f);

                // ----------------------------------------------------
                // TOTAL INFLUENCE
                // ----------------------------------------------------

                float influence =
                    radialWeight *
                    heightWeight *
                    layerStrength *
                    holeFillStrength;

                influence *=
                    Mathf.Lerp(
                        1f,
                        holeFillCenterBias,
                        centerWeight);

                influence =
                    Mathf.Clamp01(
                        influence);

                if (influence <= 0.001f)
                    continue;

                // ----------------------------------------------------
                // VERTICAL FALL
                // ----------------------------------------------------

                float verticalFall =
                    holeFillDepth *
                    influence;

                if (distance < digRadius)
                {
                    float direct01 =
                        1f -
                        Mathf.Clamp01(
                            distance /
                            Mathf.Max(
                                digRadius,
                                0.001f));

                    float direct =
                        Mathf.Pow(
                            direct01,
                            digFalloff);

                    verticalFall +=
                        digDepth *
                        direct *
                        holeFillStrength *
                        0.55f *
                        layerStrength;
                }

                // ----------------------------------------------------
                // EDGE COLLAPSE
                // ----------------------------------------------------

                verticalFall +=
                    edgeWeight *
                    edgeCollapseStrength *
                    0.12f *
                    influence;

                verticalFall =
                    Mathf.Clamp(
                        verticalFall,
                        0f,
                        holeFillDepth *
                        Mathf.Max(
                            1f,
                            layerStrength));

                Vector3 delta =
                    Vector3.down *
                    verticalFall;

                // ----------------------------------------------------
                // MOVE TOWARD HOLE
                // ----------------------------------------------------

                if (distance > 0.01f &&
                    holeFillPull > 0f)
                {
                    Vector3 towardCenter =
                        new Vector3(
                            -horizontal.x,
                            0f,
                            -horizontal.y);

                    towardCenter.Normalize();

                    float lateral =
                        holeFillPull *
                        influence *
                        Mathf.Lerp(
                            0.55f,
                            holeFillEdgePull,
                            edgeWeight);

                    if (distance < digRadius)
                    {
                        lateral *=
                            Mathf.Lerp(
                                1f,
                                holeFillCenterBias,
                                centerWeight);
                    }

                    Vector3 lateralDelta =
                        towardCenter *
                        lateral;

                    float maxLateral =
                        Mathf.Max(
                            0f,
                            verticalFall *
                            holeFillMaxLateralRatio);

                    maxLateral *=
                        1f +
                        holeFillOverlap *
                        0.35f;

                    if (lateralDelta.magnitude >
                        maxLateral)
                    {
                        lateralDelta =
                            lateralDelta.normalized *
                            maxLateral;
                    }

                    delta +=
                        lateralDelta;
                }

                // ----------------------------------------------------
                // NO UPWARD MOTION
                // ----------------------------------------------------

                if (delta.y > 0f)
                    delta.y = 0f;

                if (delta.sqrMagnitude <=
                    0.000001f)
                {
                    continue;
                }

                holeAppliedCount++;

                ApplyAccumulatedDelta(
                    straw,
                    delta);
            }
        }

        // ============================================================
        // ROOF SETTLEMENT
        // ============================================================

        private void ApplyRoofSettlement(
            Vector3 digPosition,
            HayStraw ignoredStraw)
        {
            if (roofSettlementDepth <= 0f ||
                roofSettlementRadius <= 0f)
            {
                return;
            }

            // Search the entire pile above the digging point, not just
            // the few layers used by the local hole-filling pass.
            CollectDeformationCandidates(
                digPosition,
                roofSettlementRadius + maxAccumulatedDeformation +
                    deformationGridExtraPadding,
                height + maxAccumulatedDeformation +
                    deformationGridExtraPadding);

            float radius = Mathf.Max(0.1f, roofSettlementRadius);
            float radiusSqr = radius * radius;

            for (int i = 0; i < deformationCandidates.Count; i++)
            {
                HayStraw straw = deformationCandidates[i];

                if (!CanDeform(straw, ignoredStraw))
                    continue;

                Vector3 current = GetCurrentLocalPosition(straw);

                float heightAboveDig = current.y - digPosition.y;

                // Only hay above the digging point can fall into this hole.
                if (heightAboveDig <= holeFillMinHeight)
                    continue;

                float dx = current.x - digPosition.x;
                float dz = current.z - digPosition.z;
                float distanceSqr = dx * dx + dz * dz;

                if (distanceSqr > radiusSqr)
                    continue;

                float distance01 = Mathf.Sqrt(distanceSqr) / radius;
                float radialWeight = Mathf.Pow(
                    Mathf.Clamp01(1f - distance01),
                    roofSettlementFalloff);

                // The closer the straw is to the hole horizontally, the more it settles.
                // A small per-dig step makes the pile sink progressively instead of
                // opening a permanent straight tunnel.
                float verticalFall = roofSettlementDepth * radialWeight;

                // Slightly pull the rim inward as it descends, so the opening closes
                // instead of becoming a clean vertical shaft.
                Vector3 delta = Vector3.down * verticalFall;

                if (distanceSqr > 0.0001f)
                {
                    Vector3 towardCenter = new Vector3(-dx, 0f, -dz).normalized;
                    float lateral = Mathf.Min(
                        verticalFall * 0.22f,
                        distance01 * 0.035f);

                    delta += towardCenter * lateral;
                }

                ApplyAccumulatedDelta(straw, delta, true);
            }
        }

        // ============================================================
        // DEFORMATION GRID
        // ============================================================

        private void BuildDeformationGrid()
        {
            deformationGrid.Clear();

            float cellSize = Mathf.Max(0.5f, deformationGridCellSize);

            for (int i = 0; i < straws.Count; i++)
            {
                AddStrawToDeformationGrid(straws[i], cellSize);
            }

            for (int i = 0; i < reserveStraws.Count; i++)
            {
                AddStrawToDeformationGrid(reserveStraws[i], cellSize);
            }
        }

        private void AddStrawToDeformationGrid(
            HayStraw straw,
            float cellSize)
        {
            if (straw == null)
                return;

            Vector3 p = straw.BaseLocalPosition;

            Vector3Int key = new Vector3Int(
                Mathf.FloorToInt(p.x / cellSize),
                Mathf.FloorToInt(p.y / cellSize),
                Mathf.FloorToInt(p.z / cellSize));

            if (!deformationGrid.TryGetValue(key, out List<HayStraw> cell))
            {
                cell = new List<HayStraw>(64);
                deformationGrid.Add(key, cell);
            }

            cell.Add(straw);
        }

        private void CollectDeformationCandidates(
        Vector3 center,
        float horizontalRadius,
        float verticalRadius)
        {
            deformationCandidates.Clear();

            float cellSize = Mathf.Max(0.5f, deformationGridCellSize);

            // Учитываем максимальное накопленное смещение,
            // чтобы не пропустить уже сдвинутые соломинки.
            // Радиусы уже расширены вызывающими методами.
            // Повторно добавлять maxAccumulatedDeformation нельзя.
            float searchRadius = horizontalRadius;
            float searchHeight = verticalRadius;

            int minX = Mathf.FloorToInt((center.x - searchRadius) / cellSize);
            int maxX = Mathf.FloorToInt((center.x + searchRadius) / cellSize);
            int minY = Mathf.FloorToInt((center.y - searchHeight) / cellSize);
            int maxY = Mathf.FloorToInt((center.y + searchHeight) / cellSize);
            int minZ = Mathf.FloorToInt((center.z - searchRadius) / cellSize);
            int maxZ = Mathf.FloorToInt((center.z + searchRadius) / cellSize);

            float radiusSqr = searchRadius * searchRadius;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (!deformationGrid.TryGetValue(
                                new Vector3Int(x, y, z),
                                out List<HayStraw> cell))
                        {
                            continue;
                        }

                        for (int i = 0; i < cell.Count; i++)
                        {
                            HayStraw straw = cell[i];

                            if (straw == null)
                                continue;

                            // Быстрый фильтр по базовой позиции — без вычисления текущей позиции.
                            Vector3 baseOffset = straw.BaseLocalPosition - center;

                            if (Mathf.Abs(baseOffset.y) > searchHeight)
                                continue;

                            float baseHorizontalSqr =
                                baseOffset.x * baseOffset.x +
                                baseOffset.z * baseOffset.z;

                            if (baseHorizontalSqr > radiusSqr)
                                continue;

                            // Более точная проверка только для прошедших первый фильтр.
                            Vector3 pos = GetCurrentLocalPosition(straw);
                            Vector3 offset = pos - center;

                            float horizontalSqr =
                                offset.x * offset.x +
                                offset.z * offset.z;

                            if (horizontalSqr > radiusSqr)
                                continue;

                            if (Mathf.Abs(offset.y) > searchHeight)
                                continue;

                            deformationCandidates.Add(straw);
                        }
                    }
                }
            }

            if (debugLogs)
            {
                int totalInGrid = 0;
                int nonEmptyCells = 0;

                foreach (var pair in deformationGrid)
                {
                    totalInGrid += pair.Value.Count;

                    if (pair.Value.Count > 0)
                        nonEmptyCells++;
                }

                Debug.Log(
                    $"[HayGrid] Grid cells: {deformationGrid.Count}, " +
                    $"non-empty: {nonEmptyCells}, " +
                    $"straws in grid: {totalInGrid}, " +
                    $"candidates: {deformationCandidates.Count}, " +
                    $"search radius: {searchRadius:F2}, " +
                    $"search height: {searchHeight:F2}, " +
                    $"cell size: {cellSize:F2}",
                    this);
            }
        }


        // ============================================================
        // CAN DEFORM
        // ============================================================

        private bool CanDeform(
            HayStraw straw,
            HayStraw ignoredStraw)
        {
            if (straw == null)
                return false;

            if (!straw.gameObject.activeInHierarchy)
                return false;

            if (straw == ignoredStraw)
                return false;

            if (straw.IsDetachedFromPile ||
                straw.IsTaken ||
                straw.IsReplacementMoving)
            {
                return false;
            }

            return true;
        }

        // ============================================================
        // APPLY ACCUMULATED DELTA
        // ============================================================

        private void ApplyAccumulatedDelta(
            HayStraw straw,
            Vector3 delta,
            bool allowDeepVertical = false)
        {
            if (straw == null)
                return;

            Vector3 accumulated =
                GetAccumulatedOffset(
                    straw);

            collapseAppliedCount++;

            Vector3 previousPosition =
                straw.BaseLocalPosition +
                accumulated;

            accumulated +=
                delta;

            if (maxAccumulatedDeformation > 0f)
            {
                if (allowDeepVertical)
                {
                    // Keep lateral drift bounded, but do not cap downward
                    // settlement: otherwise the top stops sinking after ~1.5m
                    // and a straight-through hole remains.
                    Vector2 lateral = new Vector2(accumulated.x, accumulated.z);
                    if (lateral.magnitude > maxAccumulatedDeformation)
                    {
                        lateral = lateral.normalized * maxAccumulatedDeformation;
                        accumulated.x = lateral.x;
                        accumulated.z = lateral.y;
                    }
                }
                else
                {
                    float magnitude = accumulated.magnitude;

                    if (magnitude > maxAccumulatedDeformation)
                    {
                        accumulated =
                            accumulated.normalized *
                            maxAccumulatedDeformation;
                    }
                }
            }

            Vector3 target =
                straw.BaseLocalPosition +
                accumulated;

            // --------------------------------------------------------
            // NEVER MOVE UP
            // --------------------------------------------------------

            if (target.y >
                previousPosition.y)
            {
                target.y =
                    previousPosition.y;

                accumulated.y =
                    target.y -
                    straw.BaseLocalPosition.y;
            }

            // --------------------------------------------------------
            // NEVER UNDER GROUND
            // --------------------------------------------------------

            float minimumY =
                groundY + 0.02f;

            if (target.y <
                minimumY)
            {
                target.y =
                    minimumY;

                accumulated.y =
                    target.y -
                    straw.BaseLocalPosition.y;
            }


            if ((target - previousPosition).sqrMagnitude <= 0.000001f)
                return;

            accumulatedOffsets[straw] = accumulated;

            Quaternion rotation =
                CalculateIncrementalRotation(
                    straw,
                    previousPosition,
                    target);

            accumulatedRotations[straw] =
                rotation;

            straw.SetDeformedTarget(
                target,
                rotation);
        }

        // ============================================================
        // CURRENT POSITION
        // ============================================================

        private Vector3 GetCurrentLocalPosition(
            HayStraw straw)
        {
            if (straw == null)
                return Vector3.zero;

            return
                straw.BaseLocalPosition +
                GetAccumulatedOffset(
                    straw);
        }

        // ============================================================
        // CURRENT OFFSET
        // ============================================================

        private Vector3 GetAccumulatedOffset(
            HayStraw straw)
        {
            if (straw == null)
                return Vector3.zero;

            if (accumulatedOffsets.TryGetValue(
                    straw,
                    out Vector3 value))
            {
                return value;
            }

            accumulatedOffsets[straw] =
                Vector3.zero;

            return Vector3.zero;
        }

        // ============================================================
        // COLLAPSE CALCULATION
        // ============================================================

        private Vector3 CalculateSingleCollapse(
            Vector3 strawPosition,
            float randomMultiplier,
            Vector3 digPosition)
        {
            Vector3 offset =
                strawPosition -
                digPosition;

            Vector2 horizontal =
                new Vector2(
                    offset.x,
                    offset.z);

            float distance =
                horizontal.magnitude;

            if (distance >
                secondaryCollapseRadius)
            {
                return Vector3.zero;
            }

            float heightAbove =
                strawPosition.y -
                digPosition.y;

            if (heightAbove <
                minimumSupportHeight)
            {
                return Vector3.zero;
            }

            float mainRadial01 =
                1f -
                Mathf.Clamp01(
                    distance /
                    Mathf.Max(
                        collapseRadius,
                        0.001f));

            float mainRadial =
                Mathf.Pow(
                    mainRadial01,
                    1.65f);

            float secondaryRadial01 =
                1f -
                Mathf.Clamp01(
                    distance /
                    Mathf.Max(
                        secondaryCollapseRadius,
                        0.001f));

            float secondaryRadial =
                secondaryRadial01 *
                secondaryRadial01;

            float height01 =
                Mathf.Clamp01(
                    heightAbove /
                    Mathf.Max(
                        collapseHeight,
                        0.001f));

            float upperWeight =
                Mathf.SmoothStep(
                    lowerLayerWeight,
                    upperLayerWeight,
                    height01);

            float mainInfluence =
                mainRadial *
                upperWeight *
                collapseStrength;

            float secondaryHeight01 =
                1f -
                Mathf.Clamp01(
                    Mathf.Max(
                        heightAbove,
                        0f) /
                    Mathf.Max(
                        secondaryCollapseHeight,
                        0.001f));

            float secondaryInfluence =
                secondaryRadial *
                secondaryHeight01 *
                secondaryCollapseStrength;

            float influence =
                Mathf.Max(
                    mainInfluence,
                    secondaryInfluence);

            if (influence <= 0.001f)
                return Vector3.zero;

            float random =
                Mathf.Lerp(
                    1f,
                    Mathf.Clamp(
                        randomMultiplier,
                        0.85f,
                        1.15f),
                    collapseRandomness);

            influence *=
                random;

            float verticalFall =
                Mathf.Lerp(
                    secondaryCollapseDepth,
                    collapseDepth,
                    mainInfluence);

            verticalFall *=
                influence;

            verticalFall =
                Mathf.Min(
                    verticalFall,
                    collapseDepth);

            Vector3 result =
                Vector3.down *
                verticalFall;

            // --------------------------------------------------------
            // LATERAL
            // --------------------------------------------------------

            if (distance > 0.025f)
            {
                Vector3 toward =
                    new Vector3(
                        -horizontal.x,
                        0f,
                        -horizontal.y);

                toward.Normalize();

                float lateralStrength =
                    Mathf.Lerp(
                        0.25f,
                        1f,
                        mainInfluence);

                Vector3 lateral =
                    toward *
                    collapsePull *
                    collapseLateralStrength *
                    influence *
                    lateralStrength;

                float maxLateral =
                    verticalFall *
                    0.65f;

                if (lateral.magnitude >
                    maxLateral)
                {
                    lateral =
                        lateral.normalized *
                        maxLateral;
                }

                result +=
                    lateral;
            }

            // --------------------------------------------------------
            // DIRECT HOLE
            // --------------------------------------------------------

            if (distance < digRadius)
            {
                float direct01 =
                    1f -
                    Mathf.Clamp01(
                        distance /
                        Mathf.Max(
                            digRadius,
                            0.001f));

                float direct =
                    Mathf.Pow(
                        direct01,
                        digFalloff);

                float vertical =
                    1f -
                    Mathf.Clamp01(
                        Mathf.Abs(
                            heightAbove) /
                        Mathf.Max(
                            digVerticalRange,
                            0.001f));

                direct *=
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        vertical);

                float directFall =
                    digDepth *
                    direct *
                    randomMultiplier;

                result +=
                    Vector3.down *
                    directFall;

                if (distance > 0.025f)
                {
                    Vector3 toward =
                        new Vector3(
                            -horizontal.x,
                            0f,
                            -horizontal.y);

                    toward.Normalize();

                    result +=
                        toward *
                        digPull *
                        direct *
                        0.45f;
                }
            }

            if (result.y > 0f)
                result.y = 0f;

            return result;
        }

        // ============================================================
        // ROTATION
        // ============================================================

        private Quaternion CalculateIncrementalRotation(
            HayStraw straw,
            Vector3 oldPosition,
            Vector3 newPosition)
        {
            Quaternion previousRotation;

            if (!accumulatedRotations.TryGetValue(
                    straw,
                    out previousRotation))
            {
                previousRotation =
                    straw.BaseLocalRotation;
            }

            Vector3 movement =
                newPosition -
                oldPosition;

            if (movement.sqrMagnitude <
                0.000001f)
            {
                return previousRotation;
            }

            Vector3 horizontalMovement =
                new Vector3(
                    movement.x,
                    0f,
                    movement.z);

            Vector3 axis =
                Vector3.Cross(
                    Vector3.up,
                    horizontalMovement);

            if (axis.sqrMagnitude >
                0.000001f)
            {
                axis.Normalize();

                float angle =
                    Mathf.Clamp(
                        horizontalMovement.magnitude *
                        35f,
                        0f,
                        8f);

                Quaternion correction =
                    Quaternion.AngleAxis(
                        angle,
                        axis);

                previousRotation =
                    correction *
                    previousRotation;
            }

            return previousRotation;
        }

        // ============================================================
        // SURFACE HEIGHT
        // ============================================================

        private float GetSurfaceHeightAtXZ(
            float x,
            float z)
        {
            float radial =
                new Vector2(
                    x,
                    z).magnitude;

            float radial01 =
                Mathf.Clamp01(
                    radial /
                    Mathf.Max(
                        radius,
                        0.001f));

            float inverseExponent =
                1f /
                Mathf.Max(
                    pileProfileExponent,
                    0.01f);

            float y01 =
                1f -
                Mathf.Pow(
                    radial01,
                    inverseExponent);

            y01 =
                Mathf.Clamp01(
                    y01);

            return
                groundY +
                y01 * height;
        }

        // ============================================================
        // SURFACE NORMAL
        // ============================================================

        private Vector3 CalculateSurfaceNormalAtPosition(
            Vector3 position)
        {
            float radial =
                new Vector2(
                    position.x,
                    position.z).magnitude;

            float radius01 =
                Mathf.Clamp01(
                    radial /
                    Mathf.Max(
                        radius,
                        0.001f));

            float inverseExponent =
                1f /
                Mathf.Max(
                    pileProfileExponent,
                    0.01f);

            float y01 =
                1f -
                Mathf.Pow(
                    radius01,
                    inverseExponent);

            y01 =
                Mathf.Clamp01(
                    y01);

            return CalculateSurfaceNormal(
                position.x,
                y01,
                position.z);
        }

        private Vector3 CalculateSurfaceNormal(
            float x,
            float y01,
            float z)
        {
            Vector3 radial =
                new Vector3(
                    x,
                    0f,
                    z);

            float radialLength =
                radial.magnitude;

            Vector3 radialDirection =
                radialLength > 0.0001f
                    ? radial /
                      radialLength
                    : Vector3.zero;

            float safeHeight =
                Mathf.Max(
                    height,
                    0.001f);

            float exponent =
                pileProfileExponent;

            float derivative =
                radius *
                exponent *
                Mathf.Pow(
                    Mathf.Max(
                        0.001f,
                        1f - y01),
                    Mathf.Max(
                        exponent - 1f,
                        -0.95f)) /
                safeHeight;

            return (
                Vector3.up -
                radialDirection *
                derivative
            ).normalized;
        }

        private Vector3 CalculateSurfaceNormalAtOriginalPile(
            Vector3 position)
        {
            float radial =
                new Vector2(
                    position.x,
                    position.z).magnitude;

            float radius01 =
                Mathf.Clamp01(
                    radial /
                    Mathf.Max(
                        radius,
                        0.001f));

            float inverseExponent =
                1f /
                Mathf.Max(
                    pileProfileExponent,
                    0.01f);

            float y01 =
                1f -
                Mathf.Pow(
                    radius01,
                    inverseExponent);

            return CalculateSurfaceNormal(
                position.x,
                Mathf.Clamp01(y01),
                position.z);
        }

        // ============================================================
        // SHELL
        // ============================================================

        private Vector3 GetShellPoint(
            out Vector3 normal)
        {
            float y01 =
                Random.value;

            float profile =
                Mathf.Pow(
                    Mathf.Clamp01(
                        1f - y01),
                    pileProfileExponent);

            float surfaceRadius =
                radius *
                profile;

            float angle =
                Random.Range(
                    0f,
                    Mathf.PI * 2f);

            Vector3 radialDirection =
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));

            Vector3 surfacePoint =
                new Vector3(
                    radialDirection.x *
                    surfaceRadius,
                    groundY +
                    y01 * height,
                    radialDirection.z *
                    surfaceRadius);

            normal =
                CalculateSurfaceNormal(
                    surfacePoint.x,
                    y01,
                    surfacePoint.z);

            int layer =
                Random.Range(
                    0,
                    Mathf.Max(
                        1,
                        surfaceLayers));

            float layerDepth =
                layer *
                shellLayerSpacing;

            layerDepth +=
                Random.Range(
                    -shellLayerJitter,
                    shellLayerJitter);

            layerDepth =
                Mathf.Max(
                    0f,
                    layerDepth);

            Vector3 point =
                surfacePoint -
                normal *
                layerDepth;

            point -=
                normal *
                Random.Range(
                    0f,
                    surfaceDepth);

            point +=
                Random.insideUnitSphere *
                surfaceJitter;

            point.y =
                Mathf.Max(
                    point.y,
                    groundY + 0.025f);

            return point;
        }

        // ============================================================
        // INNER
        // ============================================================

        private Vector3 GetInnerPoint(
            out Vector3 normal)
        {
            float y01 =
                Random.value;

            float profile =
                Mathf.Pow(
                    Mathf.Clamp01(
                        1f - y01),
                    pileProfileExponent);

            float availableRadius =
                Mathf.Max(
                    0.1f,
                    radius *
                    profile -
                    surfaceLayers *
                    shellLayerSpacing -
                    surfaceDepth);

            float radial01 =
                Mathf.Sqrt(
                    Random.value);

            float innerRadius =
                availableRadius *
                radial01;

            float angle =
                Random.Range(
                    0f,
                    Mathf.PI * 2f);

            Vector3 point =
                new Vector3(
                    Mathf.Cos(angle) *
                    innerRadius,
                    groundY +
                    y01 * height,
                    Mathf.Sin(angle) *
                    innerRadius);

            normal =
                CalculateSurfaceNormal(
                    point.x,
                    y01,
                    point.z);

            return point;
        }

        // ============================================================
        // SURFACE ROTATION
        // ============================================================

        private Quaternion CreateSurfaceRotation(
            Vector3 position,
            Vector3 normal)
        {
            Vector3 radial =
                new Vector3(
                    position.x,
                    0f,
                    position.z);

            Vector3 downhill;

            if (radial.sqrMagnitude >
                0.0001f)
            {
                downhill =
                    Vector3.ProjectOnPlane(
                        radial.normalized,
                        normal).normalized;
            }
            else
            {
                downhill =
                    Vector3.forward;
            }

            if (downhill.sqrMagnitude <
                0.0001f)
            {
                downhill =
                    Vector3.forward;
            }

            float slopeAngle =
                Random.Range(
                    -slopeRandomAngle,
                    slopeRandomAngle);

            Vector3 tangent =
                Quaternion.AngleAxis(
                    slopeAngle,
                    normal) *
                downhill;

            tangent =
                Vector3.ProjectOnPlane(
                    tangent,
                    normal).normalized;

            if (tangent.sqrMagnitude <
                0.0001f)
            {
                tangent =
                    Vector3.ProjectOnPlane(
                        Vector3.forward,
                        normal).normalized;
            }

            Vector3 tiltAxis =
                Vector3.Cross(
                    normal,
                    tangent);

            if (tiltAxis.sqrMagnitude >
                0.0001f)
            {
                float tilt =
                    Random.Range(
                        -surfaceTilt,
                        surfaceTilt);

                normal =
                    Quaternion.AngleAxis(
                        tilt,
                        tiltAxis.normalized) *
                    normal;

                normal.Normalize();
            }

            Vector3 meshAxis =
                GetMeshLongAxis();

            Quaternion rotation =
                Quaternion.FromToRotation(
                    meshAxis,
                    tangent);

            Vector3 transformedUp =
                rotation *
                Vector3.up;

            Quaternion normalCorrection =
                Quaternion.FromToRotation(
                    transformedUp,
                    normal);

            rotation =
                normalCorrection *
                rotation;

            float roll =
                Random.Range(
                    -rollRandomAngle,
                    rollRandomAngle);

            rotation =
                Quaternion.AngleAxis(
                    roll,
                    tangent) *
                rotation;

            return rotation;
        }

        // ============================================================
        // INNER ROTATION
        // ============================================================

        private Quaternion CreateInnerRotation(
            Vector3 position,
            Vector3 normal)
        {
            Vector3 randomDirection =
                Random.onUnitSphere;

            Vector3 tangent =
                Vector3.ProjectOnPlane(
                    randomDirection,
                    normal);

            if (tangent.sqrMagnitude <
                0.0001f)
            {
                tangent =
                    Vector3.Cross(
                        normal,
                        Vector3.right);

                if (tangent.sqrMagnitude <
                    0.0001f)
                {
                    tangent =
                        Vector3.Cross(
                            normal,
                            Vector3.forward);
                }
            }

            tangent.Normalize();

            Vector3 randomAxis =
                Random.onUnitSphere;

            Vector3 finalDirection =
                Vector3.Slerp(
                    tangent,
                    randomAxis,
                    innerRandomness);

            if (finalDirection.sqrMagnitude <
                0.0001f)
            {
                finalDirection =
                    tangent;
            }

            finalDirection.Normalize();

            return Quaternion.FromToRotation(
                GetMeshLongAxis(),
                finalDirection);
        }

        // ============================================================
        // MESH AXIS
        // ============================================================

        private Vector3 GetMeshLongAxis()
        {
            switch (meshLongAxis)
            {
                case HayMeshAxis.Y:
                    return Vector3.up;

                case HayMeshAxis.Z:
                    return Vector3.forward;

                default:
                    return Vector3.right;
            }
        }

        // ============================================================
        // RAY -> STRAW
        // ============================================================

        public bool TryGetStrawFromRaycast(
            Ray ray,
            RaycastHit hit,
            out HayStraw straw)
        {
            straw = null;

            if (!useSingleInteractionCollider)
                return false;

            if (interactionCollider == null)
                return false;

            if (!interactionCollider.IsChunkCollider(hit.collider))
            {
                if (interactionColliderDebugLogs)
                {
                    Debug.Log(
                        $"[HayPileGenerator] " +
                        $"Ray hit another collider: " +
                        $"{hit.collider.name}",
                        this);
                }

                return false;
            }

            return interactionCollider.TryResolveRay(
                ray,
                hit,
                straws,
                out straw);
        }

        // ============================================================
        // COLLIDER MANUAL REFRESH
        // ============================================================

        [ContextMenu("Refresh Hay Interaction Collider")]
        public void RefreshInteractionCollider()
        {
            if (!useSingleInteractionCollider)
                return;

            if (interactionCollider == null)
                return;

            interactionCollider.Rebuild(
                straws);
        }

        // ============================================================
        // CLEAR
        // ============================================================

        [ContextMenu("Clear Hay Pile")]
        public void Clear()
        {
            straws.Clear();
            reserveStraws.Clear();
            digPoints.Clear();
            layerCandidates.Clear();
            deformationGrid.Clear();
            deformationCandidates.Clear();

            accumulatedOffsets.Clear();
            accumulatedRotations.Clear();

            Transform existingRoot =
                root;

            if (existingRoot == null)
            {
                Transform parent =
                    generatedRoot != null
                        ? generatedRoot
                        : transform;

                existingRoot =
                    parent.Find(
                        "Generated_Hay");
            }

            if (existingRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(
                        existingRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(
                        existingRoot.gameObject);
                }
            }

            root = null;
            reserveRoot = null;
            interactionCollider = null;

            removalCount = 0;
            totalPhysicalStrawCount = 0;
        }

        // ============================================================
        // GIZMOS
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            Vector3 center =
                transform.position;

            center.y =
                groundY;

            Gizmos.DrawWireSphere(
                center,
                radius);

            Vector3 top =
                center +
                Vector3.up *
                Mathf.Max(
                    0.1f,
                    height);

            Gizmos.DrawLine(
                center,
                top);

            if (!debugDrawDigPoints ||
                root == null)
            {
                return;
            }

            for (int i = 0;
                 i < digPoints.Count;
                 i++)
            {
                DigPoint dig =
                    digPoints[i];

                Vector3 worldPosition =
                    root.TransformPoint(
                        dig.localPosition);

                Gizmos.DrawWireSphere(
                    worldPosition,
                    dig.radius);

                Gizmos.DrawWireSphere(
                    worldPosition,
                    collapseRadius);

                Gizmos.DrawWireSphere(
                    worldPosition,
                    holeFillRadius);

                Vector3 layerTop =
                    worldPosition +
                    Vector3.up *
                    holeFillHeight;

                Gizmos.DrawLine(
                    worldPosition,
                    layerTop);
            }
        }
    }
}