using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace XiaoZhi.Unity.Effect
{
    public class EffectManager
    {
        private const string CameraName = "MainCamera";

        private readonly DecalFade _decalFade;
        private readonly bool _hasDecalFade;

        public EffectManager()
        {
            var cameraGO = GameObject.Find(CameraName) ?? Camera.main?.gameObject;
            if (!cameraGO)
            {
                Debug.LogWarning($"EffectManager: camera '{CameraName}' not found; effects disabled.");
                return;
            }

            var volume = cameraGO.GetComponent<Volume>();
            if (!volume || !volume.profile || !volume.profile.TryGet(out _decalFade))
            {
                Debug.LogWarning("EffectManager: Volume/DecalFade missing on main camera; effects disabled.");
                return;
            }

            _hasDecalFade = true;
        }

        public async UniTask FadeOut(Color maskColor, CancellationToken cancellationToken = default)
        {
            if (!_hasDecalFade) return;

            _decalFade.MaskColor.value = maskColor;
            _decalFade.MaskOffset.value = 0;
            _decalFade.MaskFade.value = 0;
            const float offsetDuration = 0.7f;
            const float fadeDuration = 0.1f;
            var tw1 = DOVirtual.Float(0, 1, offsetDuration, value => _decalFade.MaskOffset.value = value)
                .ToUniTask(cancellationToken: cancellationToken);
            var tw2 = DOVirtual.Float(0, 1, fadeDuration, value => _decalFade.MaskFade.value = value)
                .ToUniTask(cancellationToken: cancellationToken);
            await UniTask.WhenAll(tw1, tw2);
        }

        public async UniTask FadeIn(Color maskColor, CancellationToken cancellationToken = default)
        {
            if (!_hasDecalFade) return;

            _decalFade.MaskColor.value = maskColor;
            _decalFade.MaskOffset.value = 1;
            _decalFade.MaskFade.value = 1;
            const float offsetDuration = 0.7f;
            const float fadeDuration = 0.1f;
            var tw1 = DOVirtual.Float(1, 0, offsetDuration, value => _decalFade.MaskOffset.value = value)
                .ToUniTask(cancellationToken: cancellationToken);
            var tw2 = DOVirtual.Float(1, 0, fadeDuration, value => _decalFade.MaskFade.value = value).SetDelay(offsetDuration - fadeDuration)
                .ToUniTask(cancellationToken: cancellationToken);
            await UniTask.WhenAll(tw1, tw2);
        }

        public void CancelFade()
        {
            if (!_hasDecalFade) return;

            _decalFade.MaskFade.value = 0;
            _decalFade.MaskOffset.value = 0;
        }
    }
}