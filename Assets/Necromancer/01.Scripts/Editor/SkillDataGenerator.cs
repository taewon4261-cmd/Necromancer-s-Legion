#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Necromancer
{
    /// <summary>
    /// 기획서에 명시된 20종의 스킬 데이터를 유니티 에디터 상에서 일괄 자동 생성해주는 유틸리티 스크립트입니다.
    /// ScriptableObject 에셋을 하나씩 우클릭으로 만들고 값을 넣는 반복 작업을 제거하기 위해 작성합니다.
    /// </summary>
    public class SkillDataGenerator
    {
        [MenuItem("Necromancer/Generate 20 Skill SO Data")]
        public static void GenerateSkills()
        {
            // 데이터가 생성될 대상 폴더 경로
            string path = "Assets/Necromancer/02.Data";
            
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/Necromancer", "02.Data");
            }

            // --- [본체 생존 및 유틸 계열] ---
            CreateSkill(path, "01_ScytheUpgrade", "사신의 낫", "본체 기본 공격 데미지 및 사거리 10% 증가", SkillType.ScytheUpgrade, 0);
            CreateSkill(path, "02_SoulMagnet", "영혼 자석", "경험치 보석 획득 반경 20% 증가", SkillType.SoulMagnet, 1);
            CreateSkill(path, "03_LightStep", "가벼운 발걸음", "본체 이동 속도 10% 증가", SkillType.LightStep, 2);
            CreateSkill(path, "04_SturdySkeleton", "강인한 골격", "본체 최대 체력 20 증가", SkillType.SturdySkeleton, 3);
            CreateSkill(path, "05_RegeneratingBone", "재생하는 뼈", "5초마다 본체 체력 1씩 지속 회복", SkillType.RegeneratingBone, 4);
            CreateSkill(path, "06_AuraOfDeath", "죽음의 오라", "본체 주변에 원형 지속 데미지 존(장판) 생성", SkillType.AuraOfDeath, 5);
            CreateSkill(path, "07_PhantomEvasion", "환영 회피", "피격 시 10% 확률로 데미지 무시 (회피율)", SkillType.PhantomEvasion, 6);

            // --- [군단 유틸 및 방어 계열] ---
            CreateSkill(path, "08_Leadership", "통솔력", "플레이어가 동시에 유지할 수 있는 미니언 숫자 제한 5마리 증가", SkillType.Leadership, 7);
            CreateSkill(path, "09_ToughHide", "질긴 가죽", "내 미니언의 최대 체력 20% 증가", SkillType.ToughHide, 8);
            CreateSkill(path, "10_SwiftMarch", "신속한 진군", "내 미니언의 이동 및 돌진 속도 15% 증가", SkillType.SwiftMarch, 9);
            CreateSkill(path, "11_EchoOfResurrection", "부활의 메아리", "적 처치 시 미니언으로 부활할 베이스 확률 10% 상승", SkillType.EchoOfResurrection, 10);
            CreateSkill(path, "12_VampiricTeeth", "흡혈의 이빨", "미니언이 적을 타격할 때 1% 확률로 플레이어(본체) 체력 1 회복", SkillType.VampiricTeeth, 11);
            CreateSkill(path, "13_ChainExplosion", "연쇄 폭발 (폭탄병화)", "미니언 사망 시 주변 좁은 범위에 광역 폭발(10 데미지) 발생", SkillType.ChainExplosion, 12);

            // --- [군단 공격 및 상태이상 계열] ---
            CreateSkill(path, "14_BoneGrindingStrike", "뼈 깎는 일격", "모든 미니언 공격력 15% 증가", SkillType.BoneGrindingStrike, 13);
            CreateSkill(path, "15_ToxicBlade", "독성 칼날", "미니언 타격 시 적에게 3초간 초당 2의 독(지속) 데미지 부여", SkillType.ToxicBlade, 14);
            CreateSkill(path, "16_FrostDippedWeapon", "서리 맺힌 무기", "미니언 타격 시 2초간 상대 적의 이동속도 30% 감소", SkillType.FrostDippedWeapon, 15);
            CreateSkill(path, "17_BloodFrenzy", "피의 광란", "미니언 체력이 50% 이하가 되면 공격 속도 30% 펌핑", SkillType.BloodFrenzy, 16);
            CreateSkill(path, "18_PiercingBone", "관통 뼈대", "(궁수 미니언 한정) 발사하는 화살 투사체가 적 1명 추가로 관통", SkillType.PiercingBone, 17);
            CreateSkill(path, "19_CursedStigma", "저주받은 낙인", "미니언에게 10대 이상 맞은 적은 받는 데미지 20% 영구 증폭", SkillType.CursedStigma, 18);
            CreateSkill(path, "20_GiantHunter", "거인 사냥꾼", "보스 몬스터 및 정예 몬스터에게 미니언이 입히는 데미지 30% 상승", SkillType.GiantHunter, 19);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("20종의 스킬 데이터(SO)가 성공적으로 02.Data 경로에 생성되었습니다!");
        }

        private static void CreateSkill(string path, string assetName, string skillName, string description, SkillType type, int id)
        {
            string fullPath = $"{path}/Skill_{assetName}.asset";
            
            // 기존 에셋이 있는지 확인
            SkillData existingData = AssetDatabase.LoadAssetAtPath<SkillData>(fullPath);
            if (existingData != null)
            {
                // 이미 존재하면 텍스트와 값만 덮어씌움
                existingData.skillName = skillName;
                existingData.skillDescription = description;
                existingData.type = type;
                existingData.skillID = id;
                EditorUtility.SetDirty(existingData);
                return;
            }

            // 새 에셋 생성 (ScriptableObject)
            SkillData newData = ScriptableObject.CreateInstance<SkillData>();
            newData.skillName = skillName;
            newData.skillDescription = description;
            newData.type = type;
            newData.skillID = id;

            AssetDatabase.CreateAsset(newData, fullPath);
        }
    }
}
#endif
