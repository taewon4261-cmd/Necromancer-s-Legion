using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using Necromancer.Core;
using Necromancer.Systems;

namespace Necromancer.UI
{
    /// <summary>
    /// 로비에서 유저의 프로필 확인, 닉네임 변경, 계정 연동 및 로그아웃을 지원하는 팝업 컨트롤러입니다.
    /// </summary>
    public class ProfilePopup : MonoBehaviour
    {
        [Header("UI References - Info")]
        [SerializeField] private TextMeshProUGUI tmpCurrentNickname;
        [SerializeField] private TextMeshProUGUI tmpAccountType;

        [Header("UI References - Nickname Edit")]
        [SerializeField] private InputField inputNickname;
        [SerializeField] private Button btnChangeNickname;

        [Header("UI References - Account Actions")]
        [SerializeField] private Button btnLinkAccount;
        [SerializeField] private Button btnSignOut;
        [SerializeField] private Button btnClose;

        private bool isInitialized = false;

        private void InitializeIfNeeded()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 1. [UI CLEANUP] 설정 창 복제본에서 불필요한 슬라이더/기타 항목들 싹 다 숨기기
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                string childName = child.name;
                if (childName != "Setting Image" && !childName.Contains("Close") && !childName.Contains("Back"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 2. 타이틀 텍스트를 "프로필"로 동적 변경
            var titleText = transform.Find("Setting Image/Text_Title_Setting")?.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = "프로필";
            }

            // 3. [DYNAMIC UI CREATION] 닉네임 입력을 위한 인풋필드 동적 생성 및 배치
            if (inputNickname == null)
            {
                var inputGo = DefaultControls.CreateInputField(new DefaultControls.Resources());
                inputGo.name = "InputField_Nickname";
                inputGo.transform.SetParent(this.transform, false);
                
                var rect = inputGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(400, 60);
                rect.anchoredPosition = new Vector2(0, 80); // 팝업 중앙 약간 위쪽

                // 인풋필드 내부 텍스트 폰트 및 스타일 조절
                var inputText = inputGo.transform.Find("Text")?.GetComponent<Text>();
                if (inputText != null)
                {
                    inputText.fontSize = 24;
                    inputText.color = Color.black;
                }
                var placeholderText = inputGo.transform.Find("Placeholder")?.GetComponent<Text>();
                if (placeholderText != null)
                {
                    placeholderText.text = "닉네임 입력...";
                    placeholderText.fontSize = 24;
                    placeholderText.fontStyle = FontStyle.Italic;
                }

                inputNickname = inputGo.GetComponent<InputField>();
            }

            // 4. [AUTO BINDING & TEXT REPLACEMENT] 기존 버튼들을 프로필용 액션 버튼으로 부활시키기
            var buttons = GetComponentsInChildren<Button>(true);
            
            // 1순위: 정확한 고유 이름 매칭
            foreach (var btn in buttons)
            {
                string btnName = btn.name;
                if (btnName == "Btn_Close")
                {
                    btnClose = btn;
                    btn.gameObject.SetActive(true);
                }
            }

            // 2순위: 닉네임 변경 버튼 매핑 및 닫기가 아닌 나머지 설정용 잔재 버튼들은 싹 숨김
            foreach (var btn in buttons)
            {
                string btnName = btn.name;
                string btnNameLower = btnName.ToLower();

                if (btn == btnClose) continue;

                // 닉네임 변경 버튼으로 재활용 (기존 설정 버튼 중 하나 활용)
                if (btnChangeNickname == null && (btnNameLower.Contains("privacy") || btnNameLower.Contains("tutorial") || btnNameLower.Contains("quit") || btnNameLower.Contains("setting")))
                {
                    btnChangeNickname = btn;
                    btn.gameObject.SetActive(true);
                    
                    var rect = btn.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchorMin = new Vector2(0.5f, 0.5f);
                        rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.pivot = new Vector2(0.5f, 0.5f);
                        rect.sizeDelta = new Vector2(300, 80);
                        rect.anchoredPosition = new Vector2(0, -50); // 정중앙 약간 아래에 배치
                    }

                    var btnTxt = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (btnTxt != null) btnTxt.text = "닉네임 변경";
                }
                else
                {
                    // 닉네임 변경 및 X 닫기 버튼을 제외한 모든 설정 잔재 버튼(메인으로 등)을 완벽히 꺼줌
                    btn.gameObject.SetActive(false);
                }
            }

