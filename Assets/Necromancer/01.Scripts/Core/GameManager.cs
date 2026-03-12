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

        [Header("Sub Managers (In-Game)")]
        public PoolManager poolManager;
        public WaveManager waveManager;
        public UIManager uiManager;
        public SkillManager skillManager;
        public FeedbackManager feedbackManager;

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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 씬 전화 시 매니저 재연결을 위해 이벤트 등록
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
                
                InitAllManagers();
            }
            else
            {
                Destroy(gameObject);
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
            if (scene.name == "GameScene")
            {
                RefreshInGameManagers();
            }
        }

        private void RefreshInGameManagers()
        {
            // GameScene에 있는 매니저들을 리바인딩
            poolManager = FindObjectOfType<PoolManager>();
            waveManager = FindObjectOfType<WaveManager>();
            uiManager = FindObjectOfType<UIManager>();
            skillManager = FindObjectOfType<SkillManager>();
            feedbackManager = FindObjectOfType<FeedbackManager>();

            playerTransform = GameObject.FindWithTag("Player")?.transform;

            // 재초기화
            if (poolManager != null) poolManager.Init();
            if (waveManager != null) waveManager.Init();
            if (uiManager != null) uiManager.Init();
            if (skillManager != null) skillManager.Init();

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

            Resources.Init();
            Combat.Init();
        }

        #region In-Game Logic (Original)

        public void TryReviveAsMinion(Vector3 deathPosition)
        {
            float roll = Random.Range(0f, 100f);
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
            if (uiManager != null) uiManager.UpdateExpBar(currentExp, maxExp);
            if (currentExp >= maxExp) LevelUp();
        }

        private void LevelUp()
        {
            currentExp -= maxExp;
            currentLevel++;
            maxExp = 100f + (currentLevel * 30f);

            if (uiManager != null && skillManager != null)
            {
                uiManager.UpdateExpBar(currentExp, maxExp);
                uiManager.ShowLevelUpPanel();
                Time.timeScale = 0f;
                uiManager.RefreshSkillCards(skillManager.GetRandomSkillsForLevelUp(3));
            }
        }

        public void ToggleGameSpeed()
        {
            if (currentGameSpeed <= 1.1f) currentGameSpeed = 1.5f;
            else if (currentGameSpeed <= 1.6f) currentGameSpeed = 2f;
            else if (currentGameSpeed <= 2.1f) currentGameSpeed = isThreeTimesSpeedAllowed ? 3f : 1f;
            else currentGameSpeed = 1f;

            if (uiManager != null && uiManager.levelUpPanel != null && uiManager.levelUpPanel.activeSelf) return;
            
            Time.timeScale = currentGameSpeed;
            if (uiManager != null) uiManager.UpdateSpeedToggleText(currentGameSpeed);
        }

        public void ResumeGameSpeed() => Time.timeScale = currentGameSpeed;

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
