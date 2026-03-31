using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace Necromancer.Editor
{
    [InitializeOnLoad]
    public class ViewportFixer
    {
        static ViewportFixer()
        {
            FixViewport();
        }

        [MenuItem("Tools/Necromancer/Fix Viewport Mask")]
        public static void FixViewport()
        {
            // Viewport 오브젝트 탐색 (ID 기반이 아닌 경로/이름 기반)
            GameObject viewportObj = GameObject.Find("Viewport");
            
            // Panel_Upgrade 하위의 Viewport인지 재확인
            if (viewportObj != null && viewportObj.transform.parent != null && viewportObj.transform.parent.name == "Scroll View")
            {
                // 1. 기존 Mask 제거
                Mask mask = viewportObj.GetComponent<Mask>();
                if (mask != null) Object.DestroyImmediate(mask);

                // 2. 기존 Image 제거 (Mask용으로 쓰이던 투명 이미지)
                Image img = viewportObj.GetComponent<Image>();
                if (img != null) Object.DestroyImmediate(img);

                // 3. RectMask2D 추가
                RectMask2D rectMask = viewportObj.GetComponent<RectMask2D>();
                if (rectMask == null) viewportObj.AddComponent<RectMask2D>();

                Debug.Log("<color=green><b>[SUCCESS]</b></color> Viewport Mask has been replaced with RectMask2D!");
            }
        }
    }
}
