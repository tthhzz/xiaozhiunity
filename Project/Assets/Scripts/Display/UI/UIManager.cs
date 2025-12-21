using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Pool;

namespace XiaoZhi.Unity
{
    public class UIManager : IUIService
    {
        private const string CameraName = "MainCamera";
        private const string UICameraName = "UICamera";
        private readonly Dictionary<UILayer, GameObject> _canvasMap;
        private readonly Stack<SceneStackData> _stack = new();
        private readonly Queue<Tuple<Type, NotificationUIData>> _notificationQueue = new();
        private readonly Dictionary<string, BaseUI> _uiMap = new();
        private string _currentPopup;
        private Context _context;
        public Context Context => _context;

        public UIManager()
        {
            _canvasMap = Enum.GetNames(typeof(UILayer))
                .ToDictionary(Enum.Parse<UILayer>, i => 
                {
                    // 先尝试在 UICamera 下查找，如果找不到再尝试 MainCamera
                    var canvas = GameObject.Find($"{UICameraName}/Canvas{i}");
                    if (canvas == null)
                    {
                        canvas = GameObject.Find($"{CameraName}/Canvas{i}");
                    }
                    if (canvas == null)
                    {
                        Debug.LogWarning($"找不到 Canvas 对象: {UICameraName}/Canvas{i} 或 {CameraName}/Canvas{i}");
                    }
                    return canvas;
                });
        }

        private GameObject GetOrCreateCanvas(UILayer layer)
        {
            var canvasObj = _canvasMap[layer];
            if (canvasObj != null)
            {
                return canvasObj;
            }

            // 如果找不到，尝试创建它
            var parent = GameObject.Find(UICameraName);
            if (parent == null)
            {
                parent = GameObject.Find(CameraName);
            }
            
            if (parent == null)
            {
                Debug.LogError($"找不到 UICamera 或 MainCamera，无法创建 Canvas{layer}");
                return null;
            }

            // 创建新的 Canvas GameObject
            var canvasName = $"Canvas{layer}";
            canvasObj = new GameObject(canvasName);
            canvasObj.transform.SetParent(parent.transform, false);
            
            // 添加 Canvas 组件
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            
            // 查找 UICamera 或 MainCamera 的 Camera 组件
            var camera = parent.GetComponent<Camera>();
            if (camera == null)
            {
                camera = Camera.main;
            }
            canvas.worldCamera = camera;
            
            // 添加 CanvasScaler
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2160);
            scaler.matchWidthOrHeight = 1f;
            
            // 添加 GraphicRaycaster
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // 设置 RectTransform
            var rectTransform = canvasObj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
            
            // 更新字典
            _canvasMap[layer] = canvasObj;
            
            Debug.Log($"自动创建了 Canvas 对象: {canvasName}");
            return canvasObj;
        }

        public void Inject(Context context)
        {
            _context = context;
        }

        public async UniTask Load()
        {
            var moduleCanvas = _canvasMap[UILayer.Module];
            var notifyCanvas = _canvasMap[UILayer.Notify];
            
            if (moduleCanvas == null)
            {
                Debug.LogError($"找不到 CanvasModule，无法加载 MaskUI");
                return;
            }
            
            if (notifyCanvas == null)
            {
                Debug.LogError($"找不到 CanvasNotify，无法加载 NotificationUI");
                return;
            }
            
            await UniTask.WhenAll(EnsureUI<MaskUI>(null, moduleCanvas.transform),
                EnsureUI<NotificationUI>(null, notifyCanvas.transform));
        }

        private async UniTask<T> ShowStackUI<T>(BaseUIData data, UILayer layer) where T : BaseUI, new()
        {
            var alias = typeof(T).Name;
            var canvasObj = GetOrCreateCanvas(layer);
            if (canvasObj == null)
            {
                throw new NullReferenceException($"找不到或无法创建 Canvas 对象: {layer}");
            }
            var canvas = canvasObj.transform;
            await EnsureUI<T>(null, canvas);
            switch (layer)
            {
                case UILayer.Module:
                    if (_stack.Count == 0)
                        throw new InvalidOperationException("At least one scene ui should be loaded.");
                    var moduleStack = _stack.Peek().Stack;
                    if (moduleStack.Count > 0)
                    {
                        var currentUIData = moduleStack.Peek();
                        if (currentUIData.Alias != alias) await FindUI(currentUIData.Alias).Hide();
                    }

                    break;
                case UILayer.Scene:
                    if (_stack.Count > 0) await HideSceneUI(_stack.Peek());
                    break;
            }

            var ui = FindUI<T>();
            ui.Layer = layer;
            ui.Tr.SetAsLastSibling();
            var showTasks = ListPool<UniTask>.Get();
            if (layer == UILayer.Module)
            {
                var maskUI = FindUI<MaskUI>();
                maskUI.AsMaskOf(ui);
                showTasks.Add(maskUI.Show(ui.GetMaskData()));
            }

            showTasks.Add(ui.Show(data));
            await UniTask.WhenAll(showTasks);
            ListPool<UniTask>.Release(showTasks);
            switch (layer)
            {
                case UILayer.Module:
                    var moduleStack = _stack.Peek().Stack;
                    moduleStack.Push(new StackData(alias, data));
                    break;
                case UILayer.Scene:
                    _stack.Push(new SceneStackData(alias, data));
                    break;
            }

            return ui;
        }

