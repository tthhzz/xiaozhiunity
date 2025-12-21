using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace XiaoZhi.Unity
{
    public class AnimationCtrl
    {
        private enum State
        {
            Idle,
            Stand,
            Greet,
            Talk,
            Bye
        }

        private const float StandTickIntervalMin = 10.0f;
        private const float StandTickIntervalMax = 30.0f;

        private const string NameIdle = "idle";

        private static readonly string[] ByeKeywords =
        {
            "Bye",
            "See you",
            "See ya",
            "Catch you later",
            "I'm out",
            "拜拜",
            "再见",
            "回见",
            "回头见",
            "下次见",
            "明天见",
            "下周见",
            "晚安"
        };

        private readonly Animator _animator;
        private readonly AnimatorOverrideController _controller;
        private readonly Dictionary<string, AnimationClip> _clipMap;
        private readonly AnimationClip[] _clipSlots;
        private readonly AppPresets.AnimationLib _lib;
        private readonly Talk _talk;
        private readonly AnimatorProxy _animProxy;

        private State _state;
        private bool _readyToSpeak;
        private bool _firstTimeSpeaking;
        private int _currentSlot;
        private string _currentState;
        private float _nextStandTime = float.MaxValue;
        private bool _overrideAnim;

        public AnimationCtrl(Animator animator, AppPresets.AnimationLib lib, Talk talk)
        {
            _animator = animator;
            var clips = _animator.runtimeAnimatorController.animationClips;
            _clipMap = clips.ToDictionary(i => i.name);
            _clipSlots = clips.Where(i => i.name != NameIdle).ToArray();
            _controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            _animator.runtimeAnimatorController = _controller;
            _lib = lib;
            _talk = talk;
            _talk.OnStateUpdate += OnTalkStateUpdate;
            _talk.OnChatUpdate += OnTalkChatUpdate;
            _animProxy = _animator.GetBehaviour<AnimatorProxy>();
            _animProxy.StateUpdate += OnAnimStateUpdate;
        }

        public void Dispose()
        {
            Object.Destroy(_animator.runtimeAnimatorController);
            _animProxy.StateUpdate -= OnAnimStateUpdate;
            _talk.OnStateUpdate -= OnTalkStateUpdate;
            _talk.OnChatUpdate -= OnTalkChatUpdate;
        }

        public void OverrideAnimate(params string[] labels)
        {
            _overrideAnim = true;
            Labels2Animation(labels);
        }

        public void OverrideAnimate(AnimationClip clip)
        {
            _overrideAnim = true;
            SetAnimState(FillSlot(clip), false);
        }

        public void RevertAnimation()
        {
            _overrideAnim = false;
            State2Animation(false);
        }

        private void OnTalkStateUpdate(Talk.State state)
        {
            switch (state)
            {
                case Talk.State.Speaking:
                    _readyToSpeak = true;
                    break;
                case Talk.State.Listening:
                    if (_state != State.Bye) SetState(State.Idle);
                    break;
                case Talk.State.Dancing:
                    break;
                case Talk.State.Unknown:
                case Talk.State.Starting:
                case Talk.State.Idle:
                case Talk.State.Connecting:
                case Talk.State.Activating:
                case Talk.State.Error:
                default:
                    _overrideAnim = false;
                    _firstTimeSpeaking = false;
                    if (_state != State.Bye) SetState(State.Idle);
                    break;
            }
        }

        private void OnTalkChatUpdate(string chat)
        {
            if (chat.StartsWith("% ")) return;
            if (_readyToSpeak)
            {
                _readyToSpeak = false;
                if (ByeKeywords.Any(i => chat.Contains(i, StringComparison.CurrentCultureIgnoreCase)))
                {
                    SetState(State.Bye);
                }
                else if (!_firstTimeSpeaking)
                {
                    _firstTimeSpeaking = true;
                    SetState(State.Greet);
                }
                else
                {
                    SetState(State.Talk);
                }
            }
        }

        private void SetState(State state)
        {
            if (_state == state) return;
            _state = state;
            if (!_overrideAnim)
                State2Animation();
        }

        private void State2Animation(bool? fadeIn = null)
        {
            var labels = UnityEngine.Pool.ListPool<string>.Get();
            State2Labels(labels);
            Labels2Animation(labels, fadeIn);
            UnityEngine.Pool.ListPool<string>.Release(labels);
            if (_state == State.Idle && _talk.Stat == Talk.State.Listening)
                _nextStandTime = Time.time + Random.Range(StandTickIntervalMin, StandTickIntervalMax);
            else if (_state != State.Idle)
                _nextStandTime = float.MaxValue;
        }

        private void Labels2Animation(IEnumerable<string> labels, bool? fadeIn = null)
        {
            var state = NameIdle;
            var metas = _lib.MatchAll(labels);
            var meta = AppPresets.AnimationLib.Random(metas);
            if (meta != null && meta.Name != NameIdle)
            {
                if (!_clipMap.TryGetValue(meta.Name, out var clip))
                {
                    clip = Addressables.LoadAssetAsync<AnimationClip>(meta.Path).WaitForCompletion();
                    _clipMap[meta.Name] = clip;
                }

                if (clip)
                {
                    state = FillSlot(clip);
                    fadeIn ??= meta.FadeIn;
                }
            }

            SetAnimState(state, fadeIn ?? true);
        }

        private string FillSlot(AnimationClip clip)
        {
            _currentSlot = (_currentSlot + 1) % _clipSlots.Length;
            var slotClip = _clipSlots[_currentSlot];
            _controller[slotClip] = clip;
            return slotClip.name;
        }

        private void SetAnimState(string state, bool fadeIn)
        {
            if (_currentState != state)
            {
                if (fadeIn) _animator.CrossFadeInFixedTime(state, 0.7f, 0);
                else _animator.Play(state, 0, 0);
                _currentState = state;
            }
        }

        private void State2Labels(List<string> labels)
        {
            switch (_state)
            {
                case State.Idle:
                    labels.Add("idle");
                    break;
                case State.Greet:
                    labels.Add("greet");
                    break;
                case State.Talk:
                    labels.Add("talk");
                    break;
                case State.Stand:
                    labels.Add("stand");
                    break;
                case State.Bye:
                    labels.Add("bye");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnAnimStateUpdate(AnimatorStateInfo stateInfo)
        {
            if (!string.IsNullOrEmpty(_currentState) &&
                stateInfo.IsName(_currentState))
            {
                const float endTime = 0.999f;
                if (_overrideAnim && stateInfo.normalizedTime >= endTime)
                {
                    _overrideAnim = false;
                    State2Animation();
                }
                else if (stateInfo is { loop: false, normalizedTime: >= endTime })
                {
                    if (_state is State.Greet or State.Stand or State.Bye) SetState(State.Idle);
                    else State2Animation();
                }
                else if (_state == State.Idle && _talk.Stat == Talk.State.Listening && stateInfo.loop &&
                         Time.time >= _nextStandTime)
                {
                    _nextStandTime = float.MaxValue;
                    SetState(State.Stand);
                }
            }
        }
    }
}