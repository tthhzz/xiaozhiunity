using System;
using System.Collections.Generic;
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
    public class EmojiMainUI : BaseUI
    {
        private static readonly Dictionary<string, string> Emojis = new()
        {
            { "sleep", "😴" },
            { "neutral", "🙂" },
            { "happy", "😄" },
            { "funny", "😜" },
            { "sad", "🙁" },
            { "thinking", "🤔" },
        };

        private const int SpectrumUpdateInterval = 50;
        private const float OutputScaleFactor = 0.5f;
        
        private LocalizeStringEvent _localizeStatus;
        private LocalizeStringEvent _localizeInfo;
        private TextMeshProUGUI _textInfo;
        private TextMeshProUGUI _textChat;
        private Transform _trEmotion;
        private TextMeshProUGUI _textEmotion;
        private RectTransform _trSet;
        private Button _btnSet;
        private XInputWave _xInputWave;
        private GameObject _goLoading;

        private CancellationTokenSource _loopCts;
        private CancellationTokenSource _autoHideCts;
        private Sequence _breatheSequence;
        private Sequence _wakeUpSequence;
        private Talk.State _lastTalkState;
        private float _normalizedOutputDb;

        public override string GetResourcePath()
        {
            return "Assets/Res/UI/MainUI/EmojiMainUI.prefab";
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
            _localizeStatus = Tr.Find("Status/Stat").GetComponent<LocalizeStringEvent>();
            _localizeStatus.StringReference = null;
            _localizeInfo = Tr.Find("Status/Info").GetComponent<LocalizeStringEvent>();
            _localizeInfo.StringReference = null;
            _textInfo = GetComponent<TextMeshProUGUI>(_localizeInfo, "");
            _textChat = Tr.Find("Chat").GetComponent<TextMeshProUGUI>();
            _textChat.text = "";
            GetComponent<HyperlinkText>(_textChat, "").OnClickLink
                .AddListener(_ => Application.OpenURL(AppPresets.Instance.ActivationURL));
            _trEmotion = Tr.Find("Emotion");
            _textEmotion = _trEmotion.GetComponent<TextMeshProUGUI>();
            _textEmotion.text = "";
            GetComponent<XButton>(_textEmotion).onClick.AddListener(() => Context.App.ToggleChatState().Forget());
            _goLoading = Tr.Find("Loading").gameObject;
            _trSet = GetComponent<RectTransform>(Tr, "BtnSet");
            _trSet.GetComponent<XButton>().onClick.AddListener(() => { ShowModuleUI<SettingsUI>().Forget(); });
            _xInputWave = Tr.Find("Spectrum").GetComponent<XInputWave>();
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
            Context.App.Talk.OnStateUpdate -= OnTalkStateUpdate;
            Context.App.Talk.OnStateUpdate += OnTalkStateUpdate;
            Context.App.Talk.OnEmotionUpdate -= OnTalkEmotionUpdate;
            Context.App.Talk.OnEmotionUpdate += OnTalkEmotionUpdate;
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
            Context.App.Talk.OnEmotionUpdate -= OnTalkEmotionUpdate;
            Context.App.Talk.OnInfoUpdate -= OnTalkInfoUpdate;
            Context.App.Talk.OnChatUpdate -= OnTalkChatUpdate;
            AppSettings.Instance.OnAutoHideUIUpdate -= OnAutoHideUIUpdate;
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
                _loopCts = null;
            }

            ClearAutoHideCts();
            KillCompVisibleAnim();
            StopBreathingAnimation();
            StopWakeUpAnimation();
            await UniTask.CompletedTask;
        }

        private async UniTaskVoid LoopUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(SpectrumUpdateInterval / 2, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UpdateInputWave(token);
                await UniTask.Delay(SpectrumUpdateInterval / 2, DelayType.Realtime, PlayerLoopTiming.Update, token);
                await UpdateOutputWave(token);
            }
        }

        private async UniTask UpdateInputWave(CancellationToken token)
        {
            if (!_xInputWave.gameObject.activeInHierarchy) return;
            await UniTask.SwitchToThreadPool();
            var codec = Context.App.GetCodec();
            var inputDirty = codec != null && _xInputWave.UpdateSpectrumData(codec);
            await UniTask.SwitchToMainThread(token);
            if (inputDirty) _xInputWave.SetVerticesDirty();
        }

        private async UniTask UpdateOutputWave(CancellationToken token)
        {
            if (!_trEmotion.gameObject.activeInHierarchy) return;
            await UniTask.SwitchToThreadPool();
            QuantizeOutputWave();
            await UniTask.SwitchToMainThread(token);
            var scale = 1 + _normalizedOutputDb * OutputScaleFactor;
            _trEmotion.localScale = Vector3.Lerp(_trEmotion.localScale,
                new Vector3(scale, scale, 1), 0.7f);
        }

        private void QuantizeOutputWave()
        {
            var codec = Context.App.GetCodec();
            if (codec == null) return;
            if (!codec.GetOutputSpectrum(true, out var outputSpectrum)) return;
            var sums = 0.0f;
            foreach (var sample in outputSpectrum) sums += sample;
            _normalizedOutputDb = Tools.Linear2dB(Math.Max(sums, 0) / outputSpectrum.Length);
        }

        private void UpdateLoadingState()
        {
            _goLoading.SetActive(Context.App.Talk.Stat is Talk.State.Starting);
        }

        private void OnTalkStateUpdate(Talk.State state)
        {
            ClearAutoHideCts();
            DetectCompVisible();
            if (state is Talk.State.Idle or Talk.State.Connecting) StartBreathingAnimation();
            else StopBreathingAnimation();
            if (_lastTalkState == Talk.State.Connecting && state == Talk.State.Listening)
                PlayWakeUpAnimation();
            UpdateLoadingState();
            _localizeStatus.StringReference = Lang.GetRef(Talk.State2LocalizedKey(state));
            _lastTalkState = state;
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

        private void OnTalkEmotionUpdate(string emotion)
        {
            _textEmotion.text = Emojis.GetValueOrDefault(emotion, Emojis["neutral"]);
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

        private void StartBreathingAnimation()
        {
            if (_breatheSequence != null) return;
            _breatheSequence = DOTween.Sequence();
            _breatheSequence.Append(_trEmotion.DOScale(1.1f, 2f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOScale(1f, 2f).SetEase(Ease.InOutSine))
                .SetLoops(-1);
        }

        private void StopBreathingAnimation()
        {
            if (_breatheSequence == null) return;
            _breatheSequence.Kill();
            _breatheSequence = null;
            _trEmotion.localScale = Vector3.one;
        }

        private void PlayWakeUpAnimation()
        {
            StopWakeUpAnimation();
            _wakeUpSequence = DOTween.Sequence();
            _wakeUpSequence.Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, 5), 0.1f).SetEase(Ease.OutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, -5), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, 3), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(new Vector3(0, 0, -2), 0.1f).SetEase(Ease.InOutSine))
                .Append(_trEmotion.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.OutSine));
        }

        private void StopWakeUpAnimation()
        {
            if (_wakeUpSequence == null) return;
            _wakeUpSequence.Kill();
            _wakeUpSequence = null;
            _trEmotion.localRotation = Quaternion.identity;
        }
    }
}