// File: Assets/Necromancer/01.Scripts/UI/UIManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening; // DOTween 안전 모드 방지를 위해 추가

namespace Necromancer
{
    /// <summary>
    /// 게임 내 모든 UI(경험치 바, 레벨업 패널 등)를 중앙 컨트롤하는 매니저
    /// GameManager에서 접근하여 실시간으로 수치를 갱신합니다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("In-Game HUD")]
        [Tooltip("화면 상단 경험치 바 이미지 (Image Type: Filled)")]
        public Image expFillBar;
        
        [Tooltip("상단 좌측: 게임 생존 시간 (00:00)")]
        public TextMeshProUGUI textTimer;
        
        [Tooltip("상단 중앙: 현재 진행 중인 웨이브 단계 (예: Wave 1)")]
        public TextMeshProUGUI textWave;

        [Header("Level Up Panel")]
        [Tooltip("레벨업 시 팝업되는 전체 부모 패널")]
        public GameObject levelUpPanel;

        [Tooltip("3개의 스킬 카드(버튼) 컴포넌트들")]
        public Button[] skillCardButtons = new Button[3];
        [Tooltip("3개의 스킬 카드 아이콘")]
        public Image[] skillCardIcons = new Image[3];
        [Tooltip("3개의 스킬 이름 텍스트 (TMP)")]
        public TextMeshProUGUI[] skillCardNames = new TextMeshProUGUI[3];
        [Tooltip("3개의 스킬 설명 텍스트 (TMP)")]
        public TextMeshProUGUI[] skillCardDescriptions = new TextMeshProUGUI[3];
        
        [Header("Ads Strategy")]
        [Tooltip("광고 보고 스킬 새로고침(리프레시) 버튼")]
        public Button adRefreshButton;
        
        [Header("Buttons")]
        public Button speedButton;
        public Button backToTitleButton;
        
        [Header("Fast Forward")]
        [Tooltip("배속 토글 버튼 내부의 텍스트 컴포넌트.")]
        public TextMeshProUGUI textSpeedToggle;

        [Header("Result Panel")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultTitleText;
        public TextMeshProUGUI resultStatsText;

        // 현재 창에 뽑힌 임시 3개의 스킬 목록 기억 장치
        private List<SkillData> currentOptions;

        private void OnEnable()
        {
            // 전역 이벤트 구독
            GameManager.OnExpChanged += UpdateExpBar;
            GameManager.OnLevelUp += HandleLevelUp;
            GameManager.OnWaveStarted += HandleWaveStarted;
            GameManager.OnTimeUpdated += HandleTimeUpdated;
            GameManager.OnSpeedChanged += UpdateSpeedToggleText;
            GameManager.OnGameOver += ShowResultPanel;
        }

        private void OnDisable()
        {
            // 메모리 누수 방지: 구독 해제
            GameManager.OnExpChanged -= UpdateExpBar;
            GameManager.OnLevelUp -= HandleLevelUp;
            GameManager.OnWaveStarted -= HandleWaveStarted;
            GameManager.OnTimeUpdated -= HandleTimeUpdated;
            GameManager.OnSpeedChanged -= UpdateSpeedToggleText;
            GameManager.OnGameOver -= ShowResultPanel;
        }

        /// <summary>
        /// 씬 전환 시 또는 GameManager에 의해 호출되어 UI 요소를 연결합니다.
        /// 모든 참조는 인스펙터에서 사전에 할당되어야 합니다 (성능 최적화).
        /// </summary>
        public void Init()
        {
            DOTween.KillAll();

            // 유효성 검사 (개발 단계에서 실수를 방지하기 위함)
            ValidateReferences();

            // 버튼 이벤트 바인딩
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

            // 패널 초기 상태 설정
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        private void ValidateReferences()
        {
            if (expFillBar == null) Debug.LogError("[UIManager] 'expFillBar'가 인스펙터에서 할당되지 않았습니다!");
            if (levelUpPanel == null) Debug.LogError("[UIManager] 'levelUpPanel'이 인스펙터에서 할당되지 않았습니다!");
            if (speedButton == null) Debug.LogWarning("[UIManager] 'speedButton'이 할당되지 않았습니다. 배속 기능이 작동하지 않을 수 있습니다.");
        }

        private void HandleLevelUp(List<SkillData> options)
        {
            ShowLevelUpPanel();
            RefreshSkillCards(options);
        }

        private void HandleTimeUpdated(float time)
        {
            if (textTimer != null)
            {
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                textTimer.SetText("{0:00}:{1:00}", minutes, seconds);
            }
        }

        private void HandleWaveStarted(int index, string waveName)
        {
            if (textWave != null && !string.IsNullOrEmpty(waveName))
            {
                textWave.SetText(waveName);
            }
        }

        /// <summary>
        /// 배속 토글 시 텍스트 변경
        /// </summary>
        public void UpdateSpeedToggleText(float speed)
        {
            if (textSpeedToggle != null)
            {
                // 최적화된 SetText 포맷 중 가장 안정적인 방식으로 변경
                // x{0:F1} 스타일이 TMP 버전에 따라 0으로 나올 수 있어 명시적 변환 사용
                textSpeedToggle.SetText("x" + speed.ToString("F1"));
            }
        }

        /// <summary>
        /// 경험치 바 시각적 갱신 (0.0 ~ 1.0 비율)
        /// </summary>
        public void UpdateExpBar(float currentExp, float maxExp)
        {
            if (expFillBar != null && maxExp > 0)
            {
                expFillBar.fillAmount = currentExp / maxExp;
            }
        }

        /// <summary>
        /// 상단 HUD 정보 (시간, 웨이브) 일괄 갱신
        /// </summary>
        public void UpdateHUD(float time, string waveName)
        {
            if (textTimer != null)
            {
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);
                textTimer.SetText("{0:00}:{1:00}", minutes, seconds);
            }

            if (textWave != null && !string.IsNullOrEmpty(waveName))
            {
                textWave.SetText(waveName);
            }
        }

        /// <summary>
        /// 레벨 업 시 팝업 창 열기 (시간 정지)
        /// </summary>
        public void ShowLevelUpPanel()
        {
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(true);
                // 물리 엔진 및 애니메이션 업데이트 완전 정지
                Time.timeScale = 0f; 
            }
        }

