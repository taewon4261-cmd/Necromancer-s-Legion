using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Necromancer.UI;
using UnityEditor.Events;

namespace Necromancer.Editor
{
    [InitializeOnLoad]
    public class InspectorBinder
    {
        static InspectorBinder()
        {
            // 컴파일 즉시 자동 실행
            Bind();
        }

        [MenuItem("Tools/Necromancer/Force Bind Back Button")]
        public static void Bind()
        {
            GameObject btnObj = GameObject.Find("Button_Back_Upgrade");
            if (btnObj == null) return;

            Button btn = btnObj.GetComponent<Button>();
            TitleUIController controller = GameObject.FindObjectOfType<TitleUIController>();

            if (btn != null && controller != null)
            {
                // 기존 리스너 모두 제거 (인스펙터의 OnClick 리스트 청소)
                while (btn.onClick.GetPersistentEventCount() > 0)
                {
                    UnityEventTools.RemovePersistentListener(btn.onClick, 0);
                }

                // 새로운 리스너 추가
                UnityEventTools.AddPersistentListener(btn.onClick, controller.BackToMainMenu);
                
                EditorUtility.SetDirty(btn);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(btn.gameObject.scene);
                Debug.Log("<color=green><b>[BINDING SUCCESS]</b></color> Button_Back_Upgrade -> BackToMainMenu 연결 완료!");
            }
        }
    }
}
