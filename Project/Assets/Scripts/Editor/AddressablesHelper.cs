using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace XiaoZhi.Unity.Editor
{
    public static class AddressablesHelper
    {
        [MenuItem("XiaoZhi/Addressables/Add VRM Prefabs to Addressables")]
        public static void AddVRMPrefabsToAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings 未找到。请先创建 Addressables 设置。");
                return;
            }

            // Girl prefab
            var girlPath = "Assets/Res/VRM/Girl/prefab.prefab";
            var girlGuid = AssetDatabase.AssetPathToGUID(girlPath);
            if (!string.IsNullOrEmpty(girlGuid))
            {
                AddAssetToAddressables(settings, girlGuid, girlPath);
            }
            else
            {
                Debug.LogWarning($"找不到资源: {girlPath}");
            }

            // Boy prefab
            var boyPath = "Assets/Res/VRM/Boy/prefab.prefab";
            var boyGuid = AssetDatabase.AssetPathToGUID(boyPath);
            if (!string.IsNullOrEmpty(boyGuid))
            {
                AddAssetToAddressables(settings, boyGuid, boyPath);
            }
            else
            {
                Debug.LogWarning($"找不到资源: {boyPath}");
            }

            // 刷新资源数据库
            AssetDatabase.Refresh();
            
            Debug.Log("VRM Prefabs 已添加到 Addressables 系统。请重新运行应用以生效。");
        }

        private static void AddAssetToAddressables(AddressableAssetSettings settings, string guid, string path)
        {
            // 检查是否已经添加到 Addressables
            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                Debug.Log($"资源已存在于 Addressables: {path}");
                return;
            }

            // 添加到 Addressables
            var group = settings.DefaultGroup;
            if (group == null)
            {
                group = settings.CreateGroup("Default", false, false, true, null);
            }

            entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry != null)
            {
                entry.address = path; // 使用完整路径作为地址
                Debug.Log($"已添加资源到 Addressables: {path}");
            }
            else
            {
                Debug.LogError($"添加资源失败: {path}");
            }

            // 保存设置
            AssetDatabase.SaveAssets();
        }

        [MenuItem("XiaoZhi/Addressables/Check VRM Prefabs Status")]
        public static void CheckVRMPrefabsStatus()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("Addressable Asset Settings 未找到。");
                return;
            }

            var girlPath = "Assets/Res/VRM/Girl/prefab.prefab";
            var boyPath = "Assets/Res/VRM/Boy/prefab.prefab";

            CheckAssetStatus(settings, girlPath, "Girl");
            CheckAssetStatus(settings, boyPath, "Boy");
        }

        private static void CheckAssetStatus(AddressableAssetSettings settings, string path, string name)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[{name}] 资源不存在: {path}");
                return;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                Debug.Log($"[{name}] ✓ 已添加到 Addressables - 地址: {entry.address}");
            }
            else
            {
                Debug.LogWarning($"[{name}] ✗ 未添加到 Addressables: {path}");
            }
        }
    }
}

