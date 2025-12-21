using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class AudioClipStreamReader
    {
        private AudioClip _clip;

        private int _position;

        private float[] _buffer1;

        private short[] _buffer2;

        public int Fragment => _buffer1?.Length ?? 0;

        public bool IsReady => _clip != null && _position < _clip.samples;

        public void Setup(AudioClip clip, int fragment)
        {
            _clip = clip;
            _position = 0;
            if (_buffer1 == null) _buffer1 = new float[fragment];
            else if (_buffer1.Length != fragment) Array.Resize(ref _buffer1, fragment);
            if (_buffer2 == null) _buffer2 = new short[fragment];
            else if (_buffer2.Length != fragment) Array.Resize(ref _buffer2, fragment);
        }

        public bool Read(out Memory<short> data)
        {
            if (!_clip.GetData(_buffer1, _position))
            {
                data = null;
                return false;
            }

            var length = Mathf.Min(_buffer1.Length, _clip.samples - _position);
            Tools.PCM16Float2Short(_buffer1, _buffer2);
            _position += length;
            data = _buffer2[..length];
            return true;
        }

        public void Clear()
        {
            _clip = null;
        }
    }
}