        private async UniTask<T> EnsureUI<T>(Type type = null, Transform parent = null) where T : BaseUI, new()
        {
            var alias = (type ?? typeof(T)).Name;
            T ui;
            if (_uiMap.TryGetValue(alias, out var existingUI))
            {
                ui = existingUI as T;
                if (ui == null)
                    throw new NullReferenceException(
                        $"UI instance of type {alias} already exists, but is not of type {alias}");
                if (ui.Tr.parent != parent) ui.Tr.SetParent(parent, false);
            }
            else
            {
                ui = await LoadUI<T>(type, parent);
                _uiMap[alias] = ui;
            }

            return ui;
        }

        private async UniTask HideSceneUI(SceneStackData data)
        {
            var hideTasks = ListPool<UniTask>.Get();
            hideTasks.Add(FindUI(data.Alias).Hide());
            hideTasks.AddRange(from moduleData in data.Stack
                select FindUI(moduleData.Alias)
                into moduleUI
                where moduleUI?.IsVisible == true
                select moduleUI.Hide());
            var maskUI = FindUI<MaskUI>();
            if (maskUI.IsVisible) hideTasks.Add(maskUI.Hide());
            await UniTask.WhenAll(hideTasks);
            ListPool<UniTask>.Release(hideTasks);
        }

        public T FindUI<T>() where T : BaseUI
        {
            return FindUI<T>(typeof(T).Name);
        }

        public T FindUI<T>(string alias) where T : BaseUI
        {
            return _uiMap.TryGetValue(alias, value: out var instance) ? instance as T : null;
        }

        public bool IsUIVisible<T>() where T : BaseUI
        {
            return IsUIVisible(typeof(T).Name);
        }

        public bool IsUIVisible(string alias)
        {
            var ui = FindUI(alias);
            return ui is { IsVisible: true };
        }

        public BaseUI FindUI(string alias)
        {
            return _uiMap.GetValueOrDefault(alias);
        }

        public async UniTask<T> LoadUI<T>(Type type = null, Transform parent = null) where T : BaseUI, new()
        {
            var alias = (type ?? typeof(T)).Name;
            var ui = type != null ? Activator.CreateInstance(type) as T : new T();
            if (ui == null)
                throw new NullReferenceException($"Failed to create UI instance of type {alias}");
            ui.RegisterUIService(this);
            var go = await Addressables.InstantiateAsync(ui.GetResourcePath(), parent);
            if (!go) throw new IOException($"Failed to load UI prefab: {ui.GetResourcePath()}");
            go.SetActive(false);
            ui.Init(go);
            return ui;
        }
        
