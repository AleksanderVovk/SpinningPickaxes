using System.Collections.Generic;
using UnityEngine;

namespace HayPileSystem
{
    /// <summary>
    /// Лёгкий коллайдер для подбора сена.
    ///
    /// ВАЖНО:
    /// MeshCollider не используется и не перестраивается после деформации.
    /// Используется один BoxCollider как broadphase.
    /// Конкретная соломинка определяется математическим пересечением луча
    /// с её Bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public class HayPileInteractionCollider : MonoBehaviour
    {
        [Header("BROADPHASE")]

        [Min(0f)]
        [SerializeField]
        private float colliderPadding = 0.18f;

        [Min(0.5f)]
        [SerializeField]
        private float minimumPileRadius = 0.5f;

        [Min(1)]
        [SerializeField]
        private int pyramidLevels = 7;

        [Min(0.05f)]
        [SerializeField]
        private float minimumLevelRadius = 0.15f;

        [Header("STRAW PICK")]

        [Min(0f)]
        [SerializeField]
        private float strawBoundsPadding = 0.02f;

        [Min(0f)]
        [SerializeField]
        private float pickDepthTolerance = 0.18f;

        [Tooltip("Количество соседних ячеек spatial grid вокруг точки попадания.")]
        [Range(1, 8)]
        [SerializeField]
        private int pickGridRadius = 4;

        [Header("SPATIAL GRID")]

        [Range(4, 32)]
        [SerializeField]
        private int spatialGridSize = 16;

        [Header("DEBUG")]

        [SerializeField]
        private bool debugLogs = false;


        private Transform pileRoot;

        private Bounds prefabBounds;

        private float groundY;

        private float currentRadius;

        private float currentHeight;

        private float worldMin;

        private bool initialized;

        private readonly List<BoxCollider> pyramidColliders = new List<BoxCollider>(8);

        private List<HayStraw>[] spatialCells;

        private readonly Dictionary<HayStraw, int> candidateMarks =
            new Dictionary<HayStraw, int>(4096);

        private readonly List<HayStraw> candidates =
            new List<HayStraw>(512);

        private int candidateVersion;

        private static readonly Vector3[] BoundsCorners =
            new Vector3[8];


        public Collider Collider
        {
            get
            {
                if (pyramidColliders.Count == 0) return null;
                return pyramidColliders[0];
            }
        }


        public int ChunkCount
        {
            get
            {
                return pyramidColliders.Count;
            }
        }


        // ============================================================
        // INITIALIZATION
        // ============================================================

        public void Initialize(
            Transform root,
            HayStraw strawPrefab,
            float initialGroundY,
            float boundsPadding,
            float pickDepth,
            bool logs)
        {
            pileRoot = root;

            groundY = initialGroundY;

            colliderPadding = Mathf.Max(
                0f,
                boundsPadding);

            strawBoundsPadding = Mathf.Max(
                0f,
                boundsPadding);

            pickDepthTolerance = Mathf.Max(
                0f,
                pickDepth);

            debugLogs = logs;

            spatialGridSize = Mathf.Clamp(
                spatialGridSize,
                4,
                32);

            pickGridRadius = Mathf.Clamp(
                pickGridRadius,
                1,
                8);

            prefabBounds =
                CalculatePrefabBounds(
                    strawPrefab);

            CreateBroadphaseCollider();

            CreateSpatialGrid();

            initialized = true;
        }


        private void CreateBroadphaseCollider()
        {
            pyramidLevels = Mathf.Clamp(pyramidLevels, 1, 16);
            EnsurePyramidColliders();
        }

        private void EnsurePyramidColliders()
        {
            while (pyramidColliders.Count < pyramidLevels)
            {
                int index = pyramidColliders.Count;
                GameObject go = new GameObject("Hay_Pyramid_Level_" + index.ToString("00"));
                Transform t = go.transform;
                t.SetParent(transform, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                go.layer = gameObject.layer;
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.isTrigger = false;
                box.size = Vector3.one;
                pyramidColliders.Add(box);
            }
        }

        private void CreateSpatialGrid()
        {
            int count =
                spatialGridSize *
                spatialGridSize;

            spatialCells =
                new List<HayStraw>[count];

            for (int i = 0; i < count; i++)
            {
                spatialCells[i] =
                    new List<HayStraw>(32);
            }
        }


        // ============================================================
        // REQUEST API
        // ============================================================

        public void RequestRebuild(
            IReadOnlyList<HayStraw> straws)
        {
            if (!initialized ||
                straws == null)
            {
                return;
            }

            // Строим индекс только один раз.
            // После деформации физический collider не пересобирается.
            if (currentRadius <= 0f)
            {
                BuildPickingIndex(straws);
            }
        }


        public void RequestRebuild(
            IReadOnlyList<HayStraw> straws,
            Vector3 localPosition,
            float dirtyRadius)
        {
            RequestRebuild(straws);
        }


        public void Rebuild(
            IReadOnlyList<HayStraw> straws)
        {
            if (!initialized ||
                straws == null)
            {
                return;
            }

            BuildPickingIndex(straws);
        }


        // ============================================================
        // BUILD PICKING INDEX
        // ============================================================

        private void BuildPickingIndex(
            IReadOnlyList<HayStraw> straws)
        {
            if (straws == null ||
                straws.Count == 0)
            {
                return;
            }

            currentRadius =
                CalculateCurrentRadius(straws);

            currentRadius =
                Mathf.Max(
                    currentRadius,
                    minimumPileRadius);

            currentRadius +=
                colliderPadding;

            currentHeight =
                CalculateCurrentHeight(straws);

            currentHeight =
                Mathf.Max(
                    currentHeight,
                    0.5f);

            worldMin =
                -currentRadius;

            ResizeBroadphaseCollider();

            for (int i = 0;
                 i < spatialCells.Length;
                 i++)
            {
                spatialCells[i].Clear();
            }

            candidateMarks.Clear();

            candidateVersion = 0;

            for (int i = 0;
                 i < straws.Count;
                 i++)
            {
                HayStraw straw =
                    straws[i];

                if (!CanBeUsed(straw))
                    continue;

                GetPileBounds(
                    straw,
                    out Bounds bounds);

                AddStrawToGrid(
                    straw,
                    bounds);
            }

            if (debugLogs)
            {
                Debug.Log(
                    "[HayInteraction] Picking index built. " +
                    "Radius=" +
                    currentRadius.ToString("F2") +
                    ", straws=" +
                    straws.Count,
                    this);
            }
        }


        private void ResizeBroadphaseCollider()
        {
            EnsurePyramidColliders();
            float totalRadius = Mathf.Max(currentRadius, minimumPileRadius);
            float totalHeight = Mathf.Max(0.5f, currentHeight);
            float levelHeight = totalHeight / Mathf.Max(1, pyramidColliders.Count);

            for (int i = 0; i < pyramidColliders.Count; i++)
            {
                BoxCollider box = pyramidColliders[i];
                if (box == null) continue;
                float t = pyramidColliders.Count <= 1 ? 0f : (float)i / (pyramidColliders.Count - 1);
                float r = Mathf.Lerp(totalRadius, minimumLevelRadius, t) + 0.05f;
                box.transform.localPosition = new Vector3(0f, groundY + i * levelHeight + levelHeight * 0.5f, 0f);
                box.transform.localRotation = Quaternion.identity;
                box.transform.localScale = Vector3.one;
                box.center = Vector3.zero;
                box.size = new Vector3(r * 2f, levelHeight + 0.08f, r * 2f);
                box.enabled = true;
            }
        }

        private void AddStrawToGrid(
            HayStraw straw,
            Bounds bounds)
        {
            int minX;
            int maxX;
            int minZ;
            int maxZ;

            GetGridRange(
                bounds,
                out minX,
                out maxX,
                out minZ,
                out maxZ);

            for (int z = minZ;
                 z <= maxZ;
                 z++)
            {
                int row =
                    z * spatialGridSize;

                for (int x = minX;
                     x <= maxX;
                     x++)
                {
                    spatialCells[row + x]
                        .Add(straw);
                }
            }
        }


        private void GetGridRange(
            Bounds bounds,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            WorldToGrid(
                bounds.min.x,
                bounds.min.z,
                out minX,
                out minZ);

            WorldToGrid(
                bounds.max.x,
                bounds.max.z,
                out maxX,
                out maxZ);
        }


        private void WorldToGrid(
            float x,
            float z,
            out int gx,
            out int gz)
        {
            float diameter =
                currentRadius * 2f;

            if (diameter <= 0.0001f)
            {
                gx = 0;
                gz = 0;
                return;
            }

            float u =
                (x - worldMin) /
                diameter;

            float v =
                (z - worldMin) /
                diameter;

            gx =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        u * spatialGridSize),
                    0,
                    spatialGridSize - 1);

            gz =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        v * spatialGridSize),
                    0,
                    spatialGridSize - 1);
        }


        // ============================================================
        // RAYCAST
        // ============================================================

        public bool IsChunkCollider(Collider collider)
        {
            if (collider == null) return false;
            for (int i = 0; i < pyramidColliders.Count; i++)
                if (pyramidColliders[i] == collider) return true;
            return false;
        }


        public bool TryResolveRay(
            Ray ray,
            RaycastHit colliderHit,
            IReadOnlyList<HayStraw> straws,
            out HayStraw result)
        {
            result = null;

            if (!initialized ||
                straws == null ||
                straws.Count == 0 ||
                pyramidColliders.Count == 0)
            {
                return false;
            }

            if (currentRadius <= 0f ||
                spatialCells == null)
            {
                BuildPickingIndex(straws);
            }

            // ========================================================
            // ВАЖНОЕ ИСПРАВЛЕНИЕ
            //
            // Старый код использовал:
            //
            // colliderHit.distance + 0.5f
            //
            // Из-за этого луч мог искать соломинку только возле
            // внешней поверхности BoxCollider.
            //
            // Теперь разрешаем лучу пройти через всю кучу.
            // ========================================================

            float maxDistance =
                CalculateRaySearchDistance(
                    ray,
                    colliderHit);

            Vector3 localHitPoint =
                pileRoot != null
                    ? pileRoot.InverseTransformPoint(
                        colliderHit.point)
                    : colliderHit.point;

            int cellX;
            int cellZ;

            WorldToGrid(
                localHitPoint.x,
                localHitPoint.z,
                out cellX,
                out cellZ);

            candidates.Clear();

            candidateVersion++;

            if (candidateVersion ==
                int.MaxValue)
            {
                candidateVersion = 1;
                candidateMarks.Clear();
            }

            AddGridNeighborhood(
                cellX,
                cellZ,
                pickGridRadius);

            float bestDistance =
                float.MaxValue;

            // ========================================================
            // ОСНОВНОЙ ПОИСК
            // ========================================================

            for (int i = 0;
                 i < candidates.Count;
                 i++)
            {
                HayStraw straw =
                    candidates[i];

                if (!CanBeUsed(straw))
                    continue;

                float distance;

                if (!RayIntersectsStraw(
                        ray,
                        straw,
                        maxDistance,
                        out distance))
                {
                    continue;
                }

                // Не выбираем соломинку,
                // которая находится заметно ПЕРЕД
                // поверхностью кучи.
                float minimumAllowedDistance =
                    colliderHit.distance -
                    Mathf.Max(
                        0.35f,
                        pickDepthTolerance);

                if (distance <
                    minimumAllowedDistance)
                {
                    continue;
                }

                if (distance <
                    bestDistance)
                {
                    bestDistance =
                        distance;

                    result =
                        straw;
                }
            }

            // ========================================================
            // FALLBACK
            //
            // Если grid не нашёл кандидатов,
            // делаем полный перебор.
            //
            // Это аварийный путь и при нормальной работе
            // сюда попадать не должны.
            // ========================================================

            if (result == null &&
                candidates.Count == 0)
            {
                for (int i = 0;
                     i < straws.Count;
                     i++)
                {
                    HayStraw straw =
                        straws[i];

                    if (!CanBeUsed(straw))
                        continue;

                    float distance;

                    if (!RayIntersectsStraw(
                            ray,
                            straw,
                            maxDistance,
                            out distance))
                    {
                        continue;
                    }

                    float minimumAllowedDistance =
                        colliderHit.distance -
                        Mathf.Max(
                            0.35f,
                            pickDepthTolerance);

                    if (distance <
                        minimumAllowedDistance)
                    {
                        continue;
                    }

                    if (distance <
                        bestDistance)
                    {
                        bestDistance =
                            distance;

                        result =
                            straw;
                    }
                }
            }

            return result != null;
        }


        private float CalculateRaySearchDistance(
            Ray ray,
            RaycastHit colliderHit)
        {
            // Запас по глубине должен быть достаточным,
            // чтобы пройти через всю кучу.

            float pileDepth =
                currentRadius * 2f;

            float heightDepth =
                Mathf.Max(
                    prefabBounds.size.y + 2f,
                    4f);

            float diagonal =
                Mathf.Sqrt(
                    pileDepth * pileDepth +
                    pileDepth * pileDepth +
                    heightDepth * heightDepth);

            return
                colliderHit.distance +
                diagonal +
                2f;
        }


        private void AddGridNeighborhood(
            int cellX,
            int cellZ,
            int radius)
        {
            int minX =
                Mathf.Max(
                    0,
                    cellX - radius);

            int maxX =
                Mathf.Min(
                    spatialGridSize - 1,
                    cellX + radius);

            int minZ =
                Mathf.Max(
                    0,
                    cellZ - radius);

            int maxZ =
                Mathf.Min(
                    spatialGridSize - 1,
                    cellZ + radius);

            for (int z = minZ;
                 z <= maxZ;
                 z++)
            {
                int row =
                    z * spatialGridSize;

                for (int x = minX;
                     x <= maxX;
                     x++)
                {
                    List<HayStraw> cell =
                        spatialCells[row + x];

                    for (int i = 0;
                         i < cell.Count;
                         i++)
                    {
                        HayStraw straw =
                            cell[i];

                        if (straw == null)
                            continue;

                        int version;

                        if (candidateMarks.TryGetValue(
                                straw,
                                out version) &&
                            version ==
                            candidateVersion)
                        {
                            continue;
                        }

                        candidateMarks[straw] =
                            candidateVersion;

                        candidates.Add(straw);
                    }
                }
            }
        }


        // ============================================================
        // RAY -> STRAW AABB
        // ============================================================

        private bool RayIntersectsStraw(
            Ray worldRay,
            HayStraw straw,
            float maxDistance,
            out float worldDistance)
        {
            worldDistance =
                float.MaxValue;

            Matrix4x4 worldToLocal =
                straw.transform.worldToLocalMatrix;

            Vector3 origin =
                worldToLocal.MultiplyPoint3x4(
                    worldRay.origin);

            Vector3 direction =
                worldToLocal.MultiplyVector(
                    worldRay.direction);

            if (direction.sqrMagnitude <
                0.0000001f)
            {
                return false;
            }

            Bounds bounds =
                prefabBounds;

            bounds.Expand(
                strawBoundsPadding * 2f);

            float tMin = 0f;

            float tMax =
                float.MaxValue;

            if (!Slab(
                    origin.x,
                    direction.x,
                    bounds.min.x,
                    bounds.max.x,
                    ref tMin,
                    ref tMax))
            {
                return false;
            }

            if (!Slab(
                    origin.y,
                    direction.y,
                    bounds.min.y,
                    bounds.max.y,
                    ref tMin,
                    ref tMax))
            {
                return false;
            }

            if (!Slab(
                    origin.z,
                    direction.z,
                    bounds.min.z,
                    bounds.max.z,
                    ref tMin,
                    ref tMax))
            {
                return false;
            }

            if (tMax < 0f)
                return false;

            float localT =
                Mathf.Max(
                    0f,
                    tMin);

            Vector3 localHit =
                origin +
                direction * localT;

            Vector3 worldHit =
                straw.transform.TransformPoint(
                    localHit);

            Vector3 normalizedDirection =
                worldRay.direction.normalized;

            worldDistance =
                Vector3.Dot(
                    worldHit -
                    worldRay.origin,
                    normalizedDirection);

            return
                worldDistance >= 0f &&
                worldDistance <= maxDistance;
        }


        private static bool Slab(
            float origin,
            float direction,
            float min,
            float max,
            ref float tMin,
            ref float tMax)
        {
            const float epsilon =
                0.000001f;

            if (Mathf.Abs(direction) <
                epsilon)
            {
                return
                    origin >= min &&
                    origin <= max;
            }

            float a =
                (min - origin) /
                direction;

            float b =
                (max - origin) /
                direction;

            if (a > b)
            {
                float temp = a;

                a = b;
                b = temp;
            }

            tMin =
                Mathf.Max(
                    tMin,
                    a);

            tMax =
                Mathf.Min(
                    tMax,
                    b);

            return tMin <= tMax;
        }


        // ============================================================
        // STRAW VALIDATION
        // ============================================================

        private bool CanBeUsed(
            HayStraw straw)
        {
            if (straw == null ||
                !straw.gameObject.activeInHierarchy)
            {
                return false;
            }

            return
                !straw.IsTaken &&
                !straw.IsDetachedFromPile &&
                !straw.IsReplacementMoving;
        }


        // ============================================================
        // STRAW BOUNDS
        // ============================================================

        private void GetPileBounds(
            HayStraw straw,
            out Bounds bounds)
        {
            Matrix4x4 matrix =
                GetStrawToPileMatrix(
                    straw);

            FillBoundsCorners(
                prefabBounds);

            Vector3 first =
                matrix.MultiplyPoint3x4(
                    BoundsCorners[0]);

            bounds =
                new Bounds(
                    first,
                    Vector3.zero);

            for (int i = 1;
                 i < 8;
                 i++)
            {
                bounds.Encapsulate(
                    matrix.MultiplyPoint3x4(
                        BoundsCorners[i]));
            }
        }


        private Matrix4x4 GetStrawToPileMatrix(
            HayStraw straw)
        {
            Transform parent =
                straw.transform.parent;

            Matrix4x4 parentToWorld =
                parent != null
                    ? parent.localToWorldMatrix
                    : Matrix4x4.identity;

            Matrix4x4 strawLocal =
                Matrix4x4.TRS(
                    straw.TargetLocalPosition,
                    straw.TargetLocalRotation,
                    straw.transform.localScale);

            if (pileRoot == null)
            {
                return
                    parentToWorld *
                    strawLocal;
            }

            return
                pileRoot.worldToLocalMatrix *
                parentToWorld *
                strawLocal;
        }


        private static void FillBoundsCorners(
            Bounds bounds)
        {
            Vector3 min =
                bounds.min;

            Vector3 max =
                bounds.max;

            BoundsCorners[0] =
                new Vector3(
                    min.x,
                    min.y,
                    min.z);

            BoundsCorners[1] =
                new Vector3(
                    max.x,
                    min.y,
                    min.z);

            BoundsCorners[2] =
                new Vector3(
                    max.x,
                    max.y,
                    min.z);

            BoundsCorners[3] =
                new Vector3(
                    min.x,
                    max.y,
                    min.z);

            BoundsCorners[4] =
                new Vector3(
                    min.x,
                    min.y,
                    max.z);

            BoundsCorners[5] =
                new Vector3(
                    max.x,
                    min.y,
                    max.z);

            BoundsCorners[6] =
                new Vector3(
                    max.x,
                    max.y,
                    max.z);

            BoundsCorners[7] =
                new Vector3(
                    min.x,
                    max.y,
                    max.z);
        }


        // ============================================================
        // PREFAB BOUNDS
        // ============================================================

        private static Bounds CalculatePrefabBounds(
            HayStraw prefab)
        {
            Bounds bounds =
                new Bounds(
                    Vector3.zero,
                    Vector3.zero);

            bool initialized =
                false;

            if (prefab == null)
            {
                return new Bounds(
                    Vector3.zero,
                    Vector3.one);
            }

            Transform root =
                prefab.transform;

            MeshFilter[] filters =
                root.GetComponentsInChildren<MeshFilter>(
                    true);

            for (int i = 0;
                 i < filters.Length;
                 i++)
            {
                MeshFilter filter =
                    filters[i];

                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4 matrix =
                    GetChildToRootMatrix(
                        filter.transform,
                        root);

                EncapsulateBounds(
                    ref bounds,
                    ref initialized,
                    filter.sharedMesh.bounds,
                    matrix);
            }


            SkinnedMeshRenderer[] skinned =
                root.GetComponentsInChildren<
                    SkinnedMeshRenderer>(
                    true);

            for (int i = 0;
                 i < skinned.Length;
                 i++)
            {
                SkinnedMeshRenderer renderer =
                    skinned[i];

                if (renderer == null)
                    continue;

                Matrix4x4 matrix =
                    GetChildToRootMatrix(
                        renderer.transform,
                        root);

                EncapsulateBounds(
                    ref bounds,
                    ref initialized,
                    renderer.localBounds,
                    matrix);
            }


            if (!initialized)
            {
                bounds =
                    new Bounds(
                        Vector3.zero,
                        Vector3.one);
            }

            return bounds;
        }


        private static Matrix4x4 GetChildToRootMatrix(
            Transform child,
            Transform root)
        {
            Matrix4x4 matrix =
                Matrix4x4.identity;

            Transform current =
                child;

            while (current != null &&
                   current != root)
            {
                matrix =
                    Matrix4x4.TRS(
                        current.localPosition,
                        current.localRotation,
                        current.localScale) *
                    matrix;

                current =
                    current.parent;
            }

            return matrix;
        }


        private static void EncapsulateBounds(
            ref Bounds target,
            ref bool initialized,
            Bounds source,
            Matrix4x4 matrix)
        {
            Vector3 min =
                source.min;

            Vector3 max =
                source.max;

            for (int i = 0;
                 i < 8;
                 i++)
            {
                Vector3 point;

                switch (i)
                {
                    case 0:
                        point =
                            new Vector3(
                                min.x,
                                min.y,
                                min.z);
                        break;

                    case 1:
                        point =
                            new Vector3(
                                max.x,
                                min.y,
                                min.z);
                        break;

                    case 2:
                        point =
                            new Vector3(
                                max.x,
                                max.y,
                                min.z);
                        break;

                    case 3:
                        point =
                            new Vector3(
                                min.x,
                                max.y,
                                min.z);
                        break;

                    case 4:
                        point =
                            new Vector3(
                                min.x,
                                min.y,
                                max.z);
                        break;

                    case 5:
                        point =
                            new Vector3(
                                max.x,
                                min.y,
                                max.z);
                        break;

                    case 6:
                        point =
                            new Vector3(
                                max.x,
                                max.y,
                                max.z);
                        break;

                    default:
                        point =
                            new Vector3(
                                min.x,
                                max.y,
                                max.z);
                        break;
                }

                point =
                    matrix.MultiplyPoint3x4(
                        point);

                if (!initialized)
                {
                    target =
                        new Bounds(
                            point,
                            Vector3.zero);

                    initialized = true;
                }
                else
                {
                    target.Encapsulate(point);
                }
            }
        }


        // ============================================================
        // CURRENT PILE RADIUS
        // ============================================================

        private float CalculateCurrentHeight(
            IReadOnlyList<HayStraw> straws)
        {
            float maxHeight = 0f;

            if (straws == null)
                return 1f;

            for (int i = 0; i < straws.Count; i++)
            {
                HayStraw straw = straws[i];

                if (!CanBeUsed(straw))
                    continue;

                Bounds bounds;


                GetPileBounds(
                    straw,
                    out bounds);

                maxHeight = Mathf.Max(
                    maxHeight,
                    bounds.max.y - groundY);
            }

            return maxHeight > 0.001f
                ? maxHeight
                : 1f;
        }


        private float CalculateCurrentRadius(
            IReadOnlyList<HayStraw> straws)
        {
            float maxSqr =
                0f;

            for (int i = 0;
                 i < straws.Count;
                 i++)
            {
                HayStraw straw =
                    straws[i];

                if (!CanBeUsed(straw))
                    continue;

                Bounds bounds;

                GetPileBounds(
                    straw,
                    out bounds);

                float x =
                    Mathf.Max(
                        Mathf.Abs(bounds.min.x),
                        Mathf.Abs(bounds.max.x));

                float z =
                    Mathf.Max(
                        Mathf.Abs(bounds.min.z),
                        Mathf.Abs(bounds.max.z));

                maxSqr =
                    Mathf.Max(
                        maxSqr,
                        x * x +
                        z * z);
            }

            return
                Mathf.Sqrt(maxSqr);
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private void OnDestroy()
        {
            for (int i = 0; i < pyramidColliders.Count; i++)
            {
                BoxCollider box = pyramidColliders[i];
                if (box == null) continue;

                if (Application.isPlaying)
                    Destroy(box.gameObject);
                else
                    DestroyImmediate(box.gameObject);
            }

            pyramidColliders.Clear();
            spatialCells = null;
            candidates.Clear();
            candidateMarks.Clear();
        }

    }
}