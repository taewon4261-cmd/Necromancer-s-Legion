using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using Necromancer.Core;
using Necromancer.UI;
using UnityEngine.SceneManagement;

namespace Necromancer
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Global Managers")]
        [SerializeField] private SaveDataManager _saveData;
        [SerializeField] private ResourceManager _resources;
        [SerializeField] private CombatManager _combat;
        [SerializeField] private SoundManager _sound;
        [SerializeField] private DebugConsole _debugConsole;        [SerializeField] private Necromancer.Systems.AdManager _adManager;


        [Header("Scene Managers")]
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private SkillManager _skillManager;
        [SerializeField] private FeedbackManager _feedbackManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private TitleUIController _titleUI;
        [SerializeField] private UnitManager _unitManager;

        // --- [Direct Access Properties] ---
        public SaveDataManager SaveData => _saveData;
        public ResourceManager Resources => _resources;
        public CombatManager Combat => _combat;
        public SoundManager Sound => _sound;
        public DebugConsole DebugConsole => _debugConsole;        public Necromancer.Systems.AdManager AdManager => _adManager;


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
        public static event Action<int> OnSoulChanged;
        public static event Action<float> OnTimeUpdated;
        public static event Action<float> OnSpeedChanged;
        public static event Action<bool> OnGameOver;

        public static void BroadcastTime(float time) => OnTimeUpdated?.Invoke(time);
        public static void BroadcastWave(int index, int total, string name) => OnWaveStarted?.Invoke(index, total, name);
        public static void BroadcastSoul(int amount) => OnSoulChanged?.Invoke(amount);

        public Transform playerTransform;
        public PlayerController playerController;
        public float magnetRadius = 3f;
        public float baseReviveChance = 30f;
        public string minionTag = "Minion";
        private List<string> unlockedMinionTags = new List<string>(); // [NEW] 해금된 미니언 풀 (랜덤 소환용)

        public float currentGameSpeed = 1f;
        public StageDataSO currentStage;
        public bool IsGameOver { get; private set; }

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

        private void Update()
        {
            // [STABILITY] 중앙 집중형 입력 관리 (Single Source of Truth)
            // 어떤 UI가 꺼져 있거나 파편화된 상황에서도 GameManager는 항상 ESC/Back 버튼을 감시합니다.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
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
                UpdateUnlockedMinionPool();

                if (waveManager != null) waveManager.Init();
                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();

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

            // 5. 트윈 및 시간 복구
            DOTween.KillAll();
            Time.timeScale = 1f;

            Debug.Log("<color=red>[GameManager]</color> CRITICAL CLEANUP: All game sessions objects and logic reset.");
        }


        private void InitAllManagers()
        {
            if (SaveData != null) SaveData.Init();
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
            if (Sound != null) Sound.Init();
            if (AdManager != null) AdManager.Init(); // [NEW] 광고 매니저 초기화
        }

        public void StartGame(StageDataSO stage)
        {
            currentStage = stage;
            SceneManager.LoadScene("GameScene");
        }

        public void ToggleGameSpeed()
        {
            currentGameSpeed = currentGameSpeed <= 1.1f ? 1.5f : (currentGameSpeed <= 1.6f ? 2f : 1f);
            Time.timeScale = currentGameSpeed;
            OnSpeedChanged?.Invoke(currentGameSpeed);
        }

        public void ResumeGameSpeed() => Time.timeScale = currentGameSpeed;

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
                        Time.timeScale = 0f;
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
                    string tagToSpawn = minionTag;
                    if (unlockedMinionTags.Count > 0)
                    {
                        tagToSpawn = unlockedMinionTags[UnityEngine.Random.Range(0, unlockedMinionTags.Count)];
                    }

                    poolManager.Get(tagToSpawn, pos, Quaternion.identity);
                    
                    // [SOUND] 미니언 생성 효과음 재생
                    if (Sound != null) Sound.PlaySFX(Sound.sfxCreateMinion);

                    Debug.Log($"<color=green>[GameManager]</color> Revived as: {tagToSpawn}");
                }
            }
        }

        private void UpdateUnlockedMinionPool()
        {
            unlockedMinionTags.Clear();
            unlockedMinionTags.Add(minionTag);

            if (Resources == null) return;

            if (Resources.GetUpgradeLevel("Upgrade_UnlockArcher_Lv") >= 1) unlockedMinionTags.Add("Minion_Archer");
            if (Resources.GetUpgradeLevel("Upgrade_UnlockMage_Lv") >= 1) unlockedMinionTags.Add("Minion_Mage");
            if (Resources.GetUpgradeLevel("Upgrade_UnlockGiant_Lv") >= 1) unlockedMinionTags.Add("Minion_Giant");
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
            OnGameOver?.Invoke(true);
        }

        public void OnStageFailed()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            Time.timeScale = 0f;
            if (Resources != null) Resources.CommitSessionSoul();
            OnGameOver?.Invoke(false);
        }
    }
}
