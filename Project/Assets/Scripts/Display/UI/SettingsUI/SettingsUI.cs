using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class SettingsUI : BaseUI
    {
        private readonly Type[] _tabCls = { typeof(AppSettingsUI) };
        private int _tabIndex;
        private Transform _listTab;
        private Transform _subParent;
        private BaseUI[] _subUI;

        public override string GetResourcePath()
        {
            return "Assets/Res/UI/SettingsUI/SettingsUI.prefab";
        }

        protected override void OnInit()
        {
            _subUI = new BaseUI[_tabCls.Length];
            _subParent = Tr.Find("Sub");
            _listTab = Tr.Find("Tabs");

            GetComponent<XButton>(Tr, "Top/BtnClose").onClick.AddListener(() => Close().Forget());
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            UpdateTabList();
            SelectTab(_tabIndex, true);

            Tr.DOKill();
            await Tr.SetAnchorPosX(Tr.rect.width + 16).DOAnchorPosX(0, AnimationDuration).SetEase(Ease.InOutSine);
        }

        protected override async UniTask OnHide()
        {
            Tr.DOKill();
            await Tr.DOAnchorPosX(Tr.rect.width + 16, AnimationDuration).SetEase(Ease.InOutSine);
        }

        private void UpdateTabList()
        {
            for (var i = 0; i < _tabCls.Length; i++)
            {
                var go = _listTab.GetChild(i).gameObject;
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = i == _tabIndex;
                AddUniqueListener(toggle, i, OnToggleTab);
            }
        }

        private void OnToggleTab(Toggle toggle, int index, bool isOn)
        {
            if (isOn) SelectTab(index);
        }

        private void SelectTab(int tabIndex, bool force = false)
        {
            if (_tabIndex == tabIndex && !force) return;
            _tabIndex = tabIndex;
            UpdateTabUI().Forget();
        }

        private async UniTask UpdateTabUI()
        {
            var ui = _subUI[_tabIndex] ?? await LoadUI<BaseUI>(_tabCls[_tabIndex], _subParent);
            _subUI[_tabIndex] = ui;
            await UniTask.WhenAll(_subUI.Where((i, index) => i?.IsVisible == true && index != _tabIndex)
                .Select(i => i.Hide()));
            if (!ui.IsVisible) await ui.Show();
        }
    }
}