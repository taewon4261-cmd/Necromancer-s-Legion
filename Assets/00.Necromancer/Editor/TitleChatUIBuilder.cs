using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Necromancer.UI;

namespace Necromancer.EditorTools
{
    public static class TitleChatUIBuilder
    {
        private const string MenuPath = "Tools/Necromancer/Build Title Chat UI";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            var uiRoot = GameObject.Find("UI_Root");
            if (uiRoot == null)
            {
                Debug.LogError("[TitleChatUIBuilder] UI_Root not found. Open TitleScene first.");
                return;
            }

            var oldObjects = Object.FindObjectsOfType<Transform>(true)
                .Where(t => t.name == "TitleChat" || t.name == "Button_ChatToggle" || t.name == "Panel_Chat")
                .Select(t => t.gameObject)
                .Distinct()
                .ToArray();

            foreach (var obj in oldObjects)
                Object.DestroyImmediate(obj);

            var controllerObject = CreateRectObject("TitleChat", uiRoot.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var controller = controllerObject.AddComponent<TitleChatController>();

            var toggleButton = CreateButton("Button_ChatToggle", uiRoot.transform, "Chat", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(150f, 56f));
            SetImageColor(toggleButton.gameObject, new Color(0.12f, 0.08f, 0.18f, 0.92f));

            var panel = CreatePanel("Panel_Chat", uiRoot.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 92f), new Vector2(560f, 430f), new Color(0.05f, 0.04f, 0.07f, 0.88f));
            panel.SetActive(false);

            var header = CreateText("Text_ChatHeader", panel.transform, "Title Chat", 28, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -42f), new Vector2(-48f, 42f));
            header.fontStyle = FontStyles.Bold;

            var scrollRect = CreateChatScroll(panel.transform);
            var input = CreateInputField(panel.transform);
            var sendButton = CreateButton("Button_ChatSend", panel.transform, "Send", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-72f, 40f), new Vector2(120f, 54f));
            SetImageColor(sendButton.gameObject, new Color(0.32f, 0.18f, 0.56f, 0.95f));

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("chatPanel").objectReferenceValue = panel;
            serialized.FindProperty("toggleButton").objectReferenceValue = toggleButton;
            serialized.FindProperty("sendButton").objectReferenceValue = sendButton;
            serialized.FindProperty("messageInput").objectReferenceValue = input;
            serialized.FindProperty("messageLogText").objectReferenceValue = scrollRect.content.GetComponent<TextMeshProUGUI>();
            serialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[TitleChatUIBuilder] Title chat UI created and saved.");
        }

        private static GameObject CreateRectObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return obj;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var obj = CreateRectObject(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            var image = obj.AddComponent<Image>();
            image.color = color;
            return obj;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var obj = CreatePanel(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta, new Color(0.18f, 0.14f, 0.24f, 0.95f));
            var button = obj.AddComponent<Button>();

            var text = CreateText("Text_Label", obj.transform, label, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.color = Color.white;
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var obj = CreateRectObject(name, parent, anchorMin, anchorMax, anchoredPosition, sizeDelta);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = new Color(0.94f, 0.91f, 0.98f, 1f);
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static ScrollRect CreateChatScroll(Transform parent)
        {
            var scrollObject = CreateRectObject("Scroll_ChatMessages", parent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 22f), new Vector2(-48f, -150f));
            var scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = CreatePanel("Viewport", scrollObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.18f));
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var contentText = CreateText("Text_MessageLog", viewport.transform, "", 22, TextAlignmentOptions.BottomLeft, Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-32f, -24f));
            contentText.overflowMode = TextOverflowModes.Overflow;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentText.GetComponent<RectTransform>();
            return scrollRect;
        }

        private static TMP_InputField CreateInputField(Transform parent)
        {
            var inputObject = CreatePanel("Input_ChatMessage", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-102f, 40f), new Vector2(-168f, 54f), new Color(1f, 1f, 1f, 0.12f));
            var input = inputObject.AddComponent<TMP_InputField>();

            var textArea = CreateRectObject("Text Area", inputObject.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24f, -12f));
            textArea.AddComponent<RectMask2D>();

            var placeholder = CreateText("Placeholder", textArea.transform, "Message", 21, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            placeholder.color = new Color(1f, 1f, 1f, 0.38f);

            var text = CreateText("Text", textArea.transform, "", 21, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.color = Color.white;
            text.enableWordWrapping = false;

            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 80;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static void SetImageColor(GameObject obj, Color color)
        {
            var image = obj.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }
    }
}
