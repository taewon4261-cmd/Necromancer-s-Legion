using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TMPro;
using Cysharp.Threading.Tasks;
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

        [Header("Promotion UI (Inspector Bind)")]
        [SerializeField] private Image[] starImages;
        [SerializeField] private Button promoteButton;
        [SerializeField] private TextMeshProUGUI promoteCostText;
        [SerializeField] private GameObject star3SkillBadge;
        [SerializeField] private GameObject star5SkillBadge;

        [Header("Unique Skill UI (Inspector Bind)")]
        [SerializeField] private MinionSkillInfoPopup skillInfoPopup;
        [SerializeField] private Button skill3IconButton;
        [SerializeField] private Button skill5IconButton;
        [SerializeField] private Image skill3IconImage;
        [SerializeField] private Image skill5IconImage;

        [Header("Promotion Border Colors")]
        [SerializeField] private Color bronzeBorderColor = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] private Color silverBorderColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color goldBorderColor = new Color(1.0f, 0.84f, 0f);
        [SerializeField] private Color activeStarColor = new Color(1.0f, 0.84f, 0f, 1f);
        [SerializeField] private Color inactiveStarColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
        [SerializeField] private Color lockedSkillColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);

        private MinionAltarUI owner;
        private Color originalBorderColor = Color.white;
        private Tween shakeTween;
        private AsyncOperationHandle<Sprite> _iconHandle;
        private bool bindingWarningLogged;

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
            LoadIconAsync().Forget();
            
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
                unlockButton.onClick.AddListener(OnClickAction);
            }

            if (promoteButton != null && promoteButton != unlockButton)
            {
                promoteButton.onClick.RemoveAllListeners();
                promoteButton.onClick.AddListener(OnClickAction);
            }

            if (skill3IconButton != null)
            {
                skill3IconButton.transition = Selectable.Transition.None;
                skill3IconButton.onClick.RemoveAllListeners();
                skill3IconButton.onClick.AddListener(() => OnClickSkillIcon(3));
            }

            if (skill5IconButton != null)
            {
                skill5IconButton.transition = Selectable.Transition.None;
                skill5IconButton.onClick.RemoveAllListeners();
                skill5IconButton.onClick.AddListener(() => OnClickSkillIcon(5));
            }

            Refresh();
        }

        public void Refresh()
        {
            if (Data == null || GameManager.Instance == null) return;

            var res = GameManager.Instance.Resources;
            int stars = res.GetMinionStars(Data.minionID);
            bool isUnlocked = stars >= 1;
            int maxStars = Mathf.Clamp(Data.maxStars, 1, 5);
            int targetStar = Mathf.Min(stars + 1, maxStars);
            int currentEssence = res.GetEssenceCount(Data.targetEnemyID);
            int currentSoul = res.currentSoul;

            int requiredEssence = Data.unlockCost_Essence;
            int requiredSoul = Data.unlockCost_Soul;
            if (isUnlocked && stars < maxStars)
                res.GetPromotionCost(Data, targetStar, out requiredSoul, out requiredEssence);

            bool enoughEssence = currentEssence >= requiredEssence;
            bool enoughSoul = currentSoul >= requiredSoul;

            // 1. 아이콘 및 이름 색상 (미해금 시에도 형태가 보이도록 색상값 상향 조정)
            if (iconImage != null) iconImage.color = isUnlocked ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (nameText != null) nameText.color = isUnlocked ? Color.white : Color.gray;

            // 2. 정수 진행도 표시 (해금 완료 시 텍스트 제거)
            if (progressText != null)
            {
                progressText.text = stars >= maxStars ? "" : $"정수 : {currentEssence} / {requiredEssence}";
                progressText.color = enoughEssence ? Color.green : Color.white;
            }

            // 3. 소울 비용 표시
            if (soulCostText != null)
            {
                soulCostText.text = stars >= maxStars ? "" : $"{requiredSoul} SOUL";
                soulCostText.color = enoughSoul ? Color.white : Color.red;
            }

            if (promoteCostText != null)
            {
                promoteCostText.text = !isUnlocked ? "" : (stars >= maxStars ? "MAX" : $"{requiredEssence} / {requiredSoul}");
                promoteCostText.color = enoughEssence && enoughSoul ? Color.white : Color.red;
            }

            // 4. 버튼 텍스트 및 상태 제어
            if (unlockButtonText != null)
            {
                unlockButtonText.text = !isUnlocked ? "해금" : (stars >= maxStars ? "MAX" : "승급");
            }

            Button actionButton = promoteButton != null ? promoteButton : unlockButton;
            if (actionButton != null)
            {
                actionButton.interactable = stars < maxStars;
                // 소울/정수 부족 시 시각적 표시 (UpgradeItemUI 패턴 적용)
                var img = actionButton.GetComponent<Image>();
                if (img != null && stars < maxStars)
                {
                    img.color = (enoughEssence && enoughSoul) ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            }

            // 5. 알림 뱃지
            if (alertObject != null) alertObject.SetActive(stars < maxStars && enoughEssence && enoughSoul);

            RefreshPromotionVisuals(stars);
            RefreshSkillIcons(stars);
            LogMissingPromotionBindings();
        }

        private void OnClickAction()
        {
            if (Data == null || GameManager.Instance == null || owner == null) return;

            var res = GameManager.Instance.Resources;
            int stars = res.GetMinionStars(Data.minionID);
            int maxStars = Mathf.Clamp(Data.maxStars, 1, 5);
            if (stars >= maxStars) return;

            int currentEssence = res.GetEssenceCount(Data.targetEnemyID);
            int currentSoul = res.currentSoul;
            int requiredSoul = Data.unlockCost_Soul;
            int requiredEssence = Data.unlockCost_Essence;
            if (stars >= 1)
                res.GetPromotionCost(Data, stars + 1, out requiredSoul, out requiredEssence);

            // [CHECK] 조건 미달 시 실패 연출
            if (currentEssence < requiredEssence || currentSoul < requiredSoul)
            {
                PlayFailFeedback();
                return;
            }

            bool success = stars < 1 ? res.TryUnlockMinion(Data) : res.TryPromoteMinion(Data);
            if (success)
            {
                if (GameManager.Instance.Sound != null)
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxUpgrade);

                // [FEEDBACK] 해금 성공 펀치 스케일 (UpgradeItemUI와 동일)
                transform.DOPunchScale(Vector3.one * 0.05f, 0.2f);
                
                Refresh();
                owner.RefreshAllSlots(); // [CONSISTENCY] SendMessageUpwards 대신 명시적 호출
            }
        }

        private void OnClickSkillIcon(int requiredStars)
        {
            if (Data == null) return;

            MinionUniqueSkillData skill = requiredStars == 3 ? Data.star3Skill : Data.star5Skill;
            if (skillInfoPopup != null)
            {
                skillInfoPopup.Show(Data, skill, requiredStars);
                return;
            }

            string skillName = skill != null && !string.IsNullOrEmpty(skill.skillName) ? skill.skillName : $"{requiredStars}성 스킬";
            string description = skill != null && !string.IsNullOrEmpty(skill.description) ? skill.description : "아직 스킬 데이터가 연결되지 않았습니다.";
            string cooldown = skill != null && skill.cooldown > 0f ? $"\n쿨타임: {skill.cooldown:0.#}초" : "";
            GameManager.Instance?.Popup?.ShowMessagePopup($"{skillName}\n해금 조건: {Data.minionName} {requiredStars}성\n{description}{cooldown}");
        }

        private void RefreshPromotionVisuals(int stars)
        {
            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    Image starImage = starImages[i];
                    if (starImage == null) continue;

                    starImage.gameObject.SetActive(true);
                    starImage.color = i < stars ? activeStarColor : inactiveStarColor;
                }
            }

            if (star3SkillBadge != null) star3SkillBadge.SetActive(stars >= 3);
            if (star5SkillBadge != null) star5SkillBadge.SetActive(stars >= 5);

            if (borderImage != null)
            {
                if (stars >= 5) borderImage.color = goldBorderColor;
                else if (stars >= 3) borderImage.color = silverBorderColor;
                else if (stars >= 1) borderImage.color = bronzeBorderColor;
                else borderImage.color = originalBorderColor;
            }
        }

        private void RefreshSkillIcons(int stars)
        {
            RefreshSkillIcon(3, stars, skill3IconButton, skill3IconImage);
            RefreshSkillIcon(5, stars, skill5IconButton, skill5IconImage);
        }

        private void RefreshSkillIcon(
            int requiredStars,
            int currentStars,
            Button button,
            Image iconImage)
        {
            bool unlocked = currentStars >= requiredStars;

            if (button != null)
            {
                button.gameObject.SetActive(true);
                button.interactable = true;
            }

            Image resolvedIconImage = iconImage != null ? iconImage : button != null ? button.image : null;
            if (resolvedIconImage != null)
            {
                resolvedIconImage.color = unlocked ? Color.white : lockedSkillColor;
            }
        }

        private void LogMissingPromotionBindings()
        {
            if (bindingWarningLogged || Data == null) return;

            bool missingStars = starImages == null || starImages.Length < 5;
            bool missingCost = promoteCostText == null;
            bool missingSkillButtons = skill3IconButton == null || skill5IconButton == null;
            if (!missingStars && !missingCost && !missingSkillButtons) return;

            bindingWarningLogged = true;
            Debug.LogWarning($"[MinionUnlockSlot] Promotion UI bindings are incomplete on {name}. Data: {Data.minionID}");
        }

        private async UniTaskVoid LoadIconAsync()
        {
            if (Data?.minionIcon == null || iconImage == null) return;

            if (_iconHandle.IsValid())
            {
                Addressables.Release(_iconHandle);
                _iconHandle = default;
            }

            _iconHandle = Data.minionIcon.LoadAssetAsync<Sprite>();
            while (!_iconHandle.IsDone)
                await UniTask.Yield();

            if (_iconHandle.Status == AsyncOperationStatus.Succeeded && iconImage != null)
                iconImage.sprite = _iconHandle.Result;
        }

        private void OnDestroy()
        {
            if (_iconHandle.IsValid())
                Addressables.Release(_iconHandle);
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
