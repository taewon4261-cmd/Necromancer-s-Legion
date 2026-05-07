using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Necromancer.Data;

namespace Necromancer.UI
{
    public class MinionSkillInfoPopup : MonoBehaviour
    {
        [Header("UI References (Inspector Bind)")]
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI unlockConditionText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            gameObject.SetActive(false);
        }

        public void Show(MinionUnlockSO minionData, MinionUniqueSkillData skillData, int requiredStars)
        {
            string minionName = minionData != null ? minionData.minionName : "미니언";
            string skillName = skillData != null && !string.IsNullOrEmpty(skillData.skillName)
                ? skillData.skillName
                : $"{requiredStars}성 스킬";

            if (skillNameText != null) skillNameText.text = skillName;
            if (unlockConditionText != null) unlockConditionText.text = $"해금 조건: {minionName} {requiredStars}성";
            if (descriptionText != null)
            {
                descriptionText.text = skillData != null && !string.IsNullOrEmpty(skillData.description)
                    ? skillData.description
                    : "아직 스킬 데이터가 연결되지 않았습니다.";
            }

            if (cooldownText != null)
            {
                cooldownText.text = skillData != null && skillData.cooldown > 0f
                    ? $"쿨타임: {skillData.cooldown:0.#}초"
                    : "쿨타임: -";
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
