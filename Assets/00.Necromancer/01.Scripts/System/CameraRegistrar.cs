// File: Assets/Necromancer/01.Scripts/System/CameraRegistrar.cs
using UnityEngine;
using Cinemachine;

namespace Necromancer.Systems
{
    /// <summary>
    /// 시네머신 가상 카메라가 씬 시작 시 스스로를 GameManager에 등록하게 돕는 유틸리티
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CameraRegistrar : MonoBehaviour
    {
        private void Awake()
        {
            var vcam = GetComponent<CinemachineVirtualCamera>();
            if (vcam != null && GameManager.Instance != null)
            {
                // 인게임 시야 확대 기본값 적용
                vcam.m_Lens.OrthographicSize = 13f;
                Debug.Log($"<color=cyan><b>[CameraRegistrar]</b> Virtual Camera registered for {gameObject.name}</color>");
            }
        }
    }
}
