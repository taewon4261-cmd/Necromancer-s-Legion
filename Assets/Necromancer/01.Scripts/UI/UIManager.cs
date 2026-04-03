using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Necromancer.Core;
using UnityEngine.SceneManagement;

namespace Necromancer.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Prefab Management")]
        public GameObject inGameUIPrefab;
        private GameObject inGameUIInstance;

        [Header("In-Game HUD")]
        public Image expFillBar;
        public TextMeshProUGUI textTimer;
        public TextMeshProUGUI textWave;

        [Header("Level Up Panel")]
        public GameObject levelUpPanel;
        public Button[] skillCardButtons = new Button[3];
        public Image[] skillCardIcons = new Image[3];
        public TextMeshProUGUI[] skillCardNames = new TextMeshProUGUI[3];
        public TextMeshProUGUI[] skillCardDescriptions = new TextMeshProUGUI[3];

        [Header("Buttons")]
        public Button speedButton;
        public Button backToTitleButton;
        public TextMeshProUGUI textSpeedToggle;

        [Header("Result Panel")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultTitleText;
        public TextMeshProUGUI resultStatsText;

        [Header("Screen Effects")]
        public CanvasGroup dangerOverlay;
        public float flashFrequency = 2.0f;
        public float maxAlpha = 0.4f;

        private List<SkillData> currentOptions;
        private PlayerController cachedPlayer;

        private void Update()
        {
            HandleLowHPEffect();
        }

        public void Init()
        {
            // [QA AUTO-FIX] Scene Guard - 타이틀 씬에서 불필요한 인게임 UI 생성 방지
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            DOTween.KillAll();
            cachedPlayer = null;

            // [ARCHITECTURAL PURITY] 자동 탐색/복구 로직 제거.
            // 인스턴스가 없다면 오직 지정된 프리팹으로만 생성합니다.
            if (inGameUIInstance == null && inGameUIPrefab != null)
            {
                inGameUIInstance = Instantiate(inGameUIPrefab, transform);
                inGameUIInstance.name = "[InGameUI_Root]";
            }

            if (inGameUIInstance != null)
            {
                // [PERFORMANCE] 플레이어 참조 캐싱 (매 프레임 GetComponent 방지)
                if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
                {
                    cachedPlayer = GameManager.Instance.playerTransform.GetComponent<PlayerController>();
                }

                // [DECOUPLING] 이름 기반 탐색 제거 -> InGameHUD 브릿지 컴포넌트 사용
                InGameHUD hud = inGameUIInstance.GetComponent<InGameHUD>();
                if (hud != null)
                {
                    expFillBar = hud.expFillBar;
                    textTimer = hud.textTimer;
                    textWave = hud.textWave;
                    speedButton = hud.speedButton;
                    backToTitleButton = hud.backToTitleButton;
                    dangerOverlay = hud.dangerOverlay;
                    levelUpPanel = hud.levelUpPanel;
                    resultPanel = hud.resultPanel;

                    // 스킬 카드 UI 연결
                    for (int i = 0; i < 3; i++)
                    {
                        if (i < hud.skillCardButtons.Length) skillCardButtons[i] = hud.skillCardButtons[i];
                        if (i < hud.skillCardIcons.Length) skillCardIcons[i] = hud.skillCardIcons[i];
                        if (i < hud.skillCardNames.Length) skillCardNames[i] = hud.skillCardNames[i];
                        if (i < hud.skillCardDescriptions.Length) skillCardDescriptions[i] = hud.skillCardDescriptions[i];
                    }

                    Debug.Log("<color=green>[UIManager]</color> UI References Mapped via InGameHUD component.");
                }
                else
                {
                    Debug.LogError("<color=red>[UIManager]</color> InGameHUD component NOT FOUND on UI root! Please attach it to the prefab.");
                }

                // 스피드 텍스트는 버튼의 자식에서 찾음 (이름이 다를 수 있음)
                if (speedButton != null) 
                    textSpeedToggle = speedButton.GetComponentInChildren<TextMeshProUGUI>();

                // [UI SYNC] 초기 텍스트 및 바 설정
                if (textSpeedToggle != null) 
                {
                    float currentSpeed = (GameManager.Instance != null) ? GameManager.Instance.currentGameSpeed : 1.0f;
                    textSpeedToggle.SetText("x" + currentSpeed.ToString("F1"));
                }
                if (textTimer != null) textTimer.SetText("00:00");
                if (textWave != null) textWave.SetText("WAVE 1");
                
                // [PERFORMANCE] 초기 경험치 바 동기화
                if (GameManager.Instance != null)
                {
                    UpdateExpBar(GameManager.Instance.currentExp, GameManager.Instance.maxExp);
                }
                else
                {
                    if (expFillBar != null) expFillBar.fillAmount = 0f;
                }
            }
            else
            {
                Debug.LogWarning("<color=yellow>[UIManager]</color> In-Game UI Instance/Prefab NOT FOUND.");
            }

            // 버튼 리스너 재연결
            if (speedButton != null)
            {
                speedButton.onClick.RemoveAllListeners();
                speedButton.onClick.AddListener(() => {
                    if (GameManager.Instance != null) GameManager.Instance.ToggleGameSpeed();
                });
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.onClick.RemoveAllListeners();
                backToTitleButton.onClick.AddListener(OnClick_BackToTitle);
            }

            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        // [STABILITY] 타이틀 씬 이동 시 인게임 UI 인스턴스 제거
        public void Clear()
        {
            if (inGameUIInstance != null)
            {
                Destroy(inGameUIInstance);
                inGameUIInstance = null;
                expFillBar = null;
                textTimer = null;
                textWave = null;
                levelUpPanel = null;
                resultPanel = null;
                dangerOverlay = null;
                cachedPlayer = null;
                Debug.Log("<color=orange>[UIManager]</color> In-Game UI Instance Cleared.");
            }
        }

        private void OnEnable()
        {
            GameManager.OnExpChanged += UpdateExpBar;
            GameManager.OnLevelUp += HandleLevelUp;
            GameManager.OnWaveStarted += HandleWaveStarted;
            GameManager.OnTimeUpdated += HandleTimeUpdated;
            GameManager.OnSpeedChanged += HandleSpeedChanged;
            GameManager.OnGameOver += ShowResultPanel;
        }

        private void OnDisable()
        {
            GameManager.OnExpChanged -= UpdateExpBar;
            GameManager.OnLevelUp -= HandleLevelUp;
            GameManager.OnWaveStarted -= HandleWaveStarted;
            GameManager.OnTimeUpdated -= HandleTimeUpdated;
            GameManager.OnSpeedChanged -= HandleSpeedChanged;
            GameManager.OnGameOver -= ShowResultPanel;
        }

        private void HandleLowHPEffect()
        {
            if (dangerOverlay == null || cachedPlayer == null) return;
            
            if (cachedPlayer.IsDead)
            {
                dangerOverlay.alpha = 0f;
                return;
            }

            float hpRatio = cachedPlayer.currentHp / cachedPlayer.maxHp;
            if (hpRatio <= 0.3f)
            {
                float lerp = (Mathf.Sin(Time.time * flashFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
                dangerOverlay.alpha = lerp * maxAlpha;
            }
            else dangerOverlay.alpha = 0f;
        }

        public void HandleSpeedChanged(float speed)
        {
            if (textSpeedToggle != null) textSpeedToggle.SetText("x" + speed.ToString("F1"));
        }

        public void UpdateExpBar(float currentExp, float maxExp)
        {
            if (expFillBar != null && maxExp > 0) expFillBar.fillAmount = Mathf.Clamp01(currentExp / maxExp);
        }

        private void HandleLevelUp(List<SkillData> options)
        {
            if (levelUpPanel != null) levelUpPanel.SetActive(true);
            Time.timeScale = 0f;
            RefreshSkillCards(options);
        }

        private void HandleTimeUpdated(float time)
        {
            if (textTimer != null)
            {
                int min = Mathf.FloorToInt(time / 60f);
                int sec = Mathf.FloorToInt(time % 60f);
                textTimer.SetText("{0:00}:{1:00}", min, sec);
            }
        }

        private void HandleWaveStarted(int index, string waveName)
        {
            if (textWave != null) textWave.SetText(waveName);
        }

        public void RefreshSkillCards(List<SkillData> newOptions)
        {
            if (newOptions == null) return;
            currentOptions = newOptions;

            for (int i = 0; i < 3; i++)
            {
                if (skillCardButtons == null || i >= skillCardButtons.Length || skillCardButtons[i] == null) continue;

                if (i < currentOptions.Count)
                {
                    int index = i; // 클로저 캡처 방지
                    skillCardButtons[i].gameObject.SetActive(true);
                    
                    if (skillCardIcons[i] != null) skillCardIcons[i].sprite = currentOptions[i].skillIcon;
                    if (skillCardNames[i] != null) skillCardNames[i].SetText(currentOptions[i].skillName);
                    if (skillCardDescriptions[i] != null) skillCardDescriptions[i].SetText(currentOptions[i].skillDescription);

                    // 버튼 리스너 설정 (사용자 요구사항: 3단계 프로세스)
                    skillCardButtons[i].onClick.RemoveAllListeners();
                    skillCardButtons[i].onClick.AddListener(() => OnClick_SelectSkill(index));
                }
                else
                {
                    skillCardButtons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 스킬 카드를 선택했을 때 실행되는 단계별 로직 (LearnSkill -> UI Close -> Resume Speed)
        /// </summary>
        private void OnClick_SelectSkill(int index)
        {
            if (currentOptions == null || index >= currentOptions.Count) return;

            SkillData selectedSkill = currentOptions[index];

            // 1단계. LearnSkill 호출: 선택한 스킬 데이터를 전달
            if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
            {
                GameManager.Instance.skillManager.LearnSkill(selectedSkill);
            }

            // 2단계. 레벨업 패널 비활성화 (창 닫기)
            if (levelUpPanel != null) levelUpPanel.SetActive(false);

            // 3단계. GameManager가 기억하고 있는 원래 배속으로 복구합니다.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGameSpeed();
            }

            Debug.Log($"<color=yellow>[UIManager]</color> Skill Learned: {selectedSkill.skillName}, UI Closed, Time Resumed via GameManager.");
        }

        public void ShowResultPanel(bool isVictory)
        {
            if (resultPanel == null) return;
            resultPanel.SetActive(true);
            if (resultTitleText != null)
            {
                resultTitleText.SetText(isVictory ? "STAGE CLEAR" : "YOU DIED");
                resultTitleText.color = isVictory ? Color.green : Color.red;
            }
        }

        public void OnClick_BackToTitle()
        {
            Time.timeScale = 1f;
            DOTween.KillAll();
            SceneManager.LoadScene("TitleScene");
        }

        private GameObject FindInactiveObjectByName(Transform parent, string name)
        {
            Transform[] allChildren = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == name) return child.gameObject;
            }
            return null;
        }
    }
}
