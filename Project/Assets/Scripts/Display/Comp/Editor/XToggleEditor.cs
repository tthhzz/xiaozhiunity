using UnityEditor;
using UnityEditor.UI;

namespace XiaoZhi.Unity
{
    [CustomEditor(typeof(XToggle))]
    public class XToggleEditor : ToggleEditor
    {
        private SerializedProperty _reactModifiers;
        
        private SerializedProperty _offBackground;

        private SerializedProperty _onBackground;
            
        protected override void OnEnable()
        {
            base.OnEnable();
            _reactModifiers = serializedObject.FindProperty("_reactModifiers");
            _offBackground = serializedObject.FindProperty("_offBackground");
            _onBackground = serializedObject.FindProperty("_onBackground");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.PropertyField(_reactModifiers);
            EditorGUILayout.PropertyField(_offBackground);
            EditorGUILayout.PropertyField(_onBackground);
            serializedObject.ApplyModifiedProperties();
        }
    }
}