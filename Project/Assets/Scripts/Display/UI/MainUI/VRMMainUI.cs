using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class VRMMainUI : BaseUI
    {
        private RectTransform _trSet;
        private Button _btnSet;
        private GameObject _goLoading;
        private RectTransform _trStatus;
        private LocalizeStringEvent _localizeStatus;
        private LocalizeStringEvent _localizeInfo;
        private TextMeshProUGUI _textInfo;
        private TextMeshProUGUI _textChat;
        private CancellationTokenSource _autoHideCts;
        
        public override string GetResourcePath()
        {
            return "Assets/Res/UI/MainUI/VRMMainUI.prefab";
        }

        protected override void OnInit()
        {
            Tr.GetComponent<XButton>().onClick.AddListener(() =>
            {
                if (Context.App.Talk.IsReady() && AppSettings.Instance.IsAutoHideUI())
                {
                    ClearAutoHideCts();
                    UpdateCompVisible(true);
                    AutoHideComp();
                }
            });
            _goLoading = Tr.Find("Loading").gameObject;
            _trSet = GetComponent<RectTransform>(Tr, "BtnSet");
            _trSet.GetComponent<XButton>().onClick.AddListener(() => { ShowModuleUI<SettingsUI>().Forget(); });
            GetComponent<XButton>(Tr, "ClickRole").onClick.AddListener(() => Context.App.ToggleChatState().Forget());
            _trStatus = GetComponent<RectTransform>(Tr, "Status");
            _localizeStatus = GetComponent<LocalizeStringEvent>(_trStatus, "Stat");
            _localizeStatus.StringReference = null;
            _localizeInfo = GetComponent<LocalizeStringEvent>(_trStatus, "Info");
            _localizeInfo.StringReference = null;
            _textInfo = GetComponent<TextMeshProUGUI>(_localizeInfo, "");
            _textChat = Tr.Find("Chat").GetComponent<TextMeshProUGUI>();
            _textChat.text = "";
            GetComponent<HyperlinkText>(_textChat, "").OnClickLink
                .AddListener(_ => Application.OpenURL(AppPresets.Instance.ActivationURL));
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            Context.App.Talk.OnStateUpdate -= OnTalkStateUpdate;
            Context.App.Talk.OnStateUpdate += OnTalkStateUpdate;
            Context.App.Talk.OnInfoUpdate -= OnTalkInfoUpdate;
            Context.App.Talk.OnInfoUpdate += OnTalkInfoUpdate;
            Context.App.Talk.OnChatUpdate -= OnTalkChatUpdate;
            Context.App.Talk.OnChatUpdate += OnTalkChatUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate += OnAutoHideUIUpdate;
            DetectCompVisible(true);
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnHide()
        {
            Context.App.Talk.OnStateUpdate -= OnTalkStateUpdate;
            Context.App.Talk.OnInfoUpdate -= OnTalkInfoUpdate;
            Context.App.Talk.OnChatUpdate -= OnTalkChatUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            ClearAutoHideCts();
            KillCompVisibleAnim();
            await UniTask.CompletedTask;
        }
        
        private void OnTalkStateUpdate(Talk.State state)
        {
            ClearAutoHideCts();
            DetectCompVisible();
            UpdateLoadingState();
            _localizeStatus.StringReference = Lang.GetRef(Talk.State2LocalizedKey(state));
        }

        private void OnTalkChatUpdate(string content)
        {
            _textChat.text = Context.App.Talk.Stat == Talk.State.Activating
                ? $"<u><link=\"0\">{content}</link></u>"
                : content;
        }

        private void OnTalkInfoUpdate(LocalizedString info)
        {
            _localizeInfo.StringReference = info;
            if (info == null) _textInfo.text = "";
        }

        private void OnAutoHideUIUpdate(bool autoHide)
        {
            ClearAutoHideCts();
            if (autoHide) AutoHideComp();
            else DetectCompVisible();
        }

        private void ClearAutoHideCts()
        {
            if (_autoHideCts != null)
            {
                _autoHideCts.Cancel();
                _autoHideCts.Dispose();
                _autoHideCts = null;
            }
        }

        private void AutoHideComp()
        {
            _autoHideCts = new CancellationTokenSource();
            UniTask.Void(async token =>
            {
                await UniTask.Delay(3000, cancellationToken: token);
                DetectCompVisible();
            }, _autoHideCts.Token);
        }

        private void DetectCompVisible(bool instant = false)
        {
            UpdateCompVisible(Context.App.Talk.IsReady() && !AppSettings.Instance.IsAutoHideUI(), instant);
        }

        private void UpdateCompVisible(bool visible, bool instant = false)
        {
            var trSetPosY = visible ? -100 : 100;
            if (instant)
            {
                _trSet.SetAnchorPosY(trSetPosY);
            }
            else
            {
                _trSet.DOAnchorPosY(trSetPosY, AnimationDuration).SetEase(Ease.InOutSine);
            }
        }

        private void KillCompVisibleAnim()
        {
            _trSet.DOKill();
        }
        
        private void UpdateLoadingState()
        {
            _goLoading.SetActive(Context.App.Talk.Stat is Talk.State.Starting);
        }
    }
}