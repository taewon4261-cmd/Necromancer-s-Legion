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
        [SerializeField] private DebugConsole _debugConsole;

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
        public DebugConsole DebugConsole => _debugConsole;

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
                
                // [ARCHITECTURAL PURITY] 강제 할당 체크: 에디터에서 연결하지 않으면 즉시 에러 발생
                if (_poolManager == null || _waveManager == null || _uiManager == null)
                {
                    Debug.LogError("<color=red>[GameManager]</color> CRITICAL ERROR: Essential managers (Pool, Wave, UI) are NOT assigned in the Inspector!");
                }
                
                SceneManager.sceneLoaded += OnSceneLoaded;
                InitAllManagers();
            }
            else Destroy(gameObject);
        }



        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isGameScene = scene.name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (isGameScene)
            {
                // [STABILITY] 게임 시작 시 시간 흐름 보장 (이전 세션의 Pause/GameOver 상태 초기화)
                Time.timeScale = currentGameSpeed;
                IsGameOver = false;
                if (Resources != null) 
                {
                    Resources.currentSessionSoul = 0;
                    BroadcastSoul(0); // 인게임 진입 시 HUD 소울 0으로 초기화
                }

                if (playerTransform == null)
                {
                    var player = GameObject.FindWithTag("Player");
                    if (player != null) playerTransform = player.transform;
                    else playerTransform = GameObject.FindObjectOfType<PlayerController>()?.transform;
                }

                // [ARCHITECTURAL PURITY] 자가 치유(Validate) 제거. 인스펙터 참조를 신뢰함.
                if (poolManager == null || waveManager == null || uiManager == null)
                {
                    Debug.LogError("<color=red>[GameManager]</color> Required Manager references are NULL in GameScene context!");
                    return;
                }

                if (poolManager != null) poolManager.Init();
                else Debug.LogError("<color=red>[GameManager]</color> PoolManager NOT FOUND in Hierarchy!");

                if (waveManager != null) waveManager.Init();
                else Debug.LogError("<color=red>[GameManager]</color> WaveManager NOT FOUND in Hierarchy!");

                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();

                OnSpeedChanged?.Invoke(currentGameSpeed);
                Debug.Log($"<color=cyan><b>[GameManager]</b> In-Game Context Initialized: Pool({poolManager!=null}), Wave({waveManager!=null}), UI({uiManager!=null})</color>");
            }
            else if (scene.name == "TitleScene")
            {
                if (_titleUI == null) 
                {
                    Debug.LogWarning("[GameManager] TitleUIController reference is missing in TitleScene.");
                }

                // [DATA INTEGRITY] 씬 전환 시(타이틀 복귀 등) 정산되지 않은 소울이 있다면 지갑에 반영
                if (Resources != null && Resources.currentSessionSoul > 0)
                {
                    Resources.CommitSessionSoul();
                }

                if (uiManager != null) uiManager.Clear();
                if (waveManager != null) waveManager.StopSpawning();
                
                playerTransform = null;
                currentStage = null;
                IsGameOver = false;
                currentExp = 0f;
                currentLevel = 1;
                Time.timeScale = 1f;
                if (Resources != null) Resources.currentSessionSoul = 0;
                Debug.Log("<color=cyan><b>[GameManager]</b> Session data committed and cleared for TitleScene.</color>");
            }
        }

        private void InitAllManagers()
        {
            if (SaveData != null) SaveData.Init();
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
            if (Sound != null) Sound.Init();
            
            // [ARCHITECT] 모든 매니저는 인스펙터에서 사전에 할당되어야 함
            if (_poolManager == null) Debug.LogWarning("[GameManager] PoolManager is not assigned in Inspector!");
            if (_unitManager == null) Debug.LogWarning("[GameManager] UnitManager is not assigned in Inspector!");
            
            Debug.Log("<color=cyan><b>[GameManager]</b> Global Managers initialization complete.</color>");
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
                if (poolManager != null) poolManager.Get(minionTag, pos, Quaternion.identity);
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
            
            // [NEW] 영혼 흡수(Vacuum) 연출 진행 후 최종 저장 및 UI 출력
            StartCoroutine(StageClearSequence());
        }

        private IEnumerator StageClearSequence()
        {
            // 1. 맵에 흩어진 모든 영혼 진공 흡수 (애니메이션)
            var gems = ExpGem.ActiveGems.ToList();
            foreach (var gem in gems) 
            {
                if (gem != null) gem.StartVacuum();
            }

            // 2. 흡수 완료 시점까지 대기 (약 1.5초)
            yield return new WaitForSeconds(1.5f);

            // 3. [DATA INTEGRITY] 모든 연합이 끝난 시점의 최종 획득량을 저장 (Master's Directive)
            if (Resources != null) Resources.CommitSessionSoul();

            // 4. 결과 UI 출력 알림 (UIManager가 받아 ShowResultPanel 호출)
            OnGameOver?.Invoke(true);
        }

        public void OnStageFailed()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            
            // [STABILITY] 즉각적인 게임 정지 (Master's Directive)
            Time.timeScale = 0f;

            // [DATA INTEGRITY] 정지된 시점의 소울 저장 (정합성 0% 편차 보장)
            if (Resources != null) Resources.CommitSessionSoul();

            OnGameOver?.Invoke(false);
        }
    }
}
