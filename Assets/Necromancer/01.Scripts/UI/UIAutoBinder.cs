#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

namespace Necromancer.UI
{
    /// <summary>
    /// 에디터 상에서 UI 컴포넌트를 필드에 자동으로 바인딩해주는 유틸리티입니다.
    /// [Zero-Search Architecture] 실현을 위한 빌드 타임 보조 도구입니다.
    /// </summary>
    public static class UIAutoBinder
    {
        private const BindingFlags PrivateBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void BindUpgradeUI(UpgradeUI ui)
        {
            if (ui == null) return;

            SetField(ui, "contentRoot", ui.transform.Find("Scroll View/Viewport/Content"));
            SetField(ui, "soulText", ui.transform.Find("Text_GoldDisplay")?.GetComponent<TextMeshProUGUI>());
            
            UnityEditor.EditorUtility.SetDirty(ui);
            Debug.Log($"<color=green>[UIAutoBinder]</color> <b>{ui.name}</b> Binding Complete!");
        }

        public static void BindUpgradeItemUI(UpgradeItemUI ui)
        {
            if (ui == null) return;

            SetField(ui, "iconFrame", ui.transform.Find("Icon_Frame")?.GetComponent<Image>());
            SetField(ui, "iconImage", ui.transform.Find("Icon_Frame/Icon")?.GetComponent<Image>() ?? ui.transform.Find("Icon")?.GetComponent<Image>());
            SetField(ui, "backgroundImage", ui.GetComponent<Image>());

            Transform infoGroup = ui.transform.Find("Info_Vertical_Group");
            if (infoGroup != null)
            {
                SetField(ui, "nameText", infoGroup.Find("Text_Name")?.GetComponent<TextMeshProUGUI>());
                SetField(ui, "descriptionText", infoGroup.Find("Text_Description")?.GetComponent<TextMeshProUGUI>());
                SetField(ui, "levelText", infoGroup.Find("Text_Level")?.GetComponent<TextMeshProUGUI>());
            }
            
            Transform upgradeBtn = ui.transform.Find("Button_Upgrade");
            SetField(ui, "upgradeButton", upgradeBtn?.GetComponent<Button>());
            SetField(ui, "costText", upgradeBtn?.Find("Text_Cost")?.GetComponent<TextMeshProUGUI>());
            SetField(ui, "levelSlider", ui.GetComponentInChildren<Slider>());

            UnityEditor.EditorUtility.SetDirty(ui);
            Debug.Log($"<color=green>[UIAutoBinder]</color> <b>{ui.name}</b> Binding Complete!");
        }

        public static void BindSettingUI(SettingUI ui)
        {
            if (ui == null) return;

            SetField(ui, "bgmSlider", ui.transform.Find("Slider_BGM")?.GetComponent<Slider>());
            SetField(ui, "sfxSlider", ui.transform.Find("Slider_SFX")?.GetComponent<Slider>());
            
            UnityEditor.EditorUtility.SetDirty(ui);
            Debug.Log($"<color=green>[UIAutoBinder]</color> <b>{ui.name}</b> Binding Complete!");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (value == null) return;

            FieldInfo field = target.GetType().GetField(fieldName, PrivateBindingFlags);
            if (field != null)
            {
                field.SetValue(target, value);
                Debug.Log($"<color=cyan>[UIAutoBinder]</color> Bound <b>{fieldName}</b> to <i>{target.GetType().Name}</i>");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[UIAutoBinder]</color> Field <b>{fieldName}</b> not found in {target.GetType().Name}!");
            }
        }
    }
}
#endif
