using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Necromancer.Systems;

namespace Necromancer.UI
{
    public class UpgradeItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image iconFrame;
        [SerializeField] private Image backgroundImage; // [UI.md] Red Flash 연출용
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Slider levelSlider;

        private LobbyUpgradeSO data;
        private UpgradeUI owner;
        private Color originalBgColor;

        private void Awake()
        {
            if (backgroundImage != null) 
            {
                originalBgColor = backgroundImage.color;
            }
        }

        private void OnEnable()
        {
            UpdateVisuals();
        }

        public void Setup(LobbyUpgradeSO upgradeData, UpgradeUI uiOwner)
        {
            data = upgradeData;
            owner = uiOwner;
            UpdateVisuals();

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }
        }

        public void UpdateVisuals()
        {
            if (data == null) return;
            bool unlocked = data.IsUnlocked();
            
            if (iconImage != null) 
            {
                if (data.icon != null)
                {
                    iconImage.gameObject.SetActive(true);
                    iconImage.sprite = data.icon; 
                    iconImage.color = unlocked ? Color.white : Color.gray;
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                    Debug.LogWarning($"[UI Warning] Upgrade '{data.upgradeName}' is missing its icon asset!");
                }
            }
            if (nameText != null) nameText.text = unlocked ? data.upgradeName : "??? (잠금됨)";
            if (levelText != null) levelText.text = unlocked ? $"Lv. {data.currentLevel} / {data.maxLevel}" : "";
            
            if (levelSlider != null)
            {
                levelSlider.gameObject.SetActive(unlocked);
                levelSlider.maxValue = data.maxLevel;
                levelSlider.value = data.currentLevel;
            }

            if (unlocked)
            {
                if (descriptionText != null) descriptionText.text = data.description;
                int cost = data.GetUpgradeCost();
                if (cost < 0 || data.currentLevel >= data.maxLevel)
                {
                    if (costText != null) costText.text = "MAX";
                    if (upgradeButton != null) 
                    { 
                        upgradeButton.interactable = false; 
                        var img = upgradeButton.GetComponent<Image>(); 
                        if (img != null) img.color = new Color(1f, 0.8f, 0f); // 황금색 (MAX)
                    }
                }
                else
                {
                    if (costText != null) costText.text = $"{cost:N0} Soul";
                    int currentSoul = GameManager.Instance != null && GameManager.Instance.Resources != null ? GameManager.Instance.Resources.currentSoul : 0;
                    
                    // [STABILITY] 소울이 부족해도 버튼은 켜둡니다 (피드백 연출을 위해!)
                    if (upgradeButton != null) 
                    {
                        upgradeButton.interactable = true;
                        var img = upgradeButton.GetComponent<Image>();
                        if (img != null) 
                        {
                            // 소울 부족 시 약간 어둡게 표시하여 시각적 거리둠
                            img.color = (currentSoul >= cost) ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                        }
                    }
                }
            }
            else
            {
                if (descriptionText != null) descriptionText.text = $"{data.requiredUpgrade?.upgradeName} Lv.{data.requiredLevel} 달성 시 해금";
                if (costText != null) costText.text = "LOCKED";
                
                // 해금 자체가 안 된 건 클릭 불가 유지
                if (upgradeButton != null) upgradeButton.interactable = false;
            }
        }

        private void OnUpgradeClicked()
        {
            int cost = data.GetUpgradeCost();
            if (owner.TryPurchase(cost))
            {
                data.currentLevel++;
                
                // [DATA.md] 중앙 저장 시스템을 통해 데이터 갱신 (더 이상 UI가 직접 PlayerPrefs를 건드리지 않음)
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
                {
                    GameManager.Instance.SaveData.SetUpgradeLevel(data.saveKey, data.currentLevel);
                }

                transform.DOPunchScale(Vector3.one * 0.05f, 0.2f);
                UpdateVisuals();
                owner.UpdateAllSlotVisuals();
            }
            else
            {
                // [UI.md] 구매 실패 피드백: Shake + Red Flash
                transform.DOShakePosition(0.3f, new Vector3(15f, 0, 0), 20, 90, false, true);
                if (backgroundImage != null)
                {
                    backgroundImage.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo).OnComplete(() => backgroundImage.color = originalBgColor);
                }

                // [SOUND] 구매 실패 효과음
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxFailBtn);
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Bind UI")]
        private void AutoBind()
        {
            UIAutoBinder.BindUpgradeItemUI(this);
        }
#endif
    }
}
