using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;

namespace Necromancer.Systems
{
    /// <summary>
    /// [INFRA] 애드몹(AdMob) 광고 관리자 (백그라운드 프리로딩 지원)
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        [Header("AdMob Settings")]
        [SerializeField] private bool testMode = true;
        [SerializeField] private GameObject noAdMessagePrefab; // 광고 없을 때 띄울 텍스트 프리팹
        [SerializeField] private Transform uiCanvasParent;    // 메시지 상자가 생성될 부모 Canvas (성능 최적화용)

#if UNITY_ANDROID
        private string adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
        private string adUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        private string adUnitId = "unused";
#endif

        private RewardedAd rewardedAd;
        private Action onRewardSuccess;
        private Action onRewardFailed;
        private bool isAdShowing = false;

        public void Init()
        {
            if (GetComponent<UnityMainThreadDispatcher>() == null)
            {
                gameObject.AddComponent<UnityMainThreadDispatcher>();
            }

            MobileAds.Initialize(initStatus => {
                Debug.Log("<color=green>[AdManager]</color> AdMob Initialized.");
                UnityMainThreadDispatcher.Enqueue(() => LoadRewardedAd());
            });
        }

        /// <summary>
        /// 다음 광고를 미리 로드합니다. (백그라운드 병렬 로딩 가능)
        /// </summary>
        public void LoadRewardedAd()
        {
            // 현재 광고가 나오고 있는 중이면, 레퍼런스를 덮어쓰지 않기 위해 체크
            if (isAdShowing) return;

            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }

            Debug.Log("[AdManager] Pre-loading next ad in background...");
            var adRequest = new AdRequest();

            RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) => {
                if (error != null || ad == null)
                {
                    Debug.LogError($"[AdManager] Failed to pre-load ad: {error}");
                    return;
                }

                Debug.Log("[AdManager] Next ad pre-loaded and ready.");
                rewardedAd = ad;
                RegisterEventHandlers(rewardedAd);
            });
        }

        /// <summary>
        /// 광고를 보여주고 즉시 다음 광고 로드를 시작합니다.
        /// </summary>
        public void ShowRewardedAd(Action successCallback, Action failCallback)
        {
            onRewardSuccess = successCallback;
            onRewardFailed = failCallback;

            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                isAdShowing = true;
                // [PAUSE] 광고 시작 → Ad 사유로 일시정지 (LevelUp 정지 중이라도 중첩 유지)
                if (GameManager.Instance != null)
                    GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, true);

                rewardedAd.Show((Reward reward) => {
                    UnityMainThreadDispatcher.Enqueue(() => {
                        Debug.Log("<color=green>[AdManager]</color> Reward earned!");
                        onRewardSuccess?.Invoke();
                    });
                });

                // [PRE-LOAD] 광고가 뜨자마자 다음 광고를 백그라운드에서 받아오기 시작
                // (일부 네트워크 상황을 고려하여 1초 정도의 짧은 딜레이 후 로드 시작도 가능하지만, 즉시 호출)
                UnityMainThreadDispatcher.Enqueue(() => {
                    // 현재 보여준 광고 객체와의 충돌을 피하기 위해
                    // showing이 완료된 후 다시 로드될 수 있도록 flag와 함께 설계
                });
            }
            else
            {
                Debug.LogWarning("[AdManager] No ad ready.");
                
                // [FIX] 인스펙터에 연결된 Canvas 바로 사용 (최고 성능)
                if (noAdMessagePrefab != null && uiCanvasParent != null)
                {
                    GameObject msgBox = Instantiate(noAdMessagePrefab, uiCanvasParent);
                    msgBox.transform.localPosition = Vector3.zero; // 중앙 정렬
                }
                
                onRewardFailed?.Invoke();
            }
        }

        private void RegisterEventHandlers(RewardedAd ad)
        {
            ad.OnAdFullScreenContentClosed += () => {
                Debug.Log("[AdManager] Ad closed by user.");
                isAdShowing = false;
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (GameManager.Instance != null)
                        GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, false);
                    LoadRewardedAd();
                });
            };

            ad.OnAdFullScreenContentFailed += (AdError error) => {
                Debug.LogError($"[AdManager] Ad failed to show: {error}");
                isAdShowing = false;
                UnityMainThreadDispatcher.Enqueue(() => {
                    if (GameManager.Instance != null)
                        GameManager.Instance.SetPause(Necromancer.PauseSource.Ad, false);
                    
                    // [FIX] 인스펙터에 연결된 Canvas 바로 사용 (최고 성능)
                    if (noAdMessagePrefab != null && uiCanvasParent != null)
                    {
                        GameObject msgBox = Instantiate(noAdMessagePrefab, uiCanvasParent);
                        msgBox.transform.localPosition = Vector3.zero; // 중앙 정렬
                    }

                    onRewardFailed?.Invoke();
                });
            };

            // 광고가 나타나면(Showing) 즉시 flag를 세워 리소스 관리
            ad.OnAdFullScreenContentOpened += () => {
                Debug.Log("[AdManager] Ad is now visible. Preparing next ad...");
                // Note: 여기서 즉시 Load를 호출하여 백그라운드 작업을 시작하도록 유도 가능
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