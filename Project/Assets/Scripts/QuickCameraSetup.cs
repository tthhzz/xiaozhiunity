using UnityEngine;

namespace XiaoZhi.Unity
{
    /// <summary>
    /// 快速配置摄像头 - 使用智谱AI
    /// 使用方法：
    /// 1. 在Unity场景中创建一个空GameObject
    /// 2. 挂载此脚本
    /// 3. 在Inspector中填入你的智谱AI API Key
    /// 4. 运行游戏，配置会自动应用
    /// </summary>
    public class QuickCameraSetup : MonoBehaviour
    {
        [Header("智谱AI配置")]
        [Tooltip("从 https://open.bigmodel.cn/ 获取")]
        public string zhipuApiKey = "";
        
        [Tooltip("智谱AI URL（通常不需要修改）")]
        public string zhipuUrl = "https://open.bigmodel.cn/api/paas/v4/";
        
        [Tooltip("模型名称（通常不需要修改）")]
        public string modelName = "glm-4v-plus";

        private void Start()
        {
            if (!string.IsNullOrEmpty(zhipuApiKey))
            {
                Debug.Log("[QuickCameraSetup] Configuring camera with Zhipu AI...");
                AppSettings.Instance.SetCameraVLApiKey(zhipuApiKey);
                AppSettings.Instance.SetCameraVLUrl(zhipuUrl);
                AppSettings.Instance.SetCameraModel(modelName);
                
                // 重新初始化摄像头（如果已经初始化过）
                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.SetVLApiKey(zhipuApiKey);
                    CameraManager.Instance.SetVLUrl(zhipuUrl);
                    CameraManager.Instance.SetVLModel(modelName);
                }
                
                Debug.Log("[QuickCameraSetup] Camera configured successfully!");
                Debug.Log("[QuickCameraSetup] You can now use the camera by saying '拍照' or '看看这是什么'");
            }
            else
            {
                Debug.LogWarning("[QuickCameraSetup] Zhipu API Key is not set. Please fill it in the Inspector.");
                Debug.LogWarning("[QuickCameraSetup] Get your API Key from: https://open.bigmodel.cn/");
            }
        }
    }
}

