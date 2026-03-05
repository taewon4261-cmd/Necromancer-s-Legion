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
        WeaponPowerUp, // 본체 기본 무기 강화 (데미지, 크기 등)
        MinionAura,    // 미니언 전체 스탯 펌핑 버프
        OrbitalShield, // 본체 주위를 공전하는 보호막 마법 추가
        // 추후 기획서 내 20종 스킬에 맞춰 타입을 더 세분화합니다.
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
