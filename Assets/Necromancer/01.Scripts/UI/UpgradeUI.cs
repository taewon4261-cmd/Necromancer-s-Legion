using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using DG.Tweening;
using Necromancer.Systems;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Necromancer.UI
{
    public class UpgradeUI : MonoBehaviour
    {
        [Header("Data Slots")]
        public List<LobbyUpgradeSO> upgradeList;
        
        [Header("UI References (Assign in Inspector)")]
        public Transform contentRoot;
        public GameObject slotPrefab;
        public TextMeshProUGUI goldText;
        public int initialGold = 1000;

        private List<UpgradeItemUI> activeSlots = new List<UpgradeItemUI>();
        private int lastDisplayedGold = -1;
        private Tweener goldTweener;

        private void OnEnable()
        {
            if (goldText == null)
                goldText = transform.Find("Top_Bar/Text_GoldDisplay")?.GetComponent<TextMeshProUGUI>();

            UpdateGoldUI(true);
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (contentRoot == null)
            {
                Debug.LogError($"[CRITICAL ERROR] UpgradeUI is missing 'contentRoot'!");
                return;
            }

            if (slotPrefab == null)
            {
                Debug.LogError($"[CRITICAL ERROR] UpgradeUI is missing 'slotPrefab'!");
                return;
            }

            var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(40, 40, 50, 300);
                vlg.spacing = 30;
            }

            LoadUpgradeData();
            foreach (var data in upgradeList)
            {
                if (data != null) data.LoadLevel();
            }

            UpdateGoldUI(true);

            if (upgradeList != null)
            {
                for (int i = 0; i < upgradeList.Count; i++)
                {
                    var data = upgradeList[i];
                    if (data == null) continue;

                    UpgradeItemUI slot;
                    if (i < activeSlots.Count)
                    {
                        slot = activeSlots[i];
                        if (slot != null) slot.gameObject.SetActive(true);
                    }
                    else
                    {
                        GameObject go = Instantiate(slotPrefab, contentRoot);
                        go.name = $"Slot_{data.name}";
                        slot = go.GetComponent<UpgradeItemUI>();
                        if (slot != null) activeSlots.Add(slot);
                    }

                    if (slot != null)
                    {
                        // [지시사항] 코드에서 사이즈를 강제하지 않고 프리팹 설정을 따름
                        slot.Setup(data, this);
                    }
                }

                for (int i = upgradeList.Count; i < activeSlots.Count; i++)
                {
                    if (activeSlots[i] != null) activeSlots[i].gameObject.SetActive(false);
                }
            }

            Canvas.ForceUpdateCanvases();
            if (contentRoot is RectTransform contentRT) 
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        private void LoadUpgradeData()
        {
            var resourcesData = Resources.LoadAll<LobbyUpgradeSO>("Upgrades");
            if (resourcesData.Length > 0)
            {
                upgradeList = resourcesData.OrderBy(x => x.name).ToList();
                return;
            }

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:LobbyUpgradeSO", new[] { "Assets/Necromancer/02.Data/Upgrades" });
            upgradeList = guids.Select(guid => AssetDatabase.LoadAssetAtPath<LobbyUpgradeSO>(AssetDatabase.GUIDToAssetPath(guid)))
                               .OrderBy(x => x.name)
                               .ToList();
#endif
        }

        public void UpdateGoldUI(bool immediate = false)
        {
            if (goldText == null || GameManager.Instance == null || GameManager.Instance.Resources == null) return;
            int targetGold = GameManager.Instance.Resources.currentGold;
            
            if (immediate)
            {
                goldTweener?.Kill();
                goldText.text = $"{targetGold:N0}";
                lastDisplayedGold = targetGold;
            }
            else if (lastDisplayedGold != targetGold)
            {
                goldTweener?.Kill();
                goldTweener = DOTween.To(() => lastDisplayedGold, x => {
                    lastDisplayedGold = x;
                    goldText.text = $"{x:N0}";
                }, targetGold, 0.5f).SetEase(Ease.OutQuad);
            }
        }

        public bool TryPurchase(int cost)
        {
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                if (GameManager.Instance.Resources.SpendGold(cost))
                {
                    UpdateGoldUI();
                    return true;
                }
            }
            return false;
        }

        public void UpdateAllSlotVisuals()
        {
            foreach (var slot in activeSlots)
            {
                if (slot != null && slot.gameObject.activeSelf) slot.UpdateVisuals();
            }
        }
    }
}
