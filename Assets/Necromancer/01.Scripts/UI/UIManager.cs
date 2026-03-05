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
        /// 팝업 창 안의 임시 버튼 클릭 시 호출 (시간 재개)
        /// 사용자가 버튼 온클릭 이벤트에 연결합니다.
        /// </summary>
        public void OnClick_SelectRandomSkill()
        {
            // TODO: 실제로는 선택한 스킬번으로 무기 데미지/공속 등을 올립니다. (1주차는 그냥 창만 닫기)
            Debug.Log("[UIManager] 스킬 선택 완료! 게임을 재개합니다.");
            
            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(false);
            }
            
            // 시간 정상화
            Time.timeScale = 1f; 
        }
    }
}
