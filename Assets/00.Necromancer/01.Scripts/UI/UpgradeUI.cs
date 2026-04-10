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
        [SerializeField] private List<LobbyUpgradeSO> upgradeList = new List<LobbyUpgradeSO>();
        [ContextMenu("Sync Upgrade Data")]
        public void SyncUpgradeData()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:LobbyUpgradeSO", new[] { "Assets/Necromancer/02.Data/Upgrades" });
            upgradeList = guids.Select(guid => AssetDatabase.LoadAssetAtPath<LobbyUpgradeSO>(AssetDatabase.GUIDToAssetPath(guid)))
                               .OrderBy(x => x.name.Split('_')[0]) // 숫자로 정렬할 수 있도록 보완
                               .ToList();
            
            EditorUtility.SetDirty(this);
            Debug.Log($"[SUCCESS] {upgradeList.Count} upgrades synced to list.");
#endif
        }
        
        [Header("UI References (Assign in Inspector)")]
        public Transform contentRoot;
        public GameObject slotPrefab;
        public TextMeshProUGUI soulText;
        public int initialGold = 1000;

        private List<UpgradeItemUI> activeSlots = new List<UpgradeItemUI>();
        private int lastDisplayedGold = -1;
        private Tweener goldTweener;

        private void OnEnable()
        {
            // [DATA-SAFETY] 진입 시 Load() 호출 제거 — 인게임 미저장 데이터(소울 등)를
            // 파일 값으로 덮어써 유실하는 버그 방지. 데이터 로드는 게임 시작 시 1회만 수행.
            UpdateSoulUI(true);
            RefreshUI();
            
            GameManager.OnSoulChanged += HandleSoulChanged;
        }

        private void OnDisable()
        {
            GameManager.OnSoulChanged -= HandleSoulChanged;
        }

        private void HandleSoulChanged(int amount)
        {
            UpdateSoulUI(false);
        }

        public void RefreshUI()
        {
            if (contentRoot == null || slotPrefab == null) return;

            var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.padding = new RectOffset(40, 40, 50, 300);
                vlg.spacing = 30;
            }

            // [Zero-Search] 런타임 데이터 로드 로직 제거. 미리 인스펙터에 구워진 리스트를 사용.
            foreach (var data in upgradeList)
            {
                if (data != null) data.LoadLevel();
            }

            UpdateSoulUI(true);

            if (upgradeList != null && upgradeList.Count > 0)
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
                        slot.Setup(data, this);
                    }
                }

                for (int i = upgradeList.Count; i < activeSlots.Count; i++)
                {
                    if (activeSlots[i] != null) activeSlots[i].gameObject.SetActive(false);
                }
            }
            else
            {
                 Debug.LogWarning("[UpgradeUI] Upgrade list is empty! Please right-click component and select 'Sync Upgrade Data' in editor.");
            }

            Canvas.ForceUpdateCanvases();
            if (contentRoot is RectTransform contentRT) 
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        }

        public void UpdateSoulUI(bool immediate = false)
        {
            if (soulText == null || GameManager.Instance == null || GameManager.Instance.Resources == null) return;
            int targetSoul = GameManager.Instance.Resources.currentSoul;
            
            if (immediate)
            {
                goldTweener?.Kill();
                soulText.text = $"Soul : {targetSoul:N0}";
                lastDisplayedGold = targetSoul;
            }
            else if (lastDisplayedGold != targetSoul)
            {
                goldTweener?.Kill();
                goldTweener = DOTween.To(() => lastDisplayedGold, x => {
                    lastDisplayedGold = x;
                    soulText.text = $"Soul : {x:N0}";
                }, targetSoul, 0.5f).SetEase(Ease.OutQuad);
            }
        }

        public bool TryPurchase(int cost)
        {
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                if (GameManager.Instance.Resources.SpendSoul(cost))
                {
                    UpdateSoulUI();
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