        public async UniTask<T> ShowBgUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowStackUI<T>(data, UILayer.Bg);
        }

        public async UniTask<T> ShowSceneUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowStackUI<T>(data, UILayer.Scene);
        }

        public async UniTask<T> ShowModuleUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            return await ShowStackUI<T>(data, UILayer.Module);
        }

        public async UniTask<T> ShowPopupUI<T>(BaseUIData data = null) where T : BaseUI, new()
        {
            var canvasObj = _canvasMap[UILayer.Popup];
            if (canvasObj == null)
            {
                throw new NullReferenceException($"找不到 CanvasPopup 对象");
            }
            var canvas = canvasObj.transform;
            await EnsureUI<T>(null, canvas);
            if (!string.IsNullOrEmpty(_currentPopup))
            {
                var current = FindUI(_currentPopup);
                if (current != null) await current.Hide();
            }

            var ui = FindUI<T>();
            ui.Layer = UILayer.Popup;
            var maskUI = FindUI<MaskUI>();
            maskUI.AsMaskOf(ui);
            await UniTask.WhenAll(ui.Show(data), maskUI.Show(ui.GetMaskData()));
            _currentPopup = ui.GetType().Name;
            return ui;
        }

        public async UniTask ShowNotificationUI(string message, float duration = 3.0f)
        {
            await ShowNotificationUI<NotificationUI>(new NotificationUIData(message, duration));
        }

        public async UniTask ShowNotificationUI(LocalizedString message, float duration = 3.0f)
        {
            await ShowNotificationUI<NotificationUI>(new NotificationUIData(message, duration));
        }

        public async UniTask ShowNotificationUI<T>(NotificationUIData notification) where T : NotificationUI, new()
        {
            _notificationQueue.Enqueue(new Tuple<Type, NotificationUIData>(typeof(T), notification));
            await ProcessNotificationUI();
        }

        private async UniTask ProcessNotificationUI()
        {
            if (_notificationQueue.Count == 0) return;
            var ui = FindUI<NotificationUI>();
            if (ui?.IsVisible == true) return;
            var (type, notification) = _notificationQueue.Dequeue();
            var canvasObj = _canvasMap[UILayer.Notify];
            if (canvasObj == null)
            {
                Debug.LogError($"找不到 CanvasNotify 对象，无法显示通知");
                return;
            }
            var canvas = canvasObj.transform;
            ui = await EnsureUI<NotificationUI>(type, canvas);
            ui.Layer = UILayer.Notify;
            await ui.Show(notification);
        }

        public async UniTask CloseUI<T>() where T : BaseUI
        {
            var ui = FindUI<T>();
            await CloseUI(ui);
        }

        private async UniTask<bool> CloseMaskUI()
        {
            if (!string.IsNullOrEmpty(_currentPopup))
            {
                await CloseUI(FindUI(_currentPopup)); 
                return true;
            }

            if (_stack.Count > 0)
            {
                var moduleStack = _stack.Peek().Stack;
                if (moduleStack.Count > 0)
                {
                    await CloseUI(FindUI(moduleStack.Peek().Alias));
                    return true;
                }
            }

            return false;
        }

        private async UniTask<bool> ClosePopupUI(BaseUI ui)
        {
            var maskUI = FindUI<MaskUI>();
            await UniTask.WhenAll(ui.Hide(), maskUI.Hide());
            _currentPopup = null;
            if (_stack.Count > 0)
            {
                var moduleStack = _stack.Peek().Stack;
                if (moduleStack.Count > 0)
                {
                    var moduleUI = FindUI(moduleStack.Peek().Alias);
                    maskUI.AsMaskOf(moduleUI);
                    await maskUI.Show(moduleUI.GetMaskData());
                }
            }

            return true;
        }

        private async UniTask<bool> CloseModuleUI(BaseUI ui)
        {
            if (_stack.Count == 0) return false;
            var moduleStack = _stack.Peek().Stack;
            var alias = ui.GetType().Name;
            if (moduleStack.Count == 0 || moduleStack.Peek().Alias != alias) return false;
            moduleStack.Pop();
            var maskUI = FindUI<MaskUI>();
            if (moduleStack.Count == 0)
            {
                await UniTask.WhenAll(ui.Hide(), maskUI.Hide());
            }
            else
            {
                await ui.Hide();
                var previousUIData = moduleStack.Peek();
                var previousUI = FindUI(previousUIData.Alias);
                maskUI.AsMaskOf(previousUI);
                await previousUI.Show(previousUIData.Data);
            }

            return true;
        }

        private async UniTask<bool> CloseSceneUI(BaseUI ui)
        {
            if (_stack.Count == 0) return false;
            var alias = ui.GetType().Name;
            if (_stack.Peek().Alias != alias) return false;
            await HideSceneUI(_stack.Pop());
            if (_stack.Count > 0)
            {
                var previousUIData = _stack.Peek();
                var previousUI = FindUI(previousUIData.Alias);
                await previousUI.Show(previousUIData.Data);
                var moduleStack = previousUIData.Stack;
                if (moduleStack.Count > 0)
                {
                    var moduleUIData = moduleStack.Peek();
                    var moduleUI = FindUI(moduleUIData.Alias);
                    var maskUI = FindUI<MaskUI>();
                    await UniTask.WhenAll(moduleUI.Show(moduleUIData.Data), maskUI.Show(moduleUI.GetMaskData()));
                }
            }

            return true;
        }

        public async UniTask CloseUI(BaseUI ui)
        {
            switch (ui)
            {
                case null:
                    break;
                case MaskUI:
                    await CloseMaskUI();
                    break;
                case { Layer: UILayer.Notify }:
                    await ui.Hide();
                    await ProcessNotificationUI();
                    break;
                case { Layer: UILayer.Popup }:
                    await ClosePopupUI(ui);
                    break;
                case { Layer: UILayer.Module }:
                    await CloseModuleUI(ui);
                    break;
                case { Layer: UILayer.Scene }:
                    await CloseSceneUI(ui);
                    break;
            }
        }

        public async UniTask DestroyUI<T>() where T : BaseUI
        {
            await DestroyUI(FindUI<T>());
        }

        public async UniTask DestroyUI(BaseUI ui)
        {
            if (ui.IsVisible) await ui.Hide();
            _uiMap.Remove(ui.GetType().Name);
            ui.Destroy();
        }

        public void Dispose()
        {
            _stack.Clear();
            _notificationQueue.Clear();
            UniTask.WhenAll(_uiMap.Values.ToArray().Select(DestroyUI)).Forget();
            _uiMap.Clear();
        }

        private class StackData
        {
            public string Alias { get; }
            public BaseUIData Data { get; }

            public StackData(string alias, BaseUIData data)
            {
                Alias = alias;
                Data = data;
            }
        }

        private class SceneStackData : StackData
        {
            public Stack<StackData> Stack { get; }

            public SceneStackData(string alias, BaseUIData data) : base(alias, data)
            {
                Stack = new Stack<StackData>();
            }
        }
    }
}