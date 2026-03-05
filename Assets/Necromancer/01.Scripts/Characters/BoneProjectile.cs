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
/// PoolManager에 의해 관리되며, 활성화 시 타겟을 향해 날아가고 부딪히면 데미지를 주고 소멸(반납)합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class BoneProjectile : MonoBehaviour
{
    [Header("Projectile Stats")]
    public float damage = 20f;
    public float speed = 10f;
    public float lifeTime = 3f;

    private Rigidbody2D rb;
    private CancellationTokenSource lifeCts;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        // 1. 발사될 때마다 수류탄 핀 뽑듯 수명 타이머 작동
        lifeCts?.Cancel();
        lifeCts?.Dispose();
        lifeCts = new CancellationTokenSource();
        
        AutoReleaseAfterTimeAsync(lifeCts.Token).Forget();
    }

    private void OnDisable()
    {
        lifeCts?.Cancel();
        lifeCts?.Dispose();
        lifeCts = null;
        
        // 회수 시 물리력 0으로 리셋 (다음 발사 때 궤도 꼬임 방지)
        rb.velocity = Vector2.zero; 
    }

    /// <summary>
    /// 외부(공격 스크립트)에서 발사 방향과 데미지를 세팅해주는 초기화 함수
    /// </summary>
    public void Fire(Vector2 direction, float currentDamage)
    {
        damage = currentDamage; // 레벨업 시 공격력 증가 반영
        rb.velocity = direction.normalized * speed;
        
        // 투사체가 날아가는 방향으로 쳐다보게 각도 회전 (옵션)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    /// <summary>
    /// 적과 충돌 시 데미지를 입히고 사라짐
    /// Trigger 모드로 콜라이더를 세팅해야 적을 통과하며 때리지 않고 폭파됩니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            UnitBase enemy = collision.GetComponent<UnitBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                // TODO: 뼈 타격 파티클 호출
            }

            // 맞았으므로 스스로를 창고로 반납
            ReleaseToPool();
        }
    }

    /// <summary>
    /// 허공으로 날아간 탄환을 수명(lifeTime) 뒤에 강제 회수
    /// </summary>
    private async UniTaskVoid AutoReleaseAfterTimeAsync(CancellationToken token)
    {
        // Cancel 발생 전까지 정상 대기했다면 true 반환
        bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(lifeTime), cancellationToken: token).SuppressCancellationThrow();
        
        if (!isCancelled)
        {
            ReleaseToPool();
        }
    }

    private void ReleaseToPool()
    {
        lifeCts?.Cancel(); // 타이머 중지
        
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            GameManager.Instance.poolManager.Release("BoneProjectile", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
}
