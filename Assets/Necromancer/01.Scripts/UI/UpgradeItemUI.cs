using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 개별 업그레이드 항목의 UI를 제어합니다.
    /// </summary>
    public class UpgradeItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI costText;
        public Button upgradeButton;

        private LobbyUpgradeSO data;
        private UpgradeUI owner;

        private void Awake()
        {
            // 자동 바인딩 루틴 추가
            if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
            if (nameText == null) nameText = transform.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
            if (levelText == null) levelText = transform.Find("Text_Level")?.GetComponent<TextMeshProUGUI>();
            if (descriptionText == null) descriptionText = transform.Find("Text_Description")?.GetComponent<TextMeshProUGUI>();
            if (costText == null) costText = transform.Find("Text_Cost")?.GetComponent<TextMeshProUGUI>();
            if (upgradeButton == null) upgradeButton = transform.Find("Button_Upgrade")?.GetComponent<Button>();
        }

        public void Setup(LobbyUpgradeSO upgradeData, UpgradeUI uiOwner)
        {
            data = upgradeData;
            owner = uiOwner;
            
            // 디버깅: 컴포넌트 유효성 확인
            if (iconImage == null || nameText == null || upgradeButton == null)
            {
                Debug.LogError($"UpgradeItemUI [{gameObject.name}]: Missing UI references! Icon:{iconImage!=null}, Name:{nameText!=null}, Button:{upgradeButton!=null}");
            }

            UpdateVisuals();

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }
            
            Debug.Log($"UpgradeItemUI [{gameObject.name}]: Setup complete. Data: {data.upgradeName}, Level: {data.currentLevel}");
        }

        private void UpdateVisuals()
        {
            if (data == null) return;

            bool unlocked = data.IsUnlocked();
            
            iconImage.sprite = data.icon;
            iconImage.color = unlocked ? Color.white : Color.black; // 잠금 시 검은색 실루엣

            nameText.text = unlocked ? data.upgradeName : "??? (잠금됨)";
            levelText.text = unlocked ? $"Lv. {data.currentLevel} / {data.maxLevel}" : "";
            
            if (unlocked)
            {
                descriptionText.text = data.description;
                int cost = data.GetUpgradeCost();
                if (cost < 0 || data.currentLevel >= data.maxLevel)
                {
                    costText.text = "MAX";
                    upgradeButton.interactable = false;
                }
                else
                {
                    costText.text = $"{cost:N0}";
                    int currentGold = PlayerPrefs.GetInt("TotalGold", 1000);
                    upgradeButton.interactable = currentGold >= cost;
                }
            }
            else
            {
                descriptionText.text = $"{data.requiredUpgrade.upgradeName} Lv.{data.requiredLevel} 달성 시 해금";
                costText.text = "LOCKED";
                upgradeButton.interactable = false;
            }
        }

        private void OnUpgradeClicked()
        {
            int cost = data.GetUpgradeCost();
            if (owner.TryPurchase(cost))
            {
                data.currentLevel++;
                // 데이터 저장 (나중에 별도 SaveManager 사용 권장)
                PlayerPrefs.SetInt($"Upgrade_{data.statType}_Lv", data.currentLevel);
                PlayerPrefs.Save();
                
                UpdateVisuals();
                owner.RefreshUI(); // 다른 버튼들의 구매 가능 여부 업데이트를 위해 리프레시
            }
            else
            {
                // TODO: 골드 부족 사운드나 팝업 연출
                Debug.Log("골드가 부족합니다!");
            }
        }
    }
}
