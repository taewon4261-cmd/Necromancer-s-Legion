// File: Assets/Necromancer/01.Scripts/Characters/UnitBase.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 게임 내 모든 유닛(플레이어, 적, 미니언)의 공통 기반 속성 및 행위 정의
/// 추후 Entity 패턴 등의 확장성을 고려하여 추상 클래스로 설계
/// </summary>
    public abstract class UnitBase : MonoBehaviour, IDamageable
    {
        // [IDamageable Implementation]
        public UnitBase Unit => this;
        public void ApplyDamage(float damage) => TakeDamage(damage);

        [Header("Base Stats")]
        public float maxHp = 50f;
        public float currentHp;
        public float moveSpeed = 3f;
        
        [Header("Visual Components")]
        [Header("Visual Components (Assign in Inspector)")]
        [SerializeField] protected Animator unitAnimator;
        [SerializeField] protected SpriteRenderer unitSprite;

        [Header("Hit Effect Settings")]
        public Color hitColor = Color.red;
        public float hitDuration = 0.1f;

        public bool IsDead => isDead;
        protected bool isDead = false;
        private Coroutine hitFlashCoroutine;

        public event global::System.Action<float, float> OnHealthChanged; // (current, max)

        protected virtual void Awake()
        {
            // [Pure Inspector] 모든 핵심 참조는 인스펙터에서 사전에 완료되었습니다. (Zero-Search)
            currentHp = maxHp;
        }
        
        protected virtual void OnEnable()
        {
            isDead = false;
            currentHp = maxHp;
            if (unitSprite != null) unitSprite.color = Color.white;
            
            if (unitAnimator != null)
            {
                unitAnimator.updateMode = AnimatorUpdateMode.Normal;
                unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, false);
            }
            
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }



        protected virtual void Update()
        {
            // [REDUNDANT] HP 0 체크는 이제 TakeDamage에서 수행하므로 Update에서 제거
        }

        public virtual void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHp -= damage;
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
            
            // 피격 반짝임 연출 (이미지 없는 경우 대응)
            if (gameObject.activeInHierarchy)
            {
                if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
            }

            OnHealthChanged?.Invoke(currentHp, maxHp);

            if (currentHp <= 0)
            {
                Die();
            }
        }

        private IEnumerator HitFlashRoutine()
        {
            if (unitSprite == null) yield break;

            unitSprite.color = hitColor;
            yield return new WaitForSeconds(hitDuration);
            unitSprite.color = Color.white;
            hitFlashCoroutine = null;
        }

        protected virtual void Die()
        {
            isDead = true;
            if (unitAnimator != null)
            {
                unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, true);
            }
        }
    }
}
