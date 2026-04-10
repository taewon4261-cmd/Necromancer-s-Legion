using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Necromancer.Systems;
using Cysharp.Threading.Tasks;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// [FINAL CLEAN] 타이틀 씬의 UI 전환을 100% SetActive(True/False)로만 관리합니다.
    /// CanvasGroup을 전혀 사용하지 않아 알파값 관련 버그가 원천 차단된 최적화 버전입니다.
    /// </summary>
    public class TitleUIController : MonoBehaviour
    {
        [Header("Main Panels")]
        public GameObject authPanel;          // 로그인 창 (Panel_Login)
        public GameObject mainButtonPanel;    // 메인 메뉴 버튼 그룹 (Panel_MainButtons)
        public RectTransform logoTransform;   // 로고 (애니메이션용)

        [Header("Sub Panels")]
        public GameObject stageSelectPanel;
        public GameObject upgradePanel;
        public GameObject settingPanel;

        private List<GameObject> allSubPanels = new List<GameObject>();
        private bool isTitleInitialized = false;

        private void Awake()
        {
            // 필수 할당 체크
            if (authPanel == null)        Debug.LogError("[TitleUI] authPanel is NOT assigned!");
            if (mainButtonPanel == null)  Debug.LogError("[TitleUI] mainButtonPanel is NOT assigned!");
            if (stageSelectPanel == null) Debug.LogError("[TitleUI] stageSelectPanel is NOT assigned!");
            if (upgradePanel == null)     Debug.LogError("[TitleUI] upgradePanel is NOT assigned!");
            if (settingPanel == null)     Debug.LogError("[TitleUI] settingPanel is NOT assigned!");

            if (stageSelectPanel != null) allSubPanels.Add(stageSelectPanel);
            if (upgradePanel != null) allSubPanels.Add(upgradePanel);
            if (settingPanel != null) allSubPanels.Add(settingPanel);

            // GameManager 등록
            if (GameManager.Instance != null) GameManager.Instance.RegisterTitleUI(this);

            InitAllPanels();
        }

        private void OnEnable()
        {
            AuthManager.OnAuthStateChanged += HandleAuthState;
        }

        private void OnDisable()
        {
            AuthManager.OnAuthStateChanged -= HandleAuthState;
        }

        private void Start()
        {
            SetupButtonEvents();

            // 로그인 상태 즉시 체크 (재방문 유저 대응)
            if (GameManager.Instance != null && GameManager.Instance.Auth != null)
            {
                var state = GameManager.Instance.Auth.CurrentState;
                if (state == AuthState.LoggedIn || state == AuthState.Guest || state == AuthState.Failed)
                {
                    HandleAuthState(state);
                }
            }
        }

        /// <summary>
        /// [AUTH] 로그인 확인 시 호출되어 로그인창을 끄고 메인 화면을 켭니다.
        /// </summary>
        private void HandleAuthState(AuthState state)
        {
            if (isTitleInitialized) return;

            switch (state)
            {
                case AuthState.LoggedIn:
                case AuthState.Guest:
                case AuthState.Failed:
                    isTitleInitialized = true;
                    
                    if (authPanel != null) authPanel.SetActive(false);
                    if (mainButtonPanel != null) mainButtonPanel.SetActive(true);

                    // [ROBUST] 서브 패널 부모(Sub_Panels)를 미리 켜둡니다.
                    if (stageSelectPanel != null && stageSelectPanel.transform.parent != null)
                        stageSelectPanel.transform.parent.gameObject.SetActive(true);
                    
                    Debug.Log($"<color=cyan>[TitleUI]</color> Login Verified ({state}). UI Activated.");
                    break;
            }
        }

        private void InitAllPanels()
        {
            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            foreach (var panel in allSubPanels) if (panel != null) panel.SetActive(false);
            
            // [AUTH] 로그인 기록이 있으면 로그인 창을 즉시 띄우지 않습니다. (방문 유저 경험 개선)
            bool hasLoginRecord = false;
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null)
            {
                hasLoginRecord = GameManager.Instance.SaveData.Data.lastLoginMethod != "None";
            }

            if (authPanel != null) authPanel.SetActive(!hasLoginRecord);
            
            if (hasLoginRecord)
                Debug.Log("<color=cyan>[TitleUI]</color> Login record found. Hiding login panel for auto-login.");
        }

        public void SetupInitialUI() => InitAllPanels();

        public async UniTask OnLoginSuccess()
        {
            // [STABILITY] AuthManager.OnAuthStateChanged 이벤트를 통해 HandleAuthState가 이미 실행되므로
            // 여기서는 추가적인 UI 호출 없이 로그로만 성공을 확인합니다.
            Debug.Log("<color=green>[TitleUI]</color> Login/Link Success: Navigating to Main UI.");
            await UniTask.Yield();
        }

        /// <summary>
        /// 메인 메뉴를 끄고 지정된 서브 패널을 즉시 활성화합니다.
        /// </summary>
        public void ShowPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;

            // 서브 패널의 부모 오브젝트가 있다면 먼저 켭니다.
            if (targetPanel.transform.parent != null)
                targetPanel.transform.parent.gameObject.SetActive(true);

            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            targetPanel.SetActive(true);

            // 내부 UI 갱신 로직 실행
            var upgradeUI = targetPanel.GetComponent<UpgradeUI>();
            if (upgradeUI != null) upgradeUI.RefreshUI();

            var stageUI = targetPanel.GetComponent<StageSelectUI>();
            if (stageUI != null && stageUI.selectedStage != null) stageUI.SelectStage(stageUI.selectedStage);

            Debug.Log($"<color=green>[TitleUI]</color> Open SubPanel: {targetPanel.name}");
        }

        /// <summary>
        /// 모든 서브 패널을 끄고 메인 메뉴로 복귀합니다.
        /// </summary>
        public void BackToMainMenu()
        {
            foreach (var panel in allSubPanels) if (panel != null) panel.SetActive(false);
            if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
        }

        private void SetupButtonEvents()
        {
            if (mainButtonPanel != null)
            {
                Button[] mainButtons = mainButtonPanel.GetComponentsInChildren<Button>(true);
                foreach (var btn in mainButtons) BindButtonAction(btn);
            }

            foreach (var panel in allSubPanels)
            {
                if (panel == null) continue;
                Button[] subButtons = panel.GetComponentsInChildren<Button>(true);
                foreach (var btn in subButtons) BindButtonAction(btn);
            }
        }

        private void BindButtonAction(Button btn)
        {
            string btnName = btn.name.ToLower();

            // [ROBUST] 다른 스크립트와의 리스너 충돌 방지 (특히 back 버튼)
            if (btnName.Contains("back"))
            {
                btn.onClick.RemoveAllListeners();
            }

            btn.onClick.AddListener(() => {
                Debug.Log($"<color=yellow>[TitleUI-Click]</color> Button: {btn.name}");
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }
            });

            // [BUGFIX] "back"을 최우선 체크합니다.
            // "button_back_upgrade"처럼 이름에 다른 키워드가 포함된 경우
            // 잘못된 분기로 들어가는 것을 방지합니다.
            if      (btnName.Contains("back"))    btn.onClick.AddListener(() => BackToMainMenu());
            else if (btnName.Contains("start"))   btn.onClick.AddListener(() => ShowPanel(stageSelectPanel));
            else if (btnName.Contains("upgrade")) btn.onClick.AddListener(() => ShowPanel(upgradePanel));
            else if (btnName.Contains("setting")) btn.onClick.AddListener(() => ShowPanel(settingPanel));
        }
    }
}