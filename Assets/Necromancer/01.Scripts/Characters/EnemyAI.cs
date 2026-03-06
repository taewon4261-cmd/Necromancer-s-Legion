// File: Assets/Necromancer/01.Scripts/Characters/EnemyAI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 적(인간 기사/농부) 유닛의 공통 AI
/// 플레이어를 무조건 쫓아가며, 사망 시 조상 클래스(UnitBase)의 Die()를 오버라이드하여 부활 로직 호출
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : UnitBase
{
    [Header("Enemy Data (SO)")]
    [Tooltip("에디터에서 미리 설정해둔 적의 스탯 데이터를 할당합니다.")]
    public EnemyData data;

    [Header("Enemy Settings (Legacy)")]
    [Tooltip("플레이어에게 닿았을 때 입히는 데미지")]
    public float attackDamage = 10f;
    
    [Tooltip("공격 쿨타임 (연속 타격 방지)")]
    public float hitCooldown = 0.5f;

    private Transform targetPlayer;
    private Rigidbody2D rb;
    private float lastHitTime;

    // --- [스킬 연동: 상태이상 디버프 변수들] ---
    private float originalMoveSpeed;
    private Coroutine poisonCoroutine;
    private Coroutine frostCoroutine;
    private int stigmaStacks = 0;

    protected override void Awake()
    {
        // 1. 가져온 데이터 기반으로 내 스탯 즉각 덮어쓰기
        if (data != null)
        {
            this.maxHp = data.maxHp;
            this.moveSpeed = data.moveSpeed;
            this.attackDamage = data.attackDamage;
        }

        base.Awake(); // 이 시점에 UnitBase 내부에서 currentHp = maxHp 실행됨
        rb = GetComponent<Rigidbody2D>();
        originalMoveSpeed = moveSpeed;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        moveSpeed = originalMoveSpeed;
        stigmaStacks = 0;
        
        // 🚨 최적화: 무거운 GameObject.Find 연산을 제거하고, GameManager를 통해 플레이어 위치를 직접 참조합니다.
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayer = GameManager.Instance.playerTransform;
        }
        else
        {
            Debug.LogWarning("[EnemyAI] GameManager.Instance.playerTransform이 할당되지 않았습니다!");
        }
    }

    // --- [스킬 연동 로직 시작] ---
    public void ApplyPoison(float duration, float tickDamage)
    {
        if (poisonCoroutine != null) StopCoroutine(poisonCoroutine);
        poisonCoroutine = StartCoroutine(PoisonRoutine(duration, tickDamage));
    }

    private IEnumerator PoisonRoutine(float duration, float tickDamage)
    {
        float timer = 0f;
        while (timer < duration && !isDead)
        {
            TakeDamage(tickDamage);
            timer += 1f;
            yield return new WaitForSeconds(1f);
        }
    }

    public void ApplyFrost(float duration, float slowdownRatio)
    {
        if (frostCoroutine != null) StopCoroutine(frostCoroutine);
        frostCoroutine = StartCoroutine(FrostRoutine(duration, slowdownRatio));
    }

    private IEnumerator FrostRoutine(float duration, float slowdownRatio)
    {
        moveSpeed = originalMoveSpeed * (1f - slowdownRatio);
        yield return new WaitForSeconds(duration);
        if(!isDead) moveSpeed = originalMoveSpeed;
    }

    public void AddStigmaStack()
    {
        stigmaStacks++;
        if (stigmaStacks >= 10)
        {
            stigmaStacks = 0; // 초기화
            // [스킬 효과] 저주받은 낙인: 10스택 시 잃은 체력 비례 또는 고정 피해
            TakeDamage(maxHp * 0.2f); 
            // Debug.Log("[SkillEffect] 저주받은 낙인 10스택 폭발!");
        }
    }
    // --- [스킬 연동 로직 끝] ---

    private void FixedUpdate()
    {
        if (isDead || targetPlayer == null) return;
        
        ChasePlayer();
    }

    /// <summary>
    /// 플레이어를 향해 직선 이동 (최적화를 위해 A* 대신 단순 방향 벡터 사용)
    /// </summary>
    private void ChasePlayer()
    {
        // 1. 방향 구하기 (목표지점 - 내위치)
        Vector2 direction = (targetPlayer.position - transform.position).normalized;
        
        // 2. 물리적 이동
        rb.velocity = direction * moveSpeed;
    }

    /// <summary>
    /// 물리 충돌 판정 (플레이어 타격 로직)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        // 최적화: 내가 죽었거나 쿨타임 중이면 연산 스킵
        if (isDead || Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 플레이어인지 태그로 확인
        if (collision.gameObject.CompareTag("Player"))
        {
            UnitBase targetUnit = collision.gameObject.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                // 타격 성공
                targetUnit.TakeDamage(attackDamage);
                lastHitTime = Time.time; // 쿨타임 초기화
                
                // TODO: 플레이어 피격 이펙트/사운드 호출
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        base.TakeDamage(damage);

        // [Polishing] 타격 연출 실행 (텍스트 출력 중단)
        if (FeedbackManager.Instance != null)
        {
            // 엘리트 적일 경우만 카메라 흔들림 유지
            if (data != null && data.isElite)
                FeedbackManager.Instance.ShakeCamera(0.12f, 0.15f);
        }
    }

    /// <summary>
    /// 적 사망 시 보석 드랍 및 부활 시스템 호출
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        if (poisonCoroutine != null) StopCoroutine(poisonCoroutine);
        if (frostCoroutine != null) StopCoroutine(frostCoroutine);
        
        rb.velocity = Vector2.zero;
        
        if (GameManager.Instance != null)
        {
            // 💎 1. 죽은 자리에 경험치 보석(ExpGem) 확정 드랍
            if (GameManager.Instance.poolManager != null)
            {
                GameManager.Instance.poolManager.Get("ExpGem", transform.position, Quaternion.identity);
            }

            // 🔮 2. 부활 주사위 굴려달라고 중앙 통제실(GameManager)에 요청
            GameManager.Instance.TryReviveAsMinion(transform.position);
        }

        // 3. 사망 연출 (TODO: 폭발 파티클, 피 흘림 등)
        
        // 3. 내 시체 치우기 (Destroy 대신 풀매니저 반납)
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            // 인스펙터 풀 세팅에서 설정한 이름(예: "Enemy_Peasant")을 하드코딩하지 않고 본인의 (Clone) 뗀 이름으로 반환하는 게 좋으나,
            // 1주차 프로토타입이므로 직관적으로 "Enemy" 로 고정 반환 (EnemySpawner와 태그를 맞춰야 함)
            GameManager.Instance.poolManager.Release("Enemy", gameObject);
        }
        else
        {
            // 게임매니저가 없다면 어쩔 수 없이 임시 파괴
            Destroy(gameObject);
        }
    }
}
}
