using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Necromancer.Core;
using Necromancer.Systems;
using Necromancer;

namespace Necromancer.UI
{
    public enum SettingState
    {
        Lobby,
        InGame
    }

    /// <summary>
    /// 게임 설정을 제어하는 드라이브 패널입니다.
    /// </summary>
    public class SettingUI : MonoBehaviour
    {
        [Header("Volume Sliders")]
        public Slider masterSlider;
        public Slider bgmSlider;
        public Slider sfxSlider;

        [Header("Buttons")]
        public Button loginBtn;
        public Button privacyBtn;
        public Button backBtn;
        public Button quitBtn;
        public Button mainMenuBtn;
        public Button tutorialBtn; // 게임 팁 / 도움말 버튼

        [Header("Texts")]
        public TMPro.TextMeshProUGUI loginTxt;

        [Header("Toast Notification")]
        [SerializeField] private CanvasGroup toastCanvasGroup;
        [SerializeField] private TMPro.TextMeshProUGUI toastText;

        private void Awake()
        {
            // [FORCE RESET WRONG BINDINGS] 인스펙터 상의 드래그 앤 드롭 실수(Human Error)를 런타임에 완벽히 교정하기 위해
            // 모든 버튼 변수 레퍼런스를 null로 리셋한 뒤 자가 바인딩을 실행합니다.
            backBtn = null;
            mainMenuBtn = null;
            quitBtn = null;
            loginBtn = null;
            privacyBtn = null;
            tutorialBtn = null;

            // [AUTO BINDING] 버튼 자식 텍스트 및 이름 기반 정밀 바인딩
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                var tmpText = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                var legacyText = btn.GetComponentInChildren<Text>(true);
                
                string rawText = "";
                if (tmpText != null) rawText = tmpText.text;
                else if (legacyText != null) rawText = legacyText.text;
                
                string textVal = rawText.Replace(" ", "");
                string btnName = btn.name;

                // 텍스트에 "메인으로", "메인메뉴", "로비", "나가기", "Lobby", "MainMenu" 가 들어가 있으면 mainMenuBtn으로 강제 할당
                if (textVal.Contains("메인으로") || textVal.Contains("메인메뉴") || textVal.Contains("로비") || textVal.Contains("나가기") || textVal.Contains("Lobby") || textVal.Contains("MainMenu"))
                {
                    mainMenuBtn = btn;
                }
                // 텍스트에 "닫기", "뒤로", "돌아가기", "이전", "Back" 등이 들어가 있거나 이름 매칭 시 backBtn으로 할당
                else if (textVal.Contains("닫기") || textVal.Contains("뒤로") || textVal.Contains("돌아가기") || textVal.Contains("이전") || textVal.Contains("Back") || btnName == "Btn_Close" || btnName == "Btn_Back")
                {
                    backBtn = btn;
                }
                else if (btnName.Contains("Quit") || textVal.Contains("종료"))
                {
                    quitBtn = btn;
                }
                else if (btnName.Contains("Login") || textVal.Contains("로그인") || textVal.Contains("연동"))
                {
                    loginBtn = btn;
                }
                else if (btnName.Contains("Privacy") || textVal.Contains("개인정보"))
                {
                    privacyBtn = btn;
                }
                else if (btnName.Contains("Tutorial") || btnName.Contains("Guide") || textVal.Contains("팁") || textVal.Contains("가이드") || textVal.Contains("도움"))
                {
                    tutorialBtn = btn;
                }
            }

            Debug.Log($"<color=orange>[SettingUI-AutoBind]</color> Mapping Result - backBtn: {(backBtn != null ? backBtn.name : "NULL")}, mainMenuBtn: {(mainMenuBtn != null ? mainMenuBtn.name : "NULL")}, quitBtn: {(quitBtn != null ? quitBtn.name : "NULL")}, tutorialBtn: {(tutorialBtn != null ? tutorialBtn.name : "NULL")}");

            // Fallback (텍스트가 없는 버튼 등의 보완)
            if (backBtn == null)
            {
                foreach (var btn in buttons)
                {
                    if (btn.name == "Btn_BackToMain" && btn != mainMenuBtn)
                    {
                        backBtn = btn;
                        break;
                    }
                }
            }

            if (masterSlider == null) Debug.LogWarning("[SettingUI] masterSlider is NOT assigned!");
            if (bgmSlider == null) Debug.LogWarning("[SettingUI] bgmSlider is NOT assigned!");
            if (sfxSlider == null) Debug.LogWarning("[SettingUI] sfxSlider is NOT assigned!");