            // 5. 텍스트 라벨 생성 및 매핑 (현재 닉네임 및 로그인 수단 표기용)
            if (tmpCurrentNickname == null)
            {
                var txtGo = new GameObject("Text_CurrentNickname", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(this.transform, false);
                
                var rect = txtGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(600, 50);
                rect.anchoredPosition = new Vector2(0, 200); // 최상단 영역

                tmpCurrentNickname = txtGo.GetComponent<TextMeshProUGUI>();
                tmpCurrentNickname.fontSize = 32;
                tmpCurrentNickname.alignment = TextAlignmentOptions.Center;
                tmpCurrentNickname.color = Color.white;
                if (titleText != null) tmpCurrentNickname.font = titleText.font;
            }

            if (tmpAccountType == null)
            {
                var txtGo = new GameObject("Text_AccountType", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(this.transform, false);
                
                var rect = txtGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(600, 50);
                rect.anchoredPosition = new Vector2(0, 150); // 닉네임 바로 밑

                tmpAccountType = txtGo.GetComponent<TextMeshProUGUI>();
                tmpAccountType.fontSize = 26;
                tmpAccountType.alignment = TextAlignmentOptions.Center;
                tmpAccountType.color = Color.gray;
                if (titleText != null) tmpAccountType.font = titleText.font;
            }
        }

        private void OnEnable()
        {
            Debug.Log("[ProfilePopup] OnEnable() Called!");
            InitializeIfNeeded();
            RefreshUI();
            
            if (btnChangeNickname != null)
            {
                btnChangeNickname.onClick.RemoveAllListeners();
                btnChangeNickname.onClick.AddListener(OnClick_ChangeNickname);
                Debug.Log("[ProfilePopup] btnChangeNickname listener registered.");
            }
            if (btnLinkAccount != null)
            {
                btnLinkAccount.onClick.RemoveAllListeners();
                btnLinkAccount.onClick.AddListener(OnClick_LinkAccount);
            }
            if (btnSignOut != null)
            {
                btnSignOut.onClick.RemoveAllListeners();
                btnSignOut.onClick.AddListener(OnClick_SignOut);
            }
            if (btnClose != null)
            {
                // [LOBBY TRANSITION CONTROL] TitleUIController의 BackToMainMenu 연동을 유지하기 위해
                // OnEnable 런타임에 리스너를 강제 초기화하여 덮어쓰는 것을 배제합니다.
                Debug.Log($"[ProfilePopup] btnClose ({btnClose.name}) listener is managed by TitleUIController.");
            }
        }

        private void OnDisable()
        {
            if (btnChangeNickname != null) btnChangeNickname.onClick.RemoveListener(OnClick_ChangeNickname);
            if (btnLinkAccount != null) btnLinkAccount.onClick.RemoveListener(OnClick_LinkAccount);
            if (btnSignOut != null) btnSignOut.onClick.RemoveListener(OnClick_SignOut);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        public void Close()
        {
            Debug.Log("[ProfilePopup] Close() Method Triggered!");
            PlayButtonSound();
            gameObject.SetActive(false);
            Debug.Log($"[ProfilePopup] gameObject.activeSelf is now: {gameObject.activeSelf}");
        }

        private void RefreshUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null || GameManager.Instance.SaveData.Data == null)
            {
                Debug.LogError("[ProfilePopup] GameManager or SaveData is NULL.");
                return;
            }

            var data = GameManager.Instance.SaveData.Data;
            
            // 1. 닉네임 표시
            if (tmpCurrentNickname != null)
                tmpCurrentNickname.text = $"현재 닉네임: <color=yellow>{data.nickname}</color>";
            
            if (inputNickname != null)
                inputNickname.text = data.nickname;

            // 2. 로그인 수단 및 연동 버튼 활성화 처리
            string loginMethod = data.lastLoginMethod;
            if (tmpAccountType != null)
            {
                if (loginMethod == "Google")
                    tmpAccountType.text = "로그인 방식: <color=green>Google 계정</color>";
                else if (loginMethod == "Guest")
                    tmpAccountType.text = "로그인 방식: <color=yellow>게스트 계정</color>";
                else
                    tmpAccountType.text = "로그인 방식: 알 수 없음";
            }

            if (btnLinkAccount != null)
            {
                btnLinkAccount.gameObject.SetActive(loginMethod == "Guest");
            }
        }

        private void OnClick_ChangeNickname()
        {
            PlayButtonSound();

            if (inputNickname == null) return;

            string newNickname = inputNickname.text.Trim();

            // 1. 글자 수 제한 검증 (2자 이상 8자 이하)
            if (newNickname.Length < 2 || newNickname.Length > 8)
            {
                ShowMessage("닉네임은 2자 이상 8자 이하로 설정해 주세요.");
                return;
            }

            // 2. 문자 조합 검증 (한글, 영문, 숫자만 가능 - 특수문자 및 공백 금지)
            if (!Regex.IsMatch(newNickname, @"^[a-zA-Z0-9가-힣]+$"))
            {
                ShowMessage("닉네임에 특수문자나 공백은 포함될 수 없습니다.");
                return;
            }

            // 3. 기존 닉네임과 동일 여부 검증
            var saveData = GameManager.Instance.SaveData;
            if (saveData.Data.nickname == newNickname)
            {
                ShowMessage("현재 설정된 닉네임과 동일합니다.");
                return;
            }

            // 4. 저장 및 클라우드 업로드
            saveData.Data.nickname = newNickname;
            saveData.Save();

            ShowMessage("닉네임이 성공적으로 변경되었습니다.");
            RefreshUI();
            
            if (GameManager.Instance.titleUI != null)
            {
                GameManager.Instance.titleUI.SetupInitialUI();
            }
        }

        private void OnClick_LinkAccount()
        {
            PlayButtonSound();
            if (GameManager.Instance != null && GameManager.Instance.Auth != null)
            {
                Debug.Log("[ProfilePopup] Account link requested.");
                GameManager.Instance.Auth.LinkAccount();
                Close();
            }
        }

        private void OnClick_SignOut()
        {
            PlayButtonSound();
            
            if (GameManager.Instance != null && GameManager.Instance.Popup != null)
            {
                GameManager.Instance.Popup.ShowConfirmPopup(
                    "로그아웃하시겠습니까?\n타이틀 화면으로 이동합니다.",
                    onConfirm: () =>
                    {
                        if (GameManager.Instance.Auth != null)
                        {
                            GameManager.Instance.Auth.SignOut();
                            Close();
                        }
                    },
                    onCancel: null,
                    confirmLabel: "로그아웃",
                    cancelLabel: "취소"
                );
            }
        }

        private void ShowMessage(string message)
        {
            if (GameManager.Instance != null && GameManager.Instance.Popup != null)
            {
                GameManager.Instance.Popup.ShowMessagePopup(message);
            }
            else
            {
                Debug.LogWarning($"[ProfilePopup] Message: {message}");
            }
        }

        private void PlayButtonSound()
        {
            if (GameManager.Instance != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn, SfxPriority.Critical);
            }
        }
    }
}
