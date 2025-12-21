using System.Threading;
using uLipSync;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class ULipSyncAudioProxy
    {
        public bool Enabled { get; set; } = true;

        private readonly uLipSync.uLipSync _uLipSync;

        private readonly AudioCodec _codec;

        private readonly float[] _buffer;

        private readonly uLipSyncExpressionVRM _uLipSyncExpressionVRM;

        public ULipSyncAudioProxy(uLipSync.uLipSync uLipSync, uLipSyncExpressionVRM uLipSyncExpressionVRM,
            AudioCodec codec)
        {
            _codec = codec;
            _uLipSync = uLipSync;
            _uLipSyncExpressionVRM = uLipSyncExpressionVRM;
            _uLipSync.profile.sampleCount = AudioCodec.SpectrumWindowSize;
            _uLipSync.profile.targetSampleRate = codec.OutputSampleRate;
            var configuration = AudioSettings.GetConfiguration();
            configuration.sampleRate = codec.OutputSampleRate;
            AudioSettings.Reset(configuration);
            _buffer = new float[_uLipSync.profile.sampleCount];
        }

        public void Update()
        {
            if (!Enabled)
            {
                ResetPhonemeBlend();
            }
            else if (_codec.GetOutputSpectrum(false, out var data))
            {
                data.CopyTo(_buffer);
                _uLipSync.OnDataReceived(_buffer, _codec.OutputChannels);
            }
        }

        private void ResetPhonemeBlend()
        {
            foreach (var blendShapeInfo in _uLipSyncExpressionVRM.blendShapes)
            {
                blendShapeInfo.weight = 0;
                blendShapeInfo.weightVelocity = 0;
            }
        }
    }
}