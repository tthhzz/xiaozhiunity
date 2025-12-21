#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Permission plugin for iOS.
/// </summary>
public static class IOSRuntimePermissions
{
    public enum PermissionType
    {
        Camera = 0, //相机
        PhotoLibrary, //相册
        Microphone, //麦克风
        LocationWhenInUse, //app启动时候的定位
        LocationAlways, //app总是定位的权限
    }

    public enum Permission
    {
        Granted = 1,
        ShouldAsk = 2,
        Denied = 3
    };

    // 定义位置权限类型枚举
    public enum LocationPermissionType
    {
        WhenInUse = 0,
        Always = 1
    };

    // 定义权限状态回调委托
    public delegate void PermissionStatusCallback(Permission status);

    [DllImport("__Internal")]
    private static extern int _PNativeCamera_CheckPermission();

    [DllImport("__Internal")]
    private static extern int _PNativeCamera_RequestPermission();

    [DllImport("__Internal")]
    private static extern int _PNativePhotoLibrary_CheckPermission();

    [DllImport("__Internal")]
    private static extern int _PNativePhotoLibrary_RequestPermission();

    [DllImport("__Internal")]
    private static extern int _PNativeMicrophone_CheckPermission();

    [DllImport("__Internal")]
    private static extern int _PNativeMicrophone_RequestPermission();

    [DllImport("__Internal")]
    private static extern int _PNativeLocation_CheckPermission(int permissionType);

    [DllImport("__Internal")]
    private static extern int _PNativeLocation_RequestPermission(int permissionType, PermissionStatusCallback callback);

    public static Permission CheckPermission(PermissionType permissionType)
    {
        return permissionType switch
        {
            PermissionType.Microphone => (Permission)_PNativeMicrophone_CheckPermission(),
            PermissionType.LocationWhenInUse => (Permission)_PNativeLocation_CheckPermission(
                (int)LocationPermissionType.WhenInUse),
            PermissionType.LocationAlways => (Permission)_PNativeLocation_CheckPermission(
                (int)LocationPermissionType.Always),
            PermissionType.Camera => (Permission)_PNativeCamera_CheckPermission(),
            PermissionType.PhotoLibrary => (Permission)_PNativePhotoLibrary_CheckPermission(),
            _ => Permission.Granted
        };
    }

    public static void RequestPermission(PermissionType permissionType, PermissionStatusCallback callback)
    {
        switch (permissionType)
        {
            case PermissionType.Microphone:
                callback((Permission)_PNativeMicrophone_RequestPermission());
                break;
            case PermissionType.LocationWhenInUse:
                _PNativeLocation_RequestPermission((int)LocationPermissionType.WhenInUse, callback);
                break;
            case PermissionType.LocationAlways:
                _PNativeLocation_RequestPermission((int)LocationPermissionType.Always, callback);
                break;
            case PermissionType.Camera:
                callback((Permission)_PNativeCamera_RequestPermission());
                break;
            case PermissionType.PhotoLibrary:
                callback((Permission)_PNativePhotoLibrary_RequestPermission());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null);
        }
    }

    public static async Task<Permission> RequestPermissionAsync(PermissionType permissionType)
    {
        var tcs = new TaskCompletionSource<Permission>();
        RequestPermission(permissionType, (status) => { tcs.SetResult(status); });
        return await tcs.Task;
    }

    public static async Task<Permission[]> RequestPermissionsAsync(PermissionType[] permissionTypes)
    {
        if (permissionTypes == null || permissionTypes.Length == 0)
            throw new NullReferenceException();
        return await Task.WhenAll(permissionTypes.Select(RequestPermissionAsync));
    }
}
#endif