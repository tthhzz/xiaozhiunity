using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMOD;
using FMODUnity;
using Channel = FMOD.Channel;
using Debug = UnityEngine.Debug;

namespace XiaoZhi.Unity
{
    public class FMODAudioCodec : AudioCodec
    {
        [Flags]
        private enum PlayerPauseFlag
        {
            Enabled = 1,
            Overflow = 2
        }

        private const int RecorderBufferSec = 2;
        private const int InputBufferSec = 8;
        private const int PlayerBufferSec = 8;
        
        private FMOD.System _system;
        private Sound _recorder;
        private Channel _recorderChannel;
        private bool _isRecording;
        private int _recorderId = -1;
        private int _recorderLength;
        private int _readPosition;
        private Sound _player;
        private Channel _playerChannel;
        private int _playerLength;
        private int _writePosition;
        private Memory<short> _shortBuffer1;
        private Memory<short> _shortBuffer2;
        private DSP _fftDsp;
        private Memory<float> _floatBuffer;
        private readonly RingBuffer<short> _inputBuffer;
        private readonly SpectrumAnalyzer _spectrumAnalyzer;
        private int _lastInputAnalysisPos;
        private int _lastOutputAnalysisPos;

        private FMODAudioProcessor _aps;
        private int _apsCapturePos;

        private PlayerPauseFlag _playerPauseFlag;

        public FMODAudioCodec(int inputSampleRate, int inputChannels, int outputSampleRate, int outputChannels)
        {
            this.inputSampleRate = inputSampleRate;
            this.outputSampleRate = outputSampleRate;
            this.inputChannels = inputChannels;
            this.outputChannels = outputChannels;
            _system = RuntimeManager.CoreSystem;
            _inputBuffer = new RingBuffer<short>(inputSampleRate * inputChannels * InputBufferSec);
            _spectrumAnalyzer = new SpectrumAnalyzer(SpectrumWindowSize);
            InitAudioProcessor();
            InitPlayer();
            InitRecorder();
        }

        public override void Dispose()
        {
            ClearAudioProcessor();
            ClearPlayer();
            ClearRecorder();
        }

        private void InitAudioProcessor()
        {
            _aps = new FMODAudioProcessor(inputSampleRate, inputChannels, outputSampleRate, outputChannels);
        }

        private void ClearAudioProcessor()
        {
            if (_aps != null)
            {
                _aps.Dispose();
                _aps = null;
            }
        }

        public override async UniTask Update(CancellationToken token)
        {
            DetectIfPlayToEnd();
            await UniTask.SwitchToThreadPool();
            ProcessAudio();
        }

        private void DetectIfPlayToEnd()
        {
            if (!outputEnabled) return;
            _playerChannel.getPaused(out var paused);
            if (paused) return;
            _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            if (playerPos >= _writePosition &&
                playerPos - _writePosition <= _playerLength / 2)
            {
                SetPlayerPause(PlayerPauseFlag.Overflow, true);
                FMODHelper.ClearPCM16(_player, 0, _playerLength);
                _writePosition = playerPos;
            }
        }

        private void ProcessAudio()
        {
            if (!_isRecording || !inputEnabled) return;
            var inputFrameSize = inputSampleRate / 100 * inputChannels;
            _system.getRecordPosition(_recorderId, out var pos);
            var recorderPos = (int)pos;
            var numFrames = Tools.Repeat(recorderPos - _apsCapturePos, _recorderLength) / inputFrameSize;
            if (numFrames <= 0) return;
            var captureSamples = numFrames * inputFrameSize;
            var captureSpan = Tools.EnsureMemory(ref _shortBuffer2, captureSamples);
            FMODHelper.ReadPCM16(_recorder, _apsCapturePos, captureSpan);
            _playerChannel.getPaused(out var playerPaused);
            if (!playerPaused)
            {
                var outputFrameSize = outputSampleRate / 100 * outputChannels;
                _playerChannel.getPosition(out pos, TIMEUNIT.PCM);
                var playerPos = (int)pos;
                var apsReversePos =
                    Tools.Repeat((playerPos / outputFrameSize - numFrames) * outputFrameSize, _playerLength);
                var reverseSamples = numFrames * outputFrameSize;
                var reverseSpan = Tools.EnsureMemory(ref _shortBuffer1, reverseSamples);
                FMODHelper.ReadPCM16(_player, apsReversePos, reverseSpan);
                for (var i = 0; i < numFrames; i++)
                {
                    var span1 = reverseSpan.Slice(i * outputFrameSize, outputFrameSize);
                    var result1 = _aps.ProcessReverseStream(span1, span1);
                    if (result1 != 0)
                    {
                        Debug.LogError($"ProcessReverseStream error: {result1}");
                        return;
                    }

                    _aps.SetStreamDelayMs(0);
                    var span2 = captureSpan.Slice(i * inputFrameSize, inputFrameSize);
                    var result2 = _aps.ProcessStream(span2, span2);
                    if (result2 != 0)
                    {
                        Debug.LogError($"ProcessStream error: {result2}");
                        return;
                    }
                }
            }

            _inputBuffer.TryWrite(captureSpan);
            _apsCapturePos = Tools.Repeat(_apsCapturePos + numFrames * inputFrameSize, _recorderLength);
        }

