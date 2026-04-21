using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Necromancer.Core;
using Necromancer.Systems;
using UnityEngine.SceneManagement;

namespace Necromancer.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Prefab Management")]
        public GameObject inGameUIPrefab;
        private GameObject inGameUIInstance;

        [Header("In-Game HUD")]
        public Image expFillBar;
        public TextMeshProUGUI textTimer;
        public TextMeshProUGUI textWave;
        public TextMeshProUGUI textSoul;

        [Header("Level Up Panel")]
        public GameObject levelUpPanel;
        public Button[] skillCardButtons = new Button[3];
        public Image[] skillCardIcons = new Image[3];
        public TextMeshProUGUI[] skillCardNames = new TextMeshProUGUI[3];
        public TextMeshProUGUI[] skillCardDescriptions = new TextMeshProUGUI[3];

        [Header("Buttons")]
        public Button speedButton;
        public Button backToTitleButton;
        public Button rerollButton;
        public TextMeshProUGUI textSpeedToggle;

        [Header("Result UI")]
        public ResultUI resultUI;

        [Header("Global UI")]
        public SettingUI settingUI; // [NEW] 이제 ESC를 누르면 이 창이 뜹니다.

        [Header("Tutorial")]
        [SerializeField] private GameObject tutorialPanel;

        [Header("Screen Effects")]
        public CanvasGroup dangerOverlay;
        public float flashFrequency = 2.0f;
        public float maxAlpha = 0.4f;

        private List<SkillData> currentOptions;
        private PlayerController cachedPlayer;
        private bool freeRefreshUsed = false;

        private void Update()
        {
            HandleLowHPEffect();
            // [ARCHITECTURAL PURITY] ESC 입력 감지는 이제 GameManager에서 통합 관리합니다.
        }

        // [ARCHITECTURAL PURITY] 입력 감지는 GameManager로 이관되었습니다.

        public void ToggleSettings()
        {
            if (settingUI == null)
            {
                // [AUTO-FETCH] 인스펙터 누락 시 현재 씬에서 직접 시도 (Master's Directive)
                var foundUI = UnityEngine.Object.FindFirstObjectByType<SettingUI>(FindObjectsInactive.Include);
                if (foundUI != null) settingUI = foundUI;

                if (settingUI == null) 
                {
                    Debug.LogWarning("[UIManager] SettingUI Reference is MISSING! ESC logic cancelled.");
                    return;
                }
            }

            bool isOpening = !settingUI.gameObject.activeSelf;
            
            if (isOpening)
            {
                // 열기: 설정 사유로 일시정지
                GameManager.Instance.SetPause(PauseSource.Settings, true);
                settingUI.gameObject.SetActive(true);
                
                // [SOUND] 설정창 열기 효과음
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }

                Debug.Log("<color=cyan>[UIManager]</color> Settings Opened (Paused)");
            }
            else
            {
                // 닫기: 설정창 내부의 CloseAndSave를 통해 배속 복구 및 저장 진행
                settingUI.CloseAndSave();

                // [SOUND] 설정창 닫기 효과음
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }

                Debug.Log("<color=cyan>[UIManager]</color> Settings Closed (Resumed)");
            }
        }

        public void Init()
        {
            // [QA AUTO-FIX] Scene Guard - 타이틀 씬에서 불필요한 인게임 UI 생성 방지
            bool isGameScene = SceneManager.GetActiveScene().name == "GameScene";
            if (!isGameScene) return;

            DOTween.KillAll();
            cachedPlayer = null;

            // [ARCHITECTURAL PURITY] 자동 탐색/복구 로직 제거.
            // 인스턴스가 없다면 오직 지정된 프리팹으로만 생성합니다.
            if (inGameUIInstance == null && inGameUIPrefab != null)
            {
                inGameUIInstance = Instantiate(inGameUIPrefab, transform);
                inGameUIInstance.name = "[InGameUI_Root]";
            }

            if (inGameUIInstance != null)
            {
                // [PERFORMANCE] 플레이어 참조 캐싱 (매 프레임 GetComponent 방지)
                if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
                {
                    cachedPlayer = GameManager.Instance.playerTransform.GetComponent<PlayerController>();
                }

                // [DECOUPLING] 이름 기반 탐색 제거 -> InGameHUD 브릿지 컴포넌트 사용
                InGameHUD hud = inGameUIInstance.GetComponent<InGameHUD>();
                if (hud != null)
                {
                    expFillBar = hud.expFillBar;
                    textTimer = hud.textTimer;
                    textWave = hud.textWave;
                    textSoul = hud.textSoul;
                    speedButton = hud.speedButton;
                    backToTitleButton = hud.backToTitleButton;
                    dangerOverlay = hud.dangerOverlay;
                    levelUpPanel = hud.levelUpPanel;
                    resultUI = hud.resultUI;
                    rerollButton = hud.rerollButton;
                    textSpeedToggle = hud.textSpeedToggle;
                    settingUI = hud.settingUI; // [NEW] 브릿지 연결
                    if (settingUI != null) settingUI.gameObject.SetActive(false);
                    tutorialPanel = hud.tutorialPanel;
                    if (tutorialPanel != null) tutorialPanel.SetActive(false);
                    // [TUTORIAL] 닫기 버튼을 코드로 바인딩 (프리팹에서 씬 오브젝트 직접 참조 불가 문제 해결)
                    if (hud.tutorialCloseButton != null)
                    {
                        hud.tutorialCloseButton.onClick.RemoveAllListeners();
                        hud.tutorialCloseButton.onClick.AddListener(OnClick_CloseTutorial);
                    }

                    // [STABILITY] 설정창 버튼 바인딩 (HUD Button Binding)
                    if (hud.settingsButton != null)
                    {
                        hud.settingsButton.onClick.RemoveAllListeners();
                        hud.settingsButton.onClick.AddListener(ToggleSettings);
                        Debug.Log("<color=green>[UIManager]</color> Settings Button Binded successfully.");
                    }

                    // [STABILITY] 만약 프리팹 연결이 누락되었다면, 씬에서 자동으로 찾아봅니다.
                    if (settingUI == null)
                    {
                        settingUI = Object.FindFirstObjectByType<SettingUI>(FindObjectsInactive.Include);
                        if (settingUI != null) Debug.Log("<color=cyan>[UIManager]</color> SettingUI found automatically in scene.");
                    }

                    // 스킬 카드 UI 연결
                    // [ARCHITECT] InGameHUD의 배열 정합성 체크
                    if (hud.skillCardButtons != null && hud.skillCardIcons != null)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (i < hud.skillCardButtons.Length) skillCardButtons[i] = hud.skillCardButtons[i];
                            if (i < hud.skillCardIcons.Length) skillCardIcons[i] = hud.skillCardIcons[i];
                            if (i < hud.skillCardNames.Length) skillCardNames[i] = hud.skillCardNames[i];
                            if (i < hud.skillCardDescriptions.Length) skillCardDescriptions[i] = hud.skillCardDescriptions[i];
                            
                            // [STABILITY] 누락된 참조 사전에 경고
                            if (skillCardIcons[i] == null) Debug.LogWarning($"<color=yellow>[UIManager]</color> SkillCardIcon[{i}] is NULL in HUD prefab!");
                        }
                    }
                    else
                    {
                        Debug.LogError("<color=red>[UIManager]</color> HUD Prefab arrays (Buttons/Icons) are MISSING!");
                    }

                    Debug.Log("<color=green>[UIManager]</color> UI References Mapped via InGameHUD component.");
                }
                else
                {
                    Debug.LogError("<color=red>[UIManager]</color> InGameHUD component NOT FOUND on UI root! Please attach it to the prefab.");
                }

                // [UI SYNC] 초기 텍스트 및 바 설정
                if (textSpeedToggle != null) 
                {
                    float currentSpeed = (GameManager.Instance != null) ? GameManager.Instance.currentGameSpeed : 1.0f;
                    textSpeedToggle.SetText("x" + currentSpeed.ToString("F1"));
                }
                if (textTimer != null) textTimer.SetText("00:00");
                if (textWave != null) textWave.SetText("WAVE 1");
                if (textSoul != null) 
                {
                    // [BUG-FIX] 이전 판 데이터가 남는 'Ghost Value' 현상 방지를 위해 즉시 초기화
                    textSoul.text = " SOUL : 0"; 
                }
                
                // [PERFORMANCE] 초기 경험치 바 동기화
                if (GameManager.Instance != null)
                {
                    UpdateExpBar(GameManager.Instance.currentExp, GameManager.Instance.maxExp);
                }
                else
                {
                    if (expFillBar != null) expFillBar.fillAmount = 0f;
                }
            }
            else
            {
                Debug.LogWarning("<color=yellow>[UIManager]</color> In-Game UI Instance/Prefab NOT FOUND.");
            }

            // 버튼 리스너 재연결
            if (speedButton != null)
            {
                speedButton.onClick.RemoveAllListeners();
                speedButton.onClick.AddListener(() => {
                    if (GameManager.Instance != null) GameManager.Instance.ToggleGameSpeed();
                });
            }

            if (backToTitleButton != null)
            {
                backToTitleButton.onClick.RemoveAllListeners();
                backToTitleButton.onClick.AddListener(OnClick_BackToTitle);
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(OnClick_RerollWithAds);
            }

            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (resultUI != null) resultUI.gameObject.SetActive(false);

            // [TUTORIAL] 최초 실행 시 가이드 패널 노출
            CheckAndShowTutorial();
        }

        /// <summary>
        /// hasSeenTutorial이 false일 때만 패널을 열고 게임을 일시정지합니다.
        /// </summary>
        private void CheckAndShowTutorial()
        {
            var saveData = GameManager.Instance?.SaveData?.Data;
            if (saveData == null) return;
            if (saveData.hasSeenTutorial) return;

            ShowTutorial();
        }

        /// <summary>
        /// 튜토리얼 패널을 엽니다. SettingUI의 도움말 버튼에서도 호출합니다.
        /// </summary>
        public void ShowTutorial()
        {
            if (tutorialPanel == null)
            {
                Debug.LogWarning("[UIManager] tutorialPanel이 연결되지 않았습니다.");
                return;
            }

            tutorialPanel.SetActive(true);
            GameManager.Instance?.SetPause(PauseSource.Settings, true);
            Debug.Log("<color=cyan>[UIManager]</color> Tutorial panel opened.");
        }

        /// <summary>
        /// 튜토리얼 확인 버튼 클릭 시 호출. Inspector에서 버튼 OnClick에 연결하세요.
        /// </summary>
        public void OnClick_CloseTutorial()
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);

            // 최초 확인 시에만 플래그 저장
            var saveData = GameManager.Instance?.SaveData?.Data;
            if (saveData != null && !saveData.hasSeenTutorial)
            {
                saveData.hasSeenTutorial = true;
                GameManager.Instance.SaveData.Save();
                Debug.Log("<color=green>[UIManager]</color> Tutorial flag saved.");
            }

            GameManager.Instance?.SetPause(PauseSource.Settings, false);
            Debug.Log("<color=cyan>[UIManager]</color> Tutorial panel closed.");
        }

        public void Clear()
        {
            if (inGameUIInstance != null)
            {
                Destroy(inGameUIInstance);
                inGameUIInstance = null;
                expFillBar = null;
                textTimer = null;
                textWave = null;
                levelUpPanel = null;
                resultUI = null;
                dangerOverlay = null;
                cachedPlayer = null;
                Debug.Log("<color=orange>[UIManager]</color> In-Game UI Instance Cleared.");
            }
        }

        private void OnEnable()
        {
            LevelManager.OnExpChanged += UpdateExpBar;
            LevelManager.OnLevelUp += HandleLevelUp;
            GameManager.OnWaveStarted += HandleWaveStarted;
            GameManager.OnSessionSoulChanged += UpdateSoulUI; // 인게임 HUD는 세션 획득량만 표시
            GameManager.OnTimeUpdated += HandleTimeUpdated;
            GameManager.OnSpeedChanged += HandleSpeedChanged;
            GameManager.OnGameOver += ShowResultPanel;
        }

        private void OnDisable()
        {
            LevelManager.OnExpChanged -= UpdateExpBar;
            LevelManager.OnLevelUp -= HandleLevelUp;
            GameManager.OnWaveStarted -= HandleWaveStarted;
            GameManager.OnSessionSoulChanged -= UpdateSoulUI;
            GameManager.OnTimeUpdated -= HandleTimeUpdated;
            GameManager.OnSpeedChanged -= HandleSpeedChanged;
            GameManager.OnGameOver -= ShowResultPanel;
        }

        private void HandleLowHPEffect()
        {
            if (dangerOverlay == null || cachedPlayer == null) return;
            
            if (cachedPlayer.IsDead)
            {
                dangerOverlay.alpha = 0f;
                return;
            }

            float hpRatio = cachedPlayer.currentHp / cachedPlayer.maxHp;
            if (hpRatio <= 0.3f)
            {
                float lerp = (Mathf.Sin(Time.time * flashFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
                dangerOverlay.alpha = lerp * maxAlpha;
            }
            else dangerOverlay.alpha = 0f;
        }

        public void HandleSpeedChanged(float speed)
        {
            if (textSpeedToggle != null) textSpeedToggle.SetText("x" + speed.ToString("F1"));
        }

        public void UpdateExpBar(float currentExp, float maxExp)
        {
            if (expFillBar != null && maxExp > 0) expFillBar.fillAmount = Mathf.Clamp01(currentExp / maxExp);
        }

        private void HandleLevelUp(List<SkillData> options)
        {
            if (levelUpPanel != null) levelUpPanel.SetActive(true);
            freeRefreshUsed = false;
            UpdateRefreshButtonText();
            // [NOTE] LevelUp 정지는 GameManager.AddExp에서 SetPause(LevelUp, true)로 이미 처리됨
            RefreshSkillCards(options);
        }

        private void UpdateRefreshButtonText()
        {
            if (rerollButton == null) return;
            var label = rerollButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null) return;
            label.text = freeRefreshUsed ? "광고 보고 새로고침" : "새로고침";
        }

        private void HandleTimeUpdated(float time)
        {
            if (textTimer != null)
            {
                int min = Mathf.FloorToInt(time / 60f);
                int sec = Mathf.FloorToInt(time % 60f);
                textTimer.SetText("{0:00}:{1:00}", min, sec);
            }
        }

        private void HandleWaveStarted(int index, int total, string waveName)
        {
            if (textWave != null) textWave.SetText($"WAVE {index + 1} / {total}");
        }

        private void UpdateSoulUI(int amount)
        {
            if (textSoul != null)
            {
                string result = string.Format(" SOUL : {0:N0}", amount);
                textSoul.text = result;
                Debug.Log($"<color=cyan>[QA Check]</color> Soul UI Updated to: {result}");
            }
        }

        public void RefreshSkillCards(List<SkillData> newOptions)
        {
            if (newOptions == null) return;
            currentOptions = newOptions;

            for (int i = 0; i < 3; i++)
            {
                if (skillCardButtons == null || i >= skillCardButtons.Length || skillCardButtons[i] == null) continue;

                if (i < currentOptions.Count)
                {
                    int index = i;
                    skillCardButtons[i].gameObject.SetActive(true);
                    
                    // [STABILITY] 아이콘 누락 시 투명화 방지 및 로그 출력
                    if (skillCardIcons[i] != null)
                    {
                        if (currentOptions[i].skillIcon != null)
                        {
                            skillCardIcons[i].sprite = currentOptions[i].skillIcon;
                            skillCardIcons[i].color = Color.white;
                        }
                        else
                        {
                            Debug.LogWarning($"<color=yellow>[UIManager]</color> Skill '{currentOptions[i].skillName}' (index {i}) has NO ICON assigned!");
                            // 아이콘이 없으면 기본적으로 렌더링되지 않거나 투명해질 수 있으므로, 색상 조절로 피드백 제공
                            skillCardIcons[i].sprite = null; 
                            skillCardIcons[i].color = new Color(1, 1, 1, 0.2f); 
                        }
                    }

                    if (skillCardNames[i] != null) skillCardNames[i].SetText(currentOptions[i].skillName);
                    if (skillCardDescriptions[i] != null) skillCardDescriptions[i].SetText(currentOptions[i].skillDescription);

                    skillCardButtons[i].onClick.RemoveAllListeners();
                    skillCardButtons[i].onClick.AddListener(() => OnClick_SelectSkill(index));
                }
                else
                {
                    skillCardButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnClick_SelectSkill(int index)
        {
            if (currentOptions == null || index >= currentOptions.Count) return;

            SkillData selectedSkill = currentOptions[index];

            if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
            {
                GameManager.Instance.skillManager.LearnSkill(selectedSkill);
            }

            // [SOUND] 스킬 선택 효과음
            if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
            }

            if (levelUpPanel != null) levelUpPanel.SetActive(false);

            // 스킬 선택 완료 → LevelUp 정지 사유 해소
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPause(PauseSource.LevelUp, false);
            }
        }

        public void ShowResultPanel(bool isVictory)
        {
            if (resultUI == null) return;
            
            int souls = (GameManager.Instance != null && GameManager.Instance.Resources != null) ? 
                GameManager.Instance.Resources.currentSessionSoul : 0;

            // [STABILITY] 결과창 출력 즉시 세계 정지
            resultUI.Open(isVictory, souls);
            GameManager.Instance.SetPause(PauseSource.GameOver, true);

            // [SOUND] 승리/패배 효과음 재생
            if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                var sfx = isVictory ? GameManager.Instance.Sound.sfxWin : GameManager.Instance.Sound.sfxLose;
                GameManager.Instance.Sound.PlaySFX(sfx);
            }
        }

        public void OnClick_BackToTitle()
        {
            // [CLEANUP] 이전 세션의 모든 논리/물리/사운드 강제 종료
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CleanupGameSession();
            }

            // [SOUND] 버튼 클릭 효과음 (클린업 이후에 들리도록 Resume 후 재생)
            if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                GameManager.Instance.Sound.ResumeSFX();
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
            }

            SceneManager.LoadScene("TitleScene");
        }

        /// <summary>
        /// 스킬 새로고침 (1회 무료, 이후 광고 시청)
        /// </summary>
        public void OnClick_RerollWithAds()
        {
            if (GameManager.Instance == null || GameManager.Instance.skillManager == null) return;

            if (GameManager.Instance.Sound != null) GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);

            if (!freeRefreshUsed)
            {
                freeRefreshUsed = true;
                UpdateRefreshButtonText();
                ExecuteReroll();
                return;
            }

            // 무료 횟수 소진 시 보상형 광고 호출
            if (GameManager.Instance.AdManager != null)
            {
                GameManager.Instance.AdManager.ShowRewardedAd(
                    AdManager.AdUnitType.SkillRefresh,
                    () => ExecuteReroll(),
                    () => ShowAdErrorPopup("광고를 불러올 수 없습니다.\n잠시 후 다시 시도해주세요.")
                );
            }
        }

        private void ExecuteReroll()
        {
            if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
            {
                var newOptions = GameManager.Instance.skillManager.GetRandomSkillsForLevelUp(3);
                RefreshSkillCards(newOptions);
            }
        }

        private void ShowAdErrorPopup(string message)
        {
            Debug.LogWarning($"<color=red>[UIManager]</color> AD ERROR: {message}");
            GameManager.Instance?.Popup?.ShowMessagePopup(message);
        }



    }
}
