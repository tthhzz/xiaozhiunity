using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using XiaoZhi.Unity.Effect;
using XiaoZhi.Unity.IoT;

namespace XiaoZhi.Unity
{
    public class Context
    {
        public App App { get; private set; }
        
        public ThingManager ThingManager { get; private set; }

        public UIManager UIManager { get; private set; }
        
        public EffectManager EffectManager { get; private set; }
        
        public bool Restarting { get; private set; }
        
        public void Init()
        {
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            
            UIManager = new UIManager();
            UIManager.Inject(this);
            ThingManager = new ThingManager();
            ThingManager.Inject(this);
            ThingManager.AddThing(new ThingAppSettings());
            ThingManager.AddThing(new ThingVideoPlayer());
            ThingManager.AddThing(new ThingAnimation());
            ThingManager.AddThing(new ThingDance());
            EffectManager = new EffectManager();
            App = new App();
            App.Inject(this);
            Application.runInBackground = true;
#if !UNITY_EDITOR
            Application.targetFrameRate = 60;
#endif
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            
            App?.Dispose();
            UIManager?.Dispose();
            ThingManager?.Dispose();
            DOTween.KillAll();
            AppUtility.Clear();
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        public void Start()
        {
            App.Start().Forget();
        }

        public async UniTask Restart(int delayMs = 0)
        {
            if (Restarting) return;
            Restarting = true;
            await UniTask.Delay(delayMs);
            Dispose();
            await UniTask.Delay(250);
            Init();
            Start();
            Restarting = false;
        }
        
        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.LogError(e.ExceptionObject.ToString());
        }
    }
}