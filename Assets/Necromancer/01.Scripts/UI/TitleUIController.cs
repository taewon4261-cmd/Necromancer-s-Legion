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

        private List<CanvasGroup> allPanels = new List<CanvasGroup>();
        private bool isTransitioning = false; // [STABILITY] 전환 중 중복 클릭 방지 플래그

        private void Awake()
        {
            if (Necromancer.GameManager.Instance != null) Necromancer.GameManager.Instance.titleUI = this;

            // 패널 자동 추적 및 리스트업
            if (stageSelectPanel == null) stageSelectPanel = GameObject.Find("Panel_StageSelect ")?.GetComponent<CanvasGroup>();
            if (upgradePanel == null) upgradePanel = GameObject.Find("Panel_Upgrade ")?.GetComponent<CanvasGroup>();
            if (settingPanel == null) settingPanel = GameObject.Find("Panel_Setting ")?.GetComponent<CanvasGroup>();

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
                    .SetLoops(-1, LoopType.Yoyo);
            }

            if (mainButtonPanel != null)
            {
                mainButtonPanel.alpha = 0;
                mainButtonPanel.DOFade(1, fadeDuration).SetDelay(0.3f);
                
                for (int i = 0; i < mainButtonPanel.transform.childCount; i++)
                {
                    RectTransform childRT = mainButtonPanel.transform.GetChild(i) as RectTransform;
                    if (childRT == null) continue;
                    childRT.localScale = Vector3.one * 0.95f;
                    childRT.DOScale(1f, fadeDuration).SetEase(Ease.OutCubic).SetDelay(0.4f + (i * staggerDelay));
                }
            }
        }

        private void SetupButtonEvents()
        {
            if (mainButtonPanel == null) return;
            Button[] buttons = mainButtonPanel.GetComponentsInChildren<Button>();
            
            foreach (var btn in buttons)
            {
                RectTransform rt = btn.GetComponent<RectTransform>();
                string btnName = btn.name.ToLower();

                EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
                AddEvent(trigger, EventTriggerType.PointerEnter, (d) => { if(!isTransitioning) rt.DOScale(1.05f, 0.2f); });
                AddEvent(trigger, EventTriggerType.PointerExit, (d) => { rt.DOScale(1f, 0.2f); });

                if (btnName.Contains("start")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Start Button Clicked"); ShowPanel(stageSelectPanel); });
                else if (btnName.Contains("upgrade")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Upgrade Button Clicked"); ShowPanel(upgradePanel); });
                else if (btnName.Contains("setting")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Setting Button Clicked"); ShowPanel(settingPanel); });
                else if (btnName.Contains("back")) btn.onClick.AddListener(() => { Debug.Log("[TitleUI] Back Button Clicked"); BackToMainMenu(); });
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
            if (targetPanel == null || isTransitioning) return;
            isTransitioning = true;
            
            if (mainButtonPanel != null)
            {
                mainButtonPanel.interactable = false;
                mainButtonPanel.blocksRaycasts = false;
                mainButtonPanel.DOFade(0, 0.2f).OnComplete(() => mainButtonPanel.gameObject.SetActive(false));
            }
            
            targetPanel.gameObject.SetActive(true);
            targetPanel.alpha = 0f;
            targetPanel.DOFade(1f, 0.3f).OnComplete(() => {
                targetPanel.interactable = true;
                targetPanel.blocksRaycasts = true;
                isTransitioning = false;
            });

            // 업그레이드 패널 레이아웃 강제 정렬 (UI.md 지침)
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
                    panel.DOFade(0, 0.2f).OnComplete(() => panel.gameObject.SetActive(false));
                }
            }

            if (mainButtonPanel != null)
            {
                mainButtonPanel.gameObject.SetActive(true);
                // [CRITICAL] 상호작용 속성 복구
                mainButtonPanel.interactable = true;
                mainButtonPanel.blocksRaycasts = true;
                mainButtonPanel.DOFade(1, 0.3f).OnComplete(() => isTransitioning = false);
            }
            else isTransitioning = false;
        }
    }
}
