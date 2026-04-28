// File: Assets/Necromancer/01.Scripts/UI/HPBarView.cs
using UnityEngine;
using UnityEngine.UI;

namespace Necromancer
{
    public class HPBarView : MonoBehaviour
    {
        public Image hpFillImage;
        public CanvasGroup canvasGroup;

        public void SetHealth(float current, float max)
        {
            if (hpFillImage != null && max > 0)
            {
                hpFillImage.fillAmount = current / max;
            }
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        public void SetColor(Color color)
        {
            if (hpFillImage != null) hpFillImage.color = color;
        }
    }
}
