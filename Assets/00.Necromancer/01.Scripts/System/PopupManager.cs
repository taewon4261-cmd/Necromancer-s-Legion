using System;
using UnityEngine;
using Necromancer.UI;

namespace Necromancer.Systems
{
    /// <summary>
    /// 공통 팝업 매니저. GameManager를 통해 접근합니다 (GameManager.Instance.Popup).
    /// GameManager가 DontDestroyOnLoad이므로 별도 처리 불필요.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [SerializeField] private ConfirmPopup confirmPopup;

        private void Awake()
        {
            if (confirmPopup != null)
                confirmPopup.gameObject.SetActive(false);
        }

        /// <summary>
        /// 확인/취소 팝업을 띄웁니다.
        /// </summary>
        public void ShowConfirmPopup(string message, Action onConfirm, Action onCancel,
                                     string confirmLabel = "확인", string cancelLabel = "취소")
        {
            if (confirmPopup == null)
            {
                Debug.LogError("[PopupManager] ConfirmPopup 참조가 없습니다! 인스펙터에서 연결해주세요.");
                return;
            }
            confirmPopup.Setup(message, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        /// <summary>
        /// 확인 버튼만 있는 단순 메시지 팝업을 띄웁니다.
        /// </summary>
        public void ShowMessagePopup(string message, Action onConfirm = null, string confirmLabel = "확인")
        {
            if (confirmPopup == null)
            {
                Debug.LogError("[PopupManager] ConfirmPopup 참조가 없습니다! 인스펙터에서 연결해주세요.");
                return;
            }
            // cancelLabel을 "" 로 넘겨 취소 버튼 숨김
            confirmPopup.Setup(message, onConfirm, null, confirmLabel, "");
        }
    }
}
