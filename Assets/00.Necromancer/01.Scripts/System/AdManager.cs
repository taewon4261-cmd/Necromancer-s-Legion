using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;

namespace Necromancer.Systems
{
    /// <summary>
    /// [INFRA] 애드몹(AdMob) 광고 관리자
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        public enum AdUnitType { SkillRefresh, DoubleReward, Stamina }

        [Header("Ad Unit IDs (Android Real)")]
        [SerializeField] private string skillRefreshAdUnitId = "ca-app-pub-3770611612840704/4228061831";
        [SerializeField] private string doubleRewardAdUnitId = "ca-app-pub-3770611612840704/9975818749";
        [SerializeField] private string staminaAdUnitId      = "ca-app-pub-3940256099942544/5224354917"; // 테스트 ID

        [Header("UI References")]
        [SerializeField] private GameObject noAdMessagePrefab;
        [SerializeField] private Transform uiCanvasParent;

        // [SLOTS] 각 광고 단위별 독립적인 광고 객체
        private RewardedAd skillAd;
        private RewardedAd resultAd;
        private RewardedAd staminaAd;

        private Action onRewardSuccess;
        private Action onRewardFailed;
        private bool isAdShowing = false;

        public void Init()
        {
            if (GetComponent<UnityMainThreadDispatcher>() == null)
                gameObject.AddComponent<UnityMainThreadDispatcher>();

            Debug.Log("<color=green>[AdManager]</color> Initializing AdMob with Real IDs...");

            MobileAds.Initialize(initStatus => {
                Debug.Log("<color=green>[AdManager]</color> AdMob Initialized.");
                // 종류별 광고를 동시에 백그라운드에서 로드 시작
                UnityMainThreadDispatcher.Enqueue(() => {
                    LoadRewardedAd(AdUnitType.SkillRefresh);
                    LoadRewardedAd(AdUnitType.DoubleReward);
                    LoadRewardedAd(AdUnitType.Stamina);
                });
            });
        }

        public void LoadRewardedAd(AdUnitType type)
        {
            if (isAdShowing) return;

            string targetId;
            switch (type)
            {
                case AdUnitType.SkillRefresh: targetId = skillRefreshAdUnitId; if (skillAd  != null) { skillAd.Destroy();  skillAd  = null; } break;
                case AdUnitType.DoubleReward: targetId = doubleRewardAdUnitId; if (resultAd != null) { resultAd.Destroy(); resultAd = null; } break;
                case AdUnitType.Stamina:      targetId = staminaAdUnitId;      if (staminaAd!= null) { staminaAd.Destroy();staminaAd= null; } break;
                default: return;
            }

            Debug.Log($"[AdManager] Pre-loading {type} ad (ID: {targetId})...");
            var adRequest = new AdRequest();

            RewardedAd.Load(targetId, adRequest, (RewardedAd ad, LoadAdError error) => {
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdManager] Failed to load {type} ad: {error}");
                    return;
                }

                Debug.Log($"[AdManager] {type} ad loaded and ready.");
                switch (type)
                {
                    case AdUnitType.SkillRefresh: skillAd   = ad; break;
                    case AdUnitType.DoubleReward: resultAd  = ad; break;
                    case AdUnitType.Stamina:      staminaAd = ad; break;
                }
                RegisterEventHandlers(ad, type);
            });
        }

        public void ShowRewardedAd(AdUnitType type, Action successCallback, Action failCallback)
        {
            onRewardSuccess = successCallback;
            onRewardFailed  = failCallback;

            RewardedAd targetAd;
            switch (type)
            {
                case AdUnitType.SkillRefresh: targetAd = skillAd;   break;
                case AdUnitType.DoubleReward: targetAd = resultAd;  break;
                case AdUnitType.Stamina:      targetAd = staminaAd; break;
                default: targetAd = null; break;
            }

            if (targetAd != null && targetAd.CanShowAd())
            {
                isAdShowing = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, true);

                targetAd.Show((Reward reward) => {
                    UnityMainThreadDispatcher.Enqueue(() => {
                        Debug.Log($"<color=green>[AdManager]</color> Reward earned for {type}");
                        onRewardSuccess?.Invoke();
                    });
                });
            }
            else
            {
                Debug.LogWarning($"[AdManager] {type} ad NOT ready. Loading now...");
                LoadRewardedAd(type);

                if (noAdMessagePrefab != null && uiCanvasParent != null)
                {
                    GameObject msgBox = Instantiate(noAdMessagePrefab, uiCanvasParent);
                    msgBox.transform.localPosition = Vector3.zero;
                }

                onRewardFailed?.Invoke();
            }
        }

        public bool IsAdReady(AdUnitType type)
        {
            switch (type)
            {
                case AdUnitType.SkillRefresh: return skillAd   != null && skillAd.CanShowAd();
                case AdUnitType.DoubleReward: return resultAd  != null && resultAd.CanShowAd();
                case AdUnitType.Stamina:      return staminaAd != null && staminaAd.CanShowAd();
                default: return false;
            }
        }

        private void RegisterEventHandlers(RewardedAd ad, AdUnitType type)
        {
            ad.OnAdFullScreenContentClosed += () => {
                Debug.Log($"[AdManager] {type} ad closed.");
                isAdShowing = false;
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (GameManager.Instance != null)
                        GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, false);
                    LoadRewardedAd(type); // 사용한 광고만 다시 로드
                });
            };

            ad.OnAdFullScreenContentFailed += (AdError error) => {
                Debug.LogError($"[AdManager] Ad ({type}) failed to show: {error}");
                isAdShowing = false;
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (GameManager.Instance != null)
                        GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, false);

                    if (noAdMessagePrefab != null && uiCanvasParent != null)
                    {
                        GameObject msgBox = Instantiate(noAdMessagePrefab, uiCanvasParent);
                        msgBox.transform.localPosition = Vector3.zero;
                    }

                    onRewardFailed?.Invoke();
                });
            };
        }
    }

    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static UnityMainThreadDispatcher _instance;

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_executionQueue) { _executionQueue.Enqueue(action); }
        }

        private void Awake() { if (_instance == null) _instance = this; }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                    _executionQueue.Dequeue()?.Invoke();
            }
        }
    }
}
