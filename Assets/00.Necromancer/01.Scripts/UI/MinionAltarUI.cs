using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Data;
using Necromancer.Core;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Necromancer.UI
{
    /// <summary>
    /// [UI] '영혼의 제단' (미니언 해금 상점)
    /// UpgradeUI와 동일한 아키텍처 패턴을 유지합니다.
    /// </summary>
    public class MinionAltarUI : MonoBehaviour
    {
        [Header("Data Slots")]
        [SerializeField] private List<MinionUnlockSO> minionDataList = new List<MinionUnlockSO>();

        [Header("UI References")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private TextMeshProUGUI soulText;

        private readonly List<MinionUnlockSlot> activeSlots = new List<MinionUnlockSlot>();
        private int lastDisplayedSoul = -1;
        private Tweener soulTweener;
        private bool isRefreshing = false;

        private void OnEnable()
        {
            // [DATA-SAFETY] 진입 시 Load() 호출 제거 — 인게임 미저장 데이터(소울, 정수 등)를
            // 파일 값으로 덮어써 유실하는 버그 방지. 데이터 로드는 게임 시작 시 1회만 수행.
            UpdateSoulUI(true);
            RefreshAltar();

            // [STABILITY] 소울 변화 실시간 감지
            GameManager.OnSoulChanged += HandleSoulChanged;
        }

        private void OnDisable()
        {
            GameManager.OnSoulChanged -= HandleSoulChanged;
        }

        private void HandleSoulChanged(int amount)
        {
            UpdateSoulUI(false);
            RefreshAllSlots();
        }

        public void RefreshAltar()
        {
            if (isRefreshing) return;
            if (contentParent == null || slotPrefab == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Resources == null) return;

            isRefreshing = true;
            try
            {
                int slotIndex = 0;
                for (int i = 0; i < minionDataList.Count; i++)
                {
                    var data = minionDataList[i];
                    if (data == null || data.minionID == "SkeletonWarrior") continue;

                    MinionUnlockSlot slot;
                    if (slotIndex < activeSlots.Count)
                    {
                        slot = activeSlots[slotIndex];
                        if (slot != null) slot.gameObject.SetActive(true);
                    }
                    else
                    {
                        var go = Instantiate(slotPrefab, contentParent);
                        go.name = $"Slot_{data.name}";
                        slot = go.GetComponent<MinionUnlockSlot>();
                        if (slot != null) activeSlots.Add(slot);
                    }

                    if (slot != null)
                        slot.Setup(data, this);

                    slotIndex++;
                }

                for (int i = slotIndex; i < activeSlots.Count; i++)
                {
                    if (activeSlots[i] != null) activeSlots[i].gameObject.SetActive(false);
                }
            }
            finally
            {
                isRefreshing = false;
            }
        }

        public void RefreshAllSlots()
        {
            foreach (var slot in activeSlots)
            {
                if (slot != null && slot.gameObject.activeSelf)
                    slot.Refresh();
            }
        }

        public void UpdateSoulUI(bool immediate = false)
        {
            if (soulText == null || GameManager.Instance == null || GameManager.Instance.Resources == null) return;
            int targetSoul = GameManager.Instance.Resources.currentSoul;

            if (immediate)
            {
                soulTweener?.Kill();
                soulText.text = $"Soul : {targetSoul:N0}";
                lastDisplayedSoul = targetSoul;
            }
            else if (lastDisplayedSoul != targetSoul)
            {
                soulTweener?.Kill();
                soulTweener = DOTween.To(() => lastDisplayedSoul, x => {
                    lastDisplayedSoul = x;
                    soulText.text = $"Soul : {x:N0}";
                }, targetSoul, 0.5f).SetEase(Ease.OutQuad);
            }
        }

        [ContextMenu("Sync Minion Data")]
        public void SyncMinionData()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:MinionUnlockSO",
                new[] { "Assets/00.Necromancer/02.Data/Minions" });

            minionDataList.Clear();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<MinionUnlockSO>(path);
                if (so != null) minionDataList.Add(so);
            }

            minionDataList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            EditorUtility.SetDirty(this);
            Debug.Log($"[SUCCESS] {minionDataList.Count} MinionUnlockSO synced.");
#endif
        }
    }
}
