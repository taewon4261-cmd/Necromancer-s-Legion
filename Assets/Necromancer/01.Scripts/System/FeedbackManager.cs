// File: Assets/Necromancer/01.Scripts/System/FeedbackManager.cs
using UnityEngine;
using TMPro;

namespace Necromancer
{
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (shakeDuration > 0)
            {
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
            originalPos = mainCamera.transform.localPosition;
            shakeDuration = duration;
            shakeMagnitude = magnitude;
        }

        /// <summary>
        /// 데미지 숫자 띄우기 (현재 비활성화됨)
        /// </summary>
        public void ShowDamageText(Vector3 position, float damage, bool isElite = false)
        {
            // 텍스트 출력 기능 비활성화 요청으로 인한 중단
            // if (GameManager.Instance == null || GameManager.Instance.poolManager == null) return;
            // ...
        }

        /// <summary>
        /// 피격 이펙트 생성
        /// </summary>
        public void PlayHitEffect(Vector3 position, string effectTag = "HitEffect")
        {
            if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
            {
                GameManager.Instance.poolManager.Get(effectTag, position, Quaternion.identity);
            }
        }
    }
}