        /// <summary>
        /// SkillManager에서 뽑아준 3개의 스킬(또는 리프레시된 스킬) 데이터를 바탕으로 UI 카드를 시각적으로 그립니다.
        /// </summary>
        public void RefreshSkillCards(List<SkillData> newOptions)
        {
            currentOptions = newOptions;

            for (int i = 0; i < 3; i++)
            {
                if (i < currentOptions.Count && currentOptions[i] != null)
                {
                    SkillData data = currentOptions[i];
                    skillCardButtons[i].gameObject.SetActive(true);
                    
                    if(skillCardIcons[i] != null) skillCardIcons[i].sprite = data.skillIcon;
                    if(skillCardNames[i] != null) skillCardNames[i].SetText(data.skillName);
                    if(skillCardDescriptions[i] != null) skillCardDescriptions[i].SetText(data.skillDescription);
                }
                else
                {
                    // 부족하면 카드를 하나 가림
                    skillCardButtons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 1~3번 째 스킬 카드(버튼)를 클릭했을 때 호출됩니다.
        /// </summary>
        /// <param name="index">카드 번호 (0, 1, 2)</param>
        public void OnClick_SelectSkillCard(int index)
        {
            if (currentOptions == null || index < 0 || index >= currentOptions.Count) return;

            SkillData selectedData = currentOptions[index];

            // 1. 매니저에게 실제 스탯 버프 반영 지시
            if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
            {
                GameManager.Instance.skillManager.ApplySkill(selectedData);
            }

            // 2. 창 닫고 게임 재개 (기존의 1배속 고정 대신 현재 기억해둔 배속으로 복구)
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResumeGameSpeed();
            }
            else
            {
                Time.timeScale = 1f; 
            }
        }

        public void OnClick_WatchAdToRefresh()
        {
            Debug.Log("[UIManager] 📺 보상형 광고 시청 후 리프레시 완료!");
            
            if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
            {
                List<SkillData> renewedCards = GameManager.Instance.skillManager.GetRandomSkillsForLevelUp(3);
                RefreshSkillCards(renewedCards);
            }
        }

        /// <summary>
        /// 승리 또는 패배 결과창 표시
        /// </summary>
        public void ShowResultPanel(bool isVictory)
        {
            if (resultPanel == null) return;

            resultPanel.SetActive(true);
            
            if (resultTitleText != null)
            {
                resultTitleText.SetText(isVictory ? "STAGE CLEAR" : "YOU DIED");
                resultTitleText.color = isVictory ? Color.green : Color.red;
            }

            if (resultStatsText != null)
            {
                // 간단한 통계 표시 (추후 확장 가능)
                int minutes = Mathf.FloorToInt(Time.timeSinceLevelLoad / 60f);
                int seconds = Mathf.FloorToInt(Time.timeSinceLevelLoad % 60f);
                resultStatsText.SetText("Survival Time: {0:00}:{1:00}\nGold Earned: {2}", 
                    minutes, seconds, GameManager.Instance.Resources.currentSoul);
            }
        }

        public void OnClick_BackToTitle()
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
        }
    }
}
