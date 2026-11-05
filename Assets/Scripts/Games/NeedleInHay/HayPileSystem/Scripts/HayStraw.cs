using System;
using UnityEngine;

namespace HayPileSystem
{
    [DisallowMultipleComponent]
    public class HayStraw : MonoBehaviour
    {
        // ============================================================
        // EVENTS
        // ============================================================

        public static event Action<HayStraw, bool> OnRenderStateChanged;

        // ============================================================
        // STATE
        // ============================================================

        public bool IsDetachedFromPile { get; private set; }
        public bool IsSurface { get; private set; }
        public bool IsTaken { get; private set; }
        public bool IsReplacementMoving { get; private set; }

        public Vector3 BaseLocalPosition { get; private set; }
        public Quaternion BaseLocalRotation { get; private set; }
        public Vector3 TargetLocalPosition { get; private set; }
        public Quaternion TargetLocalRotation { get; private set; }

        public float DeformationRandom { get; private set; } = 1f;

        /// <summary>
        /// Можно ли сейчас использовать соломинку
        /// для interaction/pickup.
        /// </summary>
        public bool InteractionEnabled
        {
            get;
            private set;
        } = true;

        // ============================================================
        // MOVEMENT STATE
        // ============================================================

        private float movementMultiplier = 1f;
        private float personalPhase;
        private float personalBounce;

        private bool externalMovement;

        private Vector3 velocity;

        // ============================================================
        // CACHED COMPONENTS
        // ============================================================

        private Collider[] cachedColliders;
        private Renderer[] cachedRenderers;

        // ============================================================
        // RENDER CHUNK
        // ============================================================

        private bool renderChunkManaged;
        private bool dynamicRenderState;

        // ============================================================
        // BOUNDS CACHE
        // ============================================================

        /*
         * ВАЖНО:
         *
         * Этот Bounds вычисляется один раз в Awake.
         *
         * Раньше interaction collider мог делать:
         *
         * GetComponentsInChildren<MeshFilter>()
         *
         * для каждой соломинки во время клика.
         *
         * Здесь мы один раз собираем геометрию,
         * после чего при каждом клике только трансформируем
         * уже готовый Bounds.
         */

        private Bounds cachedLocalBounds;

        private bool hasCachedLocalBounds;

        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("MOVEMENT")]

        [Min(0.01f)]
        [SerializeField]
        private float moveSpeed = 2.8f;

        [Min(0.0001f)]
        [SerializeField]
        private float positionSnapDistance = 0.004f;

        [Range(0f, 1f)]
        [SerializeField]
        private float movementSmoothness = 0.82f;

        [Min(0f)]
        [SerializeField]
        private float settlingStrength = 0.025f;

        [Min(0f)]
        [SerializeField]
        private float settlingFrequency = 7f;

        [Header("ROTATION")]

        [Min(0.01f)]
        [SerializeField]
        private float rotationSpeed = 230f;

        [Min(0.01f)]
        [SerializeField]
        private float rotationSnapAngle = 0.35f;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            personalPhase =
                UnityEngine.Random.Range(
                    0f,
                    Mathf.PI * 2f);

            personalBounce =
                UnityEngine.Random.Range(
                    0.75f,
                    1.25f);

            CacheComponents();

            // ========================================================
            // НОВОЕ:
            // bounds считаем один раз.
            // ========================================================

            CacheLocalBounds();

            InteractionEnabled = true;

