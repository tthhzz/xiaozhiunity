using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;

namespace XiaoZhi.Unity
{
    public interface IUIService: IDisposable
    {
        Context Context { get; }
        
        T FindUI<T>() where T : BaseUI;

        T FindUI<T>(string alias) where T : BaseUI;
        
        bool IsUIVisible<T>() where T : BaseUI;
        
        bool IsUIVisible(string alias);
        
        UniTask<T> LoadUI<T>(Type type = null, Transform parent = null) where T : BaseUI, new();

        UniTask<T> ShowBgUI<T>(BaseUIData data = null) where T : BaseUI, new();
        
        UniTask<T> ShowSceneUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new();

        UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new();
        
        UniTask ShowNotificationUI<T>(NotificationUIData notification) where T : NotificationUI, new();

        UniTask ShowNotificationUI(string message, float duration = 3.0f);
        
        UniTask ShowNotificationUI(LocalizedString message, float duration = 3.0f);
        
        UniTask CloseUI<T>() where T : BaseUI;

        UniTask CloseUI(BaseUI ui);
        
        UniTask DestroyUI<T>() where T : BaseUI;
        
        UniTask DestroyUI(BaseUI ui);
    }
}