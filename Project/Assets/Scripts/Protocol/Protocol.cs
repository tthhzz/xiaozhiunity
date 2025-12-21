using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XiaoZhi.Unity
{
    public enum ListeningMode
    {
        AutoStop,
        ManualStop
    }

    public enum AbortReason
    {
        None,
        WakeWordDetected
    }

    public abstract class Protocol: IDisposable
    {
        public delegate void OnAudioDataReceived(ReadOnlySpan<byte> data);

        public delegate void OnJsonMessageReceived(JObject message);

        public delegate void OnAudioChannelClosed();

        public delegate void OnAudioChannelOpened();

        public delegate void OnNetworkErrorOccurred(string message);

        public event OnAudioDataReceived OnIncomingAudio;
        public event OnJsonMessageReceived OnIncomingJson;
        public event OnAudioChannelClosed OnChannelClosed;
        public event OnAudioChannelOpened OnChannelOpened;
        public event OnNetworkErrorOccurred OnNetworkError;

        public int ServerSampleRate { get; protected set; }
        public string SessionId { get; protected set; }

        public abstract void Start();
        public abstract void Dispose();
        public abstract UniTask<bool> OpenAudioChannel();
        public abstract UniTask CloseAudioChannel();
        public abstract bool IsAudioChannelOpened();
        public abstract UniTask SendAudio(ReadOnlyMemory<byte> data);

        public virtual async UniTask SendAbortSpeaking(AbortReason reason)
        {
            if (reason == AbortReason.WakeWordDetected)
            {
                await SendText(JsonConvert.SerializeObject(new
                {
                    session_id = SessionId,
                    type = "abort",
                    reason = "wake_word_detected"
                }));
            }
            else
            {
                await SendText(JsonConvert.SerializeObject(new
                {
                    session_id = SessionId,
                    type = "abort"
                }));
            }
        }

        public virtual async UniTask SendWakeWordDetected(string wakeWord)
        {
            await SendText(JsonConvert.SerializeObject(new
            {
                session_id = SessionId,
                type = "listen",
                state = "detect",
                text = wakeWord
            }));
        }

        public virtual async UniTask SendStartListening(ListeningMode mode)
        {
            await SendText(JsonConvert.SerializeObject(new
            {
                session_id = SessionId,
                type = "listen",
                state = "start",
                mode = mode.ToString().ToLower()
            }));
        }

        public virtual async UniTask SendStopListening()
        {
            await SendText(JsonConvert.SerializeObject(new
            {
                session_id = SessionId,
                type = "listen",
                state = "stop"
            }));
        }

        public virtual async UniTask SendIotDescriptors(string descriptors)
        {
            await using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);
            await jsonWriter.WriteStartObjectAsync();
            await jsonWriter.WritePropertyNameAsync("session_id");
            await jsonWriter.WriteValueAsync(SessionId);
            await jsonWriter.WritePropertyNameAsync("type");
            await jsonWriter.WriteValueAsync("iot");
            await jsonWriter.WritePropertyNameAsync("update");
            await jsonWriter.WriteValueAsync(true);
            await jsonWriter.WritePropertyNameAsync("descriptors");
            await jsonWriter.WriteRawValueAsync(descriptors);
            await jsonWriter.WriteEndObjectAsync();
            await SendText(stringWriter.ToString());
        }

        public virtual async UniTask SendIotStates(string states)
        {
            await using var stringWriter = new StringWriter();
            using var jsonWriter = new JsonTextWriter(stringWriter);
            await jsonWriter.WriteStartObjectAsync();
            await jsonWriter.WritePropertyNameAsync("session_id");
            await jsonWriter.WriteValueAsync(SessionId);
            await jsonWriter.WritePropertyNameAsync("type");
            await jsonWriter.WriteValueAsync("iot");
            await jsonWriter.WritePropertyNameAsync("update");
            await jsonWriter.WriteValueAsync(true);
            await jsonWriter.WritePropertyNameAsync("states");
            await jsonWriter.WriteRawValueAsync(states);
            await jsonWriter.WriteEndObjectAsync();
            await SendText(stringWriter.ToString());
        }
        
        protected abstract UniTask SendText(string text);

        protected void InvokeOnAudioData(ReadOnlySpan<byte> data)
        {
            OnIncomingAudio?.Invoke(data);
        }

        protected void InvokeOnJsonMessage(JObject message)
        {
            OnIncomingJson?.Invoke(message);
        }

        protected void InvokeOnChannelClosed()
        {
            OnChannelClosed?.Invoke();
        }

        protected void InvokeOnChannelOpened()
        {
            OnChannelOpened?.Invoke();
        }

        protected void InvokeOnNetworkError(string message)
        {
            OnNetworkError?.Invoke(message);
        }
    }
}