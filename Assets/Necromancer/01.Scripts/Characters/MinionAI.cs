// File: Assets/Necromancer/01.Scripts/Characters/MinionAI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{
/// <summary>
/// 적의 시체에서 부활한 아군 해골 미니언 AI
/// 살아남은 적(Enemy)들 중 가장 가까운 대상을 찾아내어 공격하며, 수명이 다하면 소멸합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MinionAI : UnitBase
{
    [Header("Minion Settings")]
    [Tooltip("적에게 닿았을 때 입히는 데미지")]
    public float attackDamage = 15f;
    
    [Tooltip("소멸하기 전까지 유지되는 생존 시간 (초 단위)")]
    public float lifeTime = 10f;
    
    [Tooltip("타겟을 새로 갱신하는 주기 (최적화용)")]
    public float targetScanRate = 0.5f;
    
    [Tooltip("공격 쿨타임")]
    public float hitCooldown = 0.5f;

    private Rigidbody2D rb;
    private Transform currentTarget;
    private float lastHitTime;
    private float spawnTime;
    
    // UniTask 취소 토큰
    private CancellationTokenSource scanCts;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        spawnTime = Time.time;
        currentTarget = null;
        
        // 기존 토큰 초기화
        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = new CancellationTokenSource();
        
        // 스폰 즉시 주변 적 탐색 UniTask 가동
        ScanForTargetAsync(scanCts.Token).Forget();
    }

    private void OnDisable()
    {
        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = null;
    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;

        // 수명 체크 로직
        if (Time.time > spawnTime + lifeTime)
        {
            Die(); // 수명이 다하면 스스로 소멸
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        
        ChaseTarget();
    }

    /// <summary>
    /// 매 프레임 Vector3.Distance를 돌리는 최악의 사태(O(N^2))를 방지하기 위해, 지정된 0.5초 주기로만 타겟을 찾습니다. (UniTask 적용)
    /// </summary>
    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        while (!isDead && !token.IsCancellationRequested)
        {
            // 1주차 프로토타입용 타겟 탐색 (FindGameObjectsWithTag)
            // 추후 최적화: EnemySpawner가 살아있는 적의 리스트를 들고 있게 처리
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            
            float closestDistance = Mathf.Infinity;
            Transform closestEnemy = null;

            foreach (GameObject enemyObj in enemies)
            {
                // 꺼져있는 시체를 쫓아가지 않도록 보호
                if (!enemyObj.activeInHierarchy) continue;

                float distance = Vector2.Distance(transform.position, enemyObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemyObj.transform;
                }
            }

            currentTarget = closestEnemy;

            // 다음 스캔 주기까지 대기
            await UniTask.Delay(System.TimeSpan.FromSeconds(targetScanRate), cancellationToken: token);
        }
    }

    /// <summary>
    /// 찾은 타겟을 향해 이동
    /// </summary>
    private void ChaseTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            rb.velocity = Vector2.zero; // 타겟이 없으면 대기 (또는 오라 부근으로 복귀 로직 추가 가능)
            return;
        }

        Vector2 direction = (currentTarget.position - transform.position).normalized;
        rb.velocity = direction * moveSpeed;
    }

    /// <summary>
    /// 물리 충돌 판정 (적 타격 로직)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 적인지 태그로 확인
        if (collision.gameObject.CompareTag("Enemy"))
        {
            UnitBase targetUnit = collision.gameObject.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                // 적 타격 성공!
                targetUnit.TakeDamage(attackDamage);
                lastHitTime = Time.time;
                
                // TODO: 뼈 부딪히는 타격 사운드 호출
            }
        }
    }

    /// <summary>
    /// 미니언 사망 또는 수명 만료 처리
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        rb.velocity = Vector2.zero;
        
        // TODO: 미니언 특유의 소멸 파티클 (가루가 되는 연출)
        
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            GameManager.Instance.poolManager.Release("Minion", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
}
