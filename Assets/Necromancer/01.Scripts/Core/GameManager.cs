using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Global Managers (Private for Self-Healing)")]
        [SerializeField] private SaveDataManager _saveData;
        [SerializeField] private ResourceManager _resources;
        [SerializeField] private CombatManager _combat;
        [SerializeField] private SoundManager _sound;
        [SerializeField] private DebugConsole _debugConsole;

        [Header("Scene Managers (Private for Self-Healing)")]
        [SerializeField] private PoolManager _poolManager;
        [SerializeField] private WaveManager _waveManager;
        [SerializeField] private SkillManager _skillManager;
        [SerializeField] private FeedbackManager _feedbackManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private TitleUIController _titleUI;

        // --- [Self-Healing Properties] ---
        public SaveDataManager SaveData => GetManager(ref _saveData);
        public ResourceManager Resources => GetManager(ref _resources);
        public CombatManager Combat => GetManager(ref _combat);
        public SoundManager Sound => GetManager(ref _sound);
        public DebugConsole DebugConsole => GetManager(ref _debugConsole);

        public PoolManager poolManager => GetManager(ref _poolManager);
        public WaveManager waveManager => GetManager(ref _waveManager);
        public SkillManager skillManager => GetManager(ref _skillManager);
        public FeedbackManager feedbackManager => GetManager(ref _feedbackManager);
        public UIManager uiManager => GetManager(ref _uiManager);
        public TitleUIController titleUI => GetManager(ref _titleUI);

        /// <summary>
        /// 자가 치유(Self-Healing) 로직: 참조가 없으면 자식 객체에서 다시 찾습니다.
        /// </summary>
        private T GetManager<T>(ref T field) where T : MonoBehaviour
        {
            if (field == null)
            {
                field = GetComponentInChildren<T>(true);
                if (field == null)
                {
                    Debug.LogError($"<color=red><b>[GameManager]</b> Critical Error: {typeof(T).Name} is missing in Hierarchy!</color>");
                }
                else
                {
                    Debug.Log($"<color=yellow><b>[GameManager]</b> Self-Healed: {typeof(T).Name} reference restored.</color>");
                }
            }
            return field;
        }

        public static event Action<float, float> OnExpChanged;
        public static event Action<List<SkillData>> OnLevelUp;
        public static event Action<int, string> OnWaveStarted;
        public static event Action<float> OnTimeUpdated;
        public static event Action<float> OnSpeedChanged;
        public static event Action<bool> OnGameOver;

        public static void BroadcastTime(float time) => OnTimeUpdated?.Invoke(time);
        public static void BroadcastWave(int index, string name) => OnWaveStarted?.Invoke(index, name);

        public Transform playerTransform;
        public float magnetRadius = 3f;
        public float baseReviveChance = 30f;
        public string minionTag = "Minion";
        public float currentGameSpeed = 1f;
        public StageDataSO currentStage;
        public bool IsGameOver { get; private set; }

        public int currentLevel = 1;
        public float currentExp = 0f;
        public float maxExp = 100f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                
                // 프리팹 내부 참조가 이미 되어있을 것이므로, 
                // 만약 에디터에서 실수로 비워두었을 때만 보충합니다.
                ValidateManagerReferences();
                
                SceneManager.sceneLoaded += OnSceneLoaded;
                InitAllManagers();
            }
            else Destroy(gameObject);
        }

        private void ValidateManagerReferences()
        {
            if (_saveData == null) _saveData = GetComponentInChildren<SaveDataManager>(true);
            if (_resources == null) _resources = GetComponentInChildren<ResourceManager>(true);
            if (_combat == null) _combat = GetComponentInChildren<CombatManager>(true);
            if (_sound == null) _sound = GetComponentInChildren<SoundManager>(true);
            if (_debugConsole == null) _debugConsole = GetComponentInChildren<DebugConsole>(true);
            
            if (_poolManager == null) _poolManager = GetComponentInChildren<PoolManager>(true);
            if (_waveManager == null) _waveManager = GetComponentInChildren<WaveManager>(true);
            if (_skillManager == null) _skillManager = GetComponentInChildren<SkillManager>(true);
            if (_feedbackManager == null) _feedbackManager = GetComponentInChildren<FeedbackManager>(true);
            if (_uiManager == null) _uiManager = GetComponentInChildren<UIManager>(true);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool isGameScene = scene.name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (isGameScene)
            {
                if (playerTransform == null)
                {
                    var player = GameObject.FindWithTag("Player");
                    if (player != null) playerTransform = player.transform;
                    else playerTransform = GameObject.FindObjectOfType<PlayerController>()?.transform;
                }

                // [STABILITY] 프리팹 내부 참조를 그대로 사용하므로 Null 체크만 수행
                if (poolManager != null) poolManager.Init();
                if (waveManager != null) waveManager.Init();
                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();

                OnSpeedChanged?.Invoke(currentGameSpeed);
                IsGameOver = false;
                Debug.Log("<color=cyan><b>[GameManager]</b> In-Game Managers initialized from Prefab.</color>");
            }
            else if (scene.name == "TitleScene")
            {
                if (_titleUI == null) _titleUI = GameObject.FindObjectOfType<TitleUIController>();

                if (uiManager != null) uiManager.Clear();
                if (waveManager != null) waveManager.StopSpawning();
                
                playerTransform = null;
                currentStage = null;
                IsGameOver = false;
                Time.timeScale = 1f;
                Debug.Log("<color=cyan><b>[GameManager]</b> Session data cleared for TitleScene.</color>");
            }
        }

        private void InitAllManagers()
        {
            // 모든 매니저를 GameManager 하위로 강제 정렬 (프리팹 구조 유지)
            foreach(Transform child in transform)
            {
                // 특수한 경우가 아니면 자식으로 유지
            }

            if (SaveData != null) SaveData.Init();
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
            if (Sound != null) Sound.Init();
            
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
                maxExp = 100f + (currentLevel * 30f);
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
            if (currentStage != null && Resources != null) Resources.UnlockLevel(currentStage.stageID + 1);     
            OnGameOver?.Invoke(true);
            Time.timeScale = 0f;
        }

        public void OnStageFailed()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            OnGameOver?.Invoke(false);
            Time.timeScale = 0f;
        }
    }
}