using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Necromancer.UI
{
    /// <summary>
    /// 타이틀 화면의 비주얼 연출과 각 기능 패널(스테이지, 업그레이드, 세팅) 전환을 담당합니다.
    /// </summary>
    public class TitleUIController : MonoBehaviour
    {
        [Header("Main Visuals")]
        public RectTransform logoTransform;
        public CanvasGroup mainButtonPanel;
        public RectTransform backgroundImage;
        
        [Header("Sub Panels")]
        public CanvasGroup stageSelectPanel;
        public CanvasGroup upgradePanel;
        public CanvasGroup settingPanel;

        [Header("Animation Settings")]
        public float fadeDuration = 0.6f;
        public float floatingAmount = 15f;
        public float floatingDuration = 2.5f;
        public float staggerDelay = 0.12f;
        public float parallaxStrength = 25f;

        private VerticalLayoutGroup layoutGroup;
        private List<CanvasGroup> allPanels = new List<CanvasGroup>();

        private void Awake()
        {
            // GameManager에 스스로를 등록 (성능 최적화용)
            if (Necromancer.GameManager.Instance != null) Necromancer.GameManager.Instance.titleUI = this;

            layoutGroup = mainButtonPanel?.GetComponent<VerticalLayoutGroup>();
            
            // 모든 패널 리스트업 (일괄 OFF 처리용)
            if (stageSelectPanel != null) allPanels.Add(stageSelectPanel);
            if (upgradePanel != null) allPanels.Add(upgradePanel);
            if (settingPanel != null) allPanels.Add(settingPanel);
            
            InitPanels();
        }

        private void Start()
        {
            InitTitleAnimations();
            SetupButtonEvents();
        }

        private void Update()
        {
            HandleParallaxEffect();
        }

        private void InitPanels()
        {
            foreach (var panel in allPanels)
            {
                panel.alpha = 0;
                panel.blocksRaycasts = false;
                panel.interactable = false;
                panel.gameObject.SetActive(false);
            }
        }

        private void InitTitleAnimations()
        {
            // 1. 로고 부유 효과 (위아래로 둥실둥실)
            if (logoTransform != null)
            {
                logoTransform.DOAnchorPosY(logoTransform.anchoredPosition.y + floatingAmount, floatingDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            // 2. 메인 버튼 패널 애니메이션 (이동 없이 제자리에서 페이드)
            if (mainButtonPanel != null)
            {
                mainButtonPanel.alpha = 0;
                mainButtonPanel.DOFade(1, fadeDuration).SetDelay(0.3f);
                
                // 자식 버튼들 제자리에서 차례대로 페이드 인 (이동 로직 완전 제거)
                for (int i = 0; i < mainButtonPanel.transform.childCount; i++)
                {
                    RectTransform childRT = mainButtonPanel.transform.GetChild(i) as RectTransform;
                    if (childRT == null) continue;

                    childRT.localScale = Vector3.one * 0.95f; // 아주 살짝 작은 크기에서 시작
                    childRT.DOScale(1f, fadeDuration)
                        .SetEase(Ease.OutCubic)
                        .SetDelay(0.4f + (i * staggerDelay));
                }
            }
        }

        /// <summary>
        /// 모션 제거: 모바일 환경이므로 마우스 트래킹 배경 효과 삭제
        /// </summary>
        private void HandleParallaxEffect()
        {
            // 기능 삭제
        }

        private void SetupButtonEvents()
        {
            if (mainButtonPanel == null) return;
            Button[] buttons = mainButtonPanel.GetComponentsInChildren<Button>();
            
            // 버튼 이름이나 순서에 따라 기능을 매칭 (여기서는 이름 기반 예시)
            foreach (var btn in buttons)
            {
                RectTransform rt = btn.GetComponent<RectTransform>();
                string btnName = btn.name.ToLower();

                // 1. 공통 비주얼 이벤트 바인딩
                EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
                AddEvent(trigger, EventTriggerType.PointerEnter, (d) => rt.DOScale(1.05f, 0.2f));
                AddEvent(trigger, EventTriggerType.PointerExit, (d) => rt.DOScale(1f, 0.2f));
                AddEvent(trigger, EventTriggerType.PointerClick, (d) => rt.DOPunchScale(new Vector3(-0.05f, -0.05f, 0), 0.1f));

                // 2. 버튼별 기능 바인딩
                if (btnName.Contains("start")) btn.onClick.AddListener(() => ShowPanel(stageSelectPanel));
                else if (btnName.Contains("upgrade")) btn.onClick.AddListener(() => ShowPanel(upgradePanel));
                else if (btnName.Contains("setting")) btn.onClick.AddListener(() => ShowPanel(settingPanel));
                else if (btnName.Contains("back")) btn.onClick.AddListener(() => BackToMainMenu());
            }
        }

        private void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        public void ShowPanel(CanvasGroup targetPanel)
        {
            if (targetPanel == null) return;
            
            // 메인 메뉴 숨기기
            mainButtonPanel.DOFade(0, 0.2f).OnComplete(() => mainButtonPanel.gameObject.SetActive(false));
            
            targetPanel.gameObject.SetActive(true);
            targetPanel.DOFade(1, 0.3f);
            targetPanel.interactable = true;
            targetPanel.blocksRaycasts = true;

            // 추가: 스테이지 선택 패널이 열릴 때 초기 데이터 세팅이 필요하다면 여기서 처리 가능
            var stageUI = targetPanel.GetComponent<StageSelectUI>();
            if (stageUI != null && stageUI.selectedStage != null)
            {
                stageUI.SelectStage(stageUI.selectedStage);
            }
        }

        public void BackToMainMenu()
        {
            foreach (var panel in allPanels)
            {
                if (panel.gameObject.activeSelf)
                {
                    panel.DOFade(0, 0.3f).OnComplete(() => panel.gameObject.SetActive(false));
                    panel.interactable = false;
                    panel.blocksRaycasts = false;
                }
            }

            mainButtonPanel.gameObject.SetActive(true);
            mainButtonPanel.DOFade(1, 0.3f);
        }
    }
}
