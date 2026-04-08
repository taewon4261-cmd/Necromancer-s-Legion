using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Data;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// [UI] 미니언 해금 그리드 개별 슬롯
    /// 슬롯은 상태 표시만 담당하며, 클릭 이벤트를 외부(MinionAltarUI)로 위임합니다.
    /// </summary>
    public class MinionUnlockSlot : MonoBehaviour
    {
        [Header("UI References (Inspector Bind)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image bgImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private GameObject alertObject;   // 해금 가능 시 표시되는 느낌표
        [SerializeField] private Button slotButton;

        // [ARCH] 슬롯은 데이터를 전달할 뿐, 해금 로직 일절 미포함
        public MinionUnlockSO Data { get; private set; }
        public Action<MinionUnlockSO> OnSlotClicked;

        public void Setup(MinionUnlockSO minionData)
        {
            this.Data = minionData;
            if (Data == null) return;

            Refresh();

            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke(Data));
            }
        }

        /// <summary>
        /// 슬롯 상태를 현재 세이브 데이터 기준으로 갱신합니다.
        /// </summary>
        public void Refresh()
        {
            if (Data == null || GameManager.Instance == null) return;

            bool isUnlocked = GameManager.Instance.Resources.IsMinionUnlocked(Data.minionID);
            int currentEssence = GameManager.Instance.Resources.GetEssenceCount(Data.targetEnemyID);

            // 1. 이름
            if (nameText != null) nameText.text = Data.minionName;

            // 2. 아이콘: 미해금 → 실루엣(검정), 해금 → 컬러
            if (iconImage != null)
            {
                iconImage.sprite = Data.minionIcon;
                iconImage.color = isUnlocked ? Color.white : Color.black;
            }

            // 3. 등급별 배경색
            if (bgImage != null)
            {
                bgImage.color = Data.tier switch
                {
                    MinionTier.Bronze => new Color(0.6f, 0.4f, 0.2f),
                    MinionTier.Silver => new Color(0.75f, 0.75f, 0.75f),
                    MinionTier.Gold   => new Color(1.0f, 0.84f, 0f),
                    _                 => Color.white
                };
            }

            // 4. 진행도 ("7 / 10")
            if (progressText != null)
            {
                progressText.text = isUnlocked
                    ? "<color=green>UNLOCKED</color>"
                    : $"{currentEssence} / {Data.unlockCost_Essence}";
            }

            // 5. 해금 가능 알림 뱃지
            if (alertObject != null)
            {
                bool canUnlock = !isUnlocked
                    && currentEssence >= Data.unlockCost_Essence
                    && GameManager.Instance.Resources.currentSoul >= Data.unlockCost_Soul;
                alertObject.SetActive(canUnlock);
            }
        }
    }
}
