using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HayPileSystem
{
    public class HayShopSeller : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private HayPileInteractor player;
        [SerializeField] private Transform playerHandAnchor;

        [Header("Shop")]
        [SerializeField] private Transform hayReceivePoint;

        [Header("Hay Flight")]
        [SerializeField] private float hayFlyDuration = 1f;
        [SerializeField] private float hayFlyHeight = 0.2f;
        [SerializeField] private AnimationCurve hayFlyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float rotationRandomness = 0.15f;

        [Header("Straw Sounds")]
        [SerializeField] private AudioClip _strawPop;
        [SerializeField, Range(0f, 1f)] private float strawPopVolume = 1f;
        [SerializeField, Range(0.5f, 2f)] private float strawPopMinPitch = 0.95f;
        [SerializeField, Range(0.5f, 2f)] private float strawPopMaxPitch = 1.05f;

        [Header("Sale Effect")]
        [SerializeField] private ParticleSystem saleParticleSystem;
        [SerializeField] private bool restartParticleSystem = true;

        [Header("Money")]
        [SerializeField] private float pricePerStraw = 0.01f;
        [SerializeField] private float balance = 0f;
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private Transform coinTarget;
        [SerializeField] private float coinFlyDuration = 0.5f;
        [SerializeField] private float coinSpawnDelay = 0.035f;
        [SerializeField] private float coinFlyHeight = 0.3f;
        [SerializeField] private AnimationCurve coinFlyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Money UI")]
        [SerializeField] private TMP_Text moneyText;

        [Header("Sale Sounds")]
        [SerializeField] private AudioClip _cashRegister;
        [SerializeField, Range(0f, 1f)] private float cashRegisterVolume = 1f;

        [SerializeField] private AudioClip _cowMoo;
        [SerializeField, Range(0f, 1f)] private float cowMooVolume = 1f;

        private bool isSelling;

        private AudioSource cashAudioSource;
        private AudioSource cowAudioSource;
        private AudioSource strawPopAudioSource;

        public float Balance => balance;
        public bool IsSelling => isSelling;

        [SerializeField] private Animator _cow;

        private void Awake()
        {
            if (coinTarget == null && playerHandAnchor != null)
                coinTarget = playerHandAnchor;

            RefreshMoneyUI();

            cashAudioSource = CreateAudioSource("CashRegisterAudio");
            cowAudioSource = CreateAudioSource("CowMooAudio");
            strawPopAudioSource = CreateAudioSource("StrawPopAudio");

            if (saleParticleSystem != null)
                saleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private AudioSource CreateAudioSource(string sourceName)
        {
            GameObject audioObject = new GameObject(sourceName);
            audioObject.transform.SetParent(transform, false);

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            return source;
        }

        public bool TrySpendBalance(float amount)
        {
            if (amount < 0f || balance < amount)
                return false;

            balance -= amount;
            RefreshMoneyUI();
            return true;
        }

        public void AddBalance(float amount)
        {
            if (amount <= 0f)
                return;

            balance += amount;
            RefreshMoneyUI();
        }

        public void SellHayFromButton()
        {
            TrySell();
        }

        private void TrySell()
        {
            if (isSelling || player == null || player.HayInHands <= 0)
                return;

            StartCoroutine(SellAllHay());
        }

        private IEnumerator SellAllHay()
        {
            if (isSelling)
                yield break;

            isSelling = true;
            player.SetSellingState(true);

            List<HayStraw> straws = player.TakeAllHeldStrawsForSale();
            int bonusSaleUnits = player.TakePendingBonusSaleUnits();

            List<HayStraw> validStraws = new List<HayStraw>();

            if (straws != null)
            {
                for (int i = 0; i < straws.Count; i++)
                {
                    if (straws[i] != null)
                        validStraws.Add(straws[i]);
                }
            }

            int realStrawCount = validStraws.Count;
            int totalSaleUnits = realStrawCount + Mathf.Max(0, bonusSaleUnits);

            if (realStrawCount == 0 && totalSaleUnits == 0)
            {
                player.SetSellingState(false);
                isSelling = false;
                yield break;
            }

            float totalMoney = totalSaleUnits * pricePerStraw;

            balance += totalMoney;
            RefreshMoneyUI();

            // Туториал: игрок успешно продал сено.
            if (HayTutorial.Instance != null)
            {
                HayTutorial.Instance.OnHaySold();
            }

            PlayCashRegister();
            PlayCowMoo();
            PlaySaleParticles();

            if (coinPrefab != null && coinTarget != null && totalSaleUnits > 0)
                StartCoroutine(SpawnCoins(totalMoney, totalSaleUnits));

            Debug.Log($"Sold {realStrawCount} real hay + {Mathf.Max(0, bonusSaleUnits)} bonus units. " +
                      $"Earned ${totalMoney:0.00}. Balance: ${balance:0.00}");

            float totalSaleDuration = 1f;
            float startInterval = realStrawCount > 1 ? totalSaleDuration / realStrawCount : 0f;

            for (int i = 0; i < realStrawCount; i++)
            {
                HayStraw straw = validStraws[i];

                if (straw == null)
                    continue;

                float startTime = i * startInterval;
                float remainingTime = totalSaleDuration - startTime;
                float flightDuration = Mathf.Min(hayFlyDuration, remainingTime);

                PlayStrawPop();
                StartCoroutine(FlyStrawToShop(straw, flightDuration));

                if (i < realStrawCount - 1 && startInterval > 0f)
                    yield return new WaitForSeconds(startInterval);
            }

            float elapsedSaleTime = startInterval * Mathf.Max(0, realStrawCount - 1);
            float remainingSaleTime = totalSaleDuration - elapsedSaleTime;

            if (remainingSaleTime > 0f)
                yield return new WaitForSeconds(remainingSaleTime);

            player.SetSellingState(false);
            isSelling = false;
        }

        private void PlayCashRegister()
        {
            if (_cashRegister == null || cashAudioSource == null)
                return;

            cashAudioSource.clip = _cashRegister;
            cashAudioSource.volume = cashRegisterVolume;
            cashAudioSource.pitch = 1f;
            cashAudioSource.Play();
        }

        private void PlayCowMoo()
        {
            if (_cowMoo == null || cowAudioSource == null)
                return;

            cowAudioSource.clip = _cowMoo;
            cowAudioSource.volume = cowMooVolume;
            cowAudioSource.pitch = 1f;
            cowAudioSource.Play();

            _cow.SetTrigger("StartAnim");
        }

        private void PlayStrawPop()
        {
            if (_strawPop == null || strawPopAudioSource == null)
                return;

            strawPopAudioSource.clip = _strawPop;
            strawPopAudioSource.volume = strawPopVolume;
            strawPopAudioSource.pitch = Random.Range(strawPopMinPitch, strawPopMaxPitch);
            strawPopAudioSource.Play();
        }

        private IEnumerator FlyStrawToShop(HayStraw straw, float flightDuration)
        {
            if (straw == null)
                yield break;

            if (hayReceivePoint == null)
            {
                straw.FinishExternalMovement();
                yield break;
            }

            Transform strawTransform = straw.transform;
            Vector3 startPosition = strawTransform.position;
            Quaternion startRotation = strawTransform.rotation;
            Vector3 targetPosition = hayReceivePoint.position;
            Vector3 randomEuler = Random.insideUnitSphere * rotationRandomness * 360f;
            Quaternion targetRotation = hayReceivePoint.rotation * Quaternion.Euler(randomEuler);
            float elapsed = 0f;

            while (elapsed < flightDuration)
            {
                if (straw == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = flightDuration > 0f ? Mathf.Clamp01(elapsed / flightDuration) : 1f;
                float curveT = hayFlyCurve != null ? hayFlyCurve.Evaluate(t) : t;

                Vector3 position = Vector3.Lerp(startPosition, targetPosition, curveT);

                if (hayFlyHeight != 0f)
                    position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * hayFlyHeight);

                strawTransform.position = position;
                strawTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveT);

                yield return null;
            }

            strawTransform.position = targetPosition;
            strawTransform.rotation = targetRotation;
            straw.FinishExternalMovement();
            straw.gameObject.SetActive(false);
        }

        private void PlaySaleParticles()
        {
            if (saleParticleSystem == null)
                return;

            if (restartParticleSystem)
            {
                saleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                saleParticleSystem.Play();
            }
            else
            {
                saleParticleSystem.Play();
            }
        }

        private IEnumerator SpawnCoins(float totalMoney, int amount)
        {
            if (coinPrefab == null || coinTarget == null || hayReceivePoint == null)
                yield break;

            for (int i = 0; i < amount; i++)
            {
                GameObject coin = Instantiate(coinPrefab, hayReceivePoint.position, hayReceivePoint.rotation);

                if (coin != null)
                    StartCoroutine(FlyCoinToPlayer(coin));

                if (coinSpawnDelay > 0f && i < amount - 1)
                    yield return new WaitForSeconds(coinSpawnDelay);
            }
        }

        private IEnumerator FlyCoinToPlayer(GameObject coin)
        {
            if (coin == null)
                yield break;

            Transform coinTransform = coin.transform;
            Vector3 startPosition = coinTransform.position;
            Vector3 targetPosition = coinTarget.position;
            float elapsed = 0f;

            while (elapsed < coinFlyDuration)
            {
                if (coin == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = coinFlyDuration > 0f ? Mathf.Clamp01(elapsed / coinFlyDuration) : 1f;
                float curveT = coinFlyCurve != null ? coinFlyCurve.Evaluate(t) : t;

                Vector3 position = Vector3.Lerp(startPosition, targetPosition, curveT);

                if (coinFlyHeight != 0f)
                    position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * coinFlyHeight);

                coinTransform.position = position;
                coinTransform.Rotate(0f, 720f * Time.deltaTime, 0f, Space.Self);

                yield return null;
            }

            if (coin != null)
            {
                coinTransform.position = targetPosition;
                Destroy(coin);
            }
        }

        private void RefreshMoneyUI()
        {
            if (moneyText != null)
                moneyText.text = $"${balance:0.00}";
        }
    }
}