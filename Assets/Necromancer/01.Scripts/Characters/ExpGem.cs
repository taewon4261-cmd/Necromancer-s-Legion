// File: Assets/Necromancer/01.Scripts/Characters/ExpGem.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 적이 죽었을 때 드랍되는 경험치 보석.
/// 플레이어가 자석 반경 내로 접근하면 플레이어를 향해 날아갑니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ExpGem : MonoBehaviour
{
    [Header("Exp Settings")]
    [Tooltip("이 보석이 주는 경험치 량")]
    public float expAmount = 10f;
    
    [Tooltip("플레이어에게 빨려들어가는 비행 속도")]
    public float flySpeed = 15f;

    private bool isFollowing = false;
    private Transform playerTransform;

    private void OnEnable()
    {
        isFollowing = false;
        
        // 스폰 시 플레이어 위치 캐싱 (GameManager 활용)
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTransform = GameManager.Instance.playerTransform;
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // 1. 자석 반경 체크 로직 (플레이어와 거리가 가까워지면 추적 시작)
        // GameManager에 임시로 설정할 자석 반경 변수를 참조합니다.
        if (!isFollowing)
        {
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            if (GameManager.Instance != null && distance <= GameManager.Instance.magnetRadius)
            {
                isFollowing = true;
            }
        }
        else
        {
            // 2. 획득(추적) 상태일 때는 플레이어를 향해 빠르게 날아감
            transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, flySpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 마지막 몬스터 처치 시 모든 영혼을 강제로 빨아들이는 명령
    /// </summary>
    public void StartVacuum()
    {
        isFollowing = true;
        flySpeed = 40f; // 빨려오는 속도 상향
    }

    /// <summary>
    /// 플레이어의 콜라이더와 부딪히면 냠냠
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // 경험치 추가
            if (GameManager.Instance != null)
            {
                // 경험치 추가
                GameManager.Instance.AddExp(expAmount);

                // [NEW] 보석 획득 시 1 소울(영혼) 고정 추가
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
            
            // TODO: EXP 획득 짤깍 소리 재생
        }
    }
}
}
