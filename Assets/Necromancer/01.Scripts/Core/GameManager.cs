using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Necromancer.Core;
using Necromancer.UI; // UI 관련 클래스들을 위해 추가
using UnityEngine.SceneManagement;

namespace Necromancer
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Sub Managers")]
        public ResourceManager Resources;
        public CombatManager Combat;
        public SoundManager Sound;
        public DebugConsole DebugConsole;

        [Header("In-Game Managers")]
        public PoolManager poolManager;
        public WaveManager waveManager;
        public SkillManager skillManager;
        public FeedbackManager feedbackManager;
        public UIManager uiManager; 
        public TitleUIController titleUI;

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
                SceneManager.sceneLoaded += OnSceneLoaded;
                InitAllManagers();
            }
            else Destroy(gameObject);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameScene")
            {
                if (poolManager != null) poolManager.Init();
                if (waveManager != null) waveManager.Init();
                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();
                
                OnSpeedChanged?.Invoke(currentGameSpeed);
                IsGameOver = false;
                Debug.Log("<color=cyan><b>[GameManager]</b> In-Game Managers initialized for GameScene.</color>");
            }
            else if (scene.name == "TitleScene")
            {
                IsGameOver = false;
                Time.timeScale = 1f;
            }
        }

        private void InitAllManagers()
        {
            if (Resources == null) Resources = GetComponentInChildren<ResourceManager>() ?? gameObject.AddComponent<ResourceManager>();
            if (Combat == null) Combat = GetComponentInChildren<CombatManager>() ?? gameObject.AddComponent<CombatManager>();
            if (Sound == null) Sound = GetComponentInChildren<SoundManager>() ?? gameObject.AddComponent<SoundManager>();
            if (DebugConsole == null) DebugConsole = GetComponentInChildren<DebugConsole>() ?? gameObject.AddComponent<DebugConsole>();
            
            if (Resources != null) Resources.Init();
            if (Combat != null) Combat.Init();
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
