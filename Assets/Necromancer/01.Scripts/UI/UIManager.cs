// File: Assets/Necromancer/01.Scripts/UI/UIManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        // 현재 창에 뽑힌 임시 3개의 스킬 목록 기억 장치
        private List<SkillData> currentOptions;

        /// <summary>
        /// 초기화: GameManager 연동 대기 및 패널 숨김
        /// </summary>
        public void Init()
        {
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(false);
            }
            
            UpdateExpBar(0, 100); // 초기화
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
                    if(skillCardNames[i] != null) skillCardNames[i].text = data.skillName;
                    if(skillCardDescriptions[i] != null) skillCardDescriptions[i].text = data.skillDescription;
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
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null && gm.skillManager != null)
            {
                gm.skillManager.ApplySkill(selectedData);
            }

            // 2. 창 닫고 게임 재개
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            Time.timeScale = 1f; 
        }

        /// <summary>
        /// 보상형 광고 시청 후 3지선다 새로고침 버튼 클릭 시 호출
        /// </summary>
        public void OnClick_WatchAdToRefresh()
        {
            Debug.Log("[UIManager] 📺 보상형 광고 시청 후 리프레시 완료!");
            
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null && gm.skillManager != null)
            {
                // TODO: 실제로는 여기서 AD SDK 호출을 하고 OnComplete 콜백에서 아래 새로고침 로직이 들어가야 합니다.
                List<SkillData> renewedCards = gm.skillManager.GetRandomSkillsForLevelUp(3);
                RefreshSkillCards(renewedCards);
            }
        }
    }
}
