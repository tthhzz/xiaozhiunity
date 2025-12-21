using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace XiaoZhi.Unity
{
    public enum PermissionType
    {
        ReadStorage,
        WriteStorage,
        Camera,
        Microphone,
    }

    public struct PermissionResult
    {
        public PermissionType Type;
        public bool Granted;

        public PermissionResult(PermissionType type, bool granted)
        {
            Type = type;
            Granted = granted;
        }
    }

    public static class PermissionManager
    {
        public static async UniTask<IEnumerable<PermissionResult>> RequestPermissions(
            params PermissionType[] permissions)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var result =
                await AndroidRuntimePermissions.RequestPermissionsAsync(permissions.Select(ParseAndroidPermissionType)
                    .ToArray());
            return result.Select((i, index) =>
                new PermissionResult(permissions[index], ParseAndroidPermissionState(i)));
#elif UNITY_IOS && !UNITY_EDITOR
            return await permissions.Select(permission =>
            {
                return permission is PermissionType.ReadStorage or PermissionType.WriteStorage
                    ? UniTask.FromResult(new PermissionResult(permission, true))
                    : IOSRuntimePermissions.RequestPermissionAsync(ParseIOSPermissionType(permission)).AsUniTask()
                        .ContinueWith(i => new PermissionResult(permission, ParseIOSPermissionState(i)));
            });
#else
            return await UniTask.FromResult(permissions.Select(permission => new PermissionResult(permission, true)));
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string ParseAndroidPermissionType(PermissionType type)
        {
            return type switch
            {
                PermissionType.ReadStorage => "android.permission.READ_EXTERNAL_STORAGE",
                PermissionType.WriteStorage => "android.permission.WRITE_EXTERNAL_STORAGE",
                PermissionType.Camera => "android.permission.CAMERA",
                PermissionType.Microphone => "android.permission.RECORD_AUDIO",
                _ => throw new System.NotSupportedException()
            };
        }

        private static bool ParseAndroidPermissionState(AndroidRuntimePermissions.Permission state)
        {
            return state == AndroidRuntimePermissions.Permission.Granted;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private static IOSRuntimePermissions.PermissionType ParseIOSPermissionType(PermissionType type)
        {
            return type switch
            {
                PermissionType.Camera => IOSRuntimePermissions.PermissionType.Camera,
                PermissionType.Microphone => IOSRuntimePermissions.PermissionType.Microphone,
                _ => throw new System.NotSupportedException()
            };
        }

        private static bool ParseIOSPermissionState(IOSRuntimePermissions.Permission state)
        {
            return state == IOSRuntimePermissions.Permission.Granted;
        }
#endif
    }
}