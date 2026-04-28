// File: Assets/Necromancer/01.Scripts/UI/HPBarPresenter.cs
using UnityEngine;

namespace Necromancer
{
    public class HPBarPresenter : MonoBehaviour
    {
        public UnitBase unit;
        public HPBarView view;
        public bool hideWhenFull = true;

        [SerializeField] private float yOffset = -0.8f;

        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Start()
        {
            if (unit == null) unit = GetComponentInParent<UnitBase>();
            if (view == null) view = GetComponent<HPBarView>();

            if (unit != null)
            {
                ApplyVisualSettings();
                unit.OnHealthChanged += UpdateView;
                UpdateView(unit.currentHp, unit.maxHp);
            }
        }

        private void ApplyVisualSettings()
        {
            // 위치: 유닛 기준 아래쪽
            transform.localPosition = new Vector3(0f, yOffset, 0f);

            // 두께: 기존의 2/3로 슬림화
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * (2f / 3f), _baseScale.z);

            // 색상: 유닛 타입별 지정
            if (view == null) return;
            if (unit is PlayerController) view.SetColor(Color.green);
            else if (unit is MinionAI)    view.SetColor(Color.yellow);
            else if (unit is EnemyAI)     view.SetColor(Color.red);
        }

        private void OnDestroy()
        {
            if (unit != null)
                unit.OnHealthChanged -= UpdateView;
        }

        private void UpdateView(float current, float max)
        {
            if (view == null) return;

            view.SetHealth(current, max);

            if (hideWhenFull)
                view.SetVisible(current < max && current > 0);
            else
                view.SetVisible(current > 0);
        }
    }
}
