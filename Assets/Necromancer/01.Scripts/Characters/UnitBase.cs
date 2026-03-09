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
    public abstract class UnitBase : MonoBehaviour
    {
        [Header("Base Stats")]
        public float maxHp = 50f;
        public float currentHp;
        public float moveSpeed = 3f;
        
        [Header("Visual Components")]
        public Animator animator;
        public SpriteRenderer spriteRenderer;

        [Header("Hit Effect Settings")]
        public Color hitColor = Color.red;
        public float hitDuration = 0.1f;

        protected bool isDead = false;
        private Coroutine hitFlashCoroutine;

        public event global::System.Action<float, float> OnHealthChanged; // (current, max)

        protected virtual void Awake()
        {
            currentHp = maxHp;
            // 자식 오브젝트나 본인에게서 컴포넌트를 자동으로 찾음
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        protected virtual void OnEnable()
        {
            isDead = false;
            currentHp = maxHp;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            
            if (animator != null)
            {
                animator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, false);
            }
            
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }

        protected virtual void Update()
        {
            if (!isDead && currentHp <= 0)
            {
                Die();
            }
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
            if (spriteRenderer == null) yield break;

            spriteRenderer.color = hitColor;
            yield return new WaitForSeconds(hitDuration);
            spriteRenderer.color = Color.white;
            hitFlashCoroutine = null;
        }

        protected virtual void Die()
        {
            isDead = true;
            if (animator != null)
            {
                animator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, true);
            }
        }
    }
}
