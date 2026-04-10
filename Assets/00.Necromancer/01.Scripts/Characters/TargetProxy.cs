
using UnityEngine;

namespace Necromancer
{
    /// <summary>
    /// [Zero-Search Architecture] 
    /// 콜라이더가 붙은 오브젝트에 부착하여, 실질적인 피격 판정 대상(UnitBase)을 즉각 연결해주는 프록시 클래스입니다.
    /// GetComponentInParent 등의 무거운 탐색을 제거하기 위해 사용됩니다.
    /// </summary>
    public class TargetProxy : MonoBehaviour, IDamageable
    {
        [Header("Direct Reference")]
        [SerializeField] private UnitBase ownerUnit;

        public UnitBase Unit => ownerUnit;

        public void Setup(UnitBase owner)
        {
            ownerUnit = owner;
        }

        public void ApplyDamage(float damage, UnitBase attacker = null)
        {
            if (ownerUnit != null) ownerUnit.TakeDamage(damage, attacker);
        }

        public bool IsDead => ownerUnit != null && ownerUnit.IsDead;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 자동으로 본인이나 부모에게서 UnitBase를 찾아 할당 (개발 편의성)
            if (ownerUnit == null) ownerUnit = GetComponentInParent<UnitBase>();
        }
#endif
    }
}