            enabled = false;
        }

        // ============================================================
        // COMPONENT CACHE
        // ============================================================

        private void CacheComponents()
        {
            if (cachedColliders == null)
            {
                cachedColliders =
                    GetComponentsInChildren<Collider>(true);
            }

            if (cachedRenderers == null)
            {
                cachedRenderers =
                    GetComponentsInChildren<Renderer>(true);
            }
        }

        // ============================================================
        // BOUNDS CACHE
        // ============================================================

        private void CacheLocalBounds()
        {
            cachedLocalBounds =
                new Bounds(
                    Vector3.zero,
                    Vector3.zero);

            bool initialized =
                false;

            Transform root =
                transform;

            // --------------------------------------------------------
            // MESH FILTERS
            // --------------------------------------------------------

            MeshFilter[] filters =
                GetComponentsInChildren<MeshFilter>(true);

            if (filters != null)
            {
                for (int i = 0;
                     i < filters.Length;
                     i++)
                {
                    MeshFilter filter =
                        filters[i];

                    if (filter == null)
                        continue;

                    Mesh mesh =
                        filter.sharedMesh;

                    if (mesh == null)
                        continue;

                    Matrix4x4 childToRoot =
                        GetChildToRootMatrix(
                            filter.transform,
                            root);

                    EncapsulateTransformedBounds(
                        ref cachedLocalBounds,
                        ref initialized,
                        mesh.bounds,
                        childToRoot);
                }
            }

            // --------------------------------------------------------
            // SKINNED MESH
            // --------------------------------------------------------

            SkinnedMeshRenderer[] skinned =
                GetComponentsInChildren<
                    SkinnedMeshRenderer>(true);

            if (skinned != null)
            {
                for (int i = 0;
                     i < skinned.Length;
                     i++)
                {
                    SkinnedMeshRenderer renderer =
                        skinned[i];

                    if (renderer == null)
                        continue;

                    Matrix4x4 childToRoot =
                        GetChildToRootMatrix(
                            renderer.transform,
                            root);

                    EncapsulateTransformedBounds(
                        ref cachedLocalBounds,
                        ref initialized,
                        renderer.localBounds,
                        childToRoot);
                }
            }

            // --------------------------------------------------------
            // FALLBACK
            // --------------------------------------------------------

            if (!initialized)
            {
                cachedLocalBounds =
                    new Bounds(
                        Vector3.zero,
                        Vector3.one);
            }

            hasCachedLocalBounds =
                true;
        }

        // ============================================================
        // CHILD -> ROOT MATRIX
        // ============================================================

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

        // ============================================================
        // ENCAPSULATE TRANSFORMED BOUNDS
        // ============================================================

        private static void EncapsulateTransformedBounds(
            ref Bounds target,
            ref bool initialized,
            Bounds source,
            Matrix4x4 matrix)
        {
            Vector3 min =
                source.min;

            Vector3 max =
                source.max;

            // 8 углов bounds.
            for (int i = 0; i < 8; i++)
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
        // WORLD BOUNDS
        // ============================================================

        /// <summary>
        /// Получает текущий world-space Bounds.
        ///
        /// Не ищет MeshFilter/Renderer.
        /// Использует только заранее закэшированный Bounds.
        /// </summary>
        public bool TryGetWorldBounds(
            out Bounds bounds,
            float padding = 0f)
        {
            if (!hasCachedLocalBounds)
            {
                bounds =
                    new Bounds(
                        transform.position,
                        Vector3.one);

                return false;
            }

            bounds =
                TransformBounds(
                    cachedLocalBounds,
                    transform.localToWorldMatrix);

            if (padding > 0f)
            {
                bounds.Expand(
                    padding * 2f);
            }

            return true;
        }

        // ============================================================
        // PILE LOCAL BOUNDS
        // ============================================================

        /// <summary>
        /// Получает Bounds в локальных координатах pileRoot.
        ///
        /// Это используется HayPileDeformationGrid.
        /// </summary>
        public bool TryGetPileLocalBounds(
            Transform pileRoot,
            out Bounds bounds,
            float padding = 0f)
        {
            if (!hasCachedLocalBounds)
            {
                bounds =
                    new Bounds(
                        transform.position,
                        Vector3.one);

                return false;
            }

            Matrix4x4 matrix;

            if (pileRoot != null)
            {
                matrix =
                    pileRoot.worldToLocalMatrix *
                    transform.localToWorldMatrix;
            }
            else
            {
                matrix =
                    transform.localToWorldMatrix;
            }

            bounds =
                TransformBounds(
                    cachedLocalBounds,
                    matrix);

            if (padding > 0f)
            {
                bounds.Expand(
                    padding * 2f);
            }

            return true;
        }

        // ============================================================
        // TRANSFORM BOUNDS
        // ============================================================

        private static Bounds TransformBounds(
            Bounds source,
            Matrix4x4 matrix)
        {
            Vector3 min =
                source.min;

            Vector3 max =
                source.max;

            Vector3 first =
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        min.x,
                        min.y,
                        min.z));

            Bounds result =
                new Bounds(
                    first,
                    Vector3.zero);

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        max.x,
                        min.y,
                        min.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        max.x,
                        max.y,
                        min.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        min.x,
                        max.y,
                        min.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        min.x,
                        min.y,
                        max.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        max.x,
                        min.y,
                        max.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        max.x,
                        max.y,
                        max.z)));

            result.Encapsulate(
                matrix.MultiplyPoint3x4(
                    new Vector3(
                        min.x,
                        max.y,
                        max.z)));

            return result;
        }

        // ============================================================
        // SIMULATION
        // ============================================================

        private void LateUpdate()
        {
            if (externalMovement || IsDetachedFromPile)
            {
                DisableSimulation();
                return;
            }

            UpdatePosition();
            UpdateRotation();

            if (HasReachedTarget())
            {
                transform.localPosition =
                    TargetLocalPosition;

                transform.localRotation =
                    TargetLocalRotation;

                velocity =
                    Vector3.zero;

                DisableSimulation();

                SetDynamicRender(false);
            }
        }

        // ============================================================
        // INITIALIZE
        // ============================================================

        public void Initialize(
            Vector3 localPosition,
            Quaternion localRotation,
            bool isSurface,
            float deformationRandom = 1f,
            float movementRandom = 1f)
        {
            IsTaken = false;
            IsDetachedFromPile = false;
            IsSurface = isSurface;
            IsReplacementMoving = false;

            externalMovement = false;

            InteractionEnabled = true;

            DeformationRandom =
                Mathf.Clamp(
                    deformationRandom,
                    0.5f,
                    1.5f);

            movementMultiplier =
                Mathf.Clamp(
                    movementRandom,
                    0.6f,
                    1.4f);

            BaseLocalPosition =
                localPosition;

            BaseLocalRotation =
                NormalizeQuaternion(
                    localRotation);

            TargetLocalPosition =
                localPosition;

            TargetLocalRotation =
                BaseLocalRotation;

            velocity =
                Vector3.zero;

            transform.localPosition =
                localPosition;

            transform.localRotation =
                BaseLocalRotation;

            SetDynamicRender(false);

            DisableSimulation();
        }

        // ============================================================
        // DEFORMED TARGET
        // ============================================================

        public void SetDeformedTarget(
            Vector3 localPosition,
            Quaternion localRotation)
        {
            if (IsDetachedFromPile ||
                IsTaken ||
                IsReplacementMoving)
            {
                return;
            }

            TargetLocalPosition =
                localPosition;

            TargetLocalRotation =
                NormalizeQuaternion(
                    localRotation);

            if (HasReachedTarget())
            {
                transform.localPosition =
                    TargetLocalPosition;

                transform.localRotation =
                    TargetLocalRotation;

                velocity =
                    Vector3.zero;

                DisableSimulation();

                SetDynamicRender(false);

                return;
            }

            SetDynamicRender(true);

            EnableSimulation();
        }

        // ============================================================
        // VISUAL STATE
        // ============================================================

        public void SetVisualState(
            Vector3 localPosition,
            Quaternion localRotation)
        {
            transform.localPosition =
                localPosition;

            transform.localRotation =
                localRotation;

            TargetLocalPosition =
                localPosition;

            TargetLocalRotation =
                NormalizeQuaternion(
                    localRotation);

            velocity =
                Vector3.zero;

            SetDynamicRender(false);

            DisableSimulation();
        }

        // ============================================================
        // POSITION
        // ============================================================

        private void UpdatePosition()
        {
            Vector3 current =
                transform.localPosition;

            Vector3 target =
                TargetLocalPosition;

            float distance =
                Vector3.Distance(
                    current,
                    target);

            if (distance <= positionSnapDistance)
            {
                transform.localPosition =
                    target;

                velocity =
                    Vector3.zero;

                return;
            }

            float speed =
                moveSpeed *
                movementMultiplier;

            float smooth =
                Mathf.Lerp(
                    0.65f,
                    1.25f,
                    movementSmoothness);

            Vector3 delta =
                target -
                current;

            if (delta.sqrMagnitude >
                0.000001f)
            {
                velocity =
                    Vector3.Lerp(
                        velocity,
                        delta.normalized * speed,
                        smooth *
                        Time.deltaTime *
                        8f);
            }

            Vector3 next =
                Vector3.MoveTowards(
                    current,
                    target,
                    speed *
                    Time.deltaTime);

            float remaining =
                Vector3.Distance(
                    next,
                    target);

            float settling01 =
                Mathf.Clamp01(
                    1f -
                    remaining /
                    0.7f);

            if (settlingStrength > 0f &&
                settling01 > 0f)
            {
                float wave =
                    Mathf.Sin(
                        Time.time *
                        settlingFrequency +
                        personalPhase);

                Vector3 settling =
                    Vector3.down *
                    wave *
                    settlingStrength *
                    settling01 *
                    personalBounce *
                    Time.deltaTime;

                next +=
                    settling;

                if (next.y < target.y)
                    next.y =
                        target.y;
            }

            transform.localPosition =
                next;
        }

        // ============================================================
        // ROTATION
        // ============================================================

        private void UpdateRotation()
        {
            Quaternion current =
                transform.localRotation;

            Quaternion target =
                TargetLocalRotation;

            float angle =
                Quaternion.Angle(
                    current,
                    target);

            if (angle <= rotationSnapAngle)
            {
                transform.localRotation =
                    target;

                return;
            }

            transform.localRotation =
                Quaternion.RotateTowards(
                    current,
                    target,
                    rotationSpeed *
                    movementMultiplier *
                    Time.deltaTime);
        }

        // ============================================================
        // TARGET CHECK
        // ============================================================

        private bool HasReachedTarget()
        {
            float sqr =
                Vector3.SqrMagnitude(
                    transform.localPosition -
                    TargetLocalPosition);

            if (sqr >
                positionSnapDistance *
                positionSnapDistance)
            {
                return false;
            }

            return
                Quaternion.Angle(
                    transform.localRotation,
                    TargetLocalRotation)
                <= rotationSnapAngle;
        }

        // ============================================================
        // SIMULATION ENABLE / DISABLE
        // ============================================================

        private void EnableSimulation()
        {
            enabled = true;
        }

        private void DisableSimulation()
        {
            enabled = false;
        }

        // ============================================================
        // DYNAMIC RENDER
        // ============================================================

        private void SetDynamicRender(
            bool dynamic)
        {
            if (!renderChunkManaged)
            {
                SetRenderersVisible(true);
                return;
            }

            if (dynamicRenderState == dynamic)
                return;

            dynamicRenderState =
                dynamic;

            OnRenderStateChanged?.Invoke(
                this,
                dynamic);
        }

        // ============================================================
        // RENDER CHUNK MANAGED
        // ============================================================

        internal void SetRenderChunkManaged(
            bool managed)
        {
            renderChunkManaged =
                managed;

            if (managed)
            {
                SetRenderersVisible(false);
            }
            else
            {
                SetRenderersVisible(true);
            }
        }

        // ============================================================
        // RENDERERS
        // ============================================================

        internal void SetRenderersVisible(
            bool visible)
        {
            CacheComponents();

            for (int i = 0;
                 i < cachedRenderers.Length;
                 i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].enabled =
                        visible;
                }
            }
        }

        // ============================================================
        // DETACH
        // ============================================================

        public void DetachFromPile()
        {
            IsDetachedFromPile =
                true;

            IsReplacementMoving =
                false;

            externalMovement =
                true;

            InteractionEnabled =
                false;

            SetDynamicRender(true);

            DisableSimulation();
        }

        // ============================================================
        // PICKUP
        // ============================================================

        public void MarkTakenForPickup()
        {
            IsTaken =
                true;

            IsDetachedFromPile =
                true;

            IsReplacementMoving =
                false;

            externalMovement =
                true;

            InteractionEnabled =
                false;

            SetDynamicRender(true);

            DisableSimulation();
        }

        // ============================================================
        // EXTERNAL MOVEMENT
        // ============================================================

        public void BeginExternalMovement()
        {
            externalMovement =
                true;

            SetDynamicRender(true);

            DisableSimulation();
        }

        public void FinishExternalMovement()
        {
            if (IsDetachedFromPile)
            {
                externalMovement =
                    true;

                SetDynamicRender(true);

                DisableSimulation();

                return;
            }

            externalMovement =
                false;

            if (!HasReachedTarget())
            {
                SetDynamicRender(true);
                EnableSimulation();
            }
            else
            {
                SetDynamicRender(false);
                DisableSimulation();
            }
        }

        // ============================================================
        // TAKE
        // ============================================================

        public void Take()
        {
            IsTaken =
                true;

            IsDetachedFromPile =
                true;

            IsReplacementMoving =
                false;

            externalMovement =
                true;

            InteractionEnabled =
                false;

            SetDynamicRender(true);

            DisableSimulation();

            SetRenderersVisible(false);

            gameObject.SetActive(false);
        }

        // ============================================================
        // REATTACH
        // ============================================================

        public void ReattachToPile()
        {
            IsTaken =
                false;

            IsDetachedFromPile =
                false;

            IsReplacementMoving =
                false;

            externalMovement =
                false;

            InteractionEnabled =
                true;

            TargetLocalPosition =
                BaseLocalPosition;

            TargetLocalRotation =
                BaseLocalRotation;

            transform.localPosition =
                BaseLocalPosition;

            transform.localRotation =
                BaseLocalRotation;

            velocity =
                Vector3.zero;

            SetDynamicRender(false);

            DisableSimulation();
        }

        // ============================================================
        // RESERVE STRAW
        // ============================================================

        public void ActivateAsInnerStraw(
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            IsTaken =
                false;

            IsDetachedFromPile =
                false;

            IsSurface =
                false;

            IsReplacementMoving =
                false;

            externalMovement =
                false;

            InteractionEnabled =
                true;

            TargetLocalPosition =
                targetPosition;

            TargetLocalRotation =
                NormalizeQuaternion(
                    targetRotation);

            transform.localPosition =
                targetPosition;

            transform.localRotation =
                TargetLocalRotation;

            velocity =
                Vector3.zero;

            SetDynamicRender(false);

            DisableSimulation();
        }

        // ============================================================
        // INTERACTION
        // ============================================================

        public void SetInteractionEnabled(
            bool enabledState)
        {
            InteractionEnabled =
                enabledState;

            CacheComponents();

            for (int i = 0;
                 i < cachedColliders.Length;
                 i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled =
                        enabledState;
                }
            }
        }

        // ============================================================
        // RESET
        // ============================================================

        public void ResetToBase()
        {
            if (IsDetachedFromPile ||
                IsReplacementMoving)
            {
                return;
            }

            TargetLocalPosition =
                BaseLocalPosition;

            TargetLocalRotation =
                BaseLocalRotation;

            transform.localPosition =
                BaseLocalPosition;

            transform.localRotation =
                BaseLocalRotation;

            velocity =
                Vector3.zero;

            SetDynamicRender(false);

            DisableSimulation();
        }

        // ============================================================
        // QUATERNION
        // ============================================================

        private static Quaternion NormalizeQuaternion(
            Quaternion q)
        {
            float magnitude =
                Mathf.Sqrt(
                    q.x * q.x +
                    q.y * q.y +
                    q.z * q.z +
                    q.w * q.w);

            if (magnitude <
                0.000001f)
            {
                return Quaternion.identity;
            }

            float inv =
                1f / magnitude;

            return new Quaternion(
                q.x * inv,
                q.y * inv,
                q.z * inv,
                q.w * inv);
        }
    }
}