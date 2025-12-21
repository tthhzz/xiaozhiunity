using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Video;

namespace XiaoZhi.Unity
{
    public class VideoPlayer : IDisposable
    {
        private readonly UnityEngine.Video.VideoPlayer _videoPlayer;

        public bool IsPlaying => _videoPlayer.isPlaying;

        public VideoPlayer(UnityEngine.Video.VideoPlayer videoPlayer)
        {
            _videoPlayer = videoPlayer;
            _videoPlayer.enabled = false;
        }

        public async UniTask Play(VideoClip videoClip, int loopTimes = 1, CancellationToken cancellationToken = default)
        {
            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = videoClip;
            _videoPlayer.enabled = true;
            _videoPlayer.Play();
            await new VideoPlayerEventHandler(_videoPlayer, loopTimes, cancellationToken).OnInvokeAsync();
        }

        public async UniTask Play(string url, int loopTimes = 1, CancellationToken cancellationToken = default)
        {
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = url;
            _videoPlayer.enabled = true;
            _videoPlayer.Play();
            await new VideoPlayerEventHandler(_videoPlayer, loopTimes, cancellationToken).OnInvokeAsync();
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            _videoPlayer.Stop();
            _videoPlayer.clip = null;
            _videoPlayer.enabled = false;
        }

        public void Dispose()
        {
        }

        private class VideoPlayerEventHandler : IUniTaskSource, IDisposable
        {
            private readonly UnityEngine.Video.VideoPlayer _videoPlayer;
            private int _loopTimes;

            private readonly CancellationToken _cancellationToken;
            private CancellationTokenRegistration _registration;
            private bool _isDisposed;

            private UniTaskCompletionSourceCore<AsyncUnit> _core;

            public VideoPlayerEventHandler(UnityEngine.Video.VideoPlayer videoPlayer, int loopTimes,
                CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                _loopTimes = loopTimes;
                if (cancellationToken.IsCancellationRequested)
                {
                    _isDisposed = true;
                    return;
                }

                _videoPlayer = videoPlayer;
                _videoPlayer.isLooping = true;
                _videoPlayer.loopPointReached += Invoke;
                _videoPlayer.errorReceived += InvokeError;
                if (cancellationToken.CanBeCanceled)
                {
                    _registration =
                        cancellationToken.RegisterWithoutCaptureExecutionContext(CancellationCallback, this);
                }

                TaskTracker.TrackActiveTask(this, 3);
            }

            public UniTask OnInvokeAsync()
            {
                _core.Reset();
                if (_isDisposed)
                {
                    _core.TrySetCanceled(_cancellationToken);
                }

                return new UniTask(this, _core.Version);
            }

            private void Invoke(UnityEngine.Video.VideoPlayer player)
            {
                if (_loopTimes <= 0) return;
                _loopTimes -= 1;
                if (_loopTimes == 0) _core.TrySetResult(AsyncUnit.Default);
            }

            private void InvokeError(UnityEngine.Video.VideoPlayer player, string message)
            {
                _core.TrySetException(new Exception(message));
            }

            private static void CancellationCallback(object state)
            {
                var self = (AsyncUnityEventHandler)state;
                self.Dispose();
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;
                TaskTracker.RemoveTracking(this);
                _registration.Dispose();
                _videoPlayer.Stop();
                _videoPlayer.loopPointReached -= Invoke;
                _videoPlayer.errorReceived -= InvokeError;
                _core.TrySetCanceled(_cancellationToken);
            }

            void IUniTaskSource.GetResult(short token)
            {
                try
                {
                    _core.GetResult(token);
                }
                finally
                {
                    Dispose();
                }
            }

            UniTaskStatus IUniTaskSource.GetStatus(short token)
            {
                return _core.GetStatus(token);
            }

            UniTaskStatus IUniTaskSource.UnsafeGetStatus()
            {
                return _core.UnsafeGetStatus();
            }

            void IUniTaskSource.OnCompleted(Action<object> continuation, object state, short token)
            {
                _core.OnCompleted(continuation, state, token);
            }
        }
    }
}