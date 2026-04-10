// File: Assets/Necromancer/01.Scripts/System/FeedbackManager.cs
using UnityEngine;
using TMPro;

namespace Necromancer
{
    public class FeedbackManager : MonoBehaviour
    {
        [Header("Damage Popup Settings")]
        public string damageTextPoolTag = "DamageText";
        public Color normalDamageColor = Color.white;
        public Color eliteDamageColor = Color.yellow;

        [Header("Camera Shake Settings")]
        private Vector3 originalPos;
        private float shakeDuration = 0f;
        private float shakeMagnitude = 0.1f;
        private float dampingSpeed = 1.0f;
        private Camera mainCamera;

        public void Init()
        {
            mainCamera = Camera.main;
            if (mainCamera != null) originalPos = mainCamera.transform.localPosition;
            Debug.Log("<color=cyan>[FeedbackManager]</color> Initialized.");
        }

        private void Update()
        {
            if (shakeDuration > 0)
            {
                // [수정] 씬 전환 등으로 카메라가 파괴되었을 경우를 대비한 안전 체크
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera == null) return;
                    originalPos = mainCamera.transform.localPosition;
                }

                mainCamera.transform.localPosition = originalPos + Random.insideUnitSphere * shakeMagnitude;
                shakeDuration -= Time.deltaTime * dampingSpeed;
                
                if (shakeDuration <= 0)
                {
                    shakeDuration = 0f;
                    mainCamera.transform.localPosition = originalPos;
                }
            }
        }

        /// <summary>
        /// 짧고 강하게 화면 흔들기
        /// </summary>
        public void ShakeCamera(float duration = 0.1f, float magnitude = 0.15f)
        {
            // 카메라가 없으면 다시 찾기 시도
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera != null) originalPos = mainCamera.transform.localPosition;
            }

            if (mainCamera == null) return;

            // 현재 흔들리고 있지 않을 때만 초기 위치를 0으로 잡거나 현재 위치 고정
            shakeDuration = duration;
            shakeMagnitude = magnitude;
        }

        /// <summary>
        /// 데미지 숫자 띄우기 (최적화 및 안정성을 위해 비활성화)
        /// </summary>
        public void ShowDamageText(Vector3 position, float damage, bool isElite = false)
        {
            // [OPTIMIZATION] 대규모 전투 최적화를 위해 데미지 텍스트 생성을 중단합니다.
            /*
            if (GameManager.Instance == null || GameManager.Instance.poolManager == null) return;
            string tag = damageTextPoolTag;
            GameObject obj = GameManager.Instance.poolManager.Get(tag, position, Quaternion.identity);
            // ...
            */
        }

        /// <summary>
        /// 피격 이펙트 생성 (최적화 및 안정성을 위해 비활성화)
        /// </summary>
        public void PlayHitEffect(Vector3 position, string effectTag = "HitEffect")
        {
            // [STABILITY] Magenta 셰이더 에러 방지 및 GC 부하 감소를 위해 이펙트 생성을 중단합니다.
            /*
            if (GameManager.Instance == null || GameManager.Instance.poolManager == null) return;
            if (gameObject.activeInHierarchy)
            {
                GameManager.Instance.poolManager.Get(effectTag, position, Quaternion.identity);
            }
            */
        }
    }
}