        // -------------------------------- output ------------------------------- //

        public override bool GetOutputSpectrum(bool fft, out ReadOnlySpan<float> spectrum)
        {
            if (!outputEnabled || (fft && !_fftDsp.hasHandle()))
            {
                spectrum = default;
                return false;
            }

            if (fft)
            {
                _fftDsp.getParameterData((int)DSP_FFT.SPECTRUMDATA, out var unmanagedData, out _);
                var fftData = Marshal.PtrToStructure<DSP_PARAMETER_FFT>(unmanagedData);
                if (fftData.numchannels <= 0)
                {
                    spectrum = default;
                    return false;
                }

                var floatSpan = Tools.EnsureMemory(ref _floatBuffer, fftData.length);
                fftData.getSpectrum(0, floatSpan);
                spectrum = floatSpan;
            }
            else
            {
                spectrum = default;
                const int readLen = SpectrumWindowSize;
                _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
                var playerPos = (int)pos;
                var position = (playerPos / readLen - 1) * readLen;
                position = Math.Max(position, 0);
                if (_lastOutputAnalysisPos == position) return false;
                _lastOutputAnalysisPos = position;
                var shortSpan = Tools.EnsureMemory(ref _shortBuffer1, readLen);
                FMODHelper.ReadPCM16(_player, _lastOutputAnalysisPos, shortSpan);
                var floatSpan = Tools.EnsureMemory(ref _floatBuffer, shortSpan.Length);
                Tools.PCM16Short2Float(shortSpan, floatSpan);
                spectrum = floatSpan;
            }

            return true;
        }

        public override void SetOutputVolume(int volume)
        {
            base.SetOutputVolume(volume);
            _playerChannel.setVolume(volume / 100f);
        }

        public override void EnableOutput(bool enable)
        {
            if (outputEnabled == enable) return;
            base.EnableOutput(enable);
            SetPlayerPause(PlayerPauseFlag.Enabled, !outputEnabled);
        }

        public override int GetOutputLeftBuffer()
        {
            if ((_playerPauseFlag & PlayerPauseFlag.Overflow) > 0) return 0;
            _playerChannel.getPosition(out var pos, TIMEUNIT.PCM);
            var playerPos = (int)pos;
            return Tools.Repeat(_writePosition - playerPos, _playerLength);
        }

        private void SetPlayerPause(PlayerPauseFlag flag, bool pause)
        {
            if (pause) _playerPauseFlag |= flag;
            else _playerPauseFlag &= ~flag;
            _playerChannel.getPaused(out var current);
            if (current != _playerPauseFlag > 0)
            {
                _playerChannel.setPaused(_playerPauseFlag > 0);
                _playerChannel.setFrequency(_playerPauseFlag > 0 ? 0 : outputSampleRate);
            }
        }

        protected override int Write(ReadOnlySpan<short> data)
        {
            if (!outputEnabled) return 0;
            var writeLen = FMODHelper.WritePCM16(_player, _writePosition, data);
            _writePosition = Tools.Repeat(_writePosition + writeLen, _playerLength);
            SetPlayerPause(PlayerPauseFlag.Overflow, false);
            return writeLen;
        }

        private void InitPlayer()
        {
            _playerLength = outputSampleRate * outputChannels * PlayerBufferSec;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = outputSampleRate,
                length = (uint)(_playerLength << 1)
            };

