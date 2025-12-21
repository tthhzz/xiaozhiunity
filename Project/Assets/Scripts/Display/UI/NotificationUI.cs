using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class NotificationUIData : BaseUIData
    {
        public string Message;

        public LocalizedString LocalizedMessage;

        public float Duration;

        public NotificationUIData(string message, float duration = 3.0f)
        {
            Message = message;
            Duration = duration;
        }

        public NotificationUIData(LocalizedString message, float duration = 3.0f)
        {
            LocalizedMessage = message;
            Duration = duration;
        }
    }

    public class NotificationUI : BaseUI
    {
        private TextMeshProUGUI _text;

        private LocalizeStringEvent _localize;

        private CancellationTokenSource _cts;

        public override string GetResourcePath()
        {
            return "Assets/Res/UI/NotificationUI/NotificationUI.prefab";
        }

        protected override void OnInit()
        {
            _text = GetComponent<TextMeshProUGUI>(Tr, "Text");
            _localize = GetComponent<LocalizeStringEvent>(Tr, "Text");
            GetComponent<XButton>(Tr).onClick.AddListener(() => Close().Forget());
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            if (data is not NotificationUIData uiData) return;
            _localize.StringReference = uiData.LocalizedMessage;
            if (_localize.StringReference == null) _text.text = uiData.Message;
            _cts = new CancellationTokenSource();
            DelayedClose((int)(uiData.Duration * 1000), _cts.Token).Forget();

            var height = LayoutUtility.GetPreferredHeight(Tr);
            await Tr.SetAnchorPosY(height + 16).DOAnchorPosY(-60, AnimationDuration).SetEase(Ease.InOutSine);
        }

        protected override async UniTask OnHide()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            var height = LayoutUtility.GetPreferredHeight(Tr);
            await Tr.DOAnchorPosY(height + 16, AnimationDuration).SetEase(Ease.InOutSine);
        }

        private async UniTaskVoid DelayedClose(int durationMs, CancellationToken cancellationToken = default)
        {
            await UniTask.Delay(durationMs, cancellationToken: cancellationToken);
            await Close();
        }
    }
}