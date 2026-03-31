using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Necromancer.UI; // UIManager 인식을 위해 추가

namespace Necromancer.Editor
{
    [CustomEditor(typeof(UIManager))]
    public class UIManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            UIManager script = (UIManager)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Auto-Assign References (Find in Hierarchy)", GUILayout.Height(30)))
            {
                AutoAssign(script);
            }
        }

        private void AutoAssign(UIManager ui)
        {
            Undo.RecordObject(ui, "Auto Assign UIManager References");

            if (ui.expFillBar == null)
            {
                var go = GameObject.Find("ExpBar_Fill") ?? GameObject.Find("Exp_Fill");
                if (go != null) ui.expFillBar = go.GetComponent<Image>();
            }

            if (ui.levelUpPanel == null)
            {
                var go = GameObject.Find("LevelUpPanel") ?? GameObject.Find("Panel_LevelUp");
                if (go != null) ui.levelUpPanel = go;
            }

            if (ui.speedButton == null)
            {
                var go = GameObject.Find("Speed_Btn") ?? GameObject.Find("Button_Speed");
                if (go != null) ui.speedButton = go.GetComponent<Button>();
            }
            
            if (ui.textTimer == null)
            {
                var go = GameObject.Find("TextTimer");
                if (go != null) ui.textTimer = go.GetComponent<TextMeshProUGUI>();
            }

            if (ui.textWave == null)
            {
                var go = GameObject.Find("TextWave");
                if (go != null) ui.textWave = go.GetComponent<TextMeshProUGUI>();
            }

            EditorUtility.SetDirty(ui);
            Debug.Log("[UIManagerEditor] 레퍼런스 자동 할당 완료.");
        }
    }
}
