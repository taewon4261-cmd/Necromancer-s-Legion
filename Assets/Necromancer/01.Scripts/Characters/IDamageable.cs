
namespace Necromancer
{
    /// <summary>
    /// [Interface Layer] 데미지를 입을 수 있는 모든 유닛의 공통 인터페이스.
    /// GetComponent<UnitBase>와 같은 무거운 런타임 탐색을 방지하기 위해 사용됩니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 실제 데이터 및 피격 로직을 가진 UnitBase 핵심 참조
        /// </summary>
        UnitBase Unit { get; }

        /// <summary>
        /// 데미지 처리 대행 함수 (넉백 및 AI 반응을 위한 공격자 정보 포함)
        /// </summary>
        void ApplyDamage(float damage, UnitBase attacker = null);
        
        /// <summary>
        /// 유닛의 사망 여부 (최적화 타겟팅용)
        /// </summary>
        bool IsDead { get; }
    }
}
