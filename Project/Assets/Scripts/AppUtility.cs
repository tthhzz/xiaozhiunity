using UnityEngine;
using System;
using System.Linq;

namespace XiaoZhi.Unity
{
    public static class AppUtility
    {
        private static string _uuid;

        private static string _macAddress;
        
        public static void Clear()
        {
            _uuid = null;
            _macAddress = null;
        }

        public static string GetUUid()
        {
            _uuid ??= Guid.NewGuid().ToString("d");
            return _uuid;
        }

        public static string GetMacAddress()
        {
            _macAddress ??= BuildMacAddress();
            return _macAddress;
        }

        private static string BuildMacAddress()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver"))
            using (var settingsSecure = new AndroidJavaClass("android.provider.Settings$Secure"))
            {
                var androidId = settingsSecure.CallStatic<string>("getString", contentResolver, "android_id");
                var formattedId = string.Join(":", Enumerable.Range(2, 6).Select(i => androidId.Substring(i * 2, 2)));
                return formattedId;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            var vendorId = UnityEngine.iOS.Device.vendorIdentifier;
            if (!string.IsNullOrEmpty(vendorId))
            {
                vendorId = vendorId.Replace("-", "").Substring(vendorId.Length - 12, 12);
                return string.Join(":", Enumerable.Range(0, 6)
                    .Select(i => vendorId.Substring(i * 2, 2)));
            }

            return string.Empty;
#else
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var adapter = adapters.OrderByDescending(i => i.Id).FirstOrDefault(i =>
                i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                i.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                    or System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);
            if (adapter != null)
            {
                var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
                return string.Join(":", bytes.Select(b => b.ToString("x2")));
            }
            return string.Empty;
#endif
        }

        public static string GetBoardName()
        {
            return Application.productName;
        }

        public static string GetVersion()
        {
            return Application.version;
        }

        public static bool IsMobile()
        {
            return Application.isMobilePlatform;
        }
    }
}