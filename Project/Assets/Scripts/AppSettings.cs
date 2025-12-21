using System;
using UnityEngine;

namespace XiaoZhi.Unity
{
    public class AppSettings : Settings
    {
        private static AppSettings _instance;

        public static AppSettings Instance => _instance;

        public static void Load()
        {
            _instance = new AppSettings();
        }

        private DisplayMode _displayMode;

        private int _vrmModel;

        private ZoomMode _zoomMode;

        private BreakMode _breakMode;

        private string _keywords;

        private string _wallpaper;

        private bool _autoHideUI;

        private int _outputVolume;

        private string _webSocketUrl;

        private string _webSocketAccessToken;

        private string _customMacAddress;

        public event Action<bool> OnAutoHideUIUpdate;
        public event Action<int> OnOutputVolumeUpdate;

        public event Action<ZoomMode> OnZoomModeUpdate;

        public event Action<string> OnWallPaperUpdate;

        private AppSettings() : base("app")
        {
            _displayMode = (DisplayMode)GetInt("display_mode");
            _breakMode = (BreakMode)GetInt("break_mode", (int)BreakMode.Keyword);
            _autoHideUI = GetInt("auto_hide_ui", 1) == 1;
            _outputVolume = GetInt("output_volume", 50);
            _vrmModel = GetInt("vrm_model");
            _zoomMode = (ZoomMode)GetInt("zoom_mode");
            _wallpaper = GetString("wallpaper", "Stage");
        }

        public DisplayMode GetDisplayMode() => _displayMode;

        public void SetDisplayMode(DisplayMode displayMode)
        {
            if (_displayMode == displayMode) return;
            _displayMode = displayMode;
            SetInt("display_mode", (int)displayMode);
            Save();
        }

        public AppPresets.VRMModel GetVRMModelPreset()
        {
            var models = AppPresets.Instance.VRMCharacterModels;
            if (models == null || models.Length == 0)
            {
                Debug.LogError("VRM 模型列表为空，返回默认模型");
                return null;
            }
            var safeIndex = Mathf.Clamp(_vrmModel, 0, models.Length - 1);
            return models[safeIndex];
        }
        
        public int GetVRMModel() => _vrmModel;

        public void SetVRMModel(int vrmModel)
        {
            if (_vrmModel == vrmModel) return;
            _vrmModel = vrmModel;
            SetInt("vrm_model", _vrmModel);
            Save();
        }

        public ZoomMode GetZoomMode() => _zoomMode;

        public void SetZoomMode(ZoomMode zoomMode)
        {
            if (_zoomMode == zoomMode) return;
            _zoomMode = zoomMode;
            SetInt("zoom_mode", (int)zoomMode);
            Save();
            OnZoomModeUpdate?.Invoke(_zoomMode);
        }

        public BreakMode GetBreakMode() => _breakMode;

        public void SetBreakMode(BreakMode breakMode)
        {
            if (_breakMode == breakMode) return;
            _breakMode = breakMode;
            SetInt("break_mode", (int)breakMode);
            Save();
        }

        public string GetWallpaper() => _wallpaper;

        public void SetWallpaper(string wallpaper)
        {
            if (_wallpaper == wallpaper) return;
            _wallpaper = wallpaper;
            SetString("wallpaper", _wallpaper);
            Save();
            OnWallPaperUpdate?.Invoke(_wallpaper);
        }

        public string GetKeywords(bool forceUpdate = false)
        {
            if (forceUpdate) _keywords = null;
            _keywords ??=
                FileUtility.ReadAllText(FileUtility.FileType.DataPath,
                    AppPresets.Instance.GetKeyword(Lang.Code)
                        .SpotterKeyWordsFile);
            return _keywords;
        }

        public void SetKeywords(string keywords)
        {
            if (_keywords.Equals(keywords)) return;
            _keywords = keywords;
            FileUtility.WriteAllText(
                AppPresets.Instance.GetKeyword(Lang.Code).SpotterKeyWordsFile,
                _keywords);
        }

        public bool IsAutoHideUI()
        {
            return _autoHideUI;
        }

        public void SetAutoHideUI(bool autoHideUI)
        {
            if (_autoHideUI == autoHideUI) return;
            _autoHideUI = autoHideUI;
            SetInt("auto_hide_ui", _autoHideUI ? 1 : 0);
            Save();
            OnAutoHideUIUpdate?.Invoke(_autoHideUI);
        }

        public int GetOutputVolume()
        {
            return _outputVolume;
        }

        public void SetOutputVolume(int outputVolume)
        {
            if (_outputVolume == outputVolume) return;
            _outputVolume = outputVolume;
            SetInt("output_volume", _outputVolume);
            Save();
            OnOutputVolumeUpdate?.Invoke(_outputVolume);
        }

        public string GetWebSocketUrl()
        {
            _webSocketUrl ??= GetString("web_socket_url", AppPresets.Instance.WebSocketUrl);
            return _webSocketUrl;
        }

        public void SetWebSocketUrl(string url)
        {
            if (_webSocketUrl.Equals(url)) return;
            _webSocketUrl = url;
            SetString("web_socket_url", _webSocketUrl);
            Save();
        }

        public string GetWebSocketAccessToken()
        {
            _webSocketAccessToken ??= GetString("web_socket_access_token", AppPresets.Instance.WebSocketAccessToken);
            return _webSocketAccessToken;
        }

        public void SetWebSocketAccessToken(string accessToken)
        {
            if (_webSocketAccessToken.Equals(accessToken)) return;
            _webSocketAccessToken = accessToken;
            SetString("web_socket_access_token", _webSocketAccessToken);
            Save();
        }

        public string GetMacAddress()
        {
            _customMacAddress ??= GetString("custom_mac_address", AppUtility.GetMacAddress());
            return _customMacAddress;
        }

        public void SetMacAddress(string macAddress)
        {
            if (_customMacAddress.Equals(macAddress)) return;
            _customMacAddress = macAddress;
            SetString("custom_mac_address", _customMacAddress);
            Save();
        }
    }
}