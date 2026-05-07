using UnityEngine;

namespace Necromancer.Data
{
    [CreateAssetMenu(fileName = "Minion_Skill_", menuName = "Necromancer/MinionUniqueSkillData")]
    public class MinionUniqueSkillData : ScriptableObject
    {
        [Header("Identity")]
        public string skillID;
        public string skillName;
        [TextArea] public string description;

        [Header("Runtime Values")]
        public float cooldown = 5f;
        public float range = 3f;
        public float value = 1f;

        [Header("Pooled Feedback")]
        public string effectPoolTag;
        public AudioClip soundCue;
    }
}
