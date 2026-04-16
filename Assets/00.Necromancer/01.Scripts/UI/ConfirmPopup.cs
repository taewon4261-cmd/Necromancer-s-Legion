using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    /// <summary>
    /// 확인/취소 선택 팝업. PopupManager를 통해 호출합니다.
    /// </summary>
    public class ConfirmPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        [SerializeField] private TextMeshProUGUI cancelButtonText;

        private Action onConfirm;
        private Action onCancel;

        public void Setup(string message, Action onConfirm, Action onCancel,
                          string confirmLabel = "확인", string cancelLabel = "취소")
        {
            this.onConfirm = onConfirm;
            this.onCancel  = onCancel;

            if (messageText      != null) messageText.text      = message;
            if (confirmButtonText != null) confirmButtonText.text = confirmLabel;
            if (cancelButtonText  != null) cancelButtonText.text  = cancelLabel;

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnClickConfirm);

            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnClickCancel);

            // cancelLabel이 비어있으면 취소 버튼 숨김 (메시지 전용 팝업)
            if (cancelButton != null)
                cancelButton.gameObject.SetActive(!string.IsNullOrEmpty(cancelLabel));

            gameObject.SetActive(true);
        }

        private void OnClickConfirm()
        {
            gameObject.SetActive(false);
            onConfirm?.Invoke();
        }

        private void OnClickCancel()
        {
            gameObject.SetActive(false);
            onCancel?.Invoke();
        }
    }
}
