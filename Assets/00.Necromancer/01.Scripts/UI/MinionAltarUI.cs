using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Data;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// [UI] '영혼의 제단' 전체 패널 관리
    /// - 그리드: 미니언 슬롯 목록 생성 및 갱신
    /// - Detail Panel: 슬롯 클릭 시 우측에 표시되는 상세/해금 창
    /// </summary>
    public class MinionAltarUI : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // Grid
        // ─────────────────────────────────────────
        [Header("Grid References")]
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private Transform contentParent;

        // ─────────────────────────────────────────
        // Detail Panel
        // ─────────────────────────────────────────
        [Header("Detail Panel References")]
        [SerializeField] private GameObject detailPanel;

        [SerializeField] private Image   detailIcon;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private TextMeshProUGUI detailDescText;

        [SerializeField] private TextMeshProUGUI soulCostText;      // "소울: 500"
        [SerializeField] private TextMeshProUGUI essenceCostText;   // "정수: 10 / 10"
        [SerializeField] private Button          unlockButton;
        [SerializeField] private TextMeshProUGUI unlockButtonText;

        [Header("Feedback")]
        [SerializeField] private string unlockParticleTag = "FX_Unlock"; // PoolManager 태그

        // ─────────────────────────────────────────
        // Internal State
        // ─────────────────────────────────────────
        private List<MinionUnlockSO> allMinions = new List<MinionUnlockSO>();
        private List<MinionUnlockSlot> activeSlots = new List<MinionUnlockSlot>();
        private MinionUnlockSO selectedData;

        // ─────────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
            RefreshAltar();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Grid
        // ─────────────────────────────────────────────────────────────────────────
        public void RefreshAltar()
        {
            if (slotPrefab == null || contentParent == null) return;

            var loaded = UnityEngine.Resources.LoadAll<MinionUnlockSO>("Minions");
            allMinions.Clear();
            allMinions.AddRange(loaded);

            // 기존 슬롯 제거 (그리드 규모 최대 6개 → GC 오버헤드 미미)
            foreach (Transform child in contentParent)
                Destroy(child.gameObject);
            activeSlots.Clear();

            foreach (var minion in allMinions)
            {
                if (minion == null) continue;

                var slotObj = Instantiate(slotPrefab, contentParent);
                if (slotObj.TryGetComponent(out MinionUnlockSlot slot))
                {
                    slot.Setup(minion);
                    slot.OnSlotClicked += ShowDetail;
                    activeSlots.Add(slot);
                }
            }

            Debug.Log($"<color=cyan>[Altar]</color> Grid refreshed. Slots: {activeSlots.Count}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Detail Panel
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>슬롯 클릭 시 호출 — Detail Panel을 열고 선택 미니언 정보 갱신.</summary>
        public void ShowDetail(MinionUnlockSO data)
        {
            selectedData = data;
            if (detailPanel != null) detailPanel.SetActive(true);
            RefreshDetail();
        }

        /// <summary>현재 선택된 미니언 기준으로 Detail Panel UI를 갱신합니다.</summary>
        private void RefreshDetail()
        {
            if (selectedData == null || GameManager.Instance == null) return;

            var res = GameManager.Instance.Resources;
            bool isUnlocked       = res.IsMinionUnlocked(selectedData.minionID);
            int  ownedEssence     = res.GetEssenceCount(selectedData.targetEnemyID);
            int  ownedSoul        = res.currentSoul;
            bool enoughSoul       = ownedSoul    >= selectedData.unlockCost_Soul;
            bool enoughEssence    = ownedEssence >= selectedData.unlockCost_Essence;

            // 아이콘 / 이름 / 설명
            if (detailIcon != null)
            {
                detailIcon.sprite = selectedData.minionIcon;
                detailIcon.color  = isUnlocked ? Color.white : Color.black;
            }
            if (detailNameText != null) detailNameText.text = selectedData.minionName;
            if (detailDescText != null) detailDescText.text  = selectedData.description;

            // 소울 비용 — 부족하면 빨간색
            if (soulCostText != null)
            {
                soulCostText.text  = $"소울: {selectedData.unlockCost_Soul}";
                soulCostText.color = enoughSoul ? Color.white : Color.red;
            }

            // 정수 비용 — 부족하면 빨간색 (어떤 적을 더 잡아야 하는지 즉시 인지 가능)
            if (essenceCostText != null)
            {
                essenceCostText.text  = $"정수 [{selectedData.targetEnemyID}]: {ownedEssence} / {selectedData.unlockCost_Essence}";
                essenceCostText.color = enoughEssence ? Color.white : Color.red;
            }

            // 해금 버튼
            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(!isUnlocked);
                unlockButton.interactable = enoughSoul && enoughEssence;

                unlockButton.onClick.RemoveAllListeners();
                unlockButton.onClick.AddListener(OnClickUnlock);
            }
            if (unlockButtonText != null)
            {
                unlockButtonText.text = isUnlocked ? "해금 완료" : "부활 의식 거행";
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Unlock Logic
        // ─────────────────────────────────────────────────────────────────────────

        private void OnClickUnlock()
        {
            if (selectedData == null || GameManager.Instance == null) return;

            if (GameManager.Instance.Resources.TryUnlockMinion(selectedData))
            {
                StartCoroutine(UnlockFeedbackSequence());
            }
        }

        /// <summary>
        /// [QA #3] 해금 성공 피드백: 사운드 → 파티클 → UI 갱신 (0.5s 이상 연출)
        /// </summary>
        private IEnumerator UnlockFeedbackSequence()
        {
            // 1. 해금 사운드
            if (GameManager.Instance.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxUpgrade);

            // 2. 파티클 (PoolManager에 FX_Unlock 태그가 등록된 경우)
            if (GameManager.Instance.poolManager != null && !string.IsNullOrEmpty(unlockParticleTag))
            {
                Vector3 spawnPos = detailPanel != null
                    ? detailPanel.transform.position
                    : transform.position;
                GameManager.Instance.poolManager.Get(unlockParticleTag, spawnPos, Quaternion.identity);
            }

            // 3. 최소 0.5초 연출 대기
            yield return new WaitForSecondsRealtime(0.5f);

            // 4. 전체 UI 갱신 (실루엣 해제 반영)
            RefreshAllSlots();
            RefreshDetail();
        }

        /// <summary>모든 슬롯의 상태를 현재 세이브 데이터로 재갱신합니다.</summary>
        private void RefreshAllSlots()
        {
            foreach (var slot in activeSlots)
                slot.Refresh();
        }
    }
}
