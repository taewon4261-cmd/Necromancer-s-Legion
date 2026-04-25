using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// 스테이지 선택 패널의 UI와 선택 로직을 담당합니다.
    /// </summary>
    public class StageSelectUI : MonoBehaviour
    {
        [Header("Stage Info Display")]
        public TextMeshProUGUI stageNameText;
        public TextMeshProUGUI stageDescText;
        public Image stageThumbnail;
        public Button startButton;
        public Button backButton;

        [Header("Stamina UI")]
        public TextMeshProUGUI staminaText;
        public Button btnAddStamina;

        [Header("Navigations")]
        public Button prevButton;
        public Button nextButton;
        public Button closeOverlayButton; // 이제 쓸모없지만 필드는 비워둡니다.

        [Header("Stage Collection")]
        // [ARCH] Resources.LoadAll 방식에서 인스펙터 직렬화 방식으로 변경 (QA 지시사항)
        [SerializeField] private List<StageDataSO> stageList = new List<StageDataSO>();

        [Header("Current Selection")]
        public StageDataSO selectedStage;
        private int currentIndex = 0;

        private float _staminaCheckTimer = 0f;
        private float _adCheckTimer = 0f;
        private const float STAMINA_CHECK_INTERVAL = 1f;
        private const float AD_CHECK_INTERVAL = 2f;

        private void OnEnable()
        {
            if (stageList == null || stageList.Count == 0) InitStageList();
            if (stageList != null && stageList.Count > 0)
                SelectStage(stageList[currentIndex]);

            // 패널이 열리는 시점에 즉시 한 번 갱신 (앱 복귀 시 오프라인 회복 반영)
            if (GameManager.Instance?.Resources != null)
            {
                GameManager.Instance.Resources.UpdateStamina();
                UpdateStaminaUI();
            }
            _staminaCheckTimer = 0f;
            _adCheckTimer = 0f;
        }

        private void Start()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
            if (btnAddStamina != null) btnAddStamina.onClick.AddListener(OnAddStaminaClicked);

            if (prevButton != null) prevButton.onClick.AddListener(MovePrev);
            if (nextButton != null) nextButton.onClick.AddListener(MoveNext);

            InitStageList();

            if (stageList != null && stageList.Count > 0)
            {
                currentIndex = 0;
                SelectStage(stageList[currentIndex]);
            }
        }

        private void Update()
        {
            if (GameManager.Instance?.Resources == null) return;

            // 1초마다 UTC 기반 피로도 재계산 (매 프레임 DateTime/디스크 연산 방지)
            _staminaCheckTimer += Time.deltaTime;
            if (_staminaCheckTimer >= STAMINA_CHECK_INTERVAL)
            {
                _staminaCheckTimer = 0f;
                GameManager.Instance.Resources.UpdateStamina();
                UpdateStaminaUI();
            }

            // 2초마다 광고 준비 상태 확인 (SDK 매 프레임 호출 방지)
            _adCheckTimer += Time.deltaTime;
            if (_adCheckTimer >= AD_CHECK_INTERVAL)
            {
                _adCheckTimer = 0f;
                if (btnAddStamina != null && GameManager.Instance.AdManager != null)
                    btnAddStamina.interactable = GameManager.Instance.AdManager.IsAdReady(Systems.AdManager.AdUnitType.Stamina);
            }
        }

        private void UpdateStaminaUI()
        {
            if (staminaText == null) return;

            var res = GameManager.Instance.Resources;
            int cur = res.currentStamina;
            int max = ResourceManager.MAX_STAMINA;

            staminaText.text = $" {cur}/{max}";
        }

        private void OnAddStaminaClicked()
        {
            if (GameManager.Instance?.AdManager == null || GameManager.Instance?.Popup == null) return;

            // [SOUND] 버튼 클릭음
            if (GameManager.Instance.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);

            var res = GameManager.Instance.Resources;
            int adCur = res.staminaAdsWatchedToday;
            int adMax = ResourceManager.MAX_DAILY_STAMINA_ADS;

            // 오늘 광고 시청 횟수 제한 확인
            if (adCur >= adMax)
            {
                GameManager.Instance.Popup.ShowMessagePopup("오늘은 더 이상 광고를 볼 수 없습니다.");
                return;
            }

            // [UX] 팝업 메시지에 오늘 시청 횟수 표시 (현재/전체)
            string message = $"광고를 시청하고\n피로도를 1 회복하시겠습니까?\n\n(오늘 시청 횟수: {adCur}/{adMax})";

            GameManager.Instance.Popup.ShowConfirmPopup(
                message,
                () => {
                    // 확인(광고 시청)
                    GameManager.Instance.AdManager.ShowRewardedAd(
                        Necromancer.Systems.AdManager.AdUnitType.Stamina,
                        () => {
                            // 광고 성공: 피로도 1 회복
                            GameManager.Instance.Resources.AddStamina(1);
                            UpdateStaminaUI(); // 즉시 UI 갱신 (피로도 숫자 반영)
                            Debug.Log("<color=green>[StageSelectUI]</color> Stamina rewarded via Ad.");
                        },
                        () => Debug.LogWarning("[StageSelectUI] Stamina Ad failed or canceled.")
                    );
                },
                null,
                "시청하기", "취소"
            );
        }


        public void MoveNext()
        {
            if (currentIndex < stageList.Count - 1)
            {
                // 현재 스테이지가 잠겨있다면 더 이상 뒤로(오른쪽으로) 이동 불가
                bool isCurrentUnlocked = GameManager.Instance.Resources.IsStageUnlocked(stageList[currentIndex].stageID);
                if (!isCurrentUnlocked)
                {
                    // [SOUND] 잠겨서 이동 불가 효과음
                    if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                        GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxFailBtn);
                    }

                    // 진동 피드백 (잠금 영역을 더 파고들려고 시도할 때)
#if UNITY_ANDROID || UNITY_IOS
                    Handheld.Vibrate();
#endif
                    return;
                }

                currentIndex++;
                SelectStage(stageList[currentIndex]);

                // [SOUND] 스테이지 이동 효과음 추가
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }
            }
        }


        public void MovePrev()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                SelectStage(stageList[currentIndex]);

                // [SOUND] 스테이지 이동 효과음 추가
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }
            }
        }

        private void InitStageList()
        {
            // [ARCH] Resources.LoadAll 로직 삭제. 
            // 이제 에디터 인스펙터에서 직접 Drag & Drop으로 데이터를 할당해야 합니다.
            if (stageList != null && stageList.Count > 0)
            {
                stageList.Sort((a, b) => a.stageID.CompareTo(b.stageID));
            }
        }

        public void SelectStage(StageDataSO stage)
        {
            selectedStage = stage;
            UpdateStageDisplay(stage);
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            if (prevButton != null) prevButton.interactable = currentIndex > 0;
            if (nextButton != null) nextButton.interactable = currentIndex < stageList.Count - 1;
        }


        private void UpdateStageDisplay(StageDataSO stage)
        {
            if (stage == null) return;

            // 1. 잠금 여부 확인
            bool isUnlocked = GameManager.Instance.Resources.IsStageUnlocked(stage.stageID);

            // 2. 기본 정보 표시
            if (stageNameText != null) stageNameText.text = isUnlocked ? stage.stageName : "???";
            if (stageDescText != null) stageDescText.text = isUnlocked ? stage.stageDescription : "이전 스테이지를 클리어해야 합니다.";
            if (stageThumbnail != null) 
            {
                stageThumbnail.sprite = stage.stageThumbnail;
                stageThumbnail.color = isUnlocked ? Color.white : Color.black; // 잠기면 검게
            }
            
            // 3. 잠금 UI 처리
            if (startButton != null) 
            {
                // [STABILITY] 소리 피드백을 위해 버튼은 항상 켜둡니다! 
                // 대신 잠긴 스테이지일 경우 버튼의 색깔을 어둡게 하거나 텍스트를 바꿉니다.
                startButton.interactable = true; 
                var img = startButton.GetComponent<Image>();
                if (img != null) img.color = isUnlocked ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                
                var btnText = startButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = isUnlocked ? "게임 시작" : "잠김";
            }

            if (!isUnlocked)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
                Debug.Log("<color=red>[StageSelectUI]</color> Locked Stage Selected! Vibrate!");
            }
        }

        private void OnStartButtonClicked()
        {
            if (selectedStage == null) return;

            bool isUnlocked = GameManager.Instance.Resources.IsStageUnlocked(selectedStage.stageID);
            if (!isUnlocked)
            {
                if (GameManager.Instance?.Sound != null)
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxFailBtn);
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
                return;
            }

            // [STAMINA] 피로도 부족 시 입장 차단
            if (!GameManager.Instance.Resources.HasEnoughStamina(ResourceManager.STAMINA_COST))
            {
                if (GameManager.Instance?.Sound != null)
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxFailBtn);

                GameManager.Instance?.Popup?.ShowMessagePopup("피로도가 부족합니다.\n30분마다 1씩 회복됩니다.");
                return;
            }

            GameManager.Instance.Resources.ConsumeStamina(ResourceManager.STAMINA_COST);
            GameManager.Instance.SaveData.Save();

            Debug.Log($"<color=cyan>[StageSelectUI]</color> Starting Game with Stage: {selectedStage.stageName}");
            
            GameManager.Instance.Combat.SetupStageModifiers(selectedStage);
            GameManager.Instance.StartGame(selectedStage);
        }
    }
}
