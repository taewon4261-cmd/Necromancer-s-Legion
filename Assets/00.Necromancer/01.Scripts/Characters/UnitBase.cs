// File: Assets/Necromancer/01.Scripts/Characters/UnitBase.cs
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 게임 내 모든 유닛(플레이어, 적, 미니언)의 공통 기반 속성 및 행위 정의
/// 추후 Entity 패턴 등의 확장성을 고려하여 추상 클래스로 설계
/// </summary>
    public abstract class UnitBase : MonoBehaviour, IDamageable
    {
        // [ARCHITECT] 중앙 집중형 업데이트 매니저 및 격자 분할을 위한 보조 데이터
        [HideInInspector] public Vector2Int CurrentGridPos = new Vector2Int(-999, -999);
        [HideInInspector] public Vector3 LastGridUpdatePos = new Vector3(-9999, -9999, -9999);
        [HideInInspector] public bool IsStunned = false;
        
        protected List<IUnitModifier> activeModifiers = new List<IUnitModifier>();
        // [IDamageable Implementation]
        public UnitBase Unit => this;
        public virtual void ApplyDamage(float damage, UnitBase attacker = null) => TakeDamage(damage, attacker);

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
        private CancellationTokenSource _poisonFlashCts;

        public event global::System.Action<float, float> OnHealthChanged; // (current, max)

        protected virtual void Awake()
        {
            // [Pure Inspector] 모든 핵심 참조는 인스펙터에서 사전에 완료되었습니다. (Zero-Search)
            currentHp = maxHp;
        }
        
        protected virtual void OnEnable()
        {
            isDead = false;
            IsStunned = false;
            currentHp = maxHp;
            activeModifiers.Clear();
            
            if (unitSprite != null) unitSprite.color = Color.white;
            
            if (unitAnimator != null)
            {
                unitAnimator.updateMode = AnimatorUpdateMode.Normal;
                unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, false);
            }
            
            // [ARCHITECT] 중앙 관리자 등록
            if (GameManager.Instance != null && GameManager.Instance.unitManager != null) 
                GameManager.Instance.unitManager.RegisterUnit(this);
            
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }

        protected virtual void OnDisable()
        {
            // [ARCHITECT] 중앙 관리자 해제
            if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
                GameManager.Instance.unitManager.UnregisterUnit(this);

            // 독 반짝임 취소
            _poisonFlashCts?.Cancel();
            _poisonFlashCts?.Dispose();
            _poisonFlashCts = null;
        }

        /// <summary>
        /// [CENTRALIZED] UnitManager에서 매 프레임마다 호출하는 수동 업데이트.
        /// Native-C# 전환 오버헤드를 절감합니다.
        /// </summary>
        public virtual void ManualUpdate(float deltaTime)
        {
            if (isDead) return;
            
            // Modifier 업데이트 (스킬 효과 탈중앙화)
            for (int i = activeModifiers.Count - 1; i >= 0; i--)
            {
                var mod = activeModifiers[i];
                mod.OnUpdate(this, deltaTime);
                if (mod.IsExpired)
                {
                    mod.OnRemove(this);
                    activeModifiers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// [CENTRALIZED] UnitManager에서 물리 타이밍에 호출하는 수동 업데이트.
        /// </summary>
        public virtual void ManualFixedUpdate(float fixedDeltaTime)
        {
        }

        /// <summary>
        /// [IDAMAGEABLE] 데미지 스택 및 사망 연출 (attacker: 넉백 방향 등에 활용)
        /// </summary>
        public virtual void TakeDamage(float damage, UnitBase attacker = null)
        {
            if (isDead) return;

            // [SKILL: Cursed Stigma] 낙인 효과 확인 및 데미지 증폭 적용
            float finalDamage = damage;
            for (int i = 0; i < activeModifiers.Count; i++)
            {
                if (activeModifiers[i] is StigmaModifier stigma)
                {
                    finalDamage *= stigma.GetDamageMultiplier();
                    break; 
                }
            }

            currentHp -= finalDamage;
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

        // 독(Poison) 틱마다 호출되는 초록 반짝임 (UniTask)
        public void TriggerPoisonFlash()
        {
            if (unitSprite == null) return;
            _poisonFlashCts?.Cancel();
            _poisonFlashCts?.Dispose();
            _poisonFlashCts = new CancellationTokenSource();
            PoisonFlashAsync(_poisonFlashCts.Token).Forget();
        }

        private async UniTaskVoid PoisonFlashAsync(CancellationToken ct)
        {
            var green = new Color(0.2f, 1f, 0.2f);
            try
            {
                // 반짝×2 효과
                unitSprite.color = green;
                await UniTask.Delay(100, cancellationToken: ct);
                unitSprite.color = Color.white;
                await UniTask.Delay(70, cancellationToken: ct);
                unitSprite.color = green;
                await UniTask.Delay(100, cancellationToken: ct);
                unitSprite.color = Color.white;
            }
            catch (System.OperationCanceledException)
            {
                if (unitSprite != null) unitSprite.color = Color.white;
            }
        }

        protected virtual void Die()
        {
            isDead = true;
            // 사망 시 모든 모디파이어 즉시 해제
            for (int i = 0; i < activeModifiers.Count; i++) activeModifiers[i].OnRemove(this);
            activeModifiers.Clear();

            if (unitAnimator != null)
            {
                unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_Die, true);
            }
        }

        public void AddModifier(IUnitModifier modifier)
        {
            if (modifier == null || isDead) return;
            
            // [STACKING LOGIC] 동일한 ModifierId가 있다면 기존 모디파이어를 갱신하거나 합침
            for (int i = 0; i < activeModifiers.Count; i++)
            {
                if (activeModifiers[i].ModifierId == modifier.ModifierId)
                {
                    // Stigma 같은 중첩형 모디파이어 대응
                    if (activeModifiers[i] is StigmaModifier stigma)
                    {
                        stigma.AddStack();
                        return;
                    }
                    
                    // 기간 갱신형 (Poison, Frost 등) - 기존 것 제거 후 새 것 적용 또는 Refresh 메서드 도입 가능
                    // 여기서는 유연성을 위해 기존 것을 제거하고 새로 적용함 (OnRemove/OnApply 호출 보장)
                    activeModifiers[i].OnRemove(this);
                    activeModifiers.RemoveAt(i);
                    break;
                }
            }

            activeModifiers.Add(modifier);
            modifier.OnApply(this);
        }
    }
}
