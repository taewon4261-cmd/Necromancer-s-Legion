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
        public Image iconImage;
        public Image iconFrame;
        public Image backgroundImage; // [UI.md] Red Flash 연출용
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI costText;
        public Button upgradeButton;
        public Slider levelSlider;

        private LobbyUpgradeSO data;
        private UpgradeUI owner;
        private Color originalBgColor;

        private void Awake()
        {
            if (iconFrame == null) iconFrame = transform.Find("Icon_Frame")?.GetComponent<Image>();
            if (iconImage == null) iconImage = iconFrame?.transform.Find("Icon")?.GetComponent<Image>() ?? transform.Find("Icon")?.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (originalBgColor == default) originalBgColor = backgroundImage != null ? backgroundImage.color : Color.white;

            Transform infoGroup = transform.Find("Info_Vertical_Group");
            if (infoGroup != null)
            {
                if (nameText == null) nameText = infoGroup.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
                if (descriptionText == null) descriptionText = infoGroup.Find("Text_Description")?.GetComponent<TextMeshProUGUI>();
                if (levelText == null) levelText = infoGroup.Find("Text_Level")?.GetComponent<TextMeshProUGUI>();
            }
            
            if (upgradeButton == null) upgradeButton = transform.Find("Button_Upgrade")?.GetComponent<Button>();
            if (costText == null) costText = upgradeButton?.transform.Find("Text_Cost")?.GetComponent<TextMeshProUGUI>();
            if (levelSlider == null) levelSlider = GetComponentInChildren<Slider>();
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
            
            if (iconImage != null) { iconImage.sprite = data.icon; iconImage.color = unlocked ? Color.white : Color.gray; }
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
                    if (costText != null) costText.text = $"{cost:N0} G";
                    int currentGold = GameManager.Instance != null && GameManager.Instance.Resources != null ? GameManager.Instance.Resources.currentGold : 0;
                    if (upgradeButton != null) upgradeButton.interactable = currentGold >= cost;
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
                PlayerPrefs.SetInt(data.saveKey, data.currentLevel);
                PlayerPrefs.Save();
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
    }
}
