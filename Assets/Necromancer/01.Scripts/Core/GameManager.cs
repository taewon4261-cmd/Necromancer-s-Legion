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
            // [STABILITY] 씬 이름 대신 '플레이어 검색' 또는 씬 이름으로 체크 (더 견고함)
            bool isGameScene = scene.name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (isGameScene)
            {
                // [STABILITY] 씬 하이라키에서 플레이어 자동 탐색 (Awake 타이밍 이슈 방지)
                if (playerTransform == null)
                {
                    var player = GameObject.FindWithTag("Player");
                    if (player != null) playerTransform = player.transform;
                    else playerTransform = GameObject.FindObjectOfType<PlayerController>()?.transform;
                }

                if (poolManager != null) poolManager.Init();
                if (waveManager != null) waveManager.Init();
                if (skillManager != null) skillManager.Init();
                if (uiManager != null) uiManager.Init();

                // [UI SYNC] UI가 초기화 된 후 현재 게임 속도를 강제로 다시 전송
                OnSpeedChanged?.Invoke(currentGameSpeed);
                IsGameOver = false;
                Debug.Log("<color=cyan><b>[GameManager]</b> In-Game Managers initialized for GameScene.</color>");
            }
            else if (scene.name == "TitleScene")
            {
                // [QA AUTO-FIX] 타이틀 복귀 시 세션 데이터 클린업
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