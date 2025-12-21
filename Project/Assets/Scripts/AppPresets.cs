using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace XiaoZhi.Unity
{
    [Serializable]
    [CreateAssetMenu(menuName = "App Presets")]
    public class AppPresets : ScriptableObject
    {
        [Serializable]
        public class VRMModel
        {
            [SerializeField] private string _name;
            [SerializeField] private string _path;
            [SerializeField] private Color _color;

            public string Name => _name;
            public string Path => _path;
            public Color Color => _color;
        }

        [Serializable]
        public class Keyword
        {
            [SerializeField] private string _localeCode;
            [SerializeField] private string _spotterModelConfigTransducerEncoder;
            [SerializeField] private string _spotterModelConfigTransducerDecoder;
            [SerializeField] private string _spotterModelConfigTransducerJoiner;
            [SerializeField] private string _spotterModelConfigToken;
            [SerializeField] private string _spotterKeyWordsFile;
            [SerializeField] private int _spotterModelConfigNumThreads;

            public string Name => _localeCode;
            public string LocaleCode => _localeCode;
            public string SpotterModelConfigTransducerEncoder => _spotterModelConfigTransducerEncoder;
            public string SpotterModelConfigTransducerDecoder => _spotterModelConfigTransducerDecoder;
            public string SpotterModelConfigTransducerJoiner => _spotterModelConfigTransducerJoiner;
            public string SpotterModelConfigToken => _spotterModelConfigToken;
            public string SpotterKeyWordsFile => _spotterKeyWordsFile;
            public int SpotterModelConfigNumThreads => _spotterModelConfigNumThreads;
        }

        [Serializable]
        public class Video
        {
            [SerializeField] private string _name;
            [SerializeField] private string _path;

            public string Name => _name;
            public string Path => _path;
        }

        [Serializable]
        public class Wallpaper
        {
            [SerializeField] private WallpaperType _type;
            [SerializeField] private string _name;
            [SerializeField] private string _path;

            public WallpaperType Type => _type;
            public string Name => _name;
            public string Path => _path;
        }

        [Serializable]
        public class AnimationMeta
        {
            [SerializeField] private string _name;
            [SerializeField] private string _path;
            [SerializeField] private int _weight;
            [SerializeField] private bool _fadeIn = true;

            public string Name => _name;
            public string Path => _path;
            public int Weight => _weight;
            public bool FadeIn => _fadeIn;
        }

        [Serializable]
        public class AnimationSet
        {
            [SerializeField] private AnimationMeta[] _anims;
            [SerializeField] private string[] _labels;

            public AnimationMeta[] Anims => _anims;
            public string[] Labels => _labels;
        }

        [Serializable]
        public class AnimationLib
        {
            [SerializeField] private string _name;
            [SerializeField] private AnimationSet[] _sets;

            public string Name => _name;
            public AnimationSet[] Sets => _sets;

            public AnimationMeta[] MatchAny(IEnumerable<string> labels)
            {
                return _sets.Where(k => labels.Any(k.Labels.Contains)).SelectMany(k => k.Anims).ToArray();
            }

            public AnimationMeta[] MatchAll(IEnumerable<string> labels)
            {
                return _sets.Where(k => labels.All(k.Labels.Contains)).SelectMany(k => k.Anims).ToArray();
            }

            public static AnimationMeta Random(AnimationMeta[] anims)
            {
                var totalWeight = anims.Sum(k => k.Weight);
                var randomWeight = UnityEngine.Random.Range(0.0f, totalWeight);
                var weight = 0;
                foreach (var anim in anims)
                {
                    weight += anim.Weight;
                    if (weight >= randomWeight)
                        return anim;
                }

                return null;
            }
        }

        [Serializable]
        public class Dance
        {
            [SerializeField] private string _name;
            [SerializeField] private string _animation;
            [SerializeField] private string _bgm;
            
            public string Name => _name;
            public string Animation => _animation;
            public string BGM => _bgm;
        }

        [SerializeField] private string _webSocketUrl;
        [SerializeField] private string _webSocketAccessToken;
        [SerializeField] private int _opusFrameDurationMs;
        [SerializeField] private int _audioInputSampleRate;
        [SerializeField] private int _audioOutputSampleRate;
        [SerializeField] private int _serverInputSampleRate;
        [SerializeField] private bool _enableWakeService;
        [SerializeField] private string _otaVersionUrl;
        [SerializeField] private Keyword[] _keyWords;
        [SerializeField] private string _vadModelConfig;
        [SerializeField] private string _activationURL;
        [SerializeField] private VRMModel[] _vrmCharacterModels;
        [SerializeField] private Video[] _videos;
        [SerializeField] private Wallpaper[] _wallpapers;
        [SerializeField] private AnimationLib[] _animationLibs;
        [SerializeField] private Dance[] _dances;

        public string WebSocketUrl => _webSocketUrl;
        public string WebSocketAccessToken => _webSocketAccessToken;
        public int OpusFrameDurationMs => _opusFrameDurationMs;
        public int AudioInputSampleRate => _audioInputSampleRate;
        public int AudioOutputSampleRate => _audioOutputSampleRate;
        public int ServerInputSampleRate => _serverInputSampleRate;
        public bool EnableWakeService => _enableWakeService;
        public string OtaVersionUrl => _otaVersionUrl;
        public string VadModelConfig => _vadModelConfig;
        public string ActivationURL => _activationURL;
        public VRMModel[] VRMCharacterModels => _vrmCharacterModels;
        public Video[] Videos => _videos;
        public Video GetVideo(string videoName) => _videos.FirstOrDefault(k => k.Name == videoName);
        public Wallpaper[] Wallpapers => _wallpapers;
        public Wallpaper GetWallpaper(string paperName = "Default") =>
            _wallpapers.FirstOrDefault(k => k.Name == paperName);
        public AnimationLib[] AnimationLibs => _animationLibs;
        public AnimationLib GetAnimationLib(string libName = "Default") => _animationLibs.FirstOrDefault(k => k.Name == libName);
        public Keyword GetKeyword(string localeCode) => _keyWords.FirstOrDefault(k => k.LocaleCode == localeCode);
        public Dance[] Dances => _dances;
        public Dance GetDance(string danceName) => _dances.FirstOrDefault(k => k.Name == danceName);
        public static AppPresets Instance { get; private set; }

        public static async UniTask Load()
        {
            const string path = "Assets/Settings/AppPresets.asset";
            Instance = await Addressables.LoadAssetAsync<AppPresets>(path);
        }
    }
}