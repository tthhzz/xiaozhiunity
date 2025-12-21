using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Settings;

namespace XiaoZhi.Unity.IoT
{
    public class ThingAppSettings : Thing
    {
        public ThingAppSettings() : base("AppSettings", "设置中心，可以设置主题/音量/语言等")
        {
        }

        public override async UniTask Load()
        {
            _properties.AddProperty("theme", "主题", GetTheme);
            _methods.AddMethod("SetTheme", "设置主题",
                new ParameterList(new[]
                {
                    new Parameter<string>("theme", "主题模式, Light 或 Dark")
                }),
                SetTheme);
            _properties.AddProperty("volume", "当前音量值", GetVolume);
            _methods.AddMethod("SetVolume", "设置音量",
                new ParameterList(new[]
                {
                    new Parameter<int>("volume", "0到100之间的整数")
                }),
                SetVolume);
            _properties.AddProperty("lang", "语言", GetLang);
            _methods.AddMethod("SetLang", "设置语言",
                new ParameterList(new[]
                {
                    new Parameter<string>("lang", "语言, 简体中文 或 English")
                }),
                SetLang);
            _properties.AddProperty("zoom", "镜头远近", GetZoom);
            _methods.AddMethod("SetZoom", "设置镜头远近",
                new ParameterList(new[]
                {
                    new Parameter<string>("zoom", "镜头远近, " + string.Join(" 或 ", Enum.GetNames(typeof(ZoomMode))))
                }),
                SetZoom);
            var wallpaperNames = "壁纸名称, " + string.Join(" 或 ", AppPresets.Instance.Wallpapers.Select(i => i.Name));
            _methods.AddMethod("SetWallpaper", "设置壁纸",
                new ParameterList(new[]
                {
                    new Parameter<string>("wallpaperName", wallpaperNames)
                }),
                SetWallpaper);
            await base.Load();
        }

        private string GetTheme()
        {
            return ThemeManager.Theme.ToString();
        }

        private void SetTheme(ParameterList parameters)
        {
            ThemeManager.SetTheme(Enum.Parse<ThemeSettings.Theme>(parameters.GetValue<string>("theme")));
        }

        private int GetVolume()
        {
            return AppSettings.Instance.GetOutputVolume();
        }

        private void SetVolume(ParameterList parameters)
        {
            AppSettings.Instance.SetOutputVolume(parameters.GetValue<int>("volume"));
        }

        private string GetLang()
        {
            return LocalizationSettings.SelectedLocale.LocaleName;
        }

        private void SetLang(ParameterList parameters)
        {
            var lang = parameters.GetValue<string>("lang");
            var locale = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(i => i.LocaleName == lang);
            if (locale) LocalizationSettings.SelectedLocale = locale;
        }

        private string GetZoom()
        {
            return AppSettings.Instance.GetZoomMode().ToString();
        }

        private void SetZoom(ParameterList parameters)
        {
            AppSettings.Instance.SetZoomMode(Enum.Parse<ZoomMode>(parameters.GetValue<string>("zoom")));
        }

        private void SetWallpaper(ParameterList parameters)
        {
            AppSettings.Instance.SetWallpaper(parameters.GetValue<string>("wallpaperName"));
        }
    }
}