            // [RAYCAST & CLICKABILITY SAFETY GUARD]
            // "메인으로" 버튼을 포함한 설정창 내 모든 버튼들의 터치 인식을 보장하기 위해
            // interactable 활성화, 자식 텍스트의 Raycast Target 해제 및 Sibling 최전방 정렬을 적용합니다.
            var allButtons = new Button[] { loginBtn, privacyBtn, backBtn, quitBtn, mainMenuBtn, tutorialBtn };
            foreach (var btn in allButtons)
            {
                if (btn != null)
                {
                    btn.interactable = true;
                    
                    var img = btn.GetComponent<Image>();
                    if (img != null) img.raycastTarget = true;

                    var childTxts = btn.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                    foreach (var t in childTxts) t.raycastTarget = false;
                    
                    var childLegacyTxts = btn.GetComponentsInChildren<Text>(true);
                    foreach (var t in childLegacyTxts) t.raycastTarget = false;

                    btn.transform.SetAsLastSibling(); // 최전방으로 레이아웃 순서 정렬
                }
            }

            if (loginBtn != null)
            {
                loginBtn.onClick.RemoveAllListeners();
                loginBtn.onClick.AddListener(OnClick_Login);
            }
            if (privacyBtn != null)
            {
                privacyBtn.onClick.RemoveAllListeners();
                privacyBtn.onClick.AddListener(OpenPrivacyPolicy);
            }
            if (backBtn != null)
            {
                backBtn.onClick.RemoveAllListeners();
                backBtn.onClick.AddListener(CloseAndSave);
            }
            if (quitBtn != null)
            {
                quitBtn.onClick.RemoveAllListeners();
                quitBtn.onClick.AddListener(QuitGame);
            }
            if (mainMenuBtn != null)
            {
                mainMenuBtn.onClick.RemoveAllListeners();
                mainMenuBtn.onClick.AddListener(OnClick_MainMenu);
            }
            if (tutorialBtn != null)
            {
                tutorialBtn.onClick.RemoveAllListeners();
                tutorialBtn.onClick.AddListener(OnClick_Tutorial);
            }
        }

        private SettingState currentState = SettingState.Lobby;

        public void SetState(SettingState state)
        {
            currentState = state;
            ApplyState();
        }

        private void ApplyState()
        {
            switch (currentState)
            {
                case SettingState.Lobby:
                    if (mainMenuBtn != null) mainMenuBtn.gameObject.SetActive(false);
                    if (loginBtn != null) loginBtn.gameObject.SetActive(true);
                    break;
                case SettingState.InGame:
                    if (mainMenuBtn != null) mainMenuBtn.gameObject.SetActive(true);
                    if (loginBtn != null) loginBtn.gameObject.SetActive(false);
                    break;
            }
        }

        private void OnEnable()
        {
            // [UI-LAYER-OVERRIDE] 
            // 인게임의 LogView(Viewport) 등이 설정창 버튼 터치를 가로막는 레이아웃 겹침 문제를 완벽 해결하기 위해
            // 설정창 오브젝트의 Canvas Sort Order를 최상위(100)로 강제 설정하여 렌더링 및 레이캐스트 우선순위를 확보합니다.
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100; // 최상위 레이어 강제 지정
            }

            bool isGameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene";
            SetState(isGameScene ? SettingState.InGame : SettingState.Lobby);

