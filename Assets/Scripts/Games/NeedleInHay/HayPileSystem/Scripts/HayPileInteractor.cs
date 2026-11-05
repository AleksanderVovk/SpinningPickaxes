using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HayPileSystem
{
    public class HayPileInteractor : MonoBehaviour
    {
        public enum ToolType { Hand, Pitchfork }

        [System.Serializable]
        public class ToolSlotUI
        {
            public Button button;
            public Image cooldownImage;
            public GameObject selectedVisual;
            public GameObject lockIcon;
        }

        [Header("Tool unlocking")]
        [SerializeField] private bool pitchforkUnlockedAtStart = false;

        private bool pitchforkUnlocked;

        [Header("Player / Raycast")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private HayPileGenerator pile;
        [SerializeField] private float interactionDistance = 3.5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private KeyCode collectKey = KeyCode.E;

        [Header("Tool switching")]
        [SerializeField] private KeyCode toggleToolKey = KeyCode.Q;
        [SerializeField] private KeyCode handKey = KeyCode.Alpha1;
        [SerializeField] private KeyCode pitchforkKey = KeyCode.Alpha2;
        [SerializeField] private GameObject handToolVisual;
        [SerializeField] private GameObject pitchforkToolVisual;
        [SerializeField] private ToolType startingTool = ToolType.Hand;

        [Header("Tool destinations")]
        [SerializeField] private Transform handDestination;
        [SerializeField] private Transform pitchforkDestination;

        [Header("Fast tool slots")]
        [SerializeField] private ToolSlotUI handSlot;
        [SerializeField] private ToolSlotUI pitchforkSlot;

        [Header("Inventory")]
        [SerializeField] private Transform handAnchor;
        [SerializeField] private Transform stackTargetRoot;
        [SerializeField] private TMP_Text inventoryText;
        [SerializeField] private Image inventoryFillImage;
        [SerializeField] private GameObject heldHayVisual;
        [SerializeField, Min(0.01f)] private float stackSpacing = 0.035f;

        [Header("Hay stack layout")]
        [SerializeField, Min(1)] private int stackColumns = 3;
        [SerializeField, Min(1)] private int stackRows = 3;
        [SerializeField, Min(0.001f)] private float stackLayerHeight = 0.035f;

        [Header("Hay stack random rotation")]
        [SerializeField] private float randomYawMin = -25f;
        [SerializeField] private float randomYawMax = 25f;
        [SerializeField] private float randomTiltMin = -6f;
        [SerializeField] private float randomTiltMax = 6f;

        [Header("Collect pulse visual")]
        [SerializeField] private GameObject hayCollectPulseVisual;
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField, Min(0.01f)] private float pulseDuration = 0.18f;

        [Header("Straw flight")]
        [SerializeField, Min(0.01f)] private float strawFlyDuration = 0.25f;
        [SerializeField] private float strawFlyHeight = 0.15f;
        [SerializeField] private AnimationCurve flyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Decorative pitchfork straw")]
        [SerializeField] private GameObject decorativeStrawPrefab;
        [SerializeField, Min(0)] private int decorativeStrawMin = 5;
        [SerializeField, Min(0)] private int decorativeStrawMax = 8;
        [SerializeField, Min(0.01f)] private float decorativeFlyDuration = 0.35f;
        [SerializeField] private float decorativeFlyHeight = 0.35f;
        [SerializeField] private Vector3 decorativeSpawnBox = new Vector3(0.25f, 0.12f, 0.25f);

        [Header("Collection")]
        [SerializeField, Min(1)] private int startingCapacity = 20;
        [SerializeField, Min(1)] private int pitchforkStrawsPerAction = 3;
        [SerializeField, Min(0.01f)] private float startingHandCooldown = 0.6f;
        [SerializeField, Min(0.01f)] private float startingPitchforkCooldown = 0.6f;

        [Tooltip("Legacy default. If enabled, hold-to-collect starts unlocked for both tools.")]
        [SerializeField] private bool startWithHoldToCollect = false;

        [Header("Pitchfork Animation")]
        [SerializeField] private HayPitchforkMotion pitchforkMotion;
        [SerializeField] private AudioClip[] _hitClips;

        [Header("Mobile collect button")]
        [SerializeField] private Button mobileCollectButton;

        private readonly List<HayStraw> heldStraws = new List<HayStraw>();
        private readonly HashSet<HayStraw> inFlightStraws = new HashSet<HayStraw>();
        private readonly List<HayStraw> pitchforkHeldStraws = new List<HayStraw>();

        private Transform pitchforkBundleRoot;
        private float nextHandCollectionTime;
        private float nextPitchforkCollectionTime;
        private float handCooldown;
        private float pitchforkCooldown;

        private bool handHoldToCollect;
        private bool pitchforkHoldToCollect;

        private int handStrawsPerAction = 1;

        private ToolType activeTool;
        private int pendingBonusSaleUnits;
        private bool pitchforkBundleLanded;
        private bool pitchforkTransferInProgress;

        private bool mobileCollectHeld;
        private Coroutine hayPulseCoroutine;

        public int RealHayInHands => heldStraws.Count;
        public int HayInHands => heldStraws.Count + pendingBonusSaleUnits;
        public bool IsSelling { get; private set; }
        public ToolType ActiveTool => activeTool;
        public int Capacity { get; private set; }

        public bool HoldToCollectEnabled =>
            activeTool == ToolType.Hand ? handHoldToCollect : pitchforkHoldToCollect;

        public float CollectionCooldown => GetToolCollectionCooldown(activeTool);
        public Transform HandDestination => handDestination != null ? handDestination : handAnchor;
        public Transform PitchforkDestination => pitchforkDestination;
        public IReadOnlyList<HayStraw> HeldStraws => heldStraws;
        public float HandCooldown => handCooldown;
        public float PitchforkCooldown => pitchforkCooldown;

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (handDestination == null)
                handDestination = handAnchor;

            if (stackTargetRoot == null)
                stackTargetRoot = handAnchor;

            Capacity = Mathf.Max(1, startingCapacity);
            handCooldown = Mathf.Max(0.01f, startingHandCooldown);
            pitchforkCooldown = Mathf.Max(0.01f, startingPitchforkCooldown);
            handHoldToCollect = startWithHoldToCollect;
            pitchforkHoldToCollect = startWithHoldToCollect;
            handStrawsPerAction = 1;
            pitchforkStrawsPerAction = Mathf.Max(1, pitchforkStrawsPerAction);
            activeTool = startingTool;

            pitchforkUnlocked = pitchforkUnlockedAtStart;

            if (!pitchforkUnlocked && activeTool == ToolType.Pitchfork)
                activeTool = ToolType.Hand;

            SetupToolSlots();
            UpdateToolVisuals();
            RefreshInventoryUI();
            UpdateToolSlots();

            SetupMobileCollectButton();
        }

        public bool IsPitchforkUnlocked()
        {
            return pitchforkUnlocked;
        }

        public void UnlockPitchfork()
        {
            pitchforkUnlocked = true;

            UpdateToolVisuals();
            UpdateToolSlots();
        }

        private void SetupMobileCollectButton()
        {
            if (mobileCollectButton == null)
                return;

            mobileCollectButton.onClick.RemoveAllListeners();
            mobileCollectButton.onClick.AddListener(OnMobileCollectClicked);

            EventTrigger trigger = mobileCollectButton.GetComponent<EventTrigger>();

            if (trigger == null)
                trigger = mobileCollectButton.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener(delegate { OnMobileCollectPressed(); });
            trigger.triggers.Add(pointerDown);

            EventTrigger.Entry pointerUp = new EventTrigger.Entry();
            pointerUp.eventID = EventTriggerType.PointerUp;
            pointerUp.callback.AddListener(delegate { OnMobileCollectReleased(); });
            trigger.triggers.Add(pointerUp);

            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener(delegate { OnMobileCollectReleased(); });
            trigger.triggers.Add(pointerExit);
        }

        public void OnMobileCollectPressed()
        {
            mobileCollectHeld = true;
            TryBeginCollection();
        }

        public void OnMobileCollectReleased()
        {
            mobileCollectHeld = false;
        }

        private void OnMobileCollectClicked()
        {
            if (!HoldToCollectEnabled)
                TryBeginCollection();
        }

        private void SetupToolSlots()
        {
            if (handSlot != null && handSlot.button != null)
            {
                handSlot.button.onClick.RemoveAllListeners();
                handSlot.button.onClick.AddListener(delegate { SetActiveTool(ToolType.Hand); });
            }

            if (pitchforkSlot != null && pitchforkSlot.button != null)
            {
                pitchforkSlot.button.onClick.RemoveAllListeners();
                pitchforkSlot.button.onClick.AddListener(delegate { SetActiveTool(ToolType.Pitchfork); });
            }
        }

        private void Update()
        {
            /*
            if (Input.GetKeyDown(handKey))
                TrySetActiveTool(ToolType.Hand);

            if (Input.GetKeyDown(pitchforkKey))
                TrySetActiveTool(ToolType.Pitchfork);

            if (Input.GetKeyDown(toggleToolKey))
                TrySetActiveTool(activeTool == ToolType.Hand ? ToolType.Pitchfork : ToolType.Hand);
            */

            if (!IsSelling)
                UpdatePitchforkBundleLifecycle();

            if (IsSelling)
                return;

            bool holdUnlocked = activeTool == ToolType.Hand
                ? handHoldToCollect
                : pitchforkHoldToCollect;

            if (holdUnlocked &&
                (Input.GetMouseButton(0) || mobileCollectHeld) &&
                IsActiveToolReady())
            {
                TryBeginCollection();
            }

            /*
            if (Input.GetKeyDown(collectKey))
                TryBeginCollection();
            */

            UpdateToolSlots();
        }

        public void SetActiveTool(ToolType tool)
        {
            if (tool == ToolType.Pitchfork && !pitchforkUnlocked)
                return;

            activeTool = tool;
            UpdateToolVisuals();
            UpdateToolSlots();
        }

        private void TrySetActiveTool(ToolType tool)
        {
            if (tool == ToolType.Pitchfork && !pitchforkUnlocked)
                return;

            if (!IsToolReady(activeTool) || activeTool == tool)
                return;

            SetActiveTool(tool);
        }

        public void SetCapacity(int newCapacity)
        {
            Capacity = Mathf.Max(1, newCapacity);
            RefreshInventoryUI();
        }

        public void SetCollectionCooldown(float seconds)
        {
            SetToolCollectionCooldown(ToolType.Hand, seconds);
            SetToolCollectionCooldown(ToolType.Pitchfork, seconds);
        }

        public void SetToolCollectionCooldown(ToolType tool, float seconds)
        {
            seconds = Mathf.Max(0.01f, seconds);

            if (tool == ToolType.Hand)
                handCooldown = seconds;
            else
                pitchforkCooldown = seconds;
        }

        public float GetToolCollectionCooldown(ToolType tool) =>
            tool == ToolType.Hand ? handCooldown : pitchforkCooldown;

        public bool IsToolReady(ToolType tool) => Time.time >= GetNextCollectionTime(tool);

        public float GetCooldownFill(ToolType tool)
        {
            float cooldown = GetToolCollectionCooldown(tool);

            if (cooldown <= 0f)
                return 0f;

            return Mathf.Clamp01((GetNextCollectionTime(tool) - Time.time) / cooldown);
        }

        public void EnableHoldToCollect(bool enabled)
        {
            handHoldToCollect = enabled;
            pitchforkHoldToCollect = enabled;
        }

        public void EnableHoldToCollect(ToolType tool, bool enabled)
        {
            if (tool == ToolType.Hand)
                handHoldToCollect = enabled;
            else
                pitchforkHoldToCollect = enabled;
        }

        public void SetStrawsPerAction(ToolType tool, int amount)
        {
            amount = Mathf.Max(1, amount);

            if (tool == ToolType.Hand)
                handStrawsPerAction = amount;
            else
                pitchforkStrawsPerAction = amount;
        }

        public int GetStrawsPerAction(ToolType tool) =>
            tool == ToolType.Hand ? handStrawsPerAction : pitchforkStrawsPerAction;

        public void SetSellingState(bool selling)
        {
            IsSelling = selling;

            if (!selling)
                RefreshInventoryUI();
        }

        public int TakePendingBonusSaleUnits()
        {
            int result = pendingBonusSaleUnits;
            pendingBonusSaleUnits = 0;

            RefreshInventoryUI();

            return result;
        }

        public int TakeAllSaleUnits()
        {
            int total = HayInHands;

            heldStraws.Clear();
            inFlightStraws.Clear();
            pitchforkHeldStraws.Clear();
            pendingBonusSaleUnits = 0;

            if (pitchforkBundleRoot != null)
            {
                Destroy(pitchforkBundleRoot.gameObject);
                pitchforkBundleRoot = null;
            }

            pitchforkBundleLanded = false;
            pitchforkTransferInProgress = false;

            RefreshInventoryUI();

            return total;
        }

        private float GetNextCollectionTime(ToolType tool) =>
            tool == ToolType.Hand ? nextHandCollectionTime : nextPitchforkCollectionTime;

        private void SetNextCollectionTime(ToolType tool, float time)
        {
            if (tool == ToolType.Hand)
                nextHandCollectionTime = time;
            else
                nextPitchforkCollectionTime = time;
        }

        private bool IsActiveToolReady() => IsToolReady(activeTool);

        private void UpdateToolVisuals()
        {
            if (handToolVisual != null)
                handToolVisual.SetActive(activeTool == ToolType.Hand);

            if (pitchforkToolVisual != null)
                pitchforkToolVisual.SetActive(activeTool == ToolType.Pitchfork);
        }

        private void UpdateToolSlots()
        {
            UpdateToolSlot(handSlot, ToolType.Hand, nextHandCollectionTime, handCooldown);
            UpdateToolSlot(pitchforkSlot, ToolType.Pitchfork, nextPitchforkCollectionTime, pitchforkCooldown);
        }

        private void UpdateToolSlot(ToolSlotUI slot, ToolType tool, float nextReadyTime, float cooldown)
        {
            if (slot == null)
                return;

            bool unlocked = true;

            if (tool == ToolType.Pitchfork)
                unlocked = pitchforkUnlocked;

            float remaining = nextReadyTime - Time.time;
            float fill = cooldown <= 0f ? 0f : Mathf.Clamp01(remaining / cooldown);

            if (slot.cooldownImage != null)
            {
                slot.cooldownImage.type = Image.Type.Filled;
                slot.cooldownImage.fillMethod = Image.FillMethod.Radial360;
                slot.cooldownImage.fillOrigin = (int)Image.Origin360.Top;
                slot.cooldownImage.fillClockwise = true;
                slot.cooldownImage.fillAmount = fill;
                slot.cooldownImage.raycastTarget = false;
            }

            if (slot.selectedVisual != null)
                slot.selectedVisual.SetActive(activeTool == tool && unlocked);

            if (slot.lockIcon != null)
                slot.lockIcon.SetActive(!unlocked);

            if (slot.button != null)
                slot.button.interactable = unlocked && IsToolReady(activeTool);
        }

        public void TryBeginCollection()
        {
            if (playerCamera == null || pile == null || IsSelling)
                return;

            if (!IsActiveToolReady())
                return;

            if (activeTool == ToolType.Pitchfork && !pitchforkUnlocked)
                return;

            if (activeTool == ToolType.Pitchfork &&
                (pitchforkBundleRoot != null || pitchforkTransferInProgress))
                return;

            if (HayInHands >= Capacity)
            {
                RefreshInventoryUI();
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance,
                    interactionMask, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider.GetComponentInParent<HayShopSeller>() != null)
                return;

            if (!pile.TryGetStrawFromRaycast(ray, hit, out HayStraw firstStraw) ||
                firstStraw == null)
                return;

            int desired = activeTool == ToolType.Hand
                ? handStrawsPerAction
                : pitchforkStrawsPerAction;

            int room = Mathf.Max(0, Capacity - HayInHands);
            int targetCount = Mathf.Min(desired, room);

            if (targetCount <= 0)
                return;

            List<HayStraw> found = new List<HayStraw>(targetCount);
            HashSet<HayStraw> unique = new HashSet<HayStraw>();

            if (unique.Add(firstStraw))
                found.Add(firstStraw);

            Vector2[] offsets =
            {
                new Vector2(-0.018f, 0f), new Vector2(0.018f, 0f),
                new Vector2(0f, -0.018f), new Vector2(0f, 0.018f),
                new Vector2(-0.035f, 0.012f), new Vector2(0.035f, 0.012f),
                new Vector2(-0.025f, -0.025f), new Vector2(0.025f, -0.025f),
                new Vector2(0.05f, 0f), new Vector2(-0.05f, 0f),
                new Vector2(0f, 0.04f), new Vector2(0f, -0.04f),
                new Vector2(-0.07f, 0f), new Vector2(0.07f, 0f),
                new Vector2(0f, 0.07f), new Vector2(0f, -0.07f),
                new Vector2(-0.09f, 0.04f), new Vector2(0.09f, 0.04f),
                new Vector2(-0.09f, -0.04f), new Vector2(0.09f, -0.04f)
            };

            for (int i = 0; i < offsets.Length && found.Count < targetCount; i++)
            {
                Vector3 viewportPoint = new Vector3(
                    0.5f + offsets[i].x,
                    0.5f + offsets[i].y,
                    0f
                );

                Ray extraRay = playerCamera.ViewportPointToRay(viewportPoint);

                if (!Physics.Raycast(extraRay, out RaycastHit extraHit, interactionDistance,
                        interactionMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (extraHit.collider.GetComponentInParent<HayShopSeller>() != null)
                    continue;

                if (pile.TryGetStrawFromRaycast(extraRay, extraHit, out HayStraw candidate) &&
                    candidate != null && unique.Add(candidate))
                {
                    found.Add(candidate);
                }
            }

            if (found.Count == 0)
                return;

            if (_hitClips != null && _hitClips.Length > 0 && GlobalSound.instance != null)
                GlobalSound.instance.PlaySound_Custom(_hitClips[Random.Range(0, _hitClips.Length)], true);

            if (activeTool == ToolType.Pitchfork)
                CollectWithPitchfork(found, hit.point);
            else
                CollectWithHand(found);
        }

        private void CollectWithPitchfork(List<HayStraw> found, Vector3 digPosition)
        {
            if (pitchforkMotion != null)
                pitchforkMotion.PlaySwing();

            SetNextCollectionTime(ToolType.Pitchfork,
                Time.time + GetToolCollectionCooldown(ToolType.Pitchfork));

            pitchforkBundleLanded = false;
            pitchforkHeldStraws.Clear();

            int realStrawsAdded = 0;

            foreach (HayStraw straw in found)
            {
                if (straw == null || heldStraws.Contains(straw))
                    continue;

                if (HayInHands >= Capacity)
                    break;

                pile.DeformAt(straw.transform.position, straw);
                straw.MarkTakenForPickup();
                straw.BeginExternalMovement();

                heldStraws.Add(straw);
                pitchforkHeldStraws.Add(straw);
                inFlightStraws.Add(straw);

                realStrawsAdded++;
            }

            int remainingCapacity = Mathf.Max(0, Capacity - HayInHands);
            int decorativeCount = 0;

            if (decorativeStrawPrefab != null && remainingCapacity > 0)
            {
                int min = Mathf.Max(0, decorativeStrawMin);
                int max = Mathf.Max(min, decorativeStrawMax);

                int randomCount = Random.Range(min, max + 1);
                decorativeCount = Mathf.Min(randomCount, remainingCapacity);

                pendingBonusSaleUnits += decorativeCount;
            }

            if (HayTutorial.Instance != null && heldStraws.Count > 0)
            {
                HayTutorial.Instance.OnHayCollected();
            }

            RefreshInventoryUI();
            UpdateToolSlots();
            PulseHayCollectVisual();

            if (pitchforkHeldStraws.Count > 0)
                StartCoroutine(CreateAndFlyPitchforkBundle(digPosition, decorativeCount));
        }

        private void CollectWithHand(List<HayStraw> found)
        {
            SetNextCollectionTime(ToolType.Hand,
                Time.time + GetToolCollectionCooldown(ToolType.Hand));

            foreach (HayStraw straw in found)
            {
                if (straw == null || heldStraws.Contains(straw))
                    continue;

                if (HayInHands >= Capacity)
                    break;

                pile.DeformAt(straw.transform.position, straw);
                straw.MarkTakenForPickup();
                straw.BeginExternalMovement();

                heldStraws.Add(straw);
                inFlightStraws.Add(straw);
                StartCoroutine(CollectOneStraw(straw));
            }

            if (HayTutorial.Instance != null && heldStraws.Count > 0)
            {
                HayTutorial.Instance.OnHayCollected();
            }

            RefreshInventoryUI();
            UpdateToolSlots();
            PulseHayCollectVisual();
        }

        private void PulseHayCollectVisual()
        {
            if (hayCollectPulseVisual == null)
                return;

            if (hayPulseCoroutine != null)
                StopCoroutine(hayPulseCoroutine);

            hayPulseCoroutine = StartCoroutine(PulseVisualCoroutine(hayCollectPulseVisual));
        }

        private IEnumerator PulseVisualCoroutine(GameObject target)
        {
            if (target == null)
                yield break;

            Transform targetTransform = target.transform;

            Vector3 baseScale = targetTransform.localScale;
            Vector3 maxScale = baseScale * pulseScale;

            float elapsed = 0f;

            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / pulseDuration);
                float curve = Mathf.Sin(t * Mathf.PI);

                targetTransform.localScale = Vector3.Lerp(baseScale, maxScale, curve);

                yield return null;
            }

            targetTransform.localScale = baseScale;
            hayPulseCoroutine = null;
        }

        private int GetStrawsPerLayer()
        {
            return stackColumns * stackRows;
        }

        private Vector3 CalculateStackPosition(int index)
        {
            int perLayer = GetStrawsPerLayer();

            int layer = index / perLayer;
            int indexInLayer = index % perLayer;

            int column = indexInLayer % stackColumns;
            int row = indexInLayer / stackColumns;

            float x = (column - (stackColumns - 1) * 0.5f) * stackSpacing;
            float z = (row - (stackRows - 1) * 0.5f) * stackSpacing;
            float y = layer * stackLayerHeight;

            return new Vector3(x, y, z);
        }

        private Quaternion CalculateStackRotation()
        {
            float yaw = Random.Range(randomYawMin, randomYawMax);
            float tiltX = Random.Range(randomTiltMin, randomTiltMax);
            float tiltZ = Random.Range(randomTiltMax, randomTiltMax);

            return Quaternion.Euler(tiltX, yaw, tiltZ);
        }

        private Quaternion GetStackWorldRotation() =>
            stackTargetRoot != null ? stackTargetRoot.rotation : Quaternion.identity;

        private void LayoutHeldStraws()
        {
            if (stackTargetRoot == null)
                return;

            for (int i = 0; i < heldStraws.Count; i++)
            {
                HayStraw straw = heldStraws[i];

                if (straw == null || inFlightStraws.Contains(straw))
                    continue;

                straw.transform.SetParent(stackTargetRoot, false);
                straw.transform.localPosition = CalculateStackPosition(i);
                straw.transform.localRotation = CalculateStackRotation();
            }
        }

        private IEnumerator CreateAndFlyPitchforkBundle(Vector3 spawnPosition, int decorativeCount)
        {
            GameObject rootObject = new GameObject("PitchforkHayBundle");
            Transform root = rootObject.transform;
            root.position = spawnPosition;
            root.rotation = GetStackWorldRotation();
            pitchforkBundleRoot = root;

            for (int i = 0; i < pitchforkHeldStraws.Count; i++)
            {
                HayStraw straw = pitchforkHeldStraws[i];

                if (straw != null)
                    straw.transform.SetParent(root, true);
            }

            for (int i = 0; i < decorativeCount; i++)
            {
                Vector3 localOffset = new Vector3(
                    Random.Range(-decorativeSpawnBox.x, decorativeSpawnBox.x),
                    Random.Range(-decorativeSpawnBox.y, decorativeSpawnBox.y),
                    Random.Range(-decorativeSpawnBox.z, decorativeSpawnBox.z));

                GameObject visual = Instantiate(decorativeStrawPrefab, root, false);

                if (visual == null)
                    continue;

                visual.transform.localPosition = localOffset;
                visual.transform.localRotation = Random.rotation;

                foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;

                foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                }

                HayStraw decorativeComponent = visual.GetComponentInChildren<HayStraw>(true);

                if (decorativeComponent != null)
                    decorativeComponent.enabled = false;
            }

            if (root == null || IsSelling)
                yield break;

            const float gatherDuration = 0.12f;
            float elapsed = 0f;

            List<Transform> realPieces = new List<Transform>();

            foreach (HayStraw straw in pitchforkHeldStraws)
            {
                if (straw != null)
                    realPieces.Add(straw.transform);
            }

            Vector3[] startPositions = new Vector3[realPieces.Count];
            Quaternion[] startRotations = new Quaternion[realPieces.Count];
            Vector3[] targetPositions = new Vector3[realPieces.Count];
            Quaternion[] targetRotations = new Quaternion[realPieces.Count];

            for (int i = 0; i < realPieces.Count; i++)
            {
                startPositions[i] = realPieces[i].localPosition;
                startRotations[i] = realPieces[i].localRotation;
                targetPositions[i] = CalculateStackPosition(i);
                targetRotations[i] = CalculateStackRotation();
            }

            while (elapsed < gatherDuration)
            {
                if (root == null || IsSelling)
                    yield break;

                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / gatherDuration);
                float curve = flyCurve != null ? flyCurve.Evaluate(t) : t;

                for (int i = 0; i < realPieces.Count; i++)
                {
                    if (realPieces[i] == null)
                        continue;

                    realPieces[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], curve);
                    realPieces[i].localRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], curve);
                }

                yield return null;
            }

            for (int i = 0; i < realPieces.Count; i++)
            {
                if (realPieces[i] == null)
                    continue;

                realPieces[i].localPosition = targetPositions[i];
                realPieces[i].localRotation = targetRotations[i];
            }

            Transform forkTarget = PitchforkDestination != null ? PitchforkDestination : HandDestination;

            if (forkTarget != null)
            {
                yield return FlyBundleTransform(
                    root,
                    forkTarget,
                    GetStackWorldRotation(),
                    decorativeFlyDuration,
                    decorativeFlyHeight
                );
            }

            if (root == null || IsSelling)
                yield break;

            if (forkTarget != null)
            {
                root.SetParent(forkTarget, true);
                root.position = forkTarget.position;
                root.rotation = GetStackWorldRotation();
            }

            pitchforkBundleLanded = true;
        }

        private IEnumerator FlyBundleTransform(Transform tr, Transform target,
            Quaternion targetWorldRotation, float duration, float flyHeight)
        {
            if (tr == null || target == null)
                yield break;

            Vector3 start = tr.position;
            Quaternion startRotation = tr.rotation;
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (tr == null || target == null || IsSelling)
                    yield break;

                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curve = flyCurve != null ? flyCurve.Evaluate(t) : t;

                tr.position = Vector3.Lerp(start, target.position, curve) +
                              Vector3.up * (Mathf.Sin(t * Mathf.PI) * flyHeight);

                tr.rotation = Quaternion.Slerp(startRotation, targetWorldRotation, curve);

                yield return null;
            }

            if (tr != null && target != null)
            {
                tr.position = target.position;
                tr.rotation = targetWorldRotation;
            }
        }

        private void UpdatePitchforkBundleLifecycle()
        {
            if (pitchforkBundleRoot == null || !pitchforkBundleLanded ||
                pitchforkTransferInProgress || Time.time < nextPitchforkCollectionTime)
                return;

            StartCoroutine(TransferPitchforkBundleToHand());
        }

        private IEnumerator TransferPitchforkBundleToHand()
        {
            if (pitchforkTransferInProgress)
                yield break;

            pitchforkTransferInProgress = true;

            Transform bundle = pitchforkBundleRoot;
            Transform target = HandDestination;

            if (bundle != null && target != null)
            {
                yield return FlyBundleTransform(
                    bundle,
                    target,
                    GetStackWorldRotation(),
                    strawFlyDuration,
                    strawFlyHeight
                );
            }

            if (IsSelling || bundle == null)
            {
                pitchforkTransferInProgress = false;
                yield break;
            }

            foreach (HayStraw straw in pitchforkHeldStraws)
            {
                if (straw == null)
                    continue;

                straw.transform.SetParent(stackTargetRoot, false);
                inFlightStraws.Remove(straw);
                straw.FinishExternalMovement();
            }

            LayoutHeldStraws();
            Destroy(bundle.gameObject);

            pitchforkBundleRoot = null;
            pitchforkHeldStraws.Clear();
            pitchforkBundleLanded = false;
            pitchforkTransferInProgress = false;

            RefreshInventoryUI();
        }

        private IEnumerator CollectOneStraw(HayStraw straw)
        {
            if (straw == null)
                yield break;

            Transform target = HandDestination;

            yield return FlyRealStraw(straw, target, strawFlyDuration);

            if (straw == null)
                yield break;

            if (stackTargetRoot != null)
                straw.transform.SetParent(stackTargetRoot, false);

            inFlightStraws.Remove(straw);
            straw.FinishExternalMovement();
            LayoutHeldStraws();
        }

        private IEnumerator FlyRealStraw(HayStraw straw, Transform target, float duration)
        {
            if (straw == null || target == null)
                yield break;

            Transform tr = straw.transform;
            Vector3 start = tr.position;
            Quaternion startRot = tr.rotation;
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (straw == null || target == null)
                    yield break;

                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curve = flyCurve != null ? flyCurve.Evaluate(t) : t;

                tr.position = Vector3.Lerp(start, target.position, curve) +
                              Vector3.up * (Mathf.Sin(t * Mathf.PI) * strawFlyHeight);

                tr.rotation = Quaternion.Slerp(startRot, target.rotation, curve);

                yield return null;
            }

            if (straw != null && target != null)
            {
                tr.position = target.position;
                tr.rotation = target.rotation;
            }
        }

        public List<HayStraw> TakeAllHeldStrawsForSale()
        {
            List<HayStraw> result = new List<HayStraw>();
            Transform bundle = pitchforkBundleRoot;

            pitchforkBundleRoot = null;
            pitchforkBundleLanded = false;
            pitchforkTransferInProgress = false;
            pitchforkHeldStraws.Clear();

            for (int i = 0; i < heldStraws.Count; i++)
            {
                HayStraw straw = heldStraws[i];

                if (straw == null)
                    continue;

                straw.transform.SetParent(null, true);
                straw.BeginExternalMovement();
                result.Add(straw);
            }

            heldStraws.Clear();
            inFlightStraws.Clear();

            pendingBonusSaleUnits = 0;

            if (bundle != null)
                Destroy(bundle.gameObject);

            RefreshInventoryUI();

            return result;
        }

        public void DropOneStraw()
        {
            if (heldStraws.Count == 0)
            {
                if (pendingBonusSaleUnits > 0)
                    pendingBonusSaleUnits--;

                RefreshInventoryUI();
                return;
            }

            int index = heldStraws.Count - 1;
            HayStraw straw = heldStraws[index];
            heldStraws.RemoveAt(index);

            if (straw != null)
            {
                straw.transform.SetParent(null, true);
                straw.FinishExternalMovement();
                pitchforkHeldStraws.Remove(straw);
                inFlightStraws.Remove(straw);
            }

            LayoutHeldStraws();
            RefreshInventoryUI();
        }

        private void RefreshInventoryUI()
        {
            if (inventoryText != null)
                inventoryText.text = HayInHands.ToString();

            if (inventoryFillImage != null)
            {
                inventoryFillImage.type = Image.Type.Filled;
                inventoryFillImage.fillAmount =
                    Capacity <= 0 ? 0f : Mathf.Clamp01((float)HayInHands / Capacity);
            }

            if (heldHayVisual != null)
                heldHayVisual.SetActive(HayInHands > 0);
        }
    }
}