using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 개별 업그레이드 항목(슬롯)의 UI와 데이터를 바인딩합니다.
    /// 모든 UI 요소는 인스펙터에서 직접 할당해야 합니다.
    /// </summary>
    public class UpgradeSlot : MonoBehaviour
    {
        [Header("UI Elements (Assign in Inspector)")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI costText;
        public Button upgradeButton;

        private LobbyUpgradeSO data;
        private UpgradeUI owner;

        public void SetData(LobbyUpgradeSO upgradeData, UpgradeUI uiOwner)
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
                iconImage.sprite = data.icon;
                iconImage.color = unlocked ? Color.white : Color.black; 
            }

            if (nameText != null) nameText.text = unlocked ? data.upgradeName : "??? (Locked)";
            if (levelText != null) levelText.text = unlocked ? $"Lv. {data.currentLevel} / {data.maxLevel}" : "";
            
            if (unlocked)
            {
                if (descriptionText != null) descriptionText.text = data.description;
                
                int cost = data.GetUpgradeCost();
                if (costText != null)
                {
                    if (cost < 0 || data.currentLevel >= data.maxLevel)
                    {
                        costText.text = "MAX";
                        if (upgradeButton != null) upgradeButton.interactable = false;
                    }
                    else
                    {
                        costText.text = $"{cost:N0}";
                        int currentGold = PlayerPrefs.GetInt("TotalGold", 1000);
                        if (upgradeButton != null) upgradeButton.interactable = currentGold >= cost;
                    }
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
            if (data == null || owner == null) return;

            int cost = data.GetUpgradeCost();
            if (owner.TryPurchase(cost))
            {
                data.currentLevel++;
                // 데이터 저장
                PlayerPrefs.SetInt($"Upgrade_{data.statType}_Lv", data.currentLevel);
                PlayerPrefs.Save();
                
                UpdateVisuals();
                owner.RefreshUI(); 
            }
            else
            {
                Debug.Log("골드가 부족합니다!");
            }
        }
    }
}
