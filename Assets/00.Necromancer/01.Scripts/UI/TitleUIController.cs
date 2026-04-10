using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Necromancer.Systems;
using Cysharp.Threading.Tasks;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// 타이틀 씬의 UI 전환을 SetActive(True/False)로만 관리합니다.
    /// 버튼 바인딩은 전량 Inspector 직렬화 방식을 사용하며,
    /// 런타임 Find / GetComponentsInChildren 스캔이 없습니다.
    /// 서브 패널 내부 갱신은 각 패널의 OnEnable()이 자동으로 담당합니다.
    /// </summary>
    public class TitleUIController : MonoBehaviour
    {
        [Header("Main Panels")]
        public GameObject authPanel;
        public GameObject mainButtonPanel;
        public RectTransform logoTransform;

        [Header("Sub Panels")]
        public GameObject stageSelectPanel;
        public GameObject upgradePanel;
        public GameObject minionStorePanel;
        public GameObject settingPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnUpgrade;
        [SerializeField] private Button btnMinionStore;
        [SerializeField] private Button btnSetting;

        [Header("Back Buttons (Sub Panels)")]
        [SerializeField] private Button btnStageSelectBack;
        [SerializeField] private Button btnUpgradeBack;
        [SerializeField] private Button btnMinionStoreBack;
        [SerializeField] private Button btnSettingBack;

        private readonly List<GameObject> allSubPanels = new List<GameObject>();
        private bool isTitleInitialized = false;

        private void Awake()
        {
            ValidateReferences();

            if (stageSelectPanel != null) allSubPanels.Add(stageSelectPanel);
            if (upgradePanel != null)     allSubPanels.Add(upgradePanel);
            if (minionStorePanel != null) allSubPanels.Add(minionStorePanel);
            if (settingPanel != null)     allSubPanels.Add(settingPanel);

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

            if (GameManager.Instance?.Auth != null)
            {
                var state = GameManager.Instance.Auth.CurrentState;
                if (state == AuthState.LoggedIn || state == AuthState.Guest || state == AuthState.Failed)
                    HandleAuthState(state);
            }
        }

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
                    Debug.Log($"<color=cyan>[TitleUI]</color> Login Verified ({state}). UI Activated.");
                    break;
            }
        }

        private void InitAllPanels()
        {
            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            foreach (var panel in allSubPanels)
                if (panel != null) panel.SetActive(false);

            var saveData = GameManager.Instance?.SaveData?.Data;
            bool hasLoginRecord = saveData != null && saveData.lastLoginMethod != "None";

            if (authPanel != null) authPanel.SetActive(!hasLoginRecord);

            if (hasLoginRecord)
                Debug.Log("<color=cyan>[TitleUI]</color> Login record found. Hiding login panel for auto-login.");
        }

        public void SetupInitialUI() => InitAllPanels();

        public async UniTask OnLoginSuccess()
        {
            Debug.Log("<color=green>[TitleUI]</color> Login/Link Success: Navigating to Main UI.");
            await UniTask.Yield();
        }

        /// <summary>
        /// 메인 메뉴를 끄고 지정된 서브 패널을 활성화합니다.
        /// 패널 내부 갱신은 각 패널의 OnEnable()이 자동으로 처리합니다.
        /// </summary>
        public void ShowPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;
            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            targetPanel.SetActive(true);
            PlaySelectSound();
            Debug.Log($"<color=green>[TitleUI]</color> Open SubPanel: {targetPanel.name}");
        }

        /// <summary>
        /// 모든 서브 패널을 끄고 메인 메뉴로 복귀합니다.
        /// </summary>
        public void BackToMainMenu()
        {
            foreach (var panel in allSubPanels)
                if (panel != null) panel.SetActive(false);
            if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
            PlaySelectSound();
        }

        private void SetupButtonEvents()
        {
            // 메인 메뉴 → 서브 패널 열기
            btnStart?.onClick.AddListener(() => ShowPanel(stageSelectPanel));
            btnUpgrade?.onClick.AddListener(() => ShowPanel(upgradePanel));
            btnMinionStore?.onClick.AddListener(() => ShowPanel(minionStorePanel));
            btnSetting?.onClick.AddListener(() => ShowPanel(settingPanel));

            // 서브 패널 → 메인 메뉴 복귀
            btnStageSelectBack?.onClick.AddListener(BackToMainMenu);
            btnUpgradeBack?.onClick.AddListener(BackToMainMenu);
            btnMinionStoreBack?.onClick.AddListener(BackToMainMenu);
            btnSettingBack?.onClick.AddListener(BackToMainMenu);
        }

        private void PlaySelectSound()
        {
            if (GameManager.Instance?.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
        }

        private void ValidateReferences()
        {
            if (authPanel == null)        Debug.LogError("[TitleUI] authPanel is NOT assigned!");
            if (mainButtonPanel == null)  Debug.LogError("[TitleUI] mainButtonPanel is NOT assigned!");
            if (stageSelectPanel == null) Debug.LogError("[TitleUI] stageSelectPanel is NOT assigned!");
            if (upgradePanel == null)     Debug.LogError("[TitleUI] upgradePanel is NOT assigned!");
            if (minionStorePanel == null) Debug.LogError("[TitleUI] minionStorePanel is NOT assigned!");
            if (settingPanel == null)     Debug.LogError("[TitleUI] settingPanel is NOT assigned!");
            if (btnStart == null)         Debug.LogWarning("[TitleUI] btnStart is NOT assigned!");
            if (btnUpgrade == null)       Debug.LogWarning("[TitleUI] btnUpgrade is NOT assigned!");
            if (btnMinionStore == null)   Debug.LogWarning("[TitleUI] btnMinionStore is NOT assigned!");
            if (btnSetting == null)       Debug.LogWarning("[TitleUI] btnSetting is NOT assigned!");
        }
    }
}
