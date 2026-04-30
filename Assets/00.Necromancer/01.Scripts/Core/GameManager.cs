using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using Necromancer.Core;
using Necromancer.UI;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace Necromancer
{
    /// <summary>
    /// [PAUSE] 일시정지 사유 열거형
    /// 모든 정지 사유가 해소되어야만 게임이 재개됩니다.
    /// </summary>
    public enum PauseSource { Settings, LevelUp, Ad, GameOver, Debug }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Global Managers")]
        [SerializeField] private SaveDataManager _saveData;
        [SerializeField] private ResourceManager _resources;
        [SerializeField] private CombatManager _combat;
        [SerializeField] private SoundManager _sound;
        [SerializeField] private DebugConsole _debugConsole;
        [SerializeField] private Necromancer.Systems.AdManager _adManager;
        [SerializeField] private Necromancer.Systems.PopupManager _popupManager;
        [SerializeField] private Necromancer.Systems.NotificationManager _notificationManager;
        [SerializeField] private Necromancer.Systems.DownloadManager _downloadManager;
        [SerializeField] private LevelManager _levelManager;
        public Necromancer.Systems.AuthManager Auth;


        [Header("Scene Managers")]
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private SkillManager _skillManager;
        [SerializeField] private FeedbackManager _feedbackManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private TitleUIController _titleUI;
        [SerializeField] private UnitManager _unitManager;
        [SerializeField] private Necromancer.UI.LogMessageManager _logMessageManager;

        [Header("Data Config (Master's Directive)")]
        [SerializeField] private List<Necromancer.Data.MinionUnlockSO> _minionUnlockDataList = new List<Necromancer.Data.MinionUnlockSO>();
        public List<Necromancer.Data.MinionUnlockSO> minionUnlockDataList => _minionUnlockDataList;

        [ContextMenu("Sync Minion Data")]
        public void SyncMinionData()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MinionUnlockSO", new string[] { "Assets/00.Necromancer/02.Data/Minions" });
            _minionUnlockDataList.Clear();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                Necromancer.Data.MinionUnlockSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<Necromancer.Data.MinionUnlockSO>(path);
                if (so != null)
                    _minionUnlockDataList.Add(so);
            }

            // 파일명 기준 오름차순 정렬 (버블 정렬)
            for (int i = 0; i < _minionUnlockDataList.Count - 1; i++)
            {
                for (int j = 0; j < _minionUnlockDataList.Count - 1 - i; j++)
                {
                    if (string.Compare(_minionUnlockDataList[j].name, _minionUnlockDataList[j + 1].name) > 0)
                    {
                        var temp = _minionUnlockDataList[j];
                        _minionUnlockDataList[j] = _minionUnlockDataList[j + 1];
                        _minionUnlockDataList[j + 1] = temp;
                    }
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"<color=gold>[GameManager]</color> {_minionUnlockDataList.Count}개의 MinionUnlockSO 데이터가 동기화되었습니다.");
#endif
        }

        // --- [Direct Access Properties] ---
        public SaveDataManager SaveData => _saveData;
        public ResourceManager Resources => _resources;
        public CombatManager Combat => _combat;
        public SoundManager Sound => _sound;
        public DebugConsole DebugConsole => _debugConsole;
        public Necromancer.Systems.AdManager AdManager => _adManager;
        public Necromancer.Systems.PopupManager Popup => _popupManager;
        public Necromancer.Systems.NotificationManager Notification => _notificationManager;
        public Necromancer.Systems.DownloadManager Download => _downloadManager;
        public LevelManager LevelManager => _levelManager;


        public PoolManager poolManager => _poolManager;
        public WaveManager waveManager => _waveManager;
        public SkillManager skillManager => _skillManager;
        public FeedbackManager feedbackManager => _feedbackManager;
        public UIManager uiManager => _uiManager;
        public TitleUIController titleUI => _titleUI;
        public UnitManager unitManager => _unitManager;
        public Necromancer.UI.LogMessageManager logMessageManager => _logMessageManager;

        public static event Action<int, int, string> OnWaveStarted;
        public static event Action<int> OnSoulChanged;        // 로비 UI용 — 전체 보유량(currentSoul)
        public static event Action<int> OnSessionSoulChanged; // 인게임 HUD용 — 세션 획득량(currentSessionSoul)
        public static event Action<float> OnTimeUpdated;
        public static event Action<float> OnSpeedChanged;
        public static event Action<bool> OnGameOver;

        public static void BroadcastTime(float time) => OnTimeUpdated?.Invoke(time);
        public static void BroadcastWave(int index, int total, string name) => OnWaveStarted?.Invoke(index, total, name);
        public static void BroadcastSoul(int amount) => OnSoulChanged?.Invoke(amount);
        public static void BroadcastSessionSoul(int amount) => OnSessionSoulChanged?.Invoke(amount);

        public Transform playerTransform;
        public PlayerController playerController;
        public float magnetRadius = 3f;
        public bool IsGameOver { get; private set; }
        public string enemyPoolTag = "Enemy";   // [AUTOMATION] 범용 몬스터 풀 태그

        public float currentGameSpeed = 1f;
        public StageDataSO currentStage;

        // ─── [PAUSE SYSTEM] 상태 기반 일시정지 ───────────────────────────────
        private readonly HashSet<PauseSource> _activePauseSources = new HashSet<PauseSource>();

        /// <summary>
        /// 일시정지 사유를 추가/제거합니다.
        /// 활성 사유가 하나라도 남아 있으면 timeScale = 0, 모두 해소되면 currentGameSpeed로 복귀합니다.
        /// </summary>
        public void SetPause(PauseSource source, bool isPaused)
        {
            if (isPaused) _activePauseSources.Add(source);
            else          _activePauseSources.Remove(source);

            Time.timeScale = (_activePauseSources.Count > 0) ? 0f : currentGameSpeed;
            Debug.Log($"[PauseSystem] Source: {source}, isPaused: {isPaused}, ActiveCount: {_activePauseSources.Count}, timeScale: {Time.timeScale}");
        }
        // ─────────────────────────────────────────────────────────────────────

        // [SRP] 레벨·경험치 상태는 LevelManager가 전담합니다. 하위 호환을 위해 위임 프로퍼티를 유지합니다.
        public int currentLevel => _levelManager != null ? _levelManager.currentLevel : 1;
        public float currentExp => _levelManager != null ? _levelManager.currentExp : 0f;
        public float maxExp => _levelManager != null ? _levelManager.maxExp : 200f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);

                if (_poolManager == null || _waveManager == null || _uiManager == null)
                {
                    Debug.LogError("<color=red>[GameManager]</color> CRITICAL ERROR: Essential managers (Pool, Wave, UI) are NOT assigned in the Inspector!");
                }

                SceneManager.sceneLoaded += OnSceneLoaded;
                InitAllManagers();
            }
            else Destroy(gameObject);
        }


        private float lastEscTime = 0f;
        private const float ESC_COOLDOWN = 0.3f;

        private void Update()
        {
            // [STABILITY] 타이틀 씬에서는 ESC 키가 작동하지 않도록 차단 (Master's Directive)
            if (SceneManager.GetActiveScene().name == "TitleScene") return;

            // [STABILITY] 중앙 집중형 입력 관리 (Single Source of Truth)
            // 어떤 UI가 꺼져 있거나 파편화된 상황에서도 GameManager는 항상 ESC/Back 버튼을 감시합니다.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // [STABILITY] 연타 방지 쿨다운 (0.3s)
                if (Time.unscaledTime < lastEscTime + ESC_COOLDOWN) return;
                lastEscTime = Time.unscaledTime;

                if (uiManager != null)
                {
                    uiManager.ToggleSettings();
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // DownloadScene은 DownloadSceneController가 자체 처리하므로 별도 초기화 불필요
            if (scene.name == "DownloadScene") return;

            bool isGameScene = scene.name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (isGameScene)
            {
                // [NEW] 새로운 판 시작 전 잔유물 완벽 정리 (Master's Directive)
                CleanupGameSession();
                
                IsGameOver = false;
                if (Sound != null) Sound.ResumeSFX();

                if (Resources != null)
                {
                    Resources.currentSessionSoul = 0;
                    Resources.currentSessionEssences.Clear(); // 세션 정수 기록 초기화
                    BroadcastSoul(0);
                    if (unitManager != null) unitManager.UpdateUnlockedMinionPool(); // [STABILITY] 세션 시작 시 해금 풀 최신화
                }

                // [STABILITY] 플레이어 참조 갱신 및 유닛 매니저 재등록 (Ghost Unit Fix)
                if (playerTransform == null || playerController == null)
                {
                    var player = GameObject.FindWithTag("Player");
                    if (player != null) 
                    {
                        playerTransform = player.transform;
                        playerController = player.GetComponent<PlayerController>();
                    }
                }

                // 클린업으로 인해 비워진 유닛 매니저에 플레이어를 즉시 재등록하여 이동 가능하게 함
                if (playerController != null && unitManager != null)
                {
                    unitManager.RegisterUnit(playerController);
                }

                if (poolManager != null) poolManager.Init();

                if (waveManager != null) waveManager.Init();
                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();

                // [UPGRADE] 시작 미니언 소환 (Type 10)
                if (unitManager != null) unitManager.SpawnInitialMinions();

                if (Sound != null && Sound.gameBGM != null) Sound.PlayBGM(Sound.gameBGM);

                OnSpeedChanged?.Invoke(currentGameSpeed);
            }
            // ... (TitleScene 로직은 동일)
            else if (scene.name == "TitleScene")
            {
                CleanupGameSession();

                if (Resources != null && Resources.currentSessionSoul > 0)
                {
                    Resources.CommitSessionSoul();
                }

                // [PERFORMANCE] Find 함수 대신 TitleUIController가 스스로 등록하는 방식을 사용합니다.
                if (_titleUI != null) _titleUI.SetupInitialUI();

                if (uiManager != null) uiManager.Clear();
                
                playerTransform = null;
                playerController = null;
                currentStage = null;
                IsGameOver = false;
                _levelManager?.Reset();

                if (Sound != null)
                {
                    if (Sound.titleBGM != null) Sound.PlayBGM(Sound.titleBGM);
                    Sound.ResumeSFX();
                }

                if (Resources != null) Resources.currentSessionSoul = 0;
            }
        }

        /// <summary>
        /// [CLEANUP] 이전 판의 모든 데이터와 오브젝트를 완전히 정리합니다.
        /// 타이틀 복귀나 세션 초기화 시 필수 호출됩니다.
        /// </summary>
        public void CleanupGameSession()
        {
            // 1. [비동기 차단] 웨이브 루프를 먼저 중단 — 정리 도중 새 적이 소환되는 것을 방지
            if (waveManager != null) waveManager.StopSpawning();

            // 2. 논리 유닛 목록 초기화 — Wave 중단 후 allUnits 순회가 안전함
            if (unitManager != null) unitManager.ClearAll();

            // 3. 물리 오브젝트 회수 (풀링 시스템)
            if (poolManager != null) poolManager.ClearAllActiveObjects();

            // 4. 사운드 셧다운
            if (Sound != null) Sound.StopAllSFX(true);

            // 5. 트윈 및 시간 복구 — 모든 정지 사유를 일괄 해소 후 timeScale·배속 복원
            DOTween.KillAll();
            _activePauseSources.Clear();
            Time.timeScale = 1f;
            currentGameSpeed = 1f; // [BUG-FIX] currentGameSpeed 미초기화로 재시작 시 이전 배속 UI가 유지되던 버그 수정

            Debug.Log("<color=red>[GameManager]</color> CRITICAL CLEANUP: All game sessions objects and logic reset.");
        }


        private void InitAllManagers()
        {
            if (Download != null) Download.Init();
            if (SaveData != null) SaveData.Init();
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
            if (Sound != null) Sound.Init();
            if (_notificationManager != null) _notificationManager.Init();
            // [CRITICAL] Auth는 AdManager보다 반드시 먼저 초기화
            // MobileAds.Initialize()가 렌더 스레드에서 예외를 던지면 C# try-catch로 잡히지 않아
            // 뒤에 위치한 Auth.Init()이 호출되지 않는 문제 방지
            if (Auth != null)
            {
                Auth.Init();
                Debug.Log("<color=cyan>[GameManager]</color> Auth.Init() called.");
            }
            else
            {
                Debug.LogError("[GameManager] Auth is NULL! Firebase will not initialize. Check Inspector assignment.");
            }
            try
            {
                if (AdManager != null) AdManager.Init();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] AdManager.Init() failed — Google Mobile Ads AAR 누락 또는 버전 오류.\n{e.Message}");
            }
        }

        public void StartGame(StageDataSO stage)
        {
            currentStage = stage;
            SceneManager.LoadScene("GameScene");
        }

        /// <summary>
        /// [CLOUD] 클라우드 데이터 로드 후 각 매니저의 상태를 최신 데이터로 갱신합니다.
        /// </summary>
        public void RefreshSystemsAfterLoad()
        {
            Debug.Log("<color=cyan>[GameManager]</color> Cloud data detected. Refreshing all systems...");
            
            // 1. 리소스 매니저 재초기화 (소울 개수 등 반영)
            if (Resources != null) 
            {
                Resources.Init();
            }

            // 2. 타이틀 UI가 있다면 현재 소울 개수 등 다시 표시
            if (_titleUI != null)
            {
                _titleUI.SetupInitialUI();
            }

            // 3. 인게임 UI가 있다면 튜토리얼 여부 다시 체크 (복귀 유저 중복 노출 방지)
            if (_uiManager != null)
            {
                _uiManager.CheckAndShowTutorial();
            }

            Debug.Log("<color=green>[GameManager]</color> All systems synchronized with cloud data.");
        }

        public void ToggleGameSpeed()
        {
            currentGameSpeed = currentGameSpeed <= 1.1f ? 1.5f : (currentGameSpeed <= 1.6f ? 2f : 1f);
            // 일시정지 사유가 없을 때만 즉시 반영, 있으면 사유 해소 시 자동 적용됨
            if (_activePauseSources.Count == 0) Time.timeScale = currentGameSpeed;
            OnSpeedChanged?.Invoke(currentGameSpeed);
        }

        /// <summary>모든 정지 사유가 없을 때 배속을 복원합니다. SetPause 사용 권장SetPause 사용 권장.</summary>
        public void ResumeGameSpeed()
        {
            if (_activePauseSources.Count == 0) Time.timeScale = currentGameSpeed;
        }

        /// <summary>[SRP] 경험치 추가 — 실제 처리는 LevelManager에 위임합니다. 콜사이트 무변경.</summary>
        public void AddExp(float amount) => _levelManager?.AddExp(amount);

        public void OnStageClear()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            if (currentStage != null && Resources != null) 
            {
                Resources.UnlockLevel(currentStage.stageID + 1);
            }
            StartCoroutine(StageClearSequence());
        }

        private IEnumerator StageClearSequence()
        {
            var gems = ExpGem.ActiveGems.ToList();
            foreach (var gem in gems) 
            {
                if (gem != null) gem.StartVacuum();
            }
            yield return new WaitForSeconds(1.5f);
            if (Resources != null) Resources.CommitSessionSoul();
            
            // [STABILITY] 클리어 시 즉시 저장 (데이터 유실 방지)
            if (SaveData != null) SaveData.Save();

            OnGameOver?.Invoke(true);
        }

        public void OnStageFailed()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            SetPause(PauseSource.GameOver, true);
            if (Resources != null) Resources.CommitSessionSoul();
            
            // [STABILITY] 패배 시에도 즉시 저장하여 획득한 영혼/진척도 보존
            if (SaveData != null) SaveData.Save();

            OnGameOver?.Invoke(false);
        }

        // --- [AUTH SYSTEM] UI에서 버튼 클릭 시 호출될 중앙 함수 ---
        public void RequestLogin(bool isGoogle)
        {
            if (Auth == null) return;

            // [STABILITY] 중복 구독 방지 (이전 리스너 제거 후 새로 등록)
            Auth.OnLoginResult -= HandleLoginResult;
            Auth.OnLoginResult += HandleLoginResult;

            if (isGoogle) Auth.LoginWithGoogle();
            else Auth.LoginAsGuest();
        }

        private void HandleLoginResult(bool success, string uid)
        {
            // TitleUIController가 Auth.OnLoginResult에 직접 구독하여 UI 전환을 처리하므로 여기선 불필요
        }

        // --- [UI REGISTRATION] TitleUIController가 스스로를 등록할 때 사용 (성능 최적화) ---
        public void RegisterTitleUI(TitleUIController ui)
        {
            _titleUI = ui;
            if (_titleUI != null) _titleUI.SetupInitialUI();
        }
    }
}
