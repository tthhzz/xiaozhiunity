using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

namespace XiaoZhi.Unity
{
    public class AppSettingsUI : BaseUI
    {
        private const string KeywordsHelpUrl =
            "https://k2-fsa.github.io/sherpa/onnx/kws/index.html#what-is-open-vocabulary-keyword-spotting";

        private TMP_InputField _inputWebSocketUrl;
        private TMP_InputField _inputWebSocketAccessToken;
        private TMP_InputField _inputCustomMacAddress;
        private Transform _listDisplayMode;
        private GameObject _goCharacter;
        private Transform _listCharacter;
        private GameObject _goZoomMode;
        private Transform _listZoomMode;
        private Transform _listBreakMode;
        private GameObject _goKeywords;
        private TMP_InputField _inputKeywords;
        private Transform _listWallpaper;
        private XSlider _sliderVolume;
        private XButton _btnTheme;
        private XRadio _radioAutoHide;
        private XSpriteChanger _iconVolume;
        private XSpriteChanger _iconTheme;
        private Transform _listLang;
        private XButton _btnRestart;

        public override string GetResourcePath()
        {
            return "Assets/Res/UI/SettingsUI/App.prefab";
        }

        protected override void OnInit()
        {
            var content = Tr.Find("Viewport/Content");
            _inputWebSocketUrl = GetComponent<TMP_InputField>(content, "WebSocketUrl/InputField");
            _inputWebSocketUrl.onDeselect.AddListener(OnChangeWebSocketUrl);
            _inputWebSocketAccessToken = GetComponent<TMP_InputField>(content, "WebSocketAccessToken/InputField");
            _inputWebSocketAccessToken.onDeselect.AddListener(OnChangeWebSocketAccessToken);
            _inputCustomMacAddress = GetComponent<TMP_InputField>(content, "CustomMacAddress/InputField");
            _inputCustomMacAddress.onDeselect.AddListener(OnChangeCustomMacAddress);
            _listDisplayMode = content.Find("DisplayMode/List");
            _goCharacter = content.Find("Character").gameObject;
            _listCharacter = content.Find("Character/List");
            _goZoomMode = content.Find("ZoomMode").gameObject;
            _listZoomMode = content.Find("ZoomMode/List");
            _listBreakMode = content.Find("BreakMode/List");
            _inputKeywords = GetComponent<TMP_InputField>(content, "Keywords/InputField");
            _inputKeywords.onDeselect.AddListener(OnChangeKeywords);
            GetComponent<HyperlinkText>(content, "Keywords/Tips_Help/Text").OnClickLink
                .AddListener(_ => Application.OpenURL(KeywordsHelpUrl));
            _listWallpaper = content.Find("Wallpaper/List");
            _sliderVolume = GetComponent<XSlider>(content, "Volume/Slider");
            _iconVolume = GetComponent<XSpriteChanger>(content, "Volume/Title/Icon");
            _sliderVolume.onValueChanged.AddListener(value =>
            {
                AppSettings.Instance.SetOutputVolume((int)value);
                UpdateIconVolume();
            });
            _radioAutoHide = GetComponent<XRadio>(content, "AutoHide/Radio");
            _radioAutoHide.onValueChanged.AddListener(value => { AppSettings.Instance.SetAutoHideUI(value); });
            _btnTheme = GetComponent<XButton>(content, "Theme/Button");
            _btnTheme.onClick.AddListener(() =>
            {
                ThemeManager.SetTheme(ThemeManager.Theme == ThemeSettings.Theme.Dark
                    ? ThemeSettings.Theme.Light
                    : ThemeSettings.Theme.Dark);
                UpdateIconTheme();
            });
            _iconTheme = GetComponent<XSpriteChanger>(content, "Theme/Button/Icon");
            _listLang = content.Find("Lang/List");
            _btnRestart = GetComponent<XButton>(content, "Restart/Button");
            _btnRestart.onClick.AddListener(() => { Context.Restart().Forget(); });
        }

        protected override async UniTask OnShow(BaseUIData data = null)
        {
            UpdateWebSocketUrl();
            UpdateWebSocketAccessToken();
            UpdateCustomMacAddress();
            UpdateDisplayMode();
            UpdateCharacter();
            UpdateZoomMode();
            UpdateBreakMode();
            UpdateKeywords();
            UpdateWallpaper();
            UpdateVolume();
            UpdateIconVolume();
            UpdateAutoHide();
            UpdateIconTheme();
            UpdateLangList();
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnHide()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            await UniTask.CompletedTask;
        }

