using UnityEngine;
using Necromancer.Core;
using Necromancer.Data;

namespace Necromancer
{
    /// <summary>
    /// 적 처치 시 드랍되는 미니언 정수 아이템입니다.
    /// 플레이어에게 자석처럼 끌려오며, 획득 시 해당 미니언의 해금 정수를 추가합니다.
    /// </summary>
    public class EssenceItem : MonoBehaviour
    {
        [Header("Settings")]
        public float moveSpeed = 5f;
        public float acceleration = 10f;
        public float collectDistance = 0.5f;

        private MinionUnlockSO targetMinionData;
        private Transform playerTransform;
        private bool isFollowing = false;
        private float currentSpeed;

        /// <summary>
        /// 생성 시 어떤 미니언의 정수인지 설정합니다.
        /// </summary>
        public void Setup(MinionUnlockSO data)
        {
            targetMinionData = data;
            isFollowing = false;
            currentSpeed = moveSpeed;
            
            // 시각적 피드백 (미니언 아이콘 등을 띄울 수도 있음)
            // if (iconRenderer != null) iconRenderer.sprite = data.minionIcon;
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                playerTransform = GameManager.Instance.playerTransform;
        }

        private void Update()
        {
            if (playerTransform == null || targetMinionData == null) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // 자석 로직: 플레이어의 자석 반경 내에 들어오면 추적 시작
            if (!isFollowing && distance <= GameManager.Instance.magnetRadius)
            {
                isFollowing = true;
            }

            if (isFollowing)
            {
                currentSpeed += acceleration * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, currentSpeed * Time.deltaTime);

                if (distance <= collectDistance)
                {
                    Collect();
                }
            }
        }

        private void Collect()
        {
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                // 정수 추가 (targetEnemyID가 저장 키로 사용됨)
                GameManager.Instance.Resources.AddEssence(targetMinionData.targetEnemyID, 1);
                
                // 효과음 재생
                if (GameManager.Instance.Sound != null)
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSoulGain); // 소울 획득음 재사용 혹은 전용음
            }

            // 풀링 시스템으로 회수 (태그가 "Essence"라고 가정)
            if (GameManager.Instance.poolManager != null)
                GameManager.Instance.poolManager.Release("Essence", gameObject);
            else
                Destroy(gameObject);
        }
    }
}
