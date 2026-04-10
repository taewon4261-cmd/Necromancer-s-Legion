// File: Assets/Necromancer/01.Scripts/UI/HPBarPresenter.cs
using UnityEngine;

namespace Necromancer
{
    public class HPBarPresenter : MonoBehaviour
    {
        public UnitBase unit;
        public HPBarView view;
        public bool hideWhenFull = true;

        private void Start()
        {
            if (unit == null) unit = GetComponentInParent<UnitBase>();
            if (view == null) view = GetComponent<HPBarView>();

            if (unit != null)
            {
                unit.OnHealthChanged += UpdateView;
                UpdateView(unit.currentHp, unit.maxHp);
            }
        }

        private void OnDestroy()
        {
            if (unit != null)
            {
                unit.OnHealthChanged -= UpdateView;
            }
        }

        private void UpdateView(float current, float max)
        {
            if (view == null) return;

            view.SetHealth(current, max);

            if (hideWhenFull)
            {
                view.SetVisible(current < max && current > 0);
            }
        }
    }
}
