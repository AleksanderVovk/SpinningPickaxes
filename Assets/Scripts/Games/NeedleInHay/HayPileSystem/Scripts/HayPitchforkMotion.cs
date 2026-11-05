
using System.Collections;
using UnityEngine;

namespace HayPileSystem
{
    public class HayPitchforkMotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform visualPivot;

        [Header("Swing position")]
        [SerializeField]
        private Vector3 windupOffset =
            new Vector3(0f, 0.12f, -0.08f);

        [SerializeField]
        private Vector3 strikeOffset =
            new Vector3(0f, -0.12f, 0.22f);

        [Header("Swing rotation (degrees)")]
        [SerializeField]
        private Vector3 windupRotation =
            new Vector3(-18f, 0f, -5f);

        [SerializeField]
        private Vector3 strikeRotation =
            new Vector3(28f, 0f, 8f);

        [Header("Swing timing")]
        [SerializeField, Min(0.01f)] private float windupDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float strikeDuration = 0.10f;
        [SerializeField, Min(0.01f)] private float recoveryDuration = 0.14f;

        [Header("HeadBob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobPositionAmount = 0.018f;
        [SerializeField] private float bobRotationAmount = 1.5f;

        [Header("Bob movement input")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField, Range(0f, 1f)] private float bobWhileIdle = 0.12f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Coroutine swingRoutine;
        private float bobTimer;
        private bool isSwinging;

        private void Awake()
        {
            if (visualPivot == null)
                visualPivot = transform;

            basePosition = visualPivot.localPosition;
            baseRotation = visualPivot.localRotation;
        }

        private void LateUpdate()
        {
            if (visualPivot == null || isSwinging)
                return;

            Vector3 bobPosition = Vector3.zero;
            Vector3 bobEuler = Vector3.zero;

            if (enableHeadBob)
            {
                float x = 0f;
                float y = 0f;

                // These axes work with Unity's legacy Input Manager.
                try
                {
                    x = SimpleInput.GetAxisRaw(horizontalAxis);
                    y = SimpleInput.GetAxisRaw(verticalAxis);
                }
                catch
                {
                    // If the axes are unavailable, idle bob still works.
                }

                float movement = Mathf.Clamp01(
                    new Vector2(x, y).magnitude);

                float bobStrength = Mathf.Lerp(
                    bobWhileIdle, 1f, movement);

                bobTimer += Time.deltaTime * bobFrequency
                            * Mathf.Lerp(0.5f, 1.5f, movement);

                bobPosition = new Vector3(
                    Mathf.Sin(bobTimer * 0.5f) * bobPositionAmount,
                    Mathf.Abs(Mathf.Sin(bobTimer))
                        * bobPositionAmount * bobStrength,
                    0f);

                bobEuler = new Vector3(
                    Mathf.Sin(bobTimer) * bobRotationAmount,
                    Mathf.Sin(bobTimer * 0.5f) * bobRotationAmount,
                    Mathf.Sin(bobTimer * 0.7f) * bobRotationAmount);
            }

            visualPivot.localPosition = basePosition + bobPosition;
            visualPivot.localRotation =
                baseRotation * Quaternion.Euler(bobEuler);
        }

        public void PlaySwing()
        {
            if (visualPivot == null)
                return;

            if (swingRoutine != null)
                StopCoroutine(swingRoutine);

            swingRoutine = StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            isSwinging = true;

            yield return AnimatePose(
                Vector3.zero,
                Vector3.zero,
                windupOffset,
                windupRotation,
                windupDuration);

            yield return AnimatePose(
                windupOffset,
                windupRotation,
                strikeOffset,
                strikeRotation,
                strikeDuration);

            // Small recoil at the end of the strike.
            yield return AnimatePose(
                strikeOffset,
                strikeRotation,
                strikeOffset * 0.55f,
                strikeRotation * 0.45f,
                recoveryDuration * 0.45f);

            yield return AnimatePose(
                strikeOffset * 0.55f,
                strikeRotation * 0.45f,
                Vector3.zero,
                Vector3.zero,
                recoveryDuration * 0.55f);

            visualPivot.localPosition = basePosition;
            visualPivot.localRotation = baseRotation;

            isSwinging = false;
            swingRoutine = null;
        }

        private IEnumerator AnimatePose(
            Vector3 fromPosition,
            Vector3 fromRotation,
            Vector3 toPosition,
            Vector3 toRotation,
            float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                Vector3 bobPosition = Vector3.zero;
                Vector3 bobEuler = Vector3.zero;

                // A small amount of bob remains during the swing.
                if (enableHeadBob)
                {
                    bobPosition.y =
                        Mathf.Sin(bobTimer * 2f) * bobPositionAmount * 0.2f;

                    bobEuler.z =
                        Mathf.Sin(bobTimer * 2f) * bobRotationAmount * 0.2f;
                }

                visualPivot.localPosition =
                    basePosition
                    + Vector3.Lerp(fromPosition, toPosition, eased)
                    + bobPosition;

                visualPivot.localRotation =
                    baseRotation
                    * Quaternion.Euler(
                        Vector3.Lerp(fromRotation, toRotation, eased)
                        + bobEuler);

                yield return null;
            }
        }
    }
}