using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HayPileSystem
{
    /// <summary>
    /// Оптимизированный рендер огромной кучи сена.
    ///
    /// Основная идея:
    /// 1. Стационарные HayStraw объединяются в небольшое количество mesh chunks.
    /// 2. Каждый chunk хранит собственный список соломинок.
    /// 3. Динамические соломинки временно рендерятся своим обычным Renderer.
    /// 4. Изменения render-state не вызывают CombineMeshes сразу.
    ///    Chunk только помечается dirty.
    /// 5. Все dirty chunks перестраиваются один раз в LateUpdate.
    ///
    /// По умолчанию:
    /// 8 x 8 = 64 render chunks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HayPileRenderChunks : MonoBehaviour
    {
        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("RENDER CHUNKS")]

        [Min(1)]
        [SerializeField]
        private int columns = 8;

        [Min(1)]
        [SerializeField]
        private int rows = 8;

        [Min(0.05f)]
        [SerializeField]
        private float pileRadius = 6f;

        [Header("INITIALIZATION")]

        [SerializeField]
        private bool autoFindGenerator = true;

        [SerializeField]
        private bool rebuildOnStart = true;

        [Header("PERFORMANCE")]

        [Tooltip(
            "Максимальное количество chunk'ов, которые разрешено " +
            "перестраивать за один кадр. 0 = без лимита."
        )]
        [Min(0)]
        [SerializeField]
        private int maxChunkRebuildsPerFrame = 1;

        [Tooltip(
            "Если включено, rebuild выполняется в LateUpdate. " +
            "Это позволяет собрать несколько изменений в один rebuild."
        )]
        [SerializeField]
        private bool batchRebuilds = true;

        [Header("RENDERING")]

        [SerializeField]
        private ShadowCastingMode shadowCastingMode =
            ShadowCastingMode.On;

        [SerializeField]
        private bool receiveShadows = true;

        [Header("DEBUG")]

        [SerializeField]
        private bool debugLogs = false;

        // ============================================================
        // REFERENCES
        // ============================================================

        private HayPileGenerator generator;

        private Transform pileRoot;

        private HayStraw prefab;

        private bool initialized;

        private bool rebuildQueued;

        private int lastStrawCount = -1;

        // ============================================================
        // GLOBAL LIST
        // ============================================================

        private readonly List<HayStraw> allStraws =
            new List<HayStraw>(4096);

        // ============================================================
        // DYNAMIC
        // ============================================================

        private readonly HashSet<HayStraw> dynamicStraws =
            new HashSet<HayStraw>();

        // ============================================================
        // CHUNKS
        // ============================================================

        private readonly List<Chunk> chunks =
            new List<Chunk>();

        // ============================================================
        // DIRTY CHUNKS
        // ============================================================

        private readonly HashSet<int> dirtyChunks =
            new HashSet<int>();

        private readonly List<int> dirtyChunkBuffer =
            new List<int>(128);

        // ============================================================
        // STRAW -> CHUNK
        // ============================================================

        private readonly Dictionary<HayStraw, int> strawChunk =
            new Dictionary<HayStraw, int>(4096);

        // ============================================================
        // STRAW CACHE
        // ============================================================

        private readonly Dictionary<HayStraw, StrawRenderData> strawRenderData =
            new Dictionary<HayStraw, StrawRenderData>(4096);

        // ============================================================
        // CHUNK
        // ============================================================

        private sealed class Chunk
        {
            public GameObject gameObject;

            public readonly List<HayStraw> straws =
                new List<HayStraw>(128);

            public readonly List<ChunkMaterialPart> parts =
                new List<ChunkMaterialPart>(4);
        }

        // ============================================================
        // CHUNK MATERIAL PART
        // ============================================================

        private sealed class ChunkMaterialPart
        {
            public GameObject gameObject;

            public MeshFilter filter;

            public MeshRenderer renderer;

            public Mesh mesh;

            public Material material;

            public readonly List<CombineInstance> combines =
                new List<CombineInstance>(256);
        }

        // ============================================================
        // STRAW RENDER DATA
        // ============================================================

        private sealed class StrawRenderData
        {
            public MeshFilter[] filters;

            public MeshRenderer[] renderers;

            public Material[][] materials;
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstallForScene()
        {
            HayPileGenerator[] generators =
                FindObjectsOfType<HayPileGenerator>();

            for (int i = 0; i < generators.Length; i++)
            {
                HayPileGenerator current =
                    generators[i];

                if (current == null)
                    continue;

                if (current.GetComponent<HayPileRenderChunks>() == null)
                {
                    current.gameObject.AddComponent<HayPileRenderChunks>();
                }
            }
        }

        private void OnEnable()
        {
            HayStraw.OnRenderStateChanged +=
                HandleRenderStateChanged;
        }

        private void OnDisable()
        {
            HayStraw.OnRenderStateChanged -=
                HandleRenderStateChanged;
        }

        private void Start()
        {
            if (rebuildOnStart)
                QueueFullRebuild();
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                TryAutoInitialize();
                return;
            }

            // Runtime reserve straws are registered incrementally by
            // RegisterActivatedStraw(). Do not rebuild all chunks when the
            // active straw count changes: that makes every pickup rebuild
            // the entire hay pile.
            // Full rebuilds are reserved for startup/manual regeneration.

            if (rebuildQueued &&
                dynamicStraws.Count == 0)
            {
                rebuildQueued = false;
                RebuildFromGenerator();
                return;
            }

            if (batchRebuilds)
            {
                ProcessDirtyChunks();
            }
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================

        private void TryAutoInitialize()
        {
            if (autoFindGenerator &&
                generator == null)
            {
                generator =
                    GetComponent<HayPileGenerator>();
            }

            if (generator == null)
                return;

            if (generator.StrawCount <= 0)
                return;

            RebuildFromGenerator();
        }

        private void QueueFullRebuild()
        {
            rebuildQueued = true;
        }

        public void Initialize(
            Transform root,
            HayStraw strawPrefab,
            float radius)
        {
            pileRoot = root;
            prefab = strawPrefab;

            pileRadius =
                Mathf.Max(
                    0.05f,
                    radius);

            EnsureChunks();

            initialized = true;
        }

        // ============================================================
        // INCREMENTAL STRAW REGISTRATION
        // ============================================================

        /// <summary>
        /// Registers one reserve straw when it becomes active. Only its
        /// owning chunk is marked dirty; the entire pile is not rebuilt.
        /// </summary>
        public void RegisterActivatedStraw(HayStraw straw)
        {
            if (straw == null)
                return;

            if (!initialized)
            {
                // Startup order can vary. The normal initial rebuild will
                // include the straw if registration happens before init.
                rebuildQueued = true;
                return;
            }

            int existingChunk;
            if (strawChunk.TryGetValue(straw, out existingChunk))
                return;

            int chunkIndex = GetChunkIndex(straw.transform.localPosition);
            if (chunkIndex < 0 || chunkIndex >= chunks.Count)
                return;

            allStraws.Add(straw);
            CacheStrawRenderData(straw);

            straw.SetRenderChunkManaged(true);
            straw.SetRenderersVisible(false);

            strawChunk[straw] = chunkIndex;
            chunks[chunkIndex].straws.Add(straw);

            MarkChunkDirty(chunkIndex);
        }

        // ============================================================
        // FULL REBUILD
        // ============================================================

        public void RebuildFromGenerator()
        {
            if (generator == null)
            {
                generator =
                    GetComponent<HayPileGenerator>();
            }

            if (generator == null)
                return;

            if (generator.StrawCount <= 0)
                return;

            IReadOnlyList<HayStraw> source =
                generator.Straws;

            if (source == null ||
                source.Count == 0)
                return;

            HayStraw first =
                null;

            for (int i = 0; i < source.Count; i++)
            {
                HayStraw straw =
                    source[i];

                if (straw != null)
                {
                    first = straw;
                    break;
                }
            }

            if (first == null)
                return;

            pileRoot =
                first.transform.parent;

            pileRadius =
                Mathf.Max(
                    0.05f,
                    generator.Radius);

            prefab = null;

            initialized = true;

            // --------------------------------------------------------
            // CLEAR DATA
            // --------------------------------------------------------

            allStraws.Clear();
            dynamicStraws.Clear();
            strawChunk.Clear();
            strawRenderData.Clear();
            dirtyChunks.Clear();

            EnsureChunks();

            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].straws.Clear();

                ClearChunkParts(
                    chunks[i]);
            }

            // --------------------------------------------------------
            // COLLECT STRAWS
            // --------------------------------------------------------

            for (int i = 0; i < source.Count; i++)
            {
                HayStraw straw =
                    source[i];

                if (!IsRenderableStraw(straw))
                    continue;

                allStraws.Add(straw);

                CacheStrawRenderData(
                    straw);

                straw.SetRenderChunkManaged(true);
                straw.SetRenderersVisible(false);

                int chunkIndex =
                    GetChunkIndex(
                        straw.transform.localPosition);

                strawChunk[straw] =
                    chunkIndex;

                chunks[chunkIndex]
                    .straws
                    .Add(straw);
            }

            // --------------------------------------------------------
            // BUILD ALL
            // --------------------------------------------------------

            for (int i = 0; i < chunks.Count; i++)
            {
                BuildChunk(
                    chunks[i]);
            }

            dirtyChunks.Clear();

            if (debugLogs)
            {
                Debug.Log(
                    $"[HayPileRenderChunks] " +
                    $"Built {allStraws.Count} straws " +
                    $"into {chunks.Count} chunks.",
                    this);
            }
        }

        // ============================================================
        // STRAW FILTER
        // ============================================================

        private bool IsRenderableStraw(
            HayStraw straw)
        {
            return
                straw != null &&
                straw.gameObject.activeInHierarchy &&
                !straw.IsTaken;
        }

        // ============================================================
        // CHUNKS
        // ============================================================

        private void EnsureChunks()
        {
            int wanted =
                Mathf.Max(1, columns) *
                Mathf.Max(1, rows);

            while (chunks.Count < wanted)
            {
                int index =
                    chunks.Count;

                GameObject go =
                    new GameObject(
                        $"Hay_RenderChunk_{index:00}");

                go.transform.SetParent(
                    pileRoot != null
                        ? pileRoot
                        : transform,
                    false);

                go.transform.localPosition =
                    Vector3.zero;

                go.transform.localRotation =
                    Quaternion.identity;

                go.transform.localScale =
                    Vector3.one;

                chunks.Add(
                    new Chunk
                    {
                        gameObject = go
                    });
            }

            while (chunks.Count > wanted)
            {
                int index =
                    chunks.Count - 1;

                Chunk chunk =
                    chunks[index];

                chunks.RemoveAt(index);

                DestroyChunk(
                    chunk);
            }
        }

        private void DestroyChunk(
            Chunk chunk)
        {
            if (chunk == null)
                return;

            if (chunk.parts != null)
            {
                for (int i = 0;
                     i < chunk.parts.Count;
                     i++)
                {
                    ChunkMaterialPart part =
                        chunk.parts[i];

                    if (part == null)
                        continue;

                    if (part.mesh != null)
                    {
                        DestroySafe(
                            part.mesh);
                    }

                    if (part.gameObject != null)
                    {
                        DestroySafe(
                            part.gameObject);
                    }
                }
            }

            if (chunk.gameObject != null)
            {
                DestroySafe(
                    chunk.gameObject);
            }
        }

        // ============================================================
        // STRAW CACHE
        // ============================================================

        private void CacheStrawRenderData(
            HayStraw straw)
        {
            if (straw == null)
                return;

            if (strawRenderData.ContainsKey(straw))
                return;

            MeshFilter[] filters =
                straw.GetComponentsInChildren<MeshFilter>(
                    true);

            if (filters == null)
                filters =
                    new MeshFilter[0];

            MeshRenderer[] renderers =
                new MeshRenderer[filters.Length];

            Material[][] materials =
                new Material[filters.Length][];

            for (int i = 0;
                 i < filters.Length;
                 i++)
            {
                MeshFilter filter =
                    filters[i];

                if (filter == null)
                    continue;

                MeshRenderer renderer =
                    filter.GetComponent<MeshRenderer>();

                renderers[i] =
                    renderer;

                if (renderer != null)
                {
                    materials[i] =
                        renderer.sharedMaterials;
                }
                else
                {
                    materials[i] =
                        new Material[0];
                }
            }

            strawRenderData.Add(
                straw,
                new StrawRenderData
                {
                    filters = filters,
                    renderers = renderers,
                    materials = materials
                });
        }

        // ============================================================
        // CHUNK BUILD
        // ============================================================

        private void BuildChunk(
            Chunk chunk)
        {
            if (chunk == null)
                return;

            ClearChunkParts(
                chunk);

            if (chunk.straws.Count == 0)
                return;

            // --------------------------------------------------------
            // GROUP BY MATERIAL
            // --------------------------------------------------------

            Dictionary<Material, ChunkMaterialPart> materialParts =
                new Dictionary<Material, ChunkMaterialPart>();

            Matrix4x4 worldToChunk =
                chunk.gameObject
                    .transform
                    .worldToLocalMatrix;

            for (int i = 0;
                 i < chunk.straws.Count;
                 i++)
            {
                HayStraw straw =
                    chunk.straws[i];

                if (!IsStaticRenderableStraw(straw))
                    continue;

                StrawRenderData data;

                if (!strawRenderData.TryGetValue(
                        straw,
                        out data))
                {
                    CacheStrawRenderData(
                        straw);

                    if (!strawRenderData.TryGetValue(
                            straw,
                            out data))
                    {
                        continue;
                    }
                }

                AddStrawToMaterialGroups(
                    straw,
                    data,
                    worldToChunk,
                    materialParts);
            }

            // --------------------------------------------------------
            // CREATE PARTS
            // --------------------------------------------------------

            foreach (KeyValuePair<Material, ChunkMaterialPart> pair
                     in materialParts)
            {
                Material material =
                    pair.Key;

                ChunkMaterialPart part =
                    pair.Value;

                if (part.combines.Count == 0)
                    continue;

                CreateChunkPart(
                    chunk,
                    material,
                    part);

                BuildChunkPart(
                    part);
            }
        }

        private bool IsStaticRenderableStraw(
            HayStraw straw)
        {
            return
                straw != null &&
                straw.gameObject.activeInHierarchy &&
                !straw.IsTaken &&
                !dynamicStraws.Contains(straw);
        }

        // ============================================================
        // ADD STRAW GEOMETRY
        // ============================================================

        private void AddStrawToMaterialGroups(
            HayStraw straw,
            StrawRenderData data,
            Matrix4x4 worldToChunk,
            Dictionary<Material, ChunkMaterialPart> materialParts)
        {
            if (data == null ||
                data.filters == null)
                return;

            for (int i = 0;
                 i < data.filters.Length;
                 i++)
            {
                MeshFilter filter =
                    data.filters[i];

                if (filter == null)
                    continue;

                Mesh mesh =
                    filter.sharedMesh;

                if (mesh == null)
                    continue;

                MeshRenderer renderer =
                    data.renderers != null &&
                    i < data.renderers.Length
                        ? data.renderers[i]
                        : null;

                if (renderer == null)
                    continue;

                Material[] materials =
                    data.materials != null &&
                    i < data.materials.Length
                        ? data.materials[i]
                        : null;

                if (materials == null ||
                    materials.Length == 0)
                    continue;

                int subMeshCount =
                    Mathf.Min(
                        mesh.subMeshCount,
                        materials.Length);

                Matrix4x4 matrix =
                    worldToChunk *
                    filter.transform.localToWorldMatrix;

                for (int sub = 0;
                     sub < subMeshCount;
                     sub++)
                {
                    Material material =
                        materials[sub];

                    if (material == null)
                        continue;

                    ChunkMaterialPart part;

                    if (!materialParts.TryGetValue(
                            material,
                            out part))
                    {
                        part =
                            new ChunkMaterialPart
                            {
                                material = material
                            };

                        materialParts.Add(
                            material,
                            part);
                    }

                    part.combines.Add(
                        new CombineInstance
                        {
                            mesh = mesh,
                            subMeshIndex = sub,
                            transform = matrix
                        });
                }
            }
        }

        // ============================================================
        // CREATE MATERIAL PART
        // ============================================================

        private void CreateChunkPart(
            Chunk chunk,
            Material material,
            ChunkMaterialPart part)
        {
            string materialName =
                material != null
                    ? material.name
                    : "Null";

            GameObject go =
                new GameObject(
                    $"Render_{materialName}");

            go.transform.SetParent(
                chunk.gameObject.transform,
                false);

            go.transform.localPosition =
                Vector3.zero;

            go.transform.localRotation =
                Quaternion.identity;

            go.transform.localScale =
                Vector3.one;

            MeshFilter filter =
                go.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                go.AddComponent<MeshRenderer>();

            renderer.sharedMaterial =
                material;

            renderer.shadowCastingMode =
                shadowCastingMode;

            renderer.receiveShadows =
                receiveShadows;

            Mesh mesh =
                new Mesh
                {
                    name =
                        $"{chunk.gameObject.name}_{materialName}"
                };

            mesh.MarkDynamic();

            filter.sharedMesh =
                mesh;

            part.gameObject =
                go;

            part.filter =
                filter;

            part.renderer =
                renderer;

            part.mesh =
                mesh;

            chunk.parts.Add(
                part);
        }

        // ============================================================
        // BUILD PART
        // ============================================================

        private void BuildChunkPart(
            ChunkMaterialPart part)
        {
            if (part == null ||
                part.mesh == null)
                return;

            part.mesh.Clear();

            if (part.combines.Count == 0)
            {
                if (part.renderer != null)
                    part.renderer.enabled = false;

                return;
            }

            part.mesh.CombineMeshes(
                part.combines.ToArray(),
                true,
                true,
                false);

            part.mesh.RecalculateBounds();

            if (part.renderer != null)
                part.renderer.enabled = true;
        }

        // ============================================================
        // CLEAR CHUNK PARTS
        // ============================================================

        private void ClearChunkParts(
            Chunk chunk)
        {
            if (chunk == null)
                return;

            for (int i = 0;
                 i < chunk.parts.Count;
                 i++)
            {
                ChunkMaterialPart part =
                    chunk.parts[i];

                if (part == null)
                    continue;

                if (part.mesh != null)
                {
                    DestroySafe(
                        part.mesh);
                }

                if (part.gameObject != null)
                {
                    DestroySafe(
                        part.gameObject);
                }
            }

            chunk.parts.Clear();
        }

        // ============================================================
        // RENDER STATE
        // ============================================================

        private void HandleRenderStateChanged(
            HayStraw straw,
            bool dynamic)
        {
            if (!initialized ||
                straw == null)
                return;

            int oldChunk;

            if (!strawChunk.TryGetValue(
                    straw,
                    out oldChunk))
            {
                oldChunk =
                    GetChunkIndex(
                        straw.transform.localPosition);

                strawChunk[straw] =
                    oldChunk;

                if (oldChunk >= 0 &&
                    oldChunk < chunks.Count &&
                    !chunks[oldChunk].straws.Contains(straw))
                {
                    chunks[oldChunk]
                        .straws
                        .Add(straw);
                }
            }

            // --------------------------------------------------------
            // STRAW BECOMES DYNAMIC
            // --------------------------------------------------------

            if (dynamic)
            {
                if (!dynamicStraws.Add(straw))
                    return;

                // Keep the individual renderer hidden until the old combined
                // mesh has been rebuilt without this straw. This avoids z-fighting.
                straw.SetRenderersVisible(false);

                MarkChunkDirty(
                    oldChunk);

                return;
            }

            // --------------------------------------------------------
            // STRAW BECOMES STATIC
            // --------------------------------------------------------

            if (!dynamicStraws.Remove(straw))
                return;

            straw.SetRenderersVisible(
                false);

            int newChunk =
                GetChunkIndex(
                    straw.transform.localPosition);

            strawChunk[straw] =
                newChunk;

            if (newChunk != oldChunk)
            {
                MoveStrawBetweenChunks(
                    straw,
                    oldChunk,
                    newChunk);
            }

            MarkChunkDirty(
                oldChunk);

            MarkChunkDirty(
                newChunk);
        }

        // ============================================================
        // MOVE STRAW BETWEEN CHUNKS
        // ============================================================

        private void MoveStrawBetweenChunks(
            HayStraw straw,
            int oldChunk,
            int newChunk)
        {
            if (oldChunk >= 0 &&
                oldChunk < chunks.Count)
            {
                chunks[oldChunk]
                    .straws
                    .Remove(straw);
            }

            if (newChunk >= 0 &&
                newChunk < chunks.Count)
            {
                List<HayStraw> target =
                    chunks[newChunk].straws;

                if (!target.Contains(straw))
                    target.Add(straw);
            }
        }

        // ============================================================
        // DIRTY
        // ============================================================

        private void MarkChunkDirty(
            int chunkIndex)
        {
            if (chunkIndex < 0 ||
                chunkIndex >= chunks.Count)
                return;

            dirtyChunks.Add(
                chunkIndex);

            if (!batchRebuilds)
            {
                RebuildSingleChunk(
                    chunkIndex);

                dirtyChunks.Remove(
                    chunkIndex);
            }
        }

        // ============================================================
        // DIRTY PROCESSING
        // ============================================================

        private void ProcessDirtyChunks()
        {
            if (dirtyChunks.Count == 0)
                return;

            dirtyChunkBuffer.Clear();

            foreach (int index in dirtyChunks)
            {
                dirtyChunkBuffer.Add(
                    index);
            }

            int configuredLimit =
                maxChunkRebuildsPerFrame <= 0
                    ? 2
                    : maxChunkRebuildsPerFrame;

            int rebuildLimit =
                Mathf.Min(
                    configuredLimit,
                    dirtyChunkBuffer.Count);

            for (int i = 0;
                 i < rebuildLimit;
                 i++)
            {
                int chunkIndex =
                    dirtyChunkBuffer[i];

                RebuildSingleChunk(
                    chunkIndex);

                dirtyChunks.Remove(
                    chunkIndex);
            }
        }

        // ============================================================
        // SINGLE CHUNK REBUILD
        // ============================================================

        private void RebuildSingleChunk(
            int chunkIndex)
        {
            if (chunkIndex < 0 ||
                chunkIndex >= chunks.Count)
                return;

            Chunk chunk =
                chunks[chunkIndex];

            BuildChunk(chunk);

            // Reveal dynamic straws only after the old combined mesh has
            // been rebuilt and no longer contains their geometry.
            foreach (HayStraw straw in dynamicStraws)
            {
                if (straw == null)
                    continue;

                int owner;
                if (strawChunk.TryGetValue(straw, out owner) &&
                    owner == chunkIndex)
                {
                    straw.SetRenderersVisible(true);
                }
            }
        }

        // ============================================================
        // CHUNK INDEX
        // ============================================================

        private int GetChunkIndex(
            Vector3 localPosition)
        {
            float diameter =
                Mathf.Max(
                    0.1f,
                    pileRadius * 2f);

            float x01 =
                Mathf.Clamp01(
                    (localPosition.x + pileRadius) /
                    diameter);

            float z01 =
                Mathf.Clamp01(
                    (localPosition.z + pileRadius) /
                    diameter);

            int x =
                Mathf.Min(
                    columns - 1,
                    Mathf.FloorToInt(
                        x01 * columns));

            int z =
                Mathf.Min(
                    rows - 1,
                    Mathf.FloorToInt(
                        z01 * rows));

            return
                z * columns + x;
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Полностью помечает систему на rebuild.
        /// Реальный rebuild произойдет в LateUpdate.
        /// </summary>
        public void RequestRebuild()
        {
            rebuildQueued = true;
        }

        /// <summary>
        /// Принудительно перестроить все render chunks.
        /// Лучше не вызывать во время DeformAt.
        /// </summary>
        public void ForceRebuildNow()
        {
            if (generator == null)
                generator =
                    GetComponent<HayPileGenerator>();

            if (generator == null)
                return;

            if (dynamicStraws.Count > 0)
            {
                rebuildQueued = true;
                return;
            }

            RebuildFromGenerator();
        }

        // ============================================================
        // DESTROY
        // ============================================================

        private void DestroySafe(
            Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        // ============================================================
        // DESTROY
        // ============================================================

        private void OnDestroy()
        {
            HayStraw.OnRenderStateChanged -=
                HandleRenderStateChanged;

            for (int i = 0;
                 i < chunks.Count;
                 i++)
            {
                Chunk chunk =
                    chunks[i];

                if (chunk == null)
                    continue;

                if (chunk.parts != null)
                {
                    for (int p = 0;
                         p < chunk.parts.Count;
                         p++)
                    {
                        ChunkMaterialPart part =
                            chunk.parts[p];

                        if (part == null)
                            continue;

                        if (part.mesh != null)
                            DestroySafe(part.mesh);

                        if (part.gameObject != null)
                            DestroySafe(part.gameObject);
                    }
                }

                if (chunk.gameObject != null)
                    DestroySafe(chunk.gameObject);
            }

            chunks.Clear();
        }
    }
}