            // [AUTH] 패널이 열릴 때마다 현재 로그인 상태를 반영
            RefreshLoginButton();
            AuthManager.OnAuthStateChanged += OnAuthStateChanged;
            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.OnLoginResult += OnLoginResult;
        }

        private void OnDisable()
        {
            AuthManager.OnAuthStateChanged -= OnAuthStateChanged;
            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.OnLoginResult -= OnLoginResult;
        }

        private void OnLoginResult(bool success, string uid)
        {
            if (!success)
            {
                ShowToast("구글 로그인에 실패했습니다.");
                return;
            }
            var state = GameManager.Instance?.Auth?.CurrentState;
            if (state == AuthState.LoggedIn)
                ShowToast("구글 로그인 성공!");
            else if (state == AuthState.Guest)
                ShowToast("게스트 로그인 성공!");
        }

        private void ShowToast(string message)
        {
            if (toastCanvasGroup == null) return;
            StopCoroutine("ToastCoroutine");
            StartCoroutine(ToastCoroutine(message));
        }

        private IEnumerator ToastCoroutine(string message)
        {
            if (toastText != null) toastText.text = message;
            toastCanvasGroup.alpha = 0f;
            toastCanvasGroup.gameObject.SetActive(true);

            float t = 0f;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; toastCanvasGroup.alpha = Mathf.Clamp01(t / 0.3f); yield return null; }
            toastCanvasGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(2f);

            t = 0f;
            while (t < 0.5f) { t += Time.unscaledDeltaTime; toastCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.5f); yield return null; }
            toastCanvasGroup.gameObject.SetActive(false);
        }

        [ContextMenu("Test Toast - 구글 로그인 성공")]
        private void TestToastGoogle() => ShowToast("구글 로그인 성공!");

        [ContextMenu("Test Toast - 게스트 로그인 성공")]
        private void TestToastGuest() => ShowToast("게스트 로그인 성공!");

        /// <summary>
        /// [AUTH] 인증 상태 변경 시 로그인 버튼 텍스트/상호작용 갱신
        /// </summary>
        private void OnAuthStateChanged(AuthState state) => RefreshLoginButton();

        private void RefreshLoginButton()
        {
            if (loginBtn == null || loginTxt == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Auth == null) return;

            var auth = GameManager.Instance.Auth;
            bool isGoogle = (auth.CurrentState == AuthState.LoggedIn);
            bool isGuest = (auth.CurrentState == AuthState.Guest);

            // [UI-FEEDBACK] 연동 상태에 따른 텍스트 및 색상 피드백 강화
            if (isGoogle)
            {
                loginTxt.text = "다른 구글 계정으로 변경";
                loginTxt.color = Color.white;
            }
            else if (isGuest)
            {
                loginTxt.text = "구글 계정으로 연동 (데이터 보존)";
                loginTxt.color = new Color(1f, 0.6f, 0f); // 주황색
            }
            else
            {
                loginTxt.text = "구글 로그인";
                loginTxt.color = new Color(1f, 0.6f, 0f);
            }

            loginBtn.interactable = true; 
        }

        private void Start()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null || GameManager.Instance.SaveData.Data == null) return;
            if (GameManager.Instance.Sound == null) return;

            var data = GameManager.Instance.SaveData.Data;

            if (masterSlider != null) masterSlider.value = data.masterVolume;
            if (bgmSlider != null) bgmSlider.value = data.bgmVolume;
            if (sfxSlider != null) sfxSlider.value = data.sfxVolume;

            masterSlider?.onValueChanged.RemoveAllListeners();
            bgmSlider?.onValueChanged.RemoveAllListeners();
            sfxSlider?.onValueChanged.RemoveAllListeners();

            masterSlider?.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider?.onValueChanged.AddListener(OnBGMChanged);
            sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
        }

        private void OnMasterChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.masterVolume = value;
                GameManager.Instance.Sound.SetMasterVolume(value);
            }
        }

        private void OnBGMChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.bgmVolume = value;
                GameManager.Instance.Sound.SetBGMVolume(value);
            }
        }

        private void OnSFXChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.sfxVolume = value;
                GameManager.Instance.Sound.SetSFXVolume(value);
            }
        }

        private void OnClick_Login()
        {
            if (GameManager.Instance == null || GameManager.Instance.Auth == null) return;
            
            var auth = GameManager.Instance.Auth;
            
            // [AUTH] 이미 구글 로그인 중이면 계정 변경, 아니면 연동 시도
            if (auth.CurrentState == AuthState.LoggedIn)
            {
                if (GameManager.Instance.Popup != null)
                {
                    GameManager.Instance.Popup.ShowConfirmPopup(
                        "다른 구글 계정으로 변경하시겠습니까?\n(기존 데이터가 있으면 불러옵니다)",
                        onConfirm: () => auth.SwitchAccount(),
                        onCancel: null,
                        confirmLabel: "변경",
                        cancelLabel: "취소"
                    );
                }
                else
                {
                    auth.SwitchAccount();
                }
            }
            else
            {
                if (GameManager.Instance.Popup != null)
                {
                    GameManager.Instance.Popup.ShowConfirmPopup(
                        "구글 계정으로 로그인하시겠습니까?\n기존 데이터가 있으면 불러오고,\n없으면 현재 진행 데이터가 저장됩니다.",
                        onConfirm: () => auth.LinkAccount(),
                        onCancel: null,
                        confirmLabel: "로그인",
                        cancelLabel: "취소"
                    );
                }
                else
                {
                    auth.LinkAccount();
                }
            }
        }

        public void OpenPrivacyPolicy()
        {
            // [ARCHITECT] 마스터가 제공한 최종 개인정보 처리방침 링크로 업데이트
            string url = "https://gist.github.com/taewon4261-cmd/a4af2e183162369226c3a8cb83245b07"; 
            Application.OpenURL(url);
            Debug.Log($"<color=cyan>[SettingUI]</color> Redirecting to Privacy Policy: {url}");
        }

        public void QuitGame()
        {
            if (GameManager.Instance != null && GameManager.Instance.Popup != null)
            {
                GameManager.Instance.Popup.ShowConfirmPopup(
                    "게임을 종료하시겠습니까?",
                    onConfirm: () =>
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    },
                    onCancel: null,
                    confirmLabel: "종료",
                    cancelLabel: "취소"
                );
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        public void CloseAndSave()
        {
            // Settings 사유 해소 및 저장은 애니메이션 전에 즉시 처리
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPause(Necromancer.PauseSource.Settings, false);
            }

            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                GameManager.Instance.SaveData.Save();
            }

            var anim = GetComponent<UIPanelAnim>();
            if (anim != null)
                anim.Hide();
            else
                gameObject.SetActive(false);
        }

        /// <summary>
        /// 설정창에서 '게임 팁' 버튼 클릭 시 튜토리얼 패널을 다시 표시합니다.
        /// 인게임(UIManager) / 타이틀(TitleUIController) 양쪽 모두 처리합니다.
        /// </summary>
        public void OnClick_Tutorial()
        {
            bool isGameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene";

            if (isGameScene)
            {
                GameManager.Instance?.uiManager?.ShowTutorial();
            }
            else
            {
                GameManager.Instance?.titleUI?.ShowTutorial();
            }
        }

        public void OnClick_MainMenu()
        {
            if (GameManager.Instance != null && GameManager.Instance.Popup != null)
            {
                GameManager.Instance.Popup.ShowConfirmPopup(
                    "진행 중이던 스테이지는\n패배 처리됩니다.",
                    onConfirm: ExecuteBackToMain,
                    onCancel: null,
                    confirmLabel: "확인",
                    cancelLabel: "취소"
                );
            }
            else
            {
                ExecuteBackToMain();
            }
        }

        private void ExecuteBackToMain()
        {
            Debug.Log("[SettingUI] ExecuteBackToMain() Triggered!");
            try
            {
                if (GameManager.Instance != null)
                {
                    Debug.Log("[SettingUI] Committing session soul and cleaning up game session...");
                    // [DATA-SAFETY] 나가기 전 현재까지 얻은 소울 강제 커밋 & 저장
                    if (GameManager.Instance.Resources != null)
                    {
                        GameManager.Instance.Resources.CommitSessionSoul();
                    }

                    // [CLEANUP] Wave/Unit/Pool/Sound 정리 및 timeScale 복원을 CleanupGameSession에 위임
                    GameManager.Instance.CleanupGameSession();
                    Debug.Log("[SettingUI] CleanupGameSession completed successfully.");
                }
                else
                {
                    Debug.LogWarning("[SettingUI] GameManager.Instance is NULL inside ExecuteBackToMain!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SettingUI] Exception during CleanupGameSession: {ex}");
            }

            Debug.Log("[SettingUI] Redirecting to TitleScene now...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
            Debug.Log("<color=green>[SettingUI]</color> Redirecting to TitleScene finished.");
        }

        private void Update()
        {
            // [UI-RAYCAST-DEBUGGER] 마우스 클릭/터치 시 현재 포인터 밑에 잡히는 최상위 UI 오브젝트 진단
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                    eventData.position = Input.mousePosition;
                    
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);
                    
                    if (results.Count > 0)
                    {
                        Debug.LogWarning($"<color=red>[UI-Raycast-Hit]</color> 최상위 터치 감지: <b>{results[0].gameObject.name}</b> (경로: {GetGameObjectPath(results[0].gameObject)})");
                        for (int i = 1; i < results.Count; i++)
                        {
                            Debug.Log($"[UI-Raycast-Behind] 겹쳐진 오브젝트 {i}: {results[i].gameObject.name}");
                        }
                    }
                }
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = obj.name + "/" + path;
            }
            return path;
        }
    }
}
