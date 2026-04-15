#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Necromancer
{
    /// <summary>
    /// 기획서의 20종 스킬 데이터를 일괄 생성하고, 규칙에 맞는 아이콘(Sprite)을 자동 바인딩합니다.
    /// 파일명 매칭 규칙: "Skill_XX_..." 패턴을 찾아 해당 번호의 SO에 할당합니다.
    /// </summary>
    public class SkillDataGenerator
    {
        private const string DATA_PATH = "Assets/00.Necromancer/02.Data/SkillSO";
        private const string ICON_PATH = "Assets/00.Necromancer/04.Sprites/SkillIcons";

        [MenuItem("Necromancer/Generate 20 Skill SO Data")]
        public static void GenerateSkills()
        {
            if (!AssetDatabase.IsValidFolder(DATA_PATH))
            {
                // 부모 폴더 확인 및 생성
                if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer/02.Data"))
                    AssetDatabase.CreateFolder("Assets/00.Necromancer", "02.Data");
                
                AssetDatabase.CreateFolder("Assets/00.Necromancer/02.Data", "SkillSO");
            }

            // 기획서 기반 20종 데이터 정의
            CreateOrUpdateSkill("01_ScytheUpgrade", "사신의 낫", "본체 기본 공격 데미지 및 사거리 10% 증가", SkillType.ScytheUpgrade, 0);
            CreateOrUpdateSkill("02_SoulMagnet", "영혼 자석", "경험치 보석 획득 반경 20% 증가", SkillType.SoulMagnet, 1);
            CreateOrUpdateSkill("03_LightStep", "가벼운 발걸음", "본체 이동 속도 10% 증가", SkillType.LightStep, 2);
            CreateOrUpdateSkill("04_SturdySkeleton", "강인한 골격", "본체 최대 체력 20 증가", SkillType.SturdySkeleton, 3);
            CreateOrUpdateSkill("05_RegeneratingBone", "재생하는 뼈", "5초마다 본체 체력 1씩 지속 회복", SkillType.RegeneratingBone, 4);
            CreateOrUpdateSkill("06_AuraOfDeath", "죽음의 오라", "본체 주변에 원형 지속 데미지 존 생성", SkillType.AuraOfDeath, 5);
            CreateOrUpdateSkill("07_PhantomEvasion", "환영 회피", "피격 시 10% 확률로 데미지 무시", SkillType.PhantomEvasion, 6);
            CreateOrUpdateSkill("08_Leadership", "통솔력", "미니언 최대 유지 수 제한 5마리 증가", SkillType.Leadership, 7);
            CreateOrUpdateSkill("09_ToughHide", "질긴 가죽", "내 미니언의 최대 체력 20% 증가", SkillType.ToughHide, 8);
            CreateOrUpdateSkill("10_SwiftMarch", "신속한 진군", "내 미니언의 이동 및 돌진 속도 15% 증가", SkillType.SwiftMarch, 9);
            CreateOrUpdateSkill("11_EchoOfResurrection", "부활의 메아리", "적 처치 시 미니언 부활 확률 10% 상승", SkillType.EchoOfResurrection, 10);
            CreateOrUpdateSkill("12_VampiricTeeth", "흡혈의 이빨", "미니언 타격 시 1% 확률로 본체 체력 1 회복", SkillType.VampiricTeeth, 11);
            CreateOrUpdateSkill("13_ChainExplosion", "연쇄 폭발", "미니언 사망 시 주변 광역 폭발 발생", SkillType.ChainExplosion, 12);
            CreateOrUpdateSkill("14_BoneGrindingStrike", "뼈 깎는 일격", "모든 미니언 공격력 15% 증가", SkillType.BoneGrindingStrike, 13);
            CreateOrUpdateSkill("15_ToxicBlade", "독성 칼날", "미니언 타격 시 적에게 지속 독 데미지 부여", SkillType.ToxicBlade, 14);
            CreateOrUpdateSkill("16_FrostDippedWeapon", "서리 맺힌 무기", "미니언 타격 시 상대 적의 이동속도 30% 감소", SkillType.FrostDippedWeapon, 15);
            CreateOrUpdateSkill("17_BloodFrenzy", "피의 광란", "미니언 체력 50% 이하 시 공격 속도 30% 증가", SkillType.BloodFrenzy, 16);
            CreateOrUpdateSkill("18_PiercingBone", "관통 뼈대", "궁수 투사체가 적 1명을 추가로 관통", SkillType.PiercingBone, 17);
            CreateOrUpdateSkill("19_CursedStigma", "저주받은 낙인", "많이 맞은 적이 받는 데미지 20% 증폭", SkillType.CursedStigma, 18);
            CreateOrUpdateSkill("20_GiantHunter", "거인 사냥꾼", "보스/정예 대상 미니언 데미지 30% 상승", SkillType.GiantHunter, 19);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[SkillGenerator]</b></color> 20종 스킬 데이터 및 아이콘 바인딩 완료!");
        }

        private static void CreateOrUpdateSkill(string assetName, string skillName, string desc, SkillType type, int id)
        {
            string fullPath = $"{DATA_PATH}/Skill_{assetName}.asset";
            SkillData data = AssetDatabase.LoadAssetAtPath<SkillData>(fullPath);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<SkillData>();
                AssetDatabase.CreateAsset(data, fullPath);
            }

            // 기본 정보 세팅
            data.skillName = skillName;
            data.skillDescription = desc;
            data.type = type;
            data.skillID = id;

            // 아이콘 자동 찾기: assetName(예: 01_ScytheUpgrade)이 포함된 파일을 ICON_PATH에서 검색
            string[] guids = AssetDatabase.FindAssets(assetName, new[] { ICON_PATH });
            if (guids.Length > 0)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                data.skillIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            }
            else
            {
                Debug.LogWarning($"[SkillGenerator] 아이콘을 찾을 수 없음: {assetName}");
            }

            EditorUtility.SetDirty(data);
        }
    }
}
#endif
