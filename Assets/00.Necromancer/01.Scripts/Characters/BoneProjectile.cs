// File: Assets/Necromancer/01.Scripts/Characters/BoneProjectile.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{

/// <summary>
/// 네크로맨서가 쏘는 기본 뼈다귀 투사체 마법
/// [ARCHITECT] UnitBase를 상속받아 UnitManager의 중앙 업데이트 시스템에 편입되었습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class BoneProjectile : UnitBase
{
    [Header("Projectile Stats")]
    public float damage = 20f;
    public float speed = 10f;
    public float lifeTime = 3f;

    private Rigidbody2D rb;
    private CancellationTokenSource projectileCts;
    private UnitBase owner; // [NEW] 이 투사체를 발사한 주인 (회복용)

    protected override void Awake()
    {
        // UnitBase의 기본 스탯 설정 (투사체는 HP가 의미 없으므로 최소화)
        maxHp = 1f;
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // UnitManager 등록 포함
        
        projectileCts = new CancellationTokenSource();
        AutoReleaseAfterTimeAsync(projectileCts.Token).Forget();
    }

    protected override void OnDisable()
    {
        base.OnDisable(); // UnitManager 해제 포함
        
        if (rb != null) rb.velocity = Vector2.zero; 
        
        projectileCts?.Cancel();
        projectileCts?.Dispose();
        projectileCts = null;
    }

    /// <summary>
    /// 외부(공격 스크립트)에서 발사 방향과 데미지를 세팅해주는 초기화 함수
    /// </summary>
    public void Fire(Vector2 direction, float currentDamage, UnitBase fireOwner = null)
    {
        owner = fireOwner;
        damage = currentDamage;
        rb.velocity = direction.normalized * speed;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + 180f);

        // [SOUND] 발사 시 플레이어 공격 효과음 재생
        if (GameManager.Instance != null && GameManager.Instance.Sound != null)
        {
            GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxPlayerAttack);
        }
    }

    /// <summary>
    /// [CENTRALIZED] UnitManager에서 호출하는 수동 업데이트.
    /// 투사체 특화 로직이 필요할 경우 여기에 작성합니다. (현재는 물리 엔진에 의존)
    /// </summary>
    public override void ManualUpdate(float deltaTime)
    {
        base.ManualUpdate(deltaTime);
        // [FUTURE] 수동 이동 로직(transform.Translate 등)이 필요하면 여기서 처리 가능
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            if (collision.TryGetComponent<IDamageable>(out var enemy))
            {
                enemy.ApplyDamage(damage);
                
                if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
                {
                    var sManager = GameManager.Instance.skillManager;
                    // UnitBase 유닛인 경우 스킬 효과 전파
                    if (enemy.Unit != null)
                        sManager.ApplyAttackEffects(enemy.Unit);

                    // [NEW] 흡혈(Vampiric Teeth) 효과 적용 (주인 회복)
                    if (owner != null && !owner.IsDead && sManager.vampiricChance > 0f)
                    {
                        if (Random.value < sManager.vampiricChance)
                        {
                            owner.currentHp = Mathf.Min(owner.currentHp + sManager.vampiricHealAmount, owner.maxHp);
                        }
                    }
                }
            }

            ReleaseToPool();
        }
    }

    private async UniTaskVoid AutoReleaseAfterTimeAsync(CancellationToken token)
    {
        float startTime = Time.time;
        while (Time.time < startTime + lifeTime && !token.IsCancellationRequested)
        {
            // [STABILITY] 오브젝트가 이미 파괴되었거나 꺼졌다면 즉시 중단
            if (this == null || !gameObject.activeInHierarchy) return;
            await UniTask.Delay(100, cancellationToken: token).SuppressCancellationThrow();
        }
        
        if (this != null && gameObject.activeInHierarchy)
        {
            ReleaseToPool();
        }
    }

    private void ReleaseToPool()
    {
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            GameManager.Instance.poolManager.Release("BoneProjectile", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 투사체는 데미지를 입지 않거나 즉시 파괴되도록 오버라이드
    public override void TakeDamage(float damage, UnitBase attacker = null) { /* 투사체는 무적 또는 무시 */ }
    protected override void Die() { ReleaseToPool(); }
}
}
