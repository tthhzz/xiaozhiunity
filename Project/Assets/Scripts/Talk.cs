using System;
using UnityEngine.Localization;

namespace XiaoZhi.Unity
{
    public class Talk
    {
        public enum State
        {
            Unknown,
            Starting,
            Idle,
            Connecting,
            Listening,
            Speaking,
            Activating,
            Error,
            Dancing,
        }

        public static string State2LocalizedKey(State state)
        {
            return state switch
            {
                State.Unknown or State.Idle => "STATE_STANDBY",
                State.Connecting => "STATE_CONNECTING",
                State.Listening => "STATE_LISTENING",
                State.Speaking => "STATE_SPEAKING",
                State.Starting => "STATE_STARTING",
                State.Activating => "ACTIVATION",
                State.Error => "STATE_ERROR",
                State.Dancing => "STATE_DANCING",
                _ => null
            };
        }

        public event Action<State> OnStateUpdate;
        public event Action<LocalizedString> OnInfoUpdate;
        public event Action<string> OnChatUpdate;
        public event Action<string> OnEmotionUpdate;

        public bool IsReady() => _stat is State.Idle or State.Connecting
            or State.Speaking or State.Listening or State.Dancing;

        private State _stat;

        private State _bufStat;

        public State Stat
        {
            get => _stat;
            set
            {
                if (_stat == value) return;
                _stat = value;
                if (_stat == State.Listening)
                    Emotion = "neutral";
                else if (_stat == State.Dancing)
                    Emotion = "happy";
                else if (_stat != State.Speaking)
                    Emotion = "sleep";
                Info = null;
                OnStateUpdate?.Invoke(value);
            }
        }

        private LocalizedString _info;

        public LocalizedString Info
        {
            get => _info;
            set
            {
                if (_info == value) return;
                _info = value;
                OnInfoUpdate?.Invoke(value);
            }
        }

        private string _chat;

        public string Chat
        {
            get => _chat;
            set
            {
                if (_chat == value) return;
                _chat = value;
                OnChatUpdate?.Invoke(value);
            }
        }

        private string _emotion = "sleep";

        public string Emotion
        {
            get => _emotion;
            set
            {
                if (_emotion == value) return;
                _emotion = value;
                OnEmotionUpdate?.Invoke(value);
            }
        }
    }
}