using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 영구 업그레이드 슬롯 UI를 관리하며, 데이터와 시각적 요소(아이콘, 이름, 설명, 비용 등)를 동기화합니다.
    /// </summary>
    public class UpgradeSlot : MonoBehaviour
    {
        [Header("UI Elements (Assign in Inspector)")]
        public Image iconImage;
        public Image iconFrameImage; // [DESIGN] 업그레이드 전용 프레임 이미지 (인스펙터에서 할당 필요)
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

            // [QA] 업그레이드 전용 아이콘 프레임 적용
            if (iconFrameImage != null)
            {
                if (data.iconFrame != null)
                {
                    iconFrameImage.gameObject.SetActive(true);
                    iconFrameImage.sprite = data.iconFrame;
                }
                else
                {
                    // 프레임 데이터가 없을 경우 기본 프레임 유지 또는 비활성화 처리
                    // iconFrameImage.gameObject.SetActive(false); 
                }
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
                        // [OPTIMIZATION] PlayerPrefs 대신 중앙 리소스 매니저에서 영혼 잔액 확인
                        int currentSoul = GameManager.Instance != null && GameManager.Instance.Resources != null ? GameManager.Instance.Resources.currentSoul : 0;
                        if (upgradeButton != null) upgradeButton.interactable = currentSoul >= cost;
                    }
                }
            }
            else
            {
                if (descriptionText != null) descriptionText.text = $"{data.requiredUpgrade?.upgradeName} Lv.{data.requiredLevel} 이상 달성 시 해금";
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
                
                // [DATA.md] 중앙 저장 시스템을 통해 데이터 갱신 (더 이상 UI가 직접 PlayerPrefs를 건드리지 않음)
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
                {
                    GameManager.Instance.SaveData.SetUpgradeLevel(data.saveKey, data.currentLevel);
                }

                UpdateVisuals();
                owner.RefreshUI();
            }
            else
            {
                Debug.Log("<color=yellow>[UpgradeSlot]</color> 영혼이 부족하여 업그레이드에 실패했습니다.");
            }
        }
    }
}