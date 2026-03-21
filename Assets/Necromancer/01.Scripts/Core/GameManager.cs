using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Necromancer
{
    using Necromancer.Core;

    /// <summary>
    /// 게임의 전체 라이프사이클 및 하위 매니저를 중앙에서 통제하는 핵심 매니저입니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Sub Managers (New Architecture)")]
        public ResourceManager Resources;
        public CombatManager Combat;
        public SoundManager Sound;
        public DebugConsole DebugConsole;

        [Header("Sub Managers (In-Game)")]
        public PoolManager poolManager;
        public WaveManager waveManager;
        // public UIManager uiManager; // 삭제: 이제 이벤트를 통해 통신합니다.
        public SkillManager skillManager;
        public FeedbackManager feedbackManager;
        
        // --- 전역 이벤트 시스템 (성능 최적화 및 OCP 준수) ---
        public static event Action<float, float> OnExpChanged;      // (currentExp, maxExp)
        public static event Action<List<SkillData>> OnLevelUp;      // (skillOptions)
        public static event Action<int, string> OnWaveStarted;      // (waveIndex, waveName)
        public static event Action<float> OnTimeUpdated;            // (gameTime)
        public static event Action<float> OnSpeedChanged;           // (newSpeedScale)
        public static event Action<bool> OnGameOver;               // (isWin)
        // --------------------------------------------------

        // 이벤트를 외부에서 안전하게 호출하기 위한 래퍼 함수들
        public static void BroadcastTime(float time) => OnTimeUpdated?.Invoke(time);
        public static void BroadcastWave(int index, string name) => OnWaveStarted?.Invoke(index, name);
        
        [Header("Title UI")]
        public UI.TitleUIController titleUI;

        [Header("Player Tracking & Stats")]
        public Transform playerTransform;
        public float magnetRadius = 3f;
        public float baseReviveChance = 30f;
        public string minionTag = "Minion";

        [Header("In-Game Level System")]
        public int currentLevel = 1;
        public float currentExp = 0f;
        public float maxExp = 100f;

        [Header("Game Speed Settings")]
        public float currentGameSpeed = 1f;
        public bool isThreeTimesSpeedAllowed = false;

        [Header("Global State")]
        public StageDataSO currentStage;
        [Tooltip("에디터에서 GameScene만 바로 실행할 때 사용할 테스트용 스테이지 데이터")]
        public StageDataSO debugDefaultStage;
        
        public bool IsGameOver { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 'System' 오브젝트 전체를 파괴되지 않게 보호합니다.
                DontDestroyOnLoad(gameObject.transform.root.gameObject);
                
                // 씬 전환 시 매니저 재연결을 위해 이벤트 등록
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                
                InitAllManagers();
                RefreshInGameManagers(); // Added this line
            }
            else
            {
                // 이미 전역 GameManager가 있다면, '이 스크립트'만 파괴합니다.
                // 이렇게 하면 같은 오브젝트에 붙어있는 배속 버튼 등이 연결된 '현지 UIManager'는 살아남게 됩니다.
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // 씬이 로드될 때마다 현재 씬의 매니저들을 다시 찾습니다.
            RefreshInGameManagers();
            
            if (scene.name == "TitleScene")
            {
                RefreshTitleManagers();
            }
        }

        private void RefreshTitleManagers()
        {
            // TitleScene 전용 컴포넌트 (각 매니저가 Awake에서 스스로 등록하도록 구조 변경 중)
            // titleUI = FindObjectOfType<UI.TitleUIController>(); // 삭제: Self-Registration으로 변경
            
            // 필요 시 인게임 매니저 참조 초기화 (메모리 누수 방지 및 오작동 방지)
            poolManager = null;
            waveManager = null;
            skillManager = null;
            feedbackManager = null;
            
            Debug.Log("<color=yellow><b>[GameManager]</b> Title Managers Cleaned Up.</color>");
        }

        private void RefreshInGameManagers()
        {
            // GameScene에 있는 매니저들을 리바인딩
            // poolManager = FindObjectOfType<PoolManager>(); // 삭제: Self-Registration으로 변경
            // waveManager = FindObjectOfType<WaveManager>(); // 삭제: Self-Registration으로 변경
            // skillManager = FindObjectOfType<SkillManager>(); // 삭제: Self-Registration으로 변경
            // feedbackManager = FindObjectOfType<FeedbackManager>(); // 삭제: Self-Registration으로 변경

            // 에디터 테스트 지원: 직접 GameScene을 틀었을 때 stage가 없다면 디폴트 적용
            if (currentStage == null && debugDefaultStage != null)
            {
                currentStage = debugDefaultStage;
                Debug.Log($"<color=magenta><b>[GameManager]</b> Debug Mode: Using Default Stage - {currentStage.stageName}</color>");
            }

            // 재초기화
            if (poolManager != null) poolManager.Init();
            if (waveManager != null) waveManager.Init();
            if (skillManager != null) skillManager.Init();
            
            // UI 초기화 이벤트 (초기 경험치 등 전달)
            OnExpChanged?.Invoke(currentExp, maxExp);
            OnSpeedChanged?.Invoke(currentGameSpeed);

            IsGameOver = false;
            Time.timeScale = currentGameSpeed;

            Debug.Log("<color=cyan><b>[GameManager]</b> In-Game Managers Refreshed for GameScene.</color>");
            
            // 전투 개시 로그
            if (currentStage != null)
            {
                Debug.Log($"<color=orange><b>[GameManager]</b> Starting Stage: {currentStage.stageName}</color>");
            }
        }

        private void InitAllManagers()
        {
            // 1. New Core Managers (Always persist with GameManager)
            if (Resources == null) Resources = GetComponentInChildren<ResourceManager>() ?? gameObject.AddComponent<ResourceManager>();
            if (Combat == null) Combat = GetComponentInChildren<CombatManager>() ?? gameObject.AddComponent<CombatManager>();
            if (Sound == null) Sound = GetComponentInChildren<SoundManager>() ?? gameObject.AddComponent<SoundManager>();
            if (DebugConsole == null) DebugConsole = GetComponentInChildren<DebugConsole>() ?? gameObject.AddComponent<DebugConsole>();

            Resources.Init();
            Combat.Init();
        }

        #region In-Game Logic (Original)

        public void TryReviveAsMinion(Vector3 deathPosition)
        {
            float roll = UnityEngine.Random.Range(0f, 100f);
            if (roll <= baseReviveChance)
            {
                if (poolManager != null)
                {
                    poolManager.Get(minionTag, deathPosition, Quaternion.identity);
                }
            }
        }

        public void AddExp(float amount)
        {
            currentExp += amount;
            OnExpChanged?.Invoke(currentExp, maxExp);
            if (currentExp >= maxExp) LevelUp();
        }

        private void LevelUp()
        {
            currentExp -= maxExp;
            currentLevel++;
            maxExp = 100f + (currentLevel * 30f);

            if (skillManager != null)
            {
                var options = skillManager.GetRandomSkillsForLevelUp(3);
                if (options != null && options.Count > 0)
                {
                    OnExpChanged?.Invoke(currentExp, maxExp);
                    OnLevelUp?.Invoke(options);
                    Time.timeScale = 0f;
                }
                else
                {
                    Debug.LogWarning("[GameManager] SkillManager returned no skill options for level up.");
                    // 선택지가 없더라도 게임은 멈추면 안되므로 timescale은 유지하거나 리셋
                    ResumeGameSpeed();
                }
            }
        }

        public void ToggleGameSpeed()
        {
            // 레벨업 팝업이나 일시정지 중에는 배속 조절을 막습니다 (순수 OCP 방식)
            if (Time.timeScale == 0f) return;

            if (currentGameSpeed <= 1.1f) currentGameSpeed = 1.5f;
            else if (currentGameSpeed <= 1.6f) currentGameSpeed = 2f;
            else if (currentGameSpeed <= 2.1f) currentGameSpeed = isThreeTimesSpeedAllowed ? 3f : 1f;
            else currentGameSpeed = 1f;

            Time.timeScale = currentGameSpeed;
            Debug.Log($"[GameManager] Speed Toggled: {currentGameSpeed}, Time.timeScale: {Time.timeScale}");
            OnSpeedChanged?.Invoke(currentGameSpeed);
        }

        public void ResumeGameSpeed() => Time.timeScale = currentGameSpeed;

        /// <summary>
        /// 스테이지 클리어 시 호출 (WaveManager 등에서 호출)
        /// </summary>
        public void OnStageClear()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            
            // 1. 다음 스테이지 해금
            if (currentStage != null)
            {
                Resources.UnlockLevel(currentStage.stageID + 1);
            }

            // 2. 결과 UI 표시 (승리)
            OnGameOver?.Invoke(true);

            Time.timeScale = 0f;
            Debug.Log("<color=green><b>[GameManager]</b> Stage Cleared!</color>");
        }

        /// <summary>
        /// 플레이어 사망 시 호출 (PlayerController 등에서 호출)
        /// </summary>
        public void OnStageFailed()
        {
            if (IsGameOver) return;
            IsGameOver = true;

            // 1. 결과 UI 표시 (패배)
            OnGameOver?.Invoke(false);

            Time.timeScale = 0f;
            Debug.Log("<color=red><b>[GameManager]</b> Stage Failed...</color>");
        }

        #endregion

        #region Global Scene Control

        public void StartGame(StageDataSO stage)
        {
            currentStage = stage;
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        #endregion
    }
}
