using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UniVRM10;

namespace XiaoZhi.Unity
{
    [RequireComponent(typeof(Vrm10Instance))]
    public class VRMEyeBlinker : MonoBehaviour
    {
        public Vector2 BlinkInterval = new(2.0f, 8.0f);
        public float BlinkEyeCloseDuration = 0.03f;
        public float BlinkOpeningSeconds = 0.03f;
        public float BlinkClosingSeconds = 0.01f;

        private Vrm10RuntimeExpression _expression;
        private CancellationTokenSource _loopCts;

        private void Awake()
        {
            _expression = GetComponent<Vrm10Instance>().Runtime.Expression;
        }

        private void OnEnable()
        {
            if (_loopCts != null) return;
            _loopCts = new CancellationTokenSource();
            UniTask.Void(LoopUpdate, _loopCts.Token);
        }

        private void OnDisable()
        {
            if (_loopCts != null)
            {
                _loopCts.Cancel();
                _loopCts.Dispose();
                _loopCts = null;
            }
        }

        private async UniTaskVoid LoopUpdate(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) 
            {
                var interval = Random.Range(BlinkInterval.x, BlinkInterval.y);
                await UniTask.Delay((int)(interval * 1000), cancellationToken: cancellationToken);
                // 闭眼
                var expressionKey = new ExpressionKey(ExpressionPreset.blink);
                await DOVirtual.Float(0, 1, BlinkClosingSeconds, value => _expression.SetWeight(expressionKey, value))
                    .WithCancellation(cancellationToken);
                // 持续闭眼
                await UniTask.Delay((int)(BlinkEyeCloseDuration * 1000), cancellationToken: cancellationToken);
                // 睁眼
                await DOVirtual.Float(1, 0, BlinkOpeningSeconds, value => _expression.SetWeight(expressionKey, value))
                    .WithCancellation(cancellationToken);
            }
        }
    }
}