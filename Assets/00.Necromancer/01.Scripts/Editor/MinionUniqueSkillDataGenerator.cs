#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Necromancer.Data;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] MVP 미니언 고유 스킬 ScriptableObject를 생성하고 MinionUnlockSO에 연결합니다.
    /// 생성 위치: Assets/00.Necromancer/02.Data/MinionUniqueSkills
    /// </summary>
    public static class MinionUniqueSkillDataGenerator
    {
        private const string SKILL_DATA_PATH = "Assets/00.Necromancer/02.Data/MinionUniqueSkills";
        private const string MINION_DATA_PATH = "Assets/00.Necromancer/02.Data/Minions";

        [MenuItem("Necromancer/Generate Minion Unique Skill SOs")]
        public static void GenerateAll()
        {
            EnsureFolders();

            MinionUniqueSkillData warriorSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonWarrior_Slam",
                skillID: "SkeletonWarrior_Slam",
                skillName: "강타",
                description: "단일 대상에게 강한 피해를 주고 0.5초 동안 기절시킵니다.",
                cooldown: 5f,
                range: 1.6f,
                value: 1.5f,
                effectPoolTag: "VFX_SlamShockwave");

            MinionUniqueSkillData archerSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonArcher_Multishot",
                skillID: "SkeletonArcher_Multishot",
                skillName: "멀티샷",
                description: "현재 대상 방향으로 추가 화살 2발을 발사합니다.",
                cooldown: 6f,
                range: 6f,
                value: 1f,
                effectPoolTag: "VFX_ChargeSlash");

            MinionUniqueSkillData mageSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonMage_ChainLightning",
                skillID: "SkeletonMage_ChainLightning",
                skillName: "체인 라이트닝",
                description: "대상과 주변 적에게 최대 3회 전이되는 번개 피해를 줍니다.",
                cooldown: 7f,
                range: 5f,
                value: 1.2f,
                effectPoolTag: "VFX_ChainLightning");

            MinionUniqueSkillData warriorStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonWarrior_Charge",
                skillID: "SkeletonWarrior_Charge",
                skillName: "돌진",
                description: "타겟 방향으로 짧게 돌진해 경로 주변 적에게 피해를 줍니다.",
                cooldown: 17f,
                range: 3f,
                value: 1.8f,
                effectPoolTag: "VFX_ChargeSlash");

            MinionUniqueSkillData archerStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonArcher_FireArrow",
                skillID: "SkeletonArcher_FireArrow",
                skillName: "불화살",
                description: "타겟 위치 주변 적에게 3초 동안 지속 피해를 남깁니다.",
                cooldown: 19f,
                range: 2.2f,
                value: 0.45f,
                effectPoolTag: "VFX_FireZone");

            MinionUniqueSkillData mageStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonMage_Meteor",
                skillID: "SkeletonMage_Meteor",
                skillName: "메테오",
                description: "타겟 위치에 짧은 지연 후 폭발하는 운석을 떨어뜨립니다.",
                cooldown: 21f,
                range: 2.6f,
                value: 2.2f,
                effectPoolTag: "VFX_MeteorImpact");

            MinionUniqueSkillData wolfSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonWolf_Leap",
                skillID: "SkeletonWolf_Leap",
                skillName: "도약",
                description: "현재 타겟에게 순간 접근해 피해를 주고 짧게 둔화시킵니다.",
                cooldown: 7f,
                range: 5.5f,
                value: 1.4f,
                effectPoolTag: "VFX_ChargeSlash");

            MinionUniqueSkillData giantSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonGiant_Roar",
                skillID: "SkeletonGiant_Roar",
                skillName: "고함",
                description: "주변 적들의 이동 속도를 잠시 낮춰 군단을 보호합니다.",
                cooldown: 10f,
                range: 3f,
                value: 0.3f,
                effectPoolTag: "VFX_SlamShockwave");

            MinionUniqueSkillData knightSkill = CreateOrUpdate(
                fileName: "Skill_SkeletonKnight_Cleave",
                skillID: "SkeletonKnight_Cleave",
                skillName: "횡베기",
                description: "전방 가까운 적 최대 4명에게 광역 피해를 줍니다.",
                cooldown: 8f,
                range: 2.1f,
                value: 1.25f,
                effectPoolTag: "VFX_ChargeSlash");

            MinionUniqueSkillData wolfStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonWolf_GiantForm",
                skillID: "SkeletonWolf_GiantForm",
                skillName: "거대화",
                description: "6초 동안 몸집이 커지고 HP와 피해량이 증가합니다.",
                cooldown: 25f,
                range: 4f,
                value: 0.5f,
                effectPoolTag: "VFX_SlamShockwave");

            MinionUniqueSkillData giantStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonGiant_Berserk",
                skillID: "SkeletonGiant_Berserk",
                skillName: "광폭화",
                description: "5초 동안 공격 간격이 크게 감소합니다.",
                cooldown: 25f,
                range: 3f,
                value: 0.55f,
                effectPoolTag: "VFX_SlamShockwave");

            MinionUniqueSkillData knightStar5Skill = CreateOrUpdate(
                fileName: "Skill_SkeletonKnight_SummonWarriors",
                skillID: "SkeletonKnight_SummonWarriors",
                skillName: "전사 소환",
                description: "짧은 시간 동안 함께 싸우는 해골 전사 2기를 소환합니다.",
                cooldown: 30f,
                range: 3f,
                value: 2f,
                effectPoolTag: "VFX_SlamShockwave");

            LinkStar3Skill("SkeletonWarrior", warriorSkill);
            LinkStar3Skill("SkeletonArcher", archerSkill);
            LinkStar3Skill("SkeletonMage", mageSkill);
            LinkStar5Skill("SkeletonWarrior", warriorStar5Skill);
            LinkStar5Skill("SkeletonArcher", archerStar5Skill);
            LinkStar5Skill("SkeletonMage", mageStar5Skill);
            LinkStar3Skill("SkeletonWolf", wolfSkill);
            LinkStar3Skill("SkeletonGiant", giantSkill);
            LinkStar3Skill("SkeletonKnight", knightSkill);
            LinkStar5Skill("SkeletonWolf", wolfStar5Skill);
            LinkStar5Skill("SkeletonGiant", giantStar5Skill);
            LinkStar5Skill("SkeletonKnight", knightStar5Skill);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=gold><b>[MinionUniqueSkillGenerator]</b></color> MVP 3성 고유 스킬 생성 및 연결 완료.");
        }

        private static MinionUniqueSkillData CreateOrUpdate(
            string fileName,
            string skillID,
            string skillName,
            string description,
            float cooldown,
            float range,
            float value,
            string effectPoolTag)
        {
            string fullPath = $"{SKILL_DATA_PATH}/{fileName}.asset";
            MinionUniqueSkillData skill = AssetDatabase.LoadAssetAtPath<MinionUniqueSkillData>(fullPath);

            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<MinionUniqueSkillData>();
                AssetDatabase.CreateAsset(skill, fullPath);
            }

            skill.skillID = skillID;
            skill.skillName = skillName;
            skill.description = description;
            skill.cooldown = cooldown;
            skill.range = range;
            skill.value = value;
            skill.effectPoolTag = effectPoolTag;

            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static void LinkStar3Skill(string minionID, MinionUniqueSkillData skill)
        {
            if (skill == null) return;

            string[] guids = AssetDatabase.FindAssets("t:MinionUnlockSO", new[] { MINION_DATA_PATH });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MinionUnlockSO minion = AssetDatabase.LoadAssetAtPath<MinionUnlockSO>(path);
                if (minion == null || minion.minionID != minionID) continue;

                minion.maxStars = Mathf.Max(5, minion.maxStars);
                minion.star3Skill = skill;
                EditorUtility.SetDirty(minion);
                Debug.Log($"[MinionUniqueSkillGenerator] Linked {skill.skillID} -> {minion.name}.star3Skill");
                return;
            }

            Debug.LogWarning($"[MinionUniqueSkillGenerator] MinionUnlockSO not found for minionID: {minionID}");
        }

        private static void LinkStar5Skill(string minionID, MinionUniqueSkillData skill)
        {
            if (skill == null) return;

            string[] guids = AssetDatabase.FindAssets("t:MinionUnlockSO", new[] { MINION_DATA_PATH });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MinionUnlockSO minion = AssetDatabase.LoadAssetAtPath<MinionUnlockSO>(path);
                if (minion == null || minion.minionID != minionID) continue;

                minion.maxStars = Mathf.Max(5, minion.maxStars);
                minion.star5Skill = skill;
                EditorUtility.SetDirty(minion);
                Debug.Log($"[MinionUniqueSkillGenerator] Linked {skill.skillID} -> {minion.name}.star5Skill");
                return;
            }

            Debug.LogWarning($"[MinionUniqueSkillGenerator] MinionUnlockSO not found for minionID: {minionID}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer"))
                AssetDatabase.CreateFolder("Assets", "00.Necromancer");

            if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer/02.Data"))
                AssetDatabase.CreateFolder("Assets/00.Necromancer", "02.Data");

            if (!AssetDatabase.IsValidFolder(SKILL_DATA_PATH))
                AssetDatabase.CreateFolder("Assets/00.Necromancer/02.Data", "MinionUniqueSkills");
        }
    }
}
#endif
