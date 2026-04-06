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
                    // [방어 로직] 아이콘 유실 시 하얀 사각형 대신 투명화 처리하거나 로그 출력
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
                    if (upgradeButton != null) { upgradeButton.interactable = false; var img = upgradeButton.GetComponent<Image>(); if (img != null) img.color = new Color(1f, 0.8f, 0f); }
                }
                else
                {
                    if (costText != null) costText.text = $"{cost:N0} Soul";
                    int currentSoul = GameManager.Instance != null && GameManager.Instance.Resources != null ? GameManager.Instance.Resources.currentSoul : 0;
                    if (upgradeButton != null) upgradeButton.interactable = currentSoul >= cost;
                }
            }
            else
            {
                if (descriptionText != null) descriptionText.text = $"{data.requiredUpgrade?.upgradeName} Lv.{data.requiredLevel} 달성 시 해금";
                if (costText != null) costText.text = "LOCKED";
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
