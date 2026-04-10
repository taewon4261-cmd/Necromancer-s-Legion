using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Data;
using Necromancer.Core;
using DG.Tweening;

namespace Necromancer.UI
{
    /// <summary>
    /// [UI] 미니언 해금 슬롯 (상점 패널 방식)
    /// UpgradeItemUI와 동일한 피드백 및 로직 흐름을 따릅니다.
    /// </summary>
    public class MinionUnlockSlot : MonoBehaviour
    {
        [Header("UI References (Inspector Bind)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image borderImage; 
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI soulCostText;
        
        [Header("Buttons & Status")]
        [SerializeField] private Button unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonText; 
        [SerializeField] private GameObject alertObject;

        private MinionAltarUI owner;
        private Color originalBorderColor = Color.white;
        private Tween shakeTween;

        public MinionUnlockSO Data { get; private set; }

        private void Awake()
        {
            if (borderImage != null) originalBorderColor = borderImage.color;
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Setup(MinionUnlockSO minionData, MinionAltarUI uiOwner)
        {
            this.Data = minionData;
            this.owner = uiOwner;
            if (Data == null) return;

            // 기본 정보 세팅
            if (nameText != null) nameText.text = Data.minionName;
            if (descriptionText != null) descriptionText.text = Data.description;
            if (iconImage != null) iconImage.sprite = Data.minionIcon;
            
            if (bgImage != null)
            {
                bgImage.color = Data.tier switch
                {
                    MinionTier.Bronze => new Color(0.6f, 0.4f, 0.2f),
                    MinionTier.Silver => new Color(0.75f, 0.75f, 0.75f),
                    MinionTier.Gold   => new Color(1.0f, 0.84f, 0f),
                    _                 => Color.white
                };
            }

            // 버튼 리스너 등록
            if (unlockButton != null)
            {
                unlockButton.onClick.RemoveAllListeners();
                unlockButton.onClick.AddListener(OnClickUnlock);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (Data == null || GameManager.Instance == null) return;

            var res = GameManager.Instance.Resources;
            bool isUnlocked = res.IsMinionUnlocked(Data.minionID);
            int currentEssence = res.GetEssenceCount(Data.targetEnemyID);
            int currentSoul = res.currentSoul;

            bool enoughEssence = currentEssence >= Data.unlockCost_Essence;
            bool enoughSoul = currentSoul >= Data.unlockCost_Soul;

            // 1. 아이콘 및 이름 색상 (미해금 시에도 형태가 보이도록 색상값 상향 조정)
            if (iconImage != null) iconImage.color = isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);
            if (nameText != null) nameText.color = isUnlocked ? Color.white : Color.gray;

            // 2. 정수 진행도 표시 (해금 완료 시 텍스트 제거)
            if (progressText != null)
            {
                progressText.text = isUnlocked ? "" : $"정수 : {currentEssence} / {Data.unlockCost_Essence}";
                progressText.color = enoughEssence ? Color.green : Color.white;
            }

            // 3. 소울 비용 표시
            if (soulCostText != null)
            {
                soulCostText.text = isUnlocked ? "" : $"{Data.unlockCost_Soul} SOUL";
                soulCostText.color = enoughSoul ? Color.white : Color.red;
            }

            // 4. 버튼 텍스트 및 상태 제어
            if (unlockButtonText != null)
            {
                unlockButtonText.text = isUnlocked ? "해금 완료" : "해금";
            }

            if (unlockButton != null)
            {
                unlockButton.interactable = !isUnlocked;
                // 소울/정수 부족 시 시각적 표시 (UpgradeItemUI 패턴 적용)
                var img = unlockButton.GetComponent<Image>();
                if (img != null && !isUnlocked)
                {
                    img.color = (enoughEssence && enoughSoul) ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            }

            // 5. 알림 뱃지
            if (alertObject != null) alertObject.SetActive(!isUnlocked && enoughEssence && enoughSoul);
        }

        private void OnClickUnlock()
        {
            if (Data == null || GameManager.Instance == null || owner == null) return;

            var res = GameManager.Instance.Resources;
            bool isUnlocked = res.IsMinionUnlocked(Data.minionID);
            if (isUnlocked) return;

            int currentEssence = res.GetEssenceCount(Data.targetEnemyID);
            int currentSoul = res.currentSoul;

            // [CHECK] 조건 미달 시 실패 연출
            if (currentEssence < Data.unlockCost_Essence || currentSoul < Data.unlockCost_Soul)
            {
                PlayFailFeedback();
                return;
            }

            // [ACTION] 실제 해금 시도
            if (res.TryUnlockMinion(Data))
            {
                if (GameManager.Instance.Sound != null)
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxUpgrade);

                // [FEEDBACK] 해금 성공 펀치 스케일 (UpgradeItemUI와 동일)
                transform.DOPunchScale(Vector3.one * 0.05f, 0.2f);
                
                Refresh();
                owner.RefreshAllSlots(); // [CONSISTENCY] SendMessageUpwards 대신 명시적 호출
            }
        }

        private void PlayFailFeedback()
        {
            if (GameManager.Instance.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxFailBtn);

            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();
            transform.DOComplete();
            shakeTween = transform.DOShakePosition(0.4f, 10f, 20);

            if (borderImage != null)
            {
                borderImage.DOKill();
                borderImage.color = Color.red;
                borderImage.DOColor(originalBorderColor, 0.5f);
            }
        }
    }
}
