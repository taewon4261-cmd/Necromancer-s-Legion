using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 상점 UI의 전반적인 흐름을 관리합니다.
    /// 모든 리퍼런스는 인스펙터에서 수동으로 연결합니다.
    /// </summary>
    public class UpgradeUI : MonoBehaviour
    {
        [Header("Data Slots")]
        public List<LobbyUpgradeSO> upgradeList;
        
        [Header("UI References (Assign in Inspector)")]
        public Transform contentRoot;
        public GameObject slotPrefab;
        public TextMeshProUGUI goldText;

        private List<UpgradeSlot> activeSlots = new List<UpgradeSlot>();

        private void OnEnable()
        {
            RefreshUI();
        }

        public void RefreshUI()
        {
            UpdateGoldUI();

            // 기존 슬롯 제거
            foreach (var slot in activeSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            activeSlots.Clear();

            // 리스트 생성
            if (slotPrefab == null || contentRoot == null) return;

            foreach (var data in upgradeList)
            {
                if (data == null) continue;
                
                GameObject go = Instantiate(slotPrefab, contentRoot);
                go.name = $"Slot_{data.statType}";
                
                UpgradeSlot slot = go.GetComponent<UpgradeSlot>();
                if (slot != null)
                {
                    slot.SetData(data, this);
                    activeSlots.Add(slot);
                }
            }
        }

        public void UpdateGoldUI()
        {
            if (goldText == null) return;
            int currentGold = PlayerPrefs.GetInt("TotalGold", 1000);
            goldText.text = $"{currentGold:N0}";
        }

        public bool TryPurchase(int cost)
        {
            int currentGold = PlayerPrefs.GetInt("TotalGold", 1000);
            if (currentGold >= cost)
            {
                currentGold -= cost;
                PlayerPrefs.SetInt("TotalGold", currentGold);
                PlayerPrefs.Save();
                UpdateGoldUI();
                return true;
            }
            return false;
        }
    }
}