        private void UpdateDisplayMode()
        {
            // 只显示 Emoji、Girl、Boy 三个选项（隐藏旧的 VRM 选项）
            var displayModes = new[] { DisplayMode.Emoji, DisplayMode.Girl, DisplayMode.Boy };
            Tools.EnsureChildren(_listDisplayMode, displayModes.Length);
            for (var i = 0; i < displayModes.Length; i++)
            {
                var go = _listDisplayMode.GetChild(i).gameObject;
                go.SetActive(true);
                var displayMode = displayModes[i];
                GetComponent<LocalizeStringEvent>(go.transform, "Text").StringReference =
                    Lang.GetRef($"SettingsUI_DisplayMode_{Enum.GetName(typeof(DisplayMode), displayMode)}");
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                
                // 检查当前 DisplayMode 是否匹配（处理向后兼容）
                var currentMode = AppSettings.Instance.GetDisplayMode();
                var isSelected = currentMode == displayMode || 
                    (currentMode == DisplayMode.VRM && displayMode == GetCurrentVRMDisplayMode());
                
                toggle.isOn = isSelected;
                AddUniqueListener(toggle, i, OnToggleDisplayMode);
            }
        }

        private DisplayMode GetCurrentVRMDisplayMode()
        {
            // 根据当前选择的 VRM 模型判断是 Girl 还是 Boy
            var vrmModelIndex = AppSettings.Instance.GetVRMModel();
            var vrmModels = AppPresets.Instance.VRMCharacterModels;
            if (vrmModels != null && vrmModels.Length > 0)
            {
                var safeIndex = Mathf.Clamp(vrmModelIndex, 0, vrmModels.Length - 1);
                var modelName = vrmModels[safeIndex].Name;
                return modelName.Equals("Girl", StringComparison.InvariantCultureIgnoreCase) 
                    ? DisplayMode.Girl 
                    : DisplayMode.Boy;
            }
            return DisplayMode.Girl; // 默认返回 Girl
        }

        private void OnToggleDisplayMode(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                var displayModes = new[] { DisplayMode.Emoji, DisplayMode.Girl, DisplayMode.Boy };
                var displayMode = displayModes[index];
                
                // 处理向后兼容：如果当前是 VRM 模式，需要转换
                var currentMode = AppSettings.Instance.GetDisplayMode();
                if (currentMode == DisplayMode.VRM)
                {
                    currentMode = GetCurrentVRMDisplayMode();
                }
                
                if (currentMode == displayMode) return;
                
                AppSettings.Instance.SetDisplayMode(displayMode);
                
                // 如果选择 Girl 或 Boy，自动设置对应的 VRM 模型索引
                if (displayMode == DisplayMode.Girl || displayMode == DisplayMode.Boy)
                {
                    var vrmModels = AppPresets.Instance.VRMCharacterModels;
                    if (vrmModels != null && vrmModels.Length > 0)
                    {
                        var targetName = displayMode == DisplayMode.Girl ? "Girl" : "Boy";
                        bool found = false;
                        for (int i = 0; i < vrmModels.Length; i++)
                        {
                            var modelName = vrmModels[i].Name;
                            if (modelName.Equals(targetName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                AppSettings.Instance.SetVRMModel(i);
                                found = true;
                                break;
                            }
                        }
                        // 如果找不到对应的模型，显示警告并使用默认索引
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
                
                ShowNotificationUI(Lang.GetRef("SettingsUI_Modify_Tips")).Forget();
                UpdateCharacter();
                UpdateZoomMode();
            }
        }

        private void UpdateCharacter()
        {
            var displayMode = AppSettings.Instance.GetDisplayMode();
            // Girl 和 Boy 模式也显示角色相关设置（缩放模式等）
            var showCharacter = displayMode == DisplayMode.VRM || displayMode == DisplayMode.Girl || displayMode == DisplayMode.Boy;
            _goCharacter.SetActive(false); // 隐藏角色选择，因为已经在 Display Mode 中选择了
            _goZoomMode.SetActive(showCharacter);
        }

        private void OnToggleCharacter(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                var displayMode = AppSettings.Instance.GetDisplayMode();
                switch (displayMode)
                {
                    case DisplayMode.VRM:
                        if (AppSettings.Instance.GetVRMModel() == index) return;
                        AppSettings.Instance.SetVRMModel(index);
                        ShowNotificationUI(Lang.GetRef("SettingsUI_Modify_Tips")).Forget();
                        break;
                }
            }
        }

        private void UpdateZoomMode()
        {
            var values = Enum.GetValues(typeof(ZoomMode));
            Tools.EnsureChildren(_listZoomMode, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var go = _listZoomMode.GetChild(i).gameObject;
                go.SetActive(true);
                GetComponent<LocalizeStringEvent>(go.transform, "Text").StringReference =
                    Lang.GetRef($"SettingsUI_ZoomMode_{Enum.GetName(typeof(ZoomMode), i)}");
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = (ZoomMode)values.GetValue(i) == AppSettings.Instance.GetZoomMode();
                AddUniqueListener(toggle, i, OnToggleZoomMode);
            }
        }

        private void OnToggleZoomMode(Toggle toggle, int index, bool isOn)
        {
            if (isOn) AppSettings.Instance.SetZoomMode((ZoomMode)Enum.GetValues(typeof(ZoomMode)).GetValue(index));
        }

        private void UpdateBreakMode()
        {
            var values = Enum.GetValues(typeof(BreakMode));
            Tools.EnsureChildren(_listBreakMode, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var go = _listBreakMode.GetChild(i).gameObject;
                go.SetActive(true);
                GetComponent<LocalizeStringEvent>(go.transform, "Text").StringReference =
                    Lang.GetRef($"SettingsUI_BreakMode_{Enum.GetName(typeof(BreakMode), i)}");
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = (BreakMode)values.GetValue(i) == AppSettings.Instance.GetBreakMode();
                AddUniqueListener(toggle, i, OnToggleBreakMode);
            }
        }

        private void OnToggleBreakMode(Toggle toggle, int index, bool isOn)
        {
            if (isOn) AppSettings.Instance.SetBreakMode((BreakMode)Enum.GetValues(typeof(BreakMode)).GetValue(index));
        }

        private void UpdateKeywords(bool forceUpdate = false)
        {
            _inputKeywords.text = AppSettings.Instance.GetKeywords(forceUpdate);
        }

        private void OnChangeKeywords(string text)
        {
            if (AppSettings.Instance.GetKeywords().Equals(text)) return;
            AppSettings.Instance.SetKeywords(text);
            ShowNotificationUI(Lang.GetRef("SettingsUI_Modify_Tips")).Forget();
        }

        private void UpdateWallpaper()
        {
            var wallpapers = AppPresets.Instance.Wallpapers;
            Tools.EnsureChildren(_listWallpaper, wallpapers.Length);
            for (var i = 0; i < wallpapers.Length; i++)
            {
                var go = _listWallpaper.GetChild(i).gameObject;
                go.SetActive(true);
                GetComponent<TextMeshProUGUI>(go.transform, "Text").text = wallpapers[i].Name;
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = wallpapers[i].Name == AppSettings.Instance.GetWallpaper();
                AddUniqueListener(toggle, i, OnToggleWallpaper);
            }
        }

        private void OnToggleWallpaper(Toggle toggle, int index, bool isOn)
        {
            if (isOn) AppSettings.Instance.SetWallpaper(AppPresets.Instance.Wallpapers[index].Name);
        }

        private void UpdateVolume()
        {
            _sliderVolume.value = AppSettings.Instance.GetOutputVolume();
        }

        private void UpdateIconVolume()
        {
            var volume = AppSettings.Instance.GetOutputVolume();
            var index = volume switch
            {
                0 => 0,
                < 50 => 1,
                _ => 2
            };
            _iconVolume.ChangeTo(index);
        }

        private void UpdateAutoHide()
        {
            _radioAutoHide.isOn = AppSettings.Instance.IsAutoHideUI();
        }

        private void UpdateIconTheme()
        {
            _iconTheme.ChangeTo(ThemeManager.Theme == ThemeSettings.Theme.Dark ? 0 : 1);
        }

        private void UpdateLangList()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            Tools.EnsureChildren(_listLang, locales.Count);
            for (var i = 0; i < locales.Count; i++)
            {
                var go = _listLang.GetChild(i).gameObject;
                go.SetActive(true);
                go.transform.Find("Text").GetComponent<TextMeshProUGUI>().text = locales[i].LocaleName;
                var toggle = go.GetComponent<XToggle>();
                RemoveUniqueListener(toggle);
                toggle.isOn = locales[i] == LocalizationSettings.SelectedLocale;
                AddUniqueListener(toggle, i, OnToggleLang);
            }
        }

