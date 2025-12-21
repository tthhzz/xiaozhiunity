using UnityEngine;
using UnityEditor;
using XiaoZhi.Unity;

namespace XiaoZhi.Unity.Editor
{
    public class DisplayModeEditor : EditorWindow
    {
        private DisplayMode _currentDisplayMode;

        [MenuItem("XiaoZhi/Display Mode Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<DisplayModeEditor>("Display Mode Settings");
            window.LoadCurrentDisplayMode();
            window.Show();
        }

        private void OnEnable()
        {
            LoadCurrentDisplayMode();
        }

        private void LoadCurrentDisplayMode()
        {
            // 从 PlayerPrefs 读取当前设置
            int displayModeValue = PlayerPrefs.GetInt("app_display_mode", 0);
            _currentDisplayMode = (DisplayMode)displayModeValue;
        }

        private void OnGUI()
        {
            GUILayout.Label("Display Mode Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Current Display Mode:", _currentDisplayMode.ToString());
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Select Display Mode:", EditorStyles.label);
            
            EditorGUI.BeginChangeCheck();
            
            _currentDisplayMode = (DisplayMode)EditorGUILayout.EnumPopup("Display Mode", _currentDisplayMode);
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveDisplayMode();
            }

            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Set to Emoji", GUILayout.Height(30)))
            {
                _currentDisplayMode = DisplayMode.Emoji;
                SaveDisplayMode();
            }
            
            if (GUILayout.Button("Set to Girl", GUILayout.Height(30)))
            {
                _currentDisplayMode = DisplayMode.Girl;
                SaveDisplayMode();
            }
            
            if (GUILayout.Button("Set to Boy", GUILayout.Height(30)))
            {
                _currentDisplayMode = DisplayMode.Boy;
                SaveDisplayMode();
            }
            
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Clear Settings (Reset to Emoji)", GUILayout.Height(25)))
            {
                PlayerPrefs.DeleteKey("app_display_mode");
                PlayerPrefs.Save();
                _currentDisplayMode = DisplayMode.Emoji;
                Debug.Log("Display Mode settings cleared. Reset to Emoji.");
            }

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Note: Changes will take effect after restarting the application.\n" +
                "Current value: " + _currentDisplayMode + " (" + (int)_currentDisplayMode + ")",
                MessageType.Info);
        }

        private void SaveDisplayMode()
        {
            PlayerPrefs.SetInt("app_display_mode", (int)_currentDisplayMode);
            PlayerPrefs.Save();
            Debug.Log($"Display Mode set to: {_currentDisplayMode}");
        }
    }
}

