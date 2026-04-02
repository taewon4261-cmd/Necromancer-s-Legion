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

        public void Init()
        {
            // [QA AUTO-FIX] Scene Guard - 타이틀 씬에서 불필요한 인게임 UI 생성 방지
            // [STABILITY] 플레이어가 있거나 특정 씬이면 인게임 UI 초기화 허용
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene" || (GameObject.FindObjectOfType<PlayerController>() != null);
            if (!isGameScene) return;

            DOTween.KillAll();

            // [자가 치유] 인게임 UI 프리팹이 비어있다면 자동 탐색
            if (inGameUIPrefab == null)
            {
                inGameUIPrefab = Resources.Load<GameObject>("Prefabs/InGameUI");
                if (inGameUIPrefab == null) inGameUIPrefab = Resources.Load<GameObject>("HUD_Canvas");
            }

            if (inGameUIPrefab != null && inGameUIInstance == null)
            {
                inGameUIInstance = Instantiate(inGameUIPrefab, transform);
                inGameUIInstance.name = "[InGameUI_Root]";

                // [STABILITY] 이름 기반 재귀 탐색
                var components = inGameUIInstance.GetComponentsInChildren<Transform>(true);
                foreach (var child in components)
                {
                    if (child.name == "Exp_Bar_Fill" || child.name == "Fill") expFillBar = child.GetComponent<Image>();
                    if (child.name == "Text_Timer") textTimer = child.GetComponent<TextMeshProUGUI>();
                    if (child.name == "Text_Wave") textWave = child.GetComponent<TextMeshProUGUI>();
                    if (child.name == "Speed_Btn") speedButton = child.GetComponent<Button>();
                    if (child.name == "Back_Btn") backToTitleButton = child.GetComponent<Button>();
                    if (child.name == "Danger_Overlay") dangerOverlay = child.GetComponent<CanvasGroup>();
                    if (child.name == "LevelUp_Panel") levelUpPanel = child.gameObject;
                    if (child.name == "Result_Panel") resultPanel = child.gameObject;
                }

                // 스피드 텍스트는 버튼의 자식에서 찾음 (이름이 다를 수 있음)
                if (speedButton != null && textSpeedToggle == null) 
                    textSpeedToggle = speedButton.GetComponentInChildren<TextMeshProUGUI>();

                if (textTimer == null) Debug.LogError("<color=red>[UIManager]</color> 'Text_Timer' NOT FOUND in Prefab!");
                if (textWave == null) Debug.LogError("<color=red>[UIManager]</color> 'Text_Wave' NOT FOUND in Prefab!");
                
                // [UI SYNC] 초기 텍스트 설정
                if (textSpeedToggle != null && GameManager.Instance != null) 
                    textSpeedToggle.SetText("x" + GameManager.Instance.currentGameSpeed.ToString("F1"));
                if (textTimer != null) textTimer.SetText("00:00");
                if (textWave != null) textWave.SetText("WAVE 1");
                
                Debug.Log($"<color=green>[UIManager]</color> In-Game UI Root initialized. Timer: {textTimer != null}, Wave: {textWave != null}");
            }

            if (speedButton != null)
            {
                speedButton.onClick.RemoveAllListeners();
                speedButton.onClick.AddListener(() => GameManager.Instance.ToggleGameSpeed());
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

        private void Update()
        {
            HandleLowHPEffect();
        }

        private void HandleLowHPEffect()
        {
            if (dangerOverlay == null || GameManager.Instance == null || GameManager.Instance.playerTransform == null) return;
            PlayerController player = GameManager.Instance.playerTransform.GetComponent<PlayerController>();
            if (player == null || player.IsDead)
            {
                dangerOverlay.alpha = 0f;
                return;
            }

            float hpRatio = player.currentHp / player.maxHp;
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
                    skillCardButtons[i].gameObject.SetActive(true);
                    if (skillCardIcons[i] != null) skillCardIcons[i].sprite = currentOptions[i].skillIcon;
                    if (skillCardNames[i] != null) skillCardNames[i].SetText(currentOptions[i].skillName);
                    if (skillCardDescriptions[i] != null) skillCardDescriptions[i].SetText(currentOptions[i].skillDescription);
                }
                else skillCardButtons[i].gameObject.SetActive(false);
            }
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