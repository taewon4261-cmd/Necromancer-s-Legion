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

        private void ToggleChat()
        {
            if (chatPanel == null)
                return;

            chatPanel.SetActive(!chatPanel.activeSelf);

            if (chatPanel.activeSelf && messageInput != null)
                messageInput.ActivateInputField();
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
                string nickname = doc.TryGetValue("nickname", out string nick) ? nick : "Player";
                string message = doc.TryGetValue("message", out string text) ? text : "";

                if (string.IsNullOrWhiteSpace(message))
                    continue;

                lines.Add($"{nickname}: {message}");
            }

            lines.Reverse();
            messageLogText.text = string.Join("\n", lines);

            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
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
                { "nickname", BuildNickname(uid) },
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

        private static string BuildNickname(string uid)
        {
            if (string.IsNullOrEmpty(uid) || uid == "guest")
                return "Guest";

            int length = Mathf.Min(5, uid.Length);
            return $"Player-{uid.Substring(0, length)}";
        }
    }
}
