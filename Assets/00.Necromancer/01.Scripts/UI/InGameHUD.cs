using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 인게임 HUD의 모든 UI 컴포넌트 참조를 관리하는 브릿지 클래스입니다.
    /// UIManager는 이 클래스를 통해 각 요소에 접근하며, 이름 기반 탐색을 방지합니다.
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        [Header("Stat Display")]
        public Image expFillBar;
        public TextMeshProUGUI textTimer;
        public TextMeshProUGUI textWave;
        public TextMeshProUGUI textSoul;

        [Header("Panels")]
        public GameObject levelUpPanel;
        public ResultUI resultUI;
        public CanvasGroup dangerOverlay;
        public GameObject tutorialPanel;
        public Button tutorialCloseButton; // 튜토리얼 패널 안의 닫기 버튼

        [Header("Log System")]
        public RectTransform logContents; // LogView > Contents

        [Header("Buttons")]
        public Button speedButton;
        public Button backToTitleButton;
        public Button rerollButton;
        public Button settingsButton; // 설정창 토글 버튼
        public TextMeshProUGUI textSpeedToggle;
        public SettingUI settingUI; // 설정창 패널 직접 연결용
        
        [Header("Skill Selection (Part of LevelUp Panel)")]
        public Button[] skillCardButtons = new Button[3];
        public Image[] skillCardIcons = new Image[3];
        public TextMeshProUGUI[] skillCardNames = new TextMeshProUGUI[3];
        public TextMeshProUGUI[] skillCardDescriptions = new TextMeshProUGUI[3];
    }
}
