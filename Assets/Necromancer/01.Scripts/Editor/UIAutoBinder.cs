using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Necromancer.UI
{
    public static class UIAutoBinder
    {
        public static void BindUpgradeUI(UpgradeUI ui)
        {
            if (ui.contentRoot == null)
            {
                var viewport = ui.transform.Find("Scroll View/Viewport");
                if (viewport != null) ui.contentRoot = viewport.Find("Content");
            }
            if (ui.soulText == null) ui.soulText = ui.transform.Find("Text_GoldDisplay")?.GetComponent<TextMeshProUGUI>();
            
            UnityEditor.EditorUtility.SetDirty(ui);
        }

        public static void BindUpgradeItemUI(UpgradeItemUI ui)
        {
            if (ui.iconImage == null) ui.iconImage = ui.transform.Find("Icon")?.GetComponent<Image>();
            if (ui.nameText == null) ui.nameText = ui.transform.Find("Text_Name")?.GetComponent<TextMeshProUGUI>();
            if (ui.levelText == null) ui.levelText = ui.transform.Find("Text_Level")?.GetComponent<TextMeshProUGUI>();
            if (ui.descriptionText == null) ui.descriptionText = ui.transform.Find("Text_Description")?.GetComponent<TextMeshProUGUI>();
            if (ui.costText == null) ui.costText = ui.transform.Find("Text_Cost")?.GetComponent<TextMeshProUGUI>();
            if (ui.upgradeButton == null) ui.upgradeButton = ui.transform.Find("Button_Upgrade")?.GetComponent<Button>();
            
            UnityEditor.EditorUtility.SetDirty(ui);
        }

        public static void BindSettingUI(SettingUI ui)
        {
            if (ui.bgmSlider == null) ui.bgmSlider = ui.transform.Find("Slider_BGM")?.GetComponent<Slider>();
            if (ui.sfxSlider == null) ui.sfxSlider = ui.transform.Find("Slider_SFX")?.GetComponent<Slider>();
            
            UnityEditor.EditorUtility.SetDirty(ui);
        }
    }
}
