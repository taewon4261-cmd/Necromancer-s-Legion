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
        public Necromancer.Systems.AuthManager Auth;


        [Header("Scene Managers")]
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private SkillManager _skillManager;
        [SerializeField] private FeedbackManager _feedbackManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private TitleUIController _titleUI;
        [SerializeField] private UnitManager _unitManager;

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


        public PoolManager poolManager => _poolManager;
        public WaveManager waveManager => _waveManager;
        public SkillManager skillManager => _skillManager;
        public FeedbackManager feedbackManager => _feedbackManager;
        public UIManager uiManager => _uiManager;
        public TitleUIController titleUI => _titleUI;
        public UnitManager unitManager => _unitManager;

        public static event Action<float, float> OnExpChanged;
        public static event Action<List<SkillData>> OnLevelUp;
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
        public float baseReviveChance = 30f;
        public bool IsGameOver { get; private set; }
        public string minionPoolTag = "Minion"; // [AUTOMATION] 범용 미니언 풀 태그
        public string enemyPoolTag = "Enemy";   // [AUTOMATION] 범용 몬스터 풀 태그

        private List<Necromancer.Data.MinionUnlockSO> unlockedMinionDatas = new List<Necromancer.Data.MinionUnlockSO>(); // [AUTOMATION] 해금된 데이터 풀

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

        public int currentLevel = 1;
        public float currentExp = 0f;
        public float maxExp = 200f;

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
                    BroadcastSoul(0);
                    UpdateUnlockedMinionPool(); // [STABILITY] 세션 시작 시 해금 풀 최신화
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
                SpawnInitialMinions();

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
                currentExp = 0f;
                currentLevel = 1;

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
            // 1. 사운드 셧다운
            if (Sound != null) Sound.StopAllSFX(true);

            // 2. 물리 오브젝트 회수 (풀링 시스템)
            if (poolManager != null) poolManager.ClearAllActiveObjects();

            // 3. 논리 유닛 목록 초기화
            if (unitManager != null) unitManager.ClearAll();

            // 4. 웨이브 로직 중단
            if (waveManager != null) waveManager.StopSpawning();

            // 5. 트윈 및 시간 복구 — 모든 정지 사유를 일괄 해소 후 timeScale·배속 복원
            DOTween.KillAll();
            _activePauseSources.Clear();
            Time.timeScale = 1f;
            currentGameSpeed = 1f; // [BUG-FIX] currentGameSpeed 미초기화로 재시작 시 이전 배속 UI가 유지되던 버그 수정

            Debug.Log("<color=red>[GameManager]</color> CRITICAL CLEANUP: All game sessions objects and logic reset.");
        }


        private void InitAllManagers()
        {
            if (SaveData != null) SaveData.Init();
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
            if (Sound != null) Sound.Init();
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

            Debug.Log("<color=green>[GameManager]</color> All systems synchronized with cloud data.");
        }

        public void ToggleGameSpeed()
        {
            currentGameSpeed = currentGameSpeed <= 1.1f ? 1.5f : (currentGameSpeed <= 1.6f ? 2f : 1f);
            // 일시정지 사유가 없을 때만 즉시 반영, 있으면 사유 해소 시 자동 적용됨
            if (_activePauseSources.Count == 0) Time.timeScale = currentGameSpeed;
            OnSpeedChanged?.Invoke(currentGameSpeed);
        }

        /// <summary>모든 정지 사유가 없을 때 배속을 복원합니다. SetPause 사용 권장.</summary>
        public void ResumeGameSpeed()
        {
            if (_activePauseSources.Count == 0) Time.timeScale = currentGameSpeed;
        }

        public void AddExp(float amount)
        {
            currentExp += amount;
            OnExpChanged?.Invoke(currentExp, maxExp);
            if (currentExp >= maxExp)
            {
                currentExp -= maxExp;
                currentLevel++;
                maxExp = 200f + (currentLevel * 50f);
                if (skillManager != null)
                {
                    var options = skillManager.GetRandomSkillsForLevelUp(3);
                    if (options != null && options.Count > 0)
                    {
                        OnLevelUp?.Invoke(options);
                        SetPause(PauseSource.LevelUp, true);
                    }
                }
            }
        }

        public void TryReviveAsMinion(Vector3 pos)
        {
            float bonus = Resources != null ? Resources.GetUpgradeValue(UpgradeStatType.Resurrection) : 0f;     
            if (UnityEngine.Random.Range(0f, 100f) <= Mathf.Min(baseReviveChance + bonus, 90f))
            {
                if (poolManager != null)
                {
                    // [AUTOMATION] 무조건 범용 Tag("Minion")로 소환
                    GameObject minionObj = poolManager.Get(minionPoolTag, pos, Quaternion.identity);
                    if (minionObj != null && minionObj.TryGetComponent<MinionAI>(out var ai))
                    {
                        // 해금된 데이터 중 랜덤 선택 (없으면 null로 가며, AI 내부에서 기본값으로 처리됨)
                        Necromancer.Data.MinionUnlockSO selectedData = null;
                        if (unlockedMinionDatas.Count > 0)
                        {
                            selectedData = unlockedMinionDatas[UnityEngine.Random.Range(0, unlockedMinionDatas.Count)];
                        }

                        // [DATA-DRIVEN] 데이터 주입 (애니메이션/스탯 자동 설정)
                        ai.Initialize(selectedData);
                        
                        // [SOUND] 미니언 생성 효과음 재생
                        if (Sound != null) Sound.PlaySFX(Sound.sfxCreateMinion);

                        Debug.Log($"<color=green>[GameManager]</color> Automated Minion Spawned: {(selectedData != null ? selectedData.minionName : "Basic")}");
                    }
                }
            }
        }

        
        /// <summary>
        /// [LOGIC] 현재 스테이지 ID에 따라 드랍할 미니언 해금 데이터를 반환합니다. (Master's Directive)
        /// 1-10: Minion 2, 11-20: Minion 3, 21-30: Minion 4, 31-40: Minion 5, 41-50: Minion 6
        /// </summary>
        public Necromancer.Data.MinionUnlockSO GetMinionDataForCurrentStage()
        {
            if (currentStage == null || minionUnlockDataList == null || minionUnlockDataList.Count == 0) return null;

            int stageID = currentStage.stageID;
            int minionIndex = 0;

            if (stageID >= 1 && stageID <= 10) minionIndex = 1;      // Minion_02
            else if (stageID >= 11 && stageID <= 20) minionIndex = 2; // Minion_03
            else if (stageID >= 21 && stageID <= 30) minionIndex = 3; // Minion_04
            else if (stageID >= 31 && stageID <= 40) minionIndex = 4; // Minion_05
            else if (stageID >= 41) minionIndex = 5;                  // Minion_06

            // 리스트 범위를 벗어나지 않도록 방어
            if (minionIndex >= 0 && minionIndex < minionUnlockDataList.Count)
            {
                var data = minionUnlockDataList[minionIndex];
                
                // [NEW] 이미 해금된 미니언이라면 정수 드랍용 데이터 반환 안 함 (중복 파밍 방지)
                if (data != null && Resources != null && Resources.IsMinionUnlocked(data.minionID))
                {
                    return null;
                }
                
                return data;
            }

            return null;
        }

        private void UpdateUnlockedMinionPool()
        {
            Debug.Log("<color=yellow>[GameManager]</color> Updating Dynamic Minion Pool...");
            if (unlockedMinionDatas == null) unlockedMinionDatas = new List<Necromancer.Data.MinionUnlockSO>();
            unlockedMinionDatas.Clear();
            
            // 1. 기본 미니언(전사) 데이터 찾아서 추가 (ID: SkeletonWarrior)
            var warriorData = _minionUnlockDataList.Find(x => x.minionID == "SkeletonWarrior");
            if (warriorData != null) unlockedMinionDatas.Add(warriorData);

            if (Resources == null || SaveData == null || SaveData.Data == null) return;

            // 2. 해금된 다른 미니언 데이터 추가
            foreach (var data in _minionUnlockDataList)
            {
                if (data == null || data.minionID == "SkeletonWarrior") continue;

                if (Resources.IsMinionUnlocked(data.minionID))
                {
                    unlockedMinionDatas.Add(data);
                }
            }

            Debug.Log($"<color=green>[GameManager]</color> Dynamic Pool Updated. Unlocked Types: {unlockedMinionDatas.Count}");
        }

        private void SpawnInitialMinions()
        {
            if (Resources == null || poolManager == null || playerTransform == null) return;

            int count = Mathf.FloorToInt(Resources.GetUpgradeValue(UpgradeStatType.StartMinionCount));
            if (count <= 0) return;

            Debug.Log($"[GameManager] Spawning {count} initial minions from lobby upgrade.");

            for (int i = 0; i < count; i++)
            {
                // 플레이어 주변 랜덤 위치
                Vector3 spawnPos = playerTransform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 2f;
                
                GameObject minionObj = poolManager.Get(minionPoolTag, spawnPos, Quaternion.identity);
                if (minionObj != null && minionObj.TryGetComponent<MinionAI>(out var ai))
                {
                    // 기본 워리어 데이터 또는 해금된 풀 중 하나 선택
                    Necromancer.Data.MinionUnlockSO selectedData = null;
                    if (unlockedMinionDatas.Count > 0)
                        selectedData = unlockedMinionDatas[0]; // 보통 첫 번째가 워리어

                    ai.Initialize(selectedData);
                }
            }
        }

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