            var system = _system;
            system.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _player);
            system.playSound(_player, default, true, out _playerChannel);
            _playerChannel.setVolume(outputVolume / 100f);
            SetPlayerPause(PlayerPauseFlag.Overflow, true);
            system.createDSPByType(DSP_TYPE.FFT, out _fftDsp);
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOW, (int)DSP_FFT_WINDOW_TYPE.HANNING);
            _fftDsp.setParameterInt((int)DSP_FFT.WINDOWSIZE, SpectrumWindowSize);
            _playerChannel.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, _fftDsp);
        }

        private void ClearPlayer()
        {
            if (_fftDsp.hasHandle())
            {
                if (_playerChannel.hasHandle())
                    _playerChannel.removeDSP(_fftDsp);
                _fftDsp.release();
                _fftDsp.clearHandle();
            }

            if (_playerChannel.hasHandle())
            {
                _playerChannel.stop();
                _playerChannel.clearHandle();
            }

            if (_player.hasHandle())
            {
                _player.release();
                _player.clearHandle();
            }
        }

        // -------------------------------- input ------------------------------- //

        public override bool GetInputSpectrum(bool fft, out ReadOnlySpan<float> spectrum)
        {
            spectrum = default;
            if (!_isRecording || !inputEnabled) return false;
            const int readLen = SpectrumWindowSize;
            var position = (_inputBuffer.WritePosition / readLen - 1) * readLen;
            position = Math.Max(position, 0);
            if (_lastInputAnalysisPos == position) return false;
            _lastInputAnalysisPos = position;
            var shortSpan = Tools.EnsureMemory(ref _shortBuffer1, readLen);
            var success = _inputBuffer.TryReadAt(_lastInputAnalysisPos, shortSpan);
            if (!success) return false;
            if (!fft)
            {
                var floatSpan = Tools.EnsureMemory(ref _floatBuffer, shortSpan.Length);
                Tools.PCM16Short2Float(shortSpan, floatSpan);
                spectrum = floatSpan;
                return true;
            }

            return _spectrumAnalyzer.Analyze(shortSpan, out spectrum);
        }

        public override void EnableInput(bool enable)
        {
            if (inputEnabled == enable) return;
            if (enable) StartRecorder();
            else StopRecorder();
            base.EnableInput(enable);
        }

        protected override int Read(Span<short> dest)
        {
            if (!_isRecording || !inputEnabled || !_recorder.hasHandle()) return 0;
            return _inputBuffer.TryRead(dest) ? dest.Length : 0;
        }

        public override bool GetInputDevice(out InputDevice device)
        {
            _system.getRecordNumDrivers(out var numDrivers, out _);
            for (var i = 0; i < numDrivers; i++)
            {
                _system.getRecordDriverInfo(i, out var deviceName, 64, out _, out var systemRate,
                    out var speakerMode, out var speakerModeChannels, out var state);
#if !UNITY_EDITOR && UNITY_ANDROID
                if (state.HasFlag(DRIVER_STATE.CONNECTED) && deviceName.Contains("(Voice)"))
#else
                if (state.HasFlag(DRIVER_STATE.CONNECTED) && state.HasFlag(DRIVER_STATE.DEFAULT))
#endif
                {
                    device = new InputDevice
                    {
                        Id = i, Name = deviceName, SystemRate = systemRate,
                        SpeakerMode = Enum.GetName(typeof(SPEAKERMODE), speakerMode),
                        SpeakerModeChannels = speakerModeChannels
                    };
                    return true;
                }
            }

            device = default;
            return false;
        }

        private void InitRecorder()
        {
            _recorderLength = inputSampleRate * inputChannels * RecorderBufferSec;
            var exInfo = new CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                numchannels = 1,
                format = SOUND_FORMAT.PCM16,
                defaultfrequency = inputSampleRate,
                length = (uint)(_recorderLength << 1)
            };
            _system.createSound(exInfo.userdata, MODE.OPENUSER | MODE.LOOP_NORMAL, ref exInfo,
                out _recorder);
        }

        private void ClearRecorder()
        {
            if (_recorderChannel.hasHandle())
            {
                _recorderChannel.stop();
                _recorderChannel.clearHandle();
            }

            if (_recorder.hasHandle())
            {
                _recorder.release();
                _recorder.clearHandle();
            }
        }

        private void StartRecorder()
        {
            if (!_recorder.hasHandle()) return;
            var recorderId = -1;
            if (GetInputDevice(out var inputDevice)) recorderId = inputDevice.Id;
            if (_isRecording && _recorderId != recorderId)
            {
                _system.recordStop(_recorderId);
                _isRecording = false;
            }

            _recorderId = recorderId;
            if (!_isRecording && _recorderId >= 0)
            {
                _system.recordStart(_recorderId, _recorder, true);
                _isRecording = true;
            }
        }

        private void StopRecorder()
        {
            if (!_recorder.hasHandle()) return;
            if (_recorderId < 0) return;
            if (_isRecording)
            {
                _system.recordStop(_recorderId);
                _isRecording = false;
            }
        }
    }

    public static class fmod_dsp_extension
    {
        public static void getSpectrum(this DSP_PARAMETER_FFT fft, int channel, Span<float> buffer)
        {
            var bufferLength = Math.Min(fft.length, buffer.Length) * sizeof(float);
            unsafe
            {
                fixed (float* bufferPtr = buffer)
                    Buffer.MemoryCopy(fft.spectrum_internal[channel].ToPointer(), bufferPtr, bufferLength,
                        bufferLength);
            }
        }
    }
}