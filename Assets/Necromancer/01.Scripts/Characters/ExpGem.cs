
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 적이 죽었을 때 드랍되는 경험치 보석.
/// [OPTIMIZED] 매 프레임 거리 연산 대신 트리거 이벤트를 통해 추적을 시작합니다.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class ExpGem : MonoBehaviour
{
    [Header("Exp Settings")]
    public float expAmount = 10f;
    public float flySpeed = 15f;

    public static HashSet<ExpGem> ActiveGems = new HashSet<ExpGem>();

    private bool isFollowing = false;
    private Transform playerTransform;

    private void OnEnable()
    {
        ActiveGems.Add(this);
        isFollowing = false;
        
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
    }

    private void OnDisable()
    {
        ActiveGems.Remove(this);
    }

    private void Update()
    {
        if (playerTransform == null || !isFollowing) return;

        // [OPTIMIZATION] 추적 상태일 때만 비행 연산 수행
        transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, flySpeed * Time.deltaTime);
    }

    /// <summary>
    /// 플레이어의 자석 영역(Trigger)에 닿으면 호출됩니다.
    /// </summary>
    public void StartFollowing()
    {
        isFollowing = true;
    }

    public void StartVacuum()
    {
        isFollowing = true;
        flySpeed = 40f; 
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 자석 영역에 닿았을 때 (레이어나 태그로 구분 가능하지만, 
        // 여기서는 간단히 플레이어의 특정 트리거 기능을 가진 오브젝트와 닿았을 때로 처리)
        if (collision.CompareTag("MagnetArea"))
        {
            StartFollowing();
            return;
        }

        // 2. 실제 본체와 충돌하여 획득할 때
        if (collision.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddExp(expAmount);
                if (GameManager.Instance.Resources != null)
                {
                    GameManager.Instance.Resources.AddSoul(1);
                }
                GameManager.Instance.poolManager.Release("ExpGem", gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
}
