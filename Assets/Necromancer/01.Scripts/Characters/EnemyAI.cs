using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적(인간 기사/농부) 유닛의 공통 AI
/// 플레이어를 무조건 쫓아가며, 사망 시 조상 클래스(UnitBase)의 Die()를 오버라이드하여 부활 로직 호출
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : UnitBase
{
    [Header("Enemy Settings")]
    [Tooltip("플레이어에게 닿았을 때 입히는 데미지")]
    public float attackDamage = 10f;
    
    [Tooltip("공격 쿨타임 (연속 타격 방지)")]
    public float hitCooldown = 0.5f;

    private Transform targetPlayer;
    private Rigidbody2D rb;
    private float lastHitTime;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
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

    /// <summary>
    /// 적 사망 시 부활 시스템 호출 (가장 핵심적인 오버라이드)
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        rb.velocity = Vector2.zero;
        
        // 🔮 1. 뼈대 목표: 내가 여기서 죽었으니 부활(해골 생성) 주사위를 굴려달라고 중앙 통제실(GameManager)에 요청
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TryReviveAsMinion(transform.position);
        }

        // 2. 사망 연출 (TODO: 폭발 파티클, 피 흘림 등)
        
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
