using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;

namespace Necromancer.Systems
{
    /// <summary>
    /// [INFRA] 애드몹(AdMob) 광고 관리자 (다중 광고 슬롯 및 독립적 프리로딩 지원)
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        [Header("AdMob Settings")]
        [SerializeField] private bool testMode = false;
        
        [Header("Ad Unit IDs (Android Real)")]
        [SerializeField] private string skillRefreshAdUnitId = "ca-app-pub-3770611612840704/4228061831";
        [SerializeField] private string doubleRewardAdUnitId = "ca-app-pub-3770611612840704/9975818749";

        [Header("UI References")]
        [SerializeField] private GameObject noAdMessagePrefab;
        [SerializeField] private Transform uiCanvasParent;

        // AdMob 공식 테스트 ID
        private const string AndroidTestId = "ca-app-pub-3940256099942544/5224354917";

        // [SLOTS] 각 광고 단위별 독립적인 광고 객체 (주머니 2개)
        private RewardedAd skillAd;
        private RewardedAd resultAd;

        private Action onRewardSuccess;
        private Action onRewardFailed;
        private bool isAdShowing = false;

        public void Init()
        {
            if (GetComponent<UnityMainThreadDispatcher>() == null)
            {
                gameObject.AddComponent<UnityMainThreadDispatcher>();
            }

            Debug.Log("<color=green>[AdManager]</color> Initializing AdMob with Dual Slots...");

            MobileAds.Initialize(initStatus => {
                Debug.Log("<color=green>[AdManager]</color> AdMob Initialized.");
                // 두 종류의 광고를 동시에 백그라운드에서 로드 시작
                UnityMainThreadDispatcher.Enqueue(() => {
                    LoadRewardedAd(false); // 스킬 리프레시 로드
                    LoadRewardedAd(true);  // 결과창 2배 로드
                });
            });
        }

        /// <summary>
        /// 특정 광고 단위를 미리 로드합니다.
        /// </summary>
        public void LoadRewardedAd(bool isDoubleReward)
        {
            if (isAdShowing) return;

            string targetId = testMode ? AndroidTestId : (isDoubleReward ? doubleRewardAdUnitId : skillRefreshAdUnitId);
            
            // 기존 광고 객체 정리
            if (isDoubleReward && resultAd != null) { resultAd.Destroy(); resultAd = null; }
            else if (!isDoubleReward && skillAd != null) { skillAd.Destroy(); skillAd = null; }

            Debug.Log($"[AdManager] Pre-loading {(isDoubleReward ? "DoubleReward" : "SkillRefresh")} ad...");
            var adRequest = new AdRequest();

            RewardedAd.Load(targetId, adRequest, (RewardedAd ad, LoadAdError error) => {
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdManager] Failed to load {(isDoubleReward ? "DoubleReward" : "SkillRefresh")} ad: {error}");
                    return;
                }

                Debug.Log($"[AdManager] {(isDoubleReward ? "DoubleReward" : "SkillRefresh")} ad loaded and ready.");
                
                if (isDoubleReward) resultAd = ad;
                else skillAd = ad;

                RegisterEventHandlers(ad, isDoubleReward);
            });
        }

        /// <summary>
        /// 미리 로드된 특정 광고를 보여줍니다.
        /// </summary>
        public void ShowRewardedAd(bool isDoubleReward, Action successCallback, Action failCallback)
        {
            onRewardSuccess = successCallback;
            onRewardFailed = failCallback;

            RewardedAd targetAd = isDoubleReward ? resultAd : skillAd;

            if (targetAd != null && targetAd.CanShowAd())
            {
                isAdShowing = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, true);

                targetAd.Show((Reward reward) => {
                    UnityMainThreadDispatcher.Enqueue(() => {
                        Debug.Log("<color=green>[AdManager]</color> Reward earned!");
                        onRewardSuccess?.Invoke();
                    });
                });
            }
            else
            {
                Debug.LogWarning($"[AdManager] {(isDoubleReward ? "DoubleReward" : "SkillRefresh")} ad NOT ready. Loading now...");
                LoadRewardedAd(isDoubleReward);
                
                if (noAdMessagePrefab != null && uiCanvasParent != null)
                {
                    GameObject msgBox = Instantiate(noAdMessagePrefab, uiCanvasParent);
                    msgBox.transform.localPosition = Vector3.zero;
                }
                
                onRewardFailed?.Invoke();
            }
        }

        private void RegisterEventHandlers(RewardedAd ad, bool isDoubleReward)
        {
            ad.OnAdFullScreenContentClosed += () => {
                Debug.Log($"[AdManager] {(isDoubleReward ? "DoubleReward" : "SkillRefresh")} ad closed.");
                isAdShowing = false;
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (GameManager.Instance != null)
                        GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, false);
                    LoadRewardedAd(isDoubleReward); // 사용한 광고만 다시 로드
                });
            };

            ad.OnAdFullScreenContentFailed += (AdError error) => {
                Debug.LogError($"[AdManager] Ad failed to show: {error}");
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
                {
                    _executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}