        private void OnToggleLang(Toggle toggle, int index, bool isOn)
        {
            if (isOn)
            {
                Lang.SetLocale(LocalizationSettings.AvailableLocales.Locales[index]).Forget();
            }
        }

        private void UpdateCustomMacAddress()
        {
            _inputCustomMacAddress.text = AppSettings.Instance.GetMacAddress();
        }

        private void OnChangeCustomMacAddress(string value)
        {
            if (!Tools.IsValidMacAddress(value))
            {
                ShowNotificationUI(Lang.GetRef("SettingsUI_Invalid_MacAddress_Tips")).Forget();
                UpdateCustomMacAddress();
                return;
            }

            AppSettings.Instance.SetMacAddress(value);
        }

        private void UpdateWebSocketAccessToken()
        {
            _inputWebSocketAccessToken.text = AppSettings.Instance.GetWebSocketAccessToken();
        }

        private void OnChangeWebSocketAccessToken(string value)
        {
            AppSettings.Instance.SetWebSocketAccessToken(value);
        }

        private void UpdateWebSocketUrl()
        {
            _inputWebSocketUrl.text = AppSettings.Instance.GetWebSocketUrl();
        }

        private void OnChangeWebSocketUrl(string value)
        {
            if (!Tools.IsValidUrl(value))
            {
                ShowNotificationUI(Lang.GetRef("SettingsUI_Invalid_Url_Tips")).Forget();
                UpdateWebSocketUrl();
                return;
            }

            AppSettings.Instance.SetWebSocketUrl(value);
        }

        private void OnSelectedLocaleChanged(Locale locale)
        {
            UpdateKeywords(true);
        }
    }
}