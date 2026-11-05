
using System.Collections;
using TMPro;
using UnityEngine;
using YG;

namespace HayPileSystem
{
    public class HayTutorial : MonoBehaviour
    {
        public static HayTutorial Instance { get; private set; }

        private enum TutorialStep
        {
            CollectHay,
            SellHay,
            Completed
        }

        [Header("Player")]
        [SerializeField] private Transform player;
        [SerializeField] private float lineStartHeight = 1f;
        [SerializeField] private float lineTargetHeight = 0.5f;

        [Header("Tutorial Targets")]
        [SerializeField] private Transform hayTarget;
        [SerializeField] private Transform sellTarget;

        [Header("Line Renderer")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private bool hideLineWhenCompleted = true;

        [Header("Title UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private CanvasGroup titleCanvasGroup;
        [SerializeField] private float animationDuration = 0.35f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.04f;

        [Header("Completion")]
        [SerializeField] private float completionDisplayTime = 4f;

        private TutorialStep currentStep;
        private Coroutine titleCoroutine;
        private Vector3 originalTitleScale;
        private bool tutorialActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (titleText != null)
                originalTitleScale = titleText.transform.localScale;

            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
            }
        }

        private void Start()
        {
            // Не начинаем обучение, если оно уже пройдено.
            if (YG2.saves == null ||
                YG2.saves.playerData == null)
            {
                Debug.LogError(
                    "HayTutorial: данные YG2.saves.playerData не найдены.");
                return;
            }

            if (YG2.saves.playerData.tutorialCompleted)
            {
                HideTutorial();
                return;
            }

            tutorialActive = true;
            currentStep = TutorialStep.CollectHay;

            ShowStep("СОБЕРИ ПЕРВОЕ СЕНО!");
        }

        private void Update()
        {
            if (!tutorialActive)
                return;

            UpdateGuideLine();

            // Небольшая постоянная анимация заголовка.
            if (titleText != null)
            {
                float pulse = 1f +
                    Mathf.Sin(Time.unscaledTime * pulseSpeed) *
                    pulseAmount;

                titleText.transform.localScale =
                    originalTitleScale * pulse;
            }
        }

        private void UpdateGuideLine()
        {
            if (lineRenderer == null || player == null)
                return;

            Transform target = GetCurrentTarget();

            if (target == null)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;

            Vector3 start = player.position +
                            Vector3.up * lineStartHeight;

            Vector3 end = target.position +
                          Vector3.up * lineTargetHeight;

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private Transform GetCurrentTarget()
        {
            switch (currentStep)
            {
                case TutorialStep.CollectHay:
                    return hayTarget;

                case TutorialStep.SellHay:
                    return sellTarget;

                default:
                    return null;
            }
        }

        // Вызывается после успешного сбора сена.
        public void OnHayCollected()
        {
            if (!tutorialActive ||
                currentStep != TutorialStep.CollectHay)
                return;

            currentStep = TutorialStep.SellHay;
            ShowStep("ОТЛИЧНО! ТЕПЕРЬ ПРОДАЙ СЕНО!");
        }

        // Вызывается после успешной продажи сена.
        public void OnHaySold()
        {
            if (!tutorialActive ||
                currentStep != TutorialStep.SellHay)
                return;

            currentStep = TutorialStep.Completed;
            StartCoroutine(CompleteTutorial());
        }

        private void ShowStep(string message)
        {
            if (titleText == null)
                return;

            titleText.text = message;
            titleText.gameObject.SetActive(true);

            if (titleCoroutine != null)
                StopCoroutine(titleCoroutine);

            titleCoroutine = StartCoroutine(AnimateTitle());
        }

        private IEnumerator AnimateTitle()
        {
            Transform titleTransform = titleText.transform;

            Vector3 targetScale = originalTitleScale;
            Vector3 startScale = originalTitleScale * 0.65f;

            titleTransform.localScale = startScale;

            if (titleCanvasGroup != null)
                titleCanvasGroup.alpha = 0f;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, animationDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                titleTransform.localScale =
                    Vector3.Lerp(startScale, targetScale, eased);

                if (titleCanvasGroup != null)
                    titleCanvasGroup.alpha = eased;

                yield return null;
            }

            titleTransform.localScale = targetScale;

            if (titleCanvasGroup != null)
                titleCanvasGroup.alpha = 1f;

            titleCoroutine = null;
        }

        private IEnumerator CompleteTutorial()
        {
            ShowStep("СУПЕР! ТЫ ПРОШЁЛ ОБУЧЕНИЕ!");

            // Сразу сохраняем результат, не дожидаясь закрытия текста.
            YG2.saves.playerData.tutorialCompleted = true;
            YG2.SaveProgress();

            if (lineRenderer != null && hideLineWhenCompleted)
                lineRenderer.enabled = false;

            yield return new WaitForSecondsRealtime(
                completionDisplayTime);

            HideTutorial();
        }

        private void HideTutorial()
        {
            tutorialActive = false;

            if (lineRenderer != null)
                lineRenderer.enabled = false;

            if (titleCanvasGroup != null)
                titleCanvasGroup.alpha = 0f;

            if (titleText != null)
                titleText.gameObject.SetActive(false);
        }

        // Можно вызвать из кнопки, если понадобится пропуск обучения.
        public void SkipTutorial()
        {
            if (YG2.saves != null &&
                YG2.saves.playerData != null)
            {
                YG2.saves.playerData.tutorialCompleted = true;
                YG2.SaveProgress();
            }

            HideTutorial();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}