using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static XiaoZhi.Unity.Talk;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity
{
    public enum BreakMode
    {
        None,
        Keyword,
        VAD,
        Free
    }

    public enum DisplayMode
    {
        Emoji,
        VRM,
        Girl,
        Boy
    }

    public enum ZoomMode
    {
        LongShot,
        MediumShot,
        CloseShot
    }

    public enum WallpaperType
    {
        Default,
        Sprite,
        Video,
        Gif
    }

    public class App : IDisposable
    {
        private const int LocalClipSampleRate = 16000;
        private const int LocalClipFrameSize = 60;

        private Context _context;
        private Protocol _protocol;
        private Talk _talk;
        public Talk Talk => _talk;

        private bool _voiceDetected;
        public bool VoiceDetected => _voiceDetected;
        private bool _aborted;
        private ListeningMode _listeningMode;
        private int _opusDecodeSampleRate = -1;
        private WakeService _wakeService;
        private OpusEncoder _opusEncoder;
        private OpusDecoder _opusDecoder;
        private OpusResampler _inputResampler;
        private OpusResampler _outputResampler;
        private OTA _ota;
        private readonly CancellationTokenSource _cts = new();
        private IDisplay _display;
        public IDisplay GetDisplay() => _display;
        private AudioCodec _codec;
        public AudioCodec GetCodec() => _codec;
        private DateTime _vadAbortedSilenceTime;
        private DynamicBuffer<short> _freeBuffer;
        private readonly AudioClipStreamReader _clipReader = new();
        private OpusResampler _clipResampler;
        private int _clipReadTime;
        private CancellationTokenSource _danceCts;

        public void Inject(Context context)
        {
            _context = context;
        }

        public async UniTaskVoid Start()
        {
            _talk = new Talk();
            _talk.OnStateUpdate += OnTalkStateUpdate;
            AppSettings.Load();
            await AppPresets.Load();
            await InitDisplay();
            await Lang.LoadLocale();
            _talk.Stat = State.Starting;
            if (!await CheckRequestPermission())
            {
                _talk.Stat = State.Error;
                _talk.Info = Lang.GetRef("Permission_Request_Failed");
                return;
            }

            await CheckInternetReachability();
            if (!await CheckNewVersion(_cts.Token))
            {
                _talk.Stat = State.Error;
                _talk.Info = Lang.GetRef("ACTIVATION_FAILED_TIPS");
                return;
            }

            if (AppPresets.Instance.EnableWakeService)
            {
                _talk.Info = Lang.GetRef("LOADING_RESOURCES");
                await PrepareResource(_cts.Token);
                _talk.Info = Lang.GetRef("LOADING_MODEL");
                await InitializeWakeService();
            }

            InitializeAudio();
            if (!_codec.GetInputDevice(out _))
            {
                _talk.Stat = State.Error;
                _talk.Info = Lang.GetRef("STATE_MIC_NOT_FOUND");
                return;
            }

            _talk.Info = Lang.GetRef("LOADING_THINGS");
            await _context.ThingManager.Load();
            
            // 使用默认vision服务（和py-xiaozhi一样）
            if (string.IsNullOrEmpty(AppSettings.Instance.GetCameraExplainUrl()))
            {
                Debug.Log("[App] Setting default vision service URL");
                AppSettings.Instance.SetCameraExplainUrl("https://api.xiaozhi.me/vision/explain");
            }
            
            InitializeCamera();
            InitializeProtocol();
            StartDisplay();
            _talk.Stat = State.Idle;
            UniTask.Void(MainLoop, _cts.Token);
        }

        private async UniTaskVoid MainLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                CheckProtocol();
                InputAudio();
                OutputClip(Time.deltaTime);
                _display.Update(Time.deltaTime);
                await _codec.Update(token);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _display?.Dispose();
            _codec?.Dispose();
            _wakeService?.Dispose();
            _protocol?.Dispose();
            _opusDecoder?.Dispose();
            _opusEncoder?.Dispose();
            _inputResampler?.Dispose();
            _outputResampler?.Dispose();
            _clipResampler?.Dispose();
        }

        private async UniTask InitDisplay()
        {
            await _context.UIManager.Load();
            var displayMode = AppSettings.Instance.GetDisplayMode();
            
            // 处理向后兼容：VRM 模式根据当前选择的角色模型决定
            if (displayMode == DisplayMode.VRM)
            {
                var vrmModelIndex = AppSettings.Instance.GetVRMModel();
                var vrmModels = AppPresets.Instance.VRMCharacterModels;
                if (vrmModels != null && vrmModels.Length > 0)
                {
                    // 根据角色名称判断是 Girl 还是 Boy
                    var modelName = vrmModels[Mathf.Clamp(vrmModelIndex, 0, vrmModels.Length - 1)].Name;
                    displayMode = modelName.Equals("Girl", StringComparison.InvariantCultureIgnoreCase) 
                        ? DisplayMode.Girl 
                        : DisplayMode.Boy;
                    // 更新设置
                    AppSettings.Instance.SetDisplayMode(displayMode);
                }
            }
            
            // 如果选择 Girl 或 Boy，确保 VRM 模型索引正确
            if (displayMode == DisplayMode.Girl || displayMode == DisplayMode.Boy)
            {
                var vrmModels = AppPresets.Instance.VRMCharacterModels;
                if (vrmModels != null && vrmModels.Length > 0)
                {
                    var targetName = displayMode == DisplayMode.Girl ? "Girl" : "Boy";
                    bool found = false;
                    Debug.Log($"正在查找 VRM 模型: {targetName}，可用模型数量: {vrmModels.Length}");
                    for (int i = 0; i < vrmModels.Length; i++)
                    {
                        var modelName = vrmModels[i].Name;
                        Debug.Log($"模型 [{i}]: {modelName}");
                        if (modelName.Equals(targetName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            Debug.Log($"找到匹配的模型: {modelName}，索引: {i}");
                            AppSettings.Instance.SetVRMModel(i);
                            found = true;
                            break;
                        }
                    }
                    // 如果找不到对应的模型，使用默认索引 0
                    if (!found)
                    {
                        Debug.LogWarning($"找不到名为 '{targetName}' 的 VRM 模型，使用默认模型索引 0");
                        AppSettings.Instance.SetVRMModel(0);
                    }
                }
                else
                {
                    Debug.LogError("VRM 模型列表为空，无法设置角色模型");
                }
            }
            
            _display = displayMode switch
            {
                DisplayMode.Emoji => new EmojiDisplay(_context),
                DisplayMode.Girl => new VRMDisplay(_context),
                DisplayMode.Boy => new VRMDisplay(_context),
                _ => throw new ArgumentOutOfRangeException()
            };

            await _display.Load();
        }

        private void StartDisplay()
        {
            _display.Start();
        }

        private async UniTask PrepareResource(CancellationToken cancellationToken)
        {
            var keyword = AppPresets.Instance.GetKeyword(Lang.Code);
#if UNITY_ANDROID && !UNITY_EDITOR
            var streamingAssets = new[]
            {
                keyword.SpotterModelConfigTransducerEncoder,
                keyword.SpotterModelConfigTransducerDecoder,
                keyword.SpotterModelConfigTransducerJoiner,
                keyword.SpotterModelConfigToken,
                keyword.SpotterKeyWordsFile,
                AppPresets.Instance.VadModelConfig
            };
#else
            var streamingAssets = new[]
            {
                keyword.SpotterKeyWordsFile
            };
#endif
            await UniTask.WhenAll(streamingAssets.Select(i =>
                FileUtility.CopyStreamingAssetsToDataPath(i, false, cancellationToken)));
            await UniTask.SwitchToMainThread(cancellationToken);
        }

        private void OnTalkStateUpdate(State state)
        {
            Debug.Log("Talk state: " + state);
            switch (state)
            {
                case State.Listening:
                    UpdateIotStates().Forget();
                    _protocol.SendStartListening(_listeningMode).Forget();
                    _opusDecoder.ResetState();
                    _opusEncoder.ResetState();
                    break;
                case State.Speaking:
                    _opusDecoder.ResetState();
                    if (_wakeService is { IsRunning: true })
                        _wakeService.ClearVadBuffer();
                    break;
            }
        }

        public async UniTask UpdateIotStates()
        {
            if (_context.ThingManager.GetStatesJson(out var json, true))
                await _protocol.SendIotStates(json);
        }

        private async UniTask AbortSpeaking(AbortReason reason)
        {
            if (_aborted) return;
            Debug.Log("Abort speaking");
            _aborted = true;
            await _protocol.SendAbortSpeaking(reason);
        }

        private void SetListeningMode(ListeningMode mode)
        {
            _listeningMode = mode;
            _talk.Stat = State.Listening;
        }

        private async UniTask<bool> OpenAudioChannel()
        {
            if (_protocol.IsAudioChannelOpened()) return true;
            _talk.Stat = State.Connecting;
            if (!await _protocol.OpenAudioChannel())
            {
                _talk.Stat = State.Idle;
                _context.UIManager.ShowNotificationUI(Lang.GetRef("Connect_Failed_Tips")).Forget();
                return false;
            }

            return true;
        }

        public async UniTask ToggleChatState()
        {
            switch (_talk.Stat)
            {
                case State.Idle:
                    if (!await OpenAudioChannel()) return;
                    SetListeningMode(ListeningMode.AutoStop);
                    break;
                case State.Speaking:
                    await AbortSpeaking(AbortReason.None);
                    break;
                case State.Listening:
                    await _protocol.CloseAudioChannel();
                    _talk.Stat = State.Idle;
                    break;
                case State.Dancing:
                    await CancelDance();
                    break;
            }
        }

        private void InputAudio()
        {
            var times = Mathf.CeilToInt(Time.deltaTime * 1000 / AudioCodec.InputFrameSizeMs);
            for (var i = 0; i < times; i++)
            {
                if (!_codec.InputData(out var data)) break;
                if (_aborted && DateTime.Now < _vadAbortedSilenceTime) continue;
                if (_codec.InputSampleRate != _inputResampler.OutputSampleRate)
                    _inputResampler.Process(data, out data);
                if (_aborted && _freeBuffer.Count > 0)
                {
                    _freeBuffer.Write(data);
                    continue;
                }

                if (_talk.Stat is State.Listening)
                    _opusEncoder.Encode(data, opus => { _protocol.SendAudio(opus).Forget(); });
                if (_wakeService is { IsRunning: true }) _wakeService.Feed(data);
            }
        }

        private void OutputAudio(ReadOnlySpan<byte> opus)
        {
            if (_talk.Stat != State.Speaking) return;
            if (_aborted) return;
            if (!_opusDecoder.Decode(opus, out var pcm)) return;
            if (_opusDecodeSampleRate != _codec.OutputSampleRate) _outputResampler.Process(pcm, out pcm);
            _codec.OutputData(pcm);
        }

        private void SendAudio(ReadOnlySpan<short> data)
        {
            var frameSize = AppPresets.Instance.ServerInputSampleRate / 1000 * AppPresets.Instance.OpusFrameDurationMs *
                            _codec.InputChannels;
            var dataLen = data.Length;
            for (var i = 0; i < dataLen; i += frameSize)
            {
                var end = Math.Min(i + frameSize, dataLen);
                _opusEncoder.Encode(data[i..end], opus => { _protocol.SendAudio(opus).Forget(); });
            }
        }

        private void SetDecodeSampleRate(int sampleRate)
        {
            if (_opusDecodeSampleRate != sampleRate)
            {
                _opusDecodeSampleRate = sampleRate;
                _opusDecoder.Dispose();
                _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, AppPresets.Instance.OpusFrameDurationMs);
            }

            if (_opusDecodeSampleRate != _codec.OutputSampleRate)
            {
                Debug.Log($"Resampling audio from {_opusDecodeSampleRate} to {_codec.OutputSampleRate}");
                _outputResampler ??= new OpusResampler();
                _outputResampler.Configure(_opusDecodeSampleRate, _codec.OutputSampleRate);
            }

            if (LocalClipSampleRate != _codec.OutputSampleRate)
            {
                _clipResampler ??= new OpusResampler();
                _clipResampler.Configure(LocalClipSampleRate, _codec.OutputSampleRate);
            }
        }

        private async UniTask InitializeWakeService()
        {
            _freeBuffer = new DynamicBuffer<short>();
            _wakeService = new SherpaOnnxWakeService();
            _wakeService.Initialize(AppPresets.Instance.ServerInputSampleRate);
            _wakeService.OnVadStateChanged += speaking =>
            {
                if (_voiceDetected == speaking) return;
                _voiceDetected = speaking;
                if (!_voiceDetected || _talk.Stat != State.Speaking) return;
                switch (AppSettings.Instance.GetBreakMode())
                {
                    case BreakMode.VAD:
                        var count1 = _wakeService.ReadVadBuffer(ref _freeBuffer.Memory);
                        if (count1 == 0) break;
                        Debug.Log("Break by vad.");
                        _vadAbortedSilenceTime = DateTime.Now.AddMilliseconds(1000);
                        AbortSpeaking(AbortReason.WakeWordDetected).Forget();
                        break;
                    case BreakMode.Free:
                        var count2 = _wakeService.ReadVadBuffer(ref _freeBuffer.Memory);
                        if (count2 == 0) break;
                        _freeBuffer.SetCount(count2);
                        Debug.Log($"Break by free {_freeBuffer.Count}");
                        AbortSpeaking(AbortReason.WakeWordDetected).Forget();
                        break;
                }
            };
            _wakeService.OnWakeWordDetected += wakeWord =>
            {
                UniTask.Void(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    switch (_talk.Stat)
                    {
                        case State.Idle:
                        {
                            if (!await OpenAudioChannel()) return;
                            await _protocol.SendWakeWordDetected(wakeWord);
                            SetListeningMode(ListeningMode.AutoStop);
                            break;
                        }
                        case State.Speaking:
                            if (AppSettings.Instance.GetBreakMode() == BreakMode.Keyword)
                            {
                                Debug.Log("Break by keyword.");
                                await AbortSpeaking(AbortReason.WakeWordDetected);
                            }

                            break;
                        case State.Dancing:
                            await CancelDance();
                            break;
                    }
                });
            };
            await UniTask.SwitchToThreadPool();
            _wakeService.Start();
            await UniTask.SwitchToMainThread();
        }

        private void InitializeAudio()
        {
            var inputSampleRate = AppPresets.Instance.AudioInputSampleRate;
            var outputSampleRate = AppPresets.Instance.AudioOutputSampleRate;
            _opusDecodeSampleRate = outputSampleRate;
            _opusDecoder = new OpusDecoder(_opusDecodeSampleRate, 1, AppPresets.Instance.OpusFrameDurationMs);
            var resampleRate = AppPresets.Instance.ServerInputSampleRate;
            _opusEncoder = new OpusEncoder(resampleRate, 1, AppPresets.Instance.OpusFrameDurationMs);
            _inputResampler = new OpusResampler();
            _inputResampler.Configure(inputSampleRate, resampleRate);
            _codec = new FMODAudioCodec(inputSampleRate, 1, outputSampleRate, 1);
            _codec.SetOutputVolume(AppSettings.Instance.GetOutputVolume());
            _codec.Start();
            AppSettings.Instance.OnOutputVolumeUpdate += volume => { _codec.SetOutputVolume(volume); };
        }

        private void InitializeCamera()
        {
            Debug.Log("[App] Initializing camera...");
            
            // 获取智谱AI配置
            var vlApiKey = AppSettings.Instance.GetCameraVLApiKey();
            var vlUrl = AppSettings.Instance.GetCameraVLUrl();
            var vlModel = AppSettings.Instance.GetCameraModel();
            
            // 获取外部服务配置
            var explainUrl = AppSettings.Instance.GetCameraExplainUrl();
            var explainToken = AppSettings.Instance.GetCameraExplainToken();
            
            // 清理旧的错误URL（只清理tenclass域名下的错误路径）
            if (!string.IsNullOrEmpty(explainUrl) && 
                explainUrl.Contains("api.tenclass.net") && 
                explainUrl.Contains("/vision/explain"))
            {
                Debug.LogWarning($"[App] Clearing invalid tenclass URL: {explainUrl}");
                AppSettings.Instance.SetCameraExplainUrl("");
                explainUrl = "";
            }
            
            Debug.Log($"[App] VL API Key: {(!string.IsNullOrEmpty(vlApiKey) ? "Set" : "Not set")}");
            Debug.Log($"[App] VL URL: {vlUrl}");
            Debug.Log($"[App] VL Model: {vlModel}");
            Debug.Log($"[App] Explain URL: {explainUrl}");
            Debug.Log($"[App] Explain Token: {(!string.IsNullOrEmpty(explainToken) ? "Set" : "Not set")}");
            
            // 配置智谱AI（优先）
            if (!string.IsNullOrEmpty(vlApiKey))
            {
                CameraManager.Instance.SetVLApiKey(vlApiKey);
                CameraManager.Instance.SetVLUrl(vlUrl);
                CameraManager.Instance.SetVLModel(vlModel);
                Debug.Log("[App] Camera initialized with Zhipu AI mode");
            }
            // 配置外部服务（仅当有有效URL时）
            else if (!string.IsNullOrEmpty(explainUrl))
            {
                CameraManager.Instance.SetExplainUrl(explainUrl);
                CameraManager.Instance.SetExplainToken(explainToken);
                Debug.Log($"[App] Camera initialized with External explain service mode: {explainUrl}");
            }
            else
            {
                Debug.LogWarning("[App] Camera not configured. Please set VL API Key or wait for server vision config");
            }
        }

        private void InitializeProtocol()
        {
            _protocol = new WebSocketProtocol();
            _protocol.OnNetworkError += error => { _context.UIManager.ShowNotificationUI(error).Forget(); };
            _protocol.OnIncomingAudio += OutputAudio;
            _protocol.OnChannelOpened += () =>
            {
                if (_protocol.ServerSampleRate != _codec.OutputSampleRate)
                    Debug.Log(
                        $"Server sample rate {_protocol.ServerSampleRate} does not match device output sample rate {_codec.OutputSampleRate}, resampling may cause distortion");
                SetDecodeSampleRate(_protocol.ServerSampleRate);
                var json = _context.ThingManager.GetDescriptorsJson();
                UniTask.Void(async () =>
                {
                    await _protocol.SendIotDescriptors(json);
                    await UpdateIotStates();
                });
            };
            _protocol.OnChannelClosed += () =>
            {
                if (_talk.Stat is State.Speaking or State.Listening)
                {
                    _talk.Chat = "";
                    _talk.Stat = State.Idle;
                }
            };
            _protocol.OnIncomingJson += message =>
            {
                var type = message["type"].ToString();
                switch (type)
                {
                    case "hello":
                    {
                        break;
                    }
                    case "tts":
                    {
                        var state = message["state"].ToString();
                        switch (state)
                        {
                            case "start":
                            {
                                _aborted = false;
                                if (_talk.Stat is State.Idle or State.Listening)
                                    _talk.Stat = State.Speaking;
                                break;
                            }
                            case "stop":
                            {
                                if (_talk.Stat != State.Speaking) return;
                                if (_listeningMode == ListeningMode.ManualStop)
                                {
                                    _talk.Stat = State.Idle;
                                }
                                else
                                {
                                    _talk.Stat = State.Listening;
                                    if (_aborted && _freeBuffer.Count > 0)
                                    {
                                        SendAudio(_freeBuffer.Read());
                                        _freeBuffer.Clear();
                                    }
                                }

                                break;
                            }
                            case "sentence_start":
                            {
                                var text = message["text"].ToString();
                                if (!string.IsNullOrEmpty(text)) _talk.Chat = text;
                                break;
                            }
                        }

                        break;
                    }
                    case "stt":
                    {
                        var text = message["text"].ToString();
                        if (!string.IsNullOrEmpty(text)) _talk.Chat = text;
                        break;
                    }
                    case "llm":
                    {
                        var emotion = message["emotion"].ToString();
                        if (!string.IsNullOrEmpty(emotion)) _talk.Emotion = emotion;
                        break;
                    }
                    case "iot":
                    {
                        var commands = message["commands"];
                        if (commands == null) return;
                        foreach (var command in commands) _context.ThingManager.Invoke(command);
                        break;
                    }
                    case "alert":
                    {
                        var status = message["status"].ToString();
                        var content = message["message"].ToString();
                        var emotion = message["emotion"].ToString();
                        if (!string.IsNullOrEmpty(status) && !string.IsNullOrEmpty(content) &&
                            !string.IsNullOrEmpty(emotion))
                        {
                            _talk.Emotion = emotion;
                            _talk.Chat = $"{status}: {content}";
                        }

                        break;
                    }
                }
            };
            _protocol.Start();
        }

        private async UniTask<bool> CheckNewVersion(CancellationToken cancellationToken = default)
        {
            var success = false;
            var macAddr = AppSettings.Instance.GetMacAddress();
            var boardName = AppUtility.GetBoardName();
            _ota = new OTA();
            _ota.SetCheckVersionUrl(AppPresets.Instance.OtaVersionUrl);
            _ota.SetHeader("Device-Id", macAddr);
            _ota.SetHeader("Accept-Language", "zh-CN");
            _ota.SetHeader("User-Agent", $"{boardName}/{AppUtility.GetVersion()}");
            var postData = await OTA.LoadPostData(macAddr, boardName);
            _ota.SetPostData(postData);
            var showTips = true;
            const int maxRetry = 100;
            for (var i = 0; i < maxRetry; i++)
            {
                if (await _ota.CheckVersionAsync())
                {
                    if (string.IsNullOrEmpty(_ota.ActivationCode))
                    {
                        success = true;
                        break;
                    }

                    _talk.Stat = State.Activating;
                    _talk.Chat = _ota.ActivationMessage;
                    try
                    {
                        GUIUtility.systemCopyBuffer = Regex.Match(_ota.ActivationMessage, @"\d+").Value;
                        if (showTips)
                        {
                            showTips = false;
                            _context.UIManager.ShowNotificationUI(Lang.GetRef("ACTIVATION_CODE_COPIED")).Forget();
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                await UniTask.Delay(3 * 1000, cancellationToken: cancellationToken);
            }

            return success;
        }

        private async UniTask<bool> CheckRequestPermission()
        {
            var success = true;
            var result = await PermissionManager.RequestPermissions(PermissionType.ReadStorage,
                PermissionType.WriteStorage, PermissionType.Microphone);
            foreach (var i in result)
            {
                if (i.Granted) continue;
                var permissionName =
                    Lang.GetRef($"Permission_{Enum.GetName(typeof(PermissionType), i.Type)}");
                _context.UIManager.ShowNotificationUI(Lang.GetRef("Permission_One_Request_Failed",
                    new KeyValuePair<string, IVariable>("0", permissionName))).Forget();
                success = false;
            }

            return success;
        }

        private async UniTask CheckInternetReachability()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                _talk.Info = Lang.GetRef("State_Internet_Break");
            while (Application.internetReachability == NetworkReachability.NotReachable)
                await UniTask.Delay(1000);
        }

        private void CheckProtocol()
        {
            if (_talk.Stat is State.Listening or State.Speaking &&
                _protocol?.IsAudioChannelOpened() != true)
            {
                _talk.Stat = State.Idle;
                _context.UIManager.ShowNotificationUI(Lang.GetRef("CONNECTION_CLOSED_TIPS")).Forget();
            }
        }

        public async UniTask Dance(string name)
        {
            if (_talk.Stat is State.Dancing) return;
            if (_display is not VRMDisplay display) return;
            var dance = AppPresets.Instance.GetDance(name);
            if (dance == null) return;
            await CancelDance();
            _danceCts = new CancellationTokenSource();
            var preset = AppSettings.Instance.GetVRMModelPreset();
            if (preset == null) return;
            var fadeColor = preset.Color;
            await _context.EffectManager.FadeOut(fadeColor, _danceCts.Token);
            var loadAnim = Addressables.LoadAssetAsync<AnimationClip>(dance.Animation)
                .ToUniTask(cancellationToken: _danceCts.Token);
            var loadBGM = Addressables.LoadAssetAsync<AudioClip>(dance.BGM)
                .ToUniTask(cancellationToken: _danceCts.Token);
            var abortSpeaking = AbortSpeaking(AbortReason.None).AsAsyncUnitUniTask();
            var (anim, bgm, _) = await UniTask.WhenAll(loadAnim, loadBGM, abortSpeaking);
            if (_danceCts.IsCancellationRequested)
            {
                await CancelDance();
                return;
            }
            
            display.Animate(anim);
            display.EnableLipSync(false);
            _clipReader.Setup(bgm, LocalClipFrameSize * LocalClipSampleRate / 1000);
            _clipReadTime = 0;
            _talk.Stat = State.Dancing;
            await UpdateIotStates();
            await UniTask.Delay(200, cancellationToken: _danceCts.Token);
            var fadeIn = _context.EffectManager.FadeIn(fadeColor, _danceCts.Token);
            var waitToEnd = UniTask.Delay(Mathf.CeilToInt(anim.length * 1000), cancellationToken: _danceCts.Token);
            await UniTask.WhenAll(fadeIn, waitToEnd);
            await CancelDance();
        }

        private async UniTask CancelDance()
        {
            if (_danceCts == null) return;
            if (!_danceCts.IsCancellationRequested) _danceCts.Cancel();
            _danceCts.Dispose();
            _danceCts = null;
            if (_display is not VRMDisplay display) return;
            var preset = AppSettings.Instance.GetVRMModelPreset();
            if (preset == null) return;
            var fadeColor = preset.Color;
            await _context.EffectManager.FadeOut(fadeColor);
            display.RevertAnimation();
            display.EnableLipSync(true);
            _clipReader.Clear();
            await _context.EffectManager.FadeIn(fadeColor);
            if (_protocol?.IsAudioChannelOpened() != true && !await OpenAudioChannel()) return;
            if (_talk.Stat != State.Listening) SetListeningMode(ListeningMode.AutoStop);
            else await UpdateIotStates();
        }

        private void OutputClip(float deltaTime)
        {
            if ((_clipReadTime += Mathf.CeilToInt(deltaTime * 1000)) < LocalClipFrameSize) return;
            _clipReadTime -= LocalClipFrameSize;
            if (!_clipReader.IsReady || _codec.GetOutputLeftBuffer() > _clipReader.Fragment << 1) return;
            if (!_clipReader.Read(out var data)) return;
            ReadOnlySpan<short> outputData = data.Span;
            if (LocalClipSampleRate != _codec.OutputSampleRate)
                _clipResampler.Process(data.Span, out outputData);
            _codec.OutputData(outputData);
        }
    }
}