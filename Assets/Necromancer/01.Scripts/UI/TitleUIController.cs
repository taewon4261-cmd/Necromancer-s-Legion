using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Necromancer.UI
{
    /// <summary>
    /// 타이틀 씬의 메인 UI 및 서브 패널(스테이지 선택, 업그레이드, 설정) 애니메이션과 전환을 관리합니다.
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

        private List<CanvasGroup> allPanels = new List<CanvasGroup>();
        private bool isTransitioning = false; 

        private void Awake()
        {
            // [자가 치유] 인스펙터에서 유실되었을 경우 이름을 기반으로 자동 탐색
            if (mainButtonPanel == null) mainButtonPanel = FindPanel("Panel_Title ");
            if (stageSelectPanel == null) stageSelectPanel = FindPanel("Panel_StageSelect ");
            if (upgradePanel == null) upgradePanel = FindPanel("Panel_Upgrade ");
            if (settingPanel == null) settingPanel = FindPanel("Panel_Setting ");

            if (stageSelectPanel != null) allPanels.Add(stageSelectPanel);
            if (upgradePanel != null) allPanels.Add(upgradePanel);
            if (settingPanel != null) allPanels.Add(settingPanel);

            InitPanels();
        }

        private CanvasGroup FindPanel(string name)
        {
            // 씬 전체에서 탐색 (UI_Root 하위에 있을 것이므로)
            var obj = GameObject.Find(name);
            if (obj == null) obj = GameObject.Find(name.Trim()); // 공백 없는 버전도 시도
            
            if (obj != null) return obj.GetComponent<CanvasGroup>();
            return null;
        }

        private void Start()
        {
            InitTitleAnimations();
            SetupButtonEvents();
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
            if (logoTransform != null)
            {
                logoTransform.DOAnchorPosY(logoTransform.anchoredPosition.y + floatingAmount, floatingDuration) 
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(gameObject);
            }

            if (mainButtonPanel != null)
            {
                mainButtonPanel.alpha = 0;
                mainButtonPanel.DOFade(1, fadeDuration).SetDelay(0.3f).SetLink(gameObject);

                for (int i = 0; i < mainButtonPanel.transform.childCount; i++)
                {
                    RectTransform childRT = mainButtonPanel.transform.GetChild(i) as RectTransform;
                    if (childRT == null) continue;
                    childRT.localScale = Vector3.one * 0.95f;
                    childRT.DOScale(1f, fadeDuration).SetEase(Ease.OutCubic).SetDelay(0.4f + (i * staggerDelay)).SetLink(gameObject);
                }
            }
        }

        private void SetupButtonEvents()
        {
            if (mainButtonPanel != null)
            {
                Button[] mainButtons = mainButtonPanel.GetComponentsInChildren<Button>();
                foreach (var btn in mainButtons) BindButtonAction(btn);
            }

            foreach (var panel in allPanels)
            {
                Button[] subButtons = panel.GetComponentsInChildren<Button>(true);
                foreach (var btn in subButtons) BindButtonAction(btn);
            }
        }


        private void BindButtonAction(Button btn)
        {
            RectTransform rt = btn.GetComponent<RectTransform>();
            string btnName = btn.name.ToLower();

            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
            AddEvent(trigger, EventTriggerType.PointerEnter, (d) => { if (!isTransitioning) rt.DOScale(1.05f, 0.2f).SetLink(btn.gameObject); });
            AddEvent(trigger, EventTriggerType.PointerExit, (d) => { if (rt != null) rt.DOScale(1f, 0.2f).SetLink(btn.gameObject); });  

            btn.onClick.AddListener(() => {
                // [SOUND] 모든 버튼 클릭 시 공통 효과음 재생
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }
            });

            if (btnName.Contains("start")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Start Button Clicked"); ShowPanel(stageSelectPanel); });
            else if (btnName.Contains("upgrade")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Upgrade Button Clicked"); ShowPanel(upgradePanel); });
            else if (btnName.Contains("setting")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Setting Button Clicked"); ShowPanel(settingPanel); });
            else if (btnName.Contains("back")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Back Button Clicked"); BackToMainMenu(); });
        }

        private void AddEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        public void ShowPanel(CanvasGroup targetPanel)
        {
            if (targetPanel == null || isTransitioning) return;
            isTransitioning = true;

            if (mainButtonPanel != null)
            {
                mainButtonPanel.interactable = false;
                mainButtonPanel.blocksRaycasts = false;
                mainButtonPanel.DOFade(0, 0.2f).SetLink(gameObject).OnComplete(() => mainButtonPanel.gameObject.SetActive(false));  
            }

            targetPanel.gameObject.SetActive(true);
            targetPanel.alpha = 0f;
            targetPanel.DOFade(1f, 0.3f).SetLink(gameObject).OnComplete(() => {
                targetPanel.interactable = true;
                targetPanel.blocksRaycasts = true;
                isTransitioning = false;
            });

            var upgradeUI = targetPanel.GetComponent<UpgradeUI>();
            if (upgradeUI != null)
            {
                upgradeUI.RefreshUI();
                if (upgradeUI.contentRoot != null)
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(upgradeUI.contentRoot as RectTransform);        
                }
            }

            var stageUI = targetPanel.GetComponent<StageSelectUI>();
            if (stageUI != null && stageUI.selectedStage != null) stageUI.SelectStage(stageUI.selectedStage);   
        }

        public void BackToMainMenu()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            foreach (var panel in allPanels)
            {
                if (panel.gameObject.activeSelf)
                {
                    panel.interactable = false;
                    panel.blocksRaycasts = false;
                    panel.DOFade(0, 0.2f).SetLink(gameObject).OnComplete(() => panel.gameObject.SetActive(false));
                }
            }

            if (mainButtonPanel != null)
            {
                mainButtonPanel.gameObject.SetActive(true);
                mainButtonPanel.alpha = 0f; // [QA] 페이드 시작 전 알파 초기화
                mainButtonPanel.interactable = true; // [QA] 버튼 상호작용 복구
                mainButtonPanel.blocksRaycasts = true; 
                mainButtonPanel.DOFade(1, 0.3f).SetLink(gameObject).OnComplete(() => {
                    isTransitioning = false;
                    Debug.Log("<color=green>[TitleUI]</color> Main Menu Panel restored and interactable.");
                });
            }
            else
            {
                isTransitioning = false;
            }
        }
    }
}