// File: Assets/Necromancer/01.Scripts/Data/SkillData.cs
using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// 인게임 레벨업 시 등장하는 20종 스킬의 종류와 스탯을 직렬화하여 설계하는 데이터 클래스.
    /// ScriptableObject를 상속받아 에셋 창에서 데이터를 직접 생성 및 수정합니다.
    /// </summary>
    public enum SkillType
    {
        // --- [본체 생존 및 유틸 계열] ---
        ScytheUpgrade,      // 1. 사신의 낫 (기본 무기)
        SoulMagnet,         // 2. 영혼 자석
        LightStep,          // 3. 가벼운 발걸음
        SturdySkeleton,     // 4. 강인한 골격
        RegeneratingBone,   // 5. 재생하는 뼈
        AuraOfDeath,        // 6. 죽음의 오라
        PhantomEvasion,     // 7. 환영 회피

        // --- [군단 유틸 및 방어 계열] ---
        Leadership,         // 8. 통솔력
        ToughHide,          // 9. 질긴 가죽
        SwiftMarch,         // 10. 신속한 진군
        EchoOfResurrection, // 11. 부활의 메아리
        VampiricTeeth,      // 12. 흡혈의 이빨
        ChainExplosion,     // 13. 연쇄 폭발 (폭탄병화)

        // --- [군단 공격 및 상태이상 계열] ---
        BoneGrindingStrike, // 14. 뼈 깎는 일격
        ToxicBlade,         // 15. 독성 칼날
        FrostDippedWeapon,  // 16. 서리 맺힌 무기
        BloodFrenzy,        // 17. 피의 광란
        PiercingBone,       // 18. 관통 뼈대 (궁수 전용)
        CursedStigma,       // 19. 저주받은 낙인
        GiantHunter         // 20. 거인 사냥꾼
    }

    [CreateAssetMenu(fileName = "New Skill Data", menuName = "Necromancer/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("UI Info")]
        [Tooltip("인게임 스킬 뽑기 창에 표시될 이름")]
        public string skillName;
        
        [Tooltip("인게임 스킬 뽑기 창에 표시될 아이콘. 임시로 Sprite 없음 허용")]
        public Sprite skillIcon;
        
        [TextArea(3, 5)]
        [Tooltip("스킬 효과에 대한 UI 설명 박스 텍스트")]
        public string skillDescription;

        [Header("Skill Properties")]
        [Tooltip("이 스킬이 플레이어 본체용인지, 군단 버프용인지, 신규 마법인지 구별하는 타입")]
        public SkillType type;
        
        [Tooltip("이 스킬 에셋의 고유 식별 번호. 로직 매칭용 ID (0~19)")]
        public int skillID;

        [Header("Stats Upgrade Values")]
        [Tooltip("데미지 퍼센트 등 증가 수치 (1.1f = 10% 증가 등)")]
        public float value1;
    }
}
