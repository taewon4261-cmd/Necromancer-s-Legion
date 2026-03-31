using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Necromancer.Core;
using UnityEngine.SceneManagement; // 씬 관리를 위해 추가

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
            DOTween.KillAll();

            if (inGameUIPrefab != null && inGameUIInstance == null)
            {
                inGameUIInstance = Instantiate(inGameUIPrefab, transform);
                inGameUIInstance.name = "[InGameUI_Root]";
                
                expFillBar = inGameUIInstance.transform.Find("HUD/Exp_Bar/Fill")?.GetComponent<Image>();
                textTimer = inGameUIInstance.transform.Find("HUD/Text_Timer")?.GetComponent<TextMeshProUGUI>();
                textWave = inGameUIInstance.transform.Find("HUD/Text_Wave")?.GetComponent<TextMeshProUGUI>();
                levelUpPanel = inGameUIInstance.transform.Find("Panels/LevelUp_Panel")?.gameObject;
                resultPanel = inGameUIInstance.transform.Find("Panels/Result_Panel")?.gameObject;
                dangerOverlay = inGameUIInstance.transform.Find("Effects/Danger_Overlay")?.GetComponent<CanvasGroup>();
                speedButton = inGameUIInstance.transform.Find("HUD/Buttons/Speed_Btn")?.GetComponent<Button>();
                backToTitleButton = inGameUIInstance.transform.Find("HUD/Buttons/Back_Btn")?.GetComponent<Button>();
                
                if (speedButton != null) textSpeedToggle = speedButton.GetComponentInChildren<TextMeshProUGUI>();
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
            if (player == null || player.IsDead) // isDead 대신 IsDead 프로퍼티 사용
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
            SceneManager.LoadScene("TitleScene");
        }
    }
}
