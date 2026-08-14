using System;
using System.Collections.Generic;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Necromancer.Core;
using Necromancer.Systems;

namespace Necromancer.UI
{
    public class TitleChatController : MonoBehaviour
    {
        private const float AutoScrollThreshold = 0.05f;
        private const string DefaultGuestName = "Guest";
        private const string DefaultPlayerPrefix = "Player";

        [Header("UI")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_InputField messageInput;
        [SerializeField] private TextMeshProUGUI messageLogText;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Firestore")]
        [SerializeField] private string collectionName = "titleChatMessages";
        [SerializeField] private int maxMessages = 50;
        [SerializeField] private int maxMessageLength = 80;

        private ListenerRegistration listener;
        private FirebaseFirestore db;
        private bool isListening;

        private void Awake()
        {
            if (chatPanel != null)
                chatPanel.SetActive(true);

            if (toggleButton != null)
                toggleButton.gameObject.SetActive(false);

            // [SCROLL-BOUNCE-FIX] Text_MessageLog에 ContentSizeFitter가 없어 height가 고정되어
            // ScrollRect가 영역 오버플로우로 판단하고 손을 놓으면 Elastic 탄성에 의해 원위치 튕김 현상이 발생하는 버그를 완벽 해결
            if (messageLogText != null)
            {
                var rectTransform = messageLogText.rectTransform;
                if (rectTransform != null)
                {
                    rectTransform.pivot = new Vector2(0.5f, 1f);
                }

                var fitter = messageLogText.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = messageLogText.gameObject.AddComponent<ContentSizeFitter>();
                }
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void OnEnable()
        {
            if (toggleButton != null && toggleButton.gameObject.activeInHierarchy)
                toggleButton.onClick.AddListener(ToggleChat);

            if (sendButton != null)
                sendButton.onClick.AddListener(SendMessage);

            if (messageInput != null)
                messageInput.onSubmit.AddListener(OnInputSubmit);

            AuthManager.OnFirebaseReady += StartListening;

            if (GameManager.Instance?.Auth != null && GameManager.Instance.Auth.IsFirebaseReady)
                StartListening();
        }

        private void OnDisable()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(ToggleChat);

            if (sendButton != null)
                sendButton.onClick.RemoveListener(SendMessage);

            if (messageInput != null)
                messageInput.onSubmit.RemoveListener(OnInputSubmit);

            AuthManager.OnFirebaseReady -= StartListening;

            StopListening();
        }

        private void OnInputSubmit(string _)
        {
            SendMessage();
        }

        private bool isFirstLoad = true;

        private void ToggleChat()
        {
            if (chatPanel == null)
                return;

            bool willShow = !chatPanel.activeSelf;
            chatPanel.SetActive(willShow);

            if (willShow)
            {
                if (messageInput != null)
                    messageInput.ActivateInputField();

                // 채팅창을 열 때 항상 최신 대화(맨 아래)부터 보이도록 정렬
                ScrollToBottom();
            }
        }

        private void StartListening()
        {
            if (isListening)
                return;

            try
            {
                db = FirebaseFirestore.DefaultInstance;
                listener = db.Collection(collectionName)
                    .OrderByDescending("createdAt")
                    .Limit(maxMessages)
                    .Listen(OnSnapshot);
                isListening = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TitleChatController] Listen failed: {e.Message}");
            }
        }

        private void StopListening()
        {
            listener?.Stop();
            listener = null;
            isListening = false;
        }

        private void OnSnapshot(QuerySnapshot snapshot)
        {
            if (messageLogText == null || snapshot == null)
                return;

            var lines = new List<string>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                string nickname = doc.TryGetValue("nickname", out string nick) ? nick : DefaultPlayerPrefix;
                string message = doc.TryGetValue("message", out string text) ? text : "";

                if (string.IsNullOrWhiteSpace(message))
                    continue;

                lines.Add($"{nickname}: {message}");
            }

            lines.Reverse();
            string newText = string.Join("\n", lines);

            // [PERFORMANCE] 텍스트 변경이 없을 경우 UI 리빌드 및 스크롤 연산 전체 스킵
            if (messageLogText.text == newText && !isFirstLoad)
                return;

            messageLogText.text = newText;

            // [SMART AUTO-SCROLL] 최초 로드 시에는 무조건 최하단(최신 대화)으로 정렬하고,
            // 이후에는 사용자가 스크롤을 올려둔 상태가 아닐 때만 최하단으로 정렬합니다.
            if (scrollRect != null)
            {
                if (isFirstLoad)
                {
                    isFirstLoad = false;
                    ScrollToBottom();
                }
                else
                {
                    bool isAtBottom = scrollRect.verticalNormalizedPosition <= AutoScrollThreshold;
                    if (isAtBottom)
                    {
                        ScrollToBottom();
                    }
                }
            }
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private async void SendMessage()
        {
            if (messageInput == null)
                return;

            string message = messageInput.text.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            if (message.Length > maxMessageLength)
                message = message.Substring(0, maxMessageLength);

            string uid = GameManager.Instance?.SaveData?.CurrentUid;
            if (string.IsNullOrEmpty(uid))
                uid = "guest";

            var data = new Dictionary<string, object>
            {
                { "uid", uid },
                { "nickname", GetCurrentNickname(uid) },
                { "message", message },
                { "createdAt", FieldValue.ServerTimestamp }
            };

            try
            {
                db ??= FirebaseFirestore.DefaultInstance;
                await db.Collection(collectionName).AddAsync(data);
                messageInput.text = string.Empty;
                messageInput.ActivateInputField();
            }
            catch (Exception e)
            {
                Debug.LogError($"[TitleChatController] Send failed: {e.Message}");
            }
        }

        private static string GetCurrentNickname(string uid)
        {
            // 1. 유효한 유저 변경 닉네임 우선 참조
            if (GameManager.Instance?.SaveData?.Data != null && !string.IsNullOrWhiteSpace(GameManager.Instance.SaveData.Data.nickname))
            {
                return GameManager.Instance.SaveData.Data.nickname;
            }

            // 2. Fallback (게스트 및 미설정 유저)
            if (string.IsNullOrEmpty(uid) || uid == "guest")
                return DefaultGuestName;

            int length = Mathf.Min(5, uid.Length);
            return $"{DefaultPlayerPrefix}-{uid.Substring(0, length)}";
        }
    }
}
