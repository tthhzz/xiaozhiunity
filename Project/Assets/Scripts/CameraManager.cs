using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// 用于JSON序列化的类
[Serializable]
public class VLRequest
{
    public string model;
    public VLMessage[] messages;
}

[Serializable]
public class VLMessage
{
    public string role;
    public VLContent[] content;
}

[Serializable]
public class VLContent
{
    public string type;
    public string text;
    public VLImageUrl image_url;
}

[Serializable]
public class VLImageUrl
{
    public string url;
}

[Serializable]
public class VLResponse
{
    public VLChoice[] choices;
}

[Serializable]
public class VLChoice
{
    public VLResponseMessage message;
}

[Serializable]
public class VLResponseMessage
{
    public string content;
}

namespace XiaoZhi.Unity
{
    public class CameraManager : MonoBehaviour
    {
        private static CameraManager _instance;
        public static CameraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CameraManager");
                    _instance = go.AddComponent<CameraManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private WebCamTexture webCamTexture;
        private byte[] jpegData;
        private string explainUrl = "";
        private string explainToken = "";
        private string vlApiKey = "";
        private string vlUrl = "";
        private string vlModel = "";
        private bool isInitialized = false;

        public void SetExplainUrl(string url)
        {
            explainUrl = url;
            Debug.Log($"[CameraManager] Explain URL set: {url}");
        }

        public void SetExplainToken(string token)
        {
            explainToken = token;
            Debug.Log($"[CameraManager] Explain token set: {(!string.IsNullOrEmpty(token) ? "Yes" : "No")}");
        }

        public void SetVLApiKey(string apiKey)
        {
            vlApiKey = apiKey;
            Debug.Log($"[CameraManager] VL API Key set: {(!string.IsNullOrEmpty(apiKey) ? "Yes" : "No")}");
        }

        public void SetVLUrl(string url)
        {
            vlUrl = url;
            Debug.Log($"[CameraManager] VL URL set: {url}");
        }

        public void SetVLModel(string model)
        {
            vlModel = model;
            Debug.Log($"[CameraManager] VL Model set: {model}");
        }

        private void ReleaseWebCamTexture()
        {
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                {
                    webCamTexture.Stop();
                }
                DestroyImmediate(webCamTexture);
                webCamTexture = null;
                Debug.Log("[CameraManager] Camera texture released");
            }
            isInitialized = false;
        }

        /// <summary>
        /// 捕获图像 - 模仿 py-xiaozhi 的简单逻辑：打开摄像头 -> 读取帧 -> 立即释放
        /// </summary>
        public async UniTask<bool> Capture()
        {
            try
            {
                Debug.Log("[CameraManager] Starting capture...");
                
                // 先释放已有的摄像头资源
                ReleaseWebCamTexture();
                
                WebCamDevice[] devices = WebCamTexture.devices;
                Debug.Log($"[CameraManager] Found {devices.Length} camera devices");
                
                if (devices.Length == 0)
                {
                    Debug.LogError("[CameraManager] No camera devices found");
                    return false;
                }

                // 创建并启动摄像头
                Debug.Log($"[CameraManager] Using camera: {devices[0].name}");
                webCamTexture = new WebCamTexture(devices[0].name, 640, 480);
                webCamTexture.Play();
                isInitialized = true;
                
                // 等待摄像头启动
                Debug.Log("[CameraManager] Waiting for camera to start...");
                int timeout = 50; // 5秒超时
                while (!webCamTexture.didUpdateThisFrame && timeout > 0)
                {
                    await UniTask.Delay(100);
                    timeout--;
                }
                
                if (timeout == 0)
                {
                    Debug.LogError("[CameraManager] Camera timeout - no frame received");
                    ReleaseWebCamTexture();
                    return false;
                }
                
                Debug.Log($"[CameraManager] Camera resolution: {webCamTexture.width}x{webCamTexture.height}");
                
                // 关键修复：等待摄像头预热，让曝光和白平衡稳定
                // 摄像头刚启动时前几帧通常是黑色或曝光不正确的
                Debug.Log("[CameraManager] Warming up camera (waiting for valid frames)...");
                for (int i = 0; i < 15; i++)  // 等待约15帧，让摄像头稳定
                {
                    await UniTask.WaitForEndOfFrame();
                    await UniTask.Delay(100);  // 每帧间隔100ms
                }
                
                // 确保获取最新帧
                await UniTask.WaitForEndOfFrame();
                
                // 捕获帧
                Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height);
                snapshot.SetPixels(webCamTexture.GetPixels());
                snapshot.Apply();

                // 立即释放摄像头（模仿 py-xiaozhi: cap.release()）
                ReleaseWebCamTexture();

                // 缩放图像，使最长边为320（与 py-xiaozhi 一致）
                int maxDim = Mathf.Max(snapshot.width, snapshot.height);
                if (maxDim > 320)
                {
                    float scale = 320f / maxDim;
                    int newWidth = (int)(snapshot.width * scale);
                    int newHeight = (int)(snapshot.height * scale);
                    Debug.Log($"[CameraManager] Resizing from {snapshot.width}x{snapshot.height} to {newWidth}x{newHeight}");
                    snapshot = ResizeTexture(snapshot, newWidth, newHeight);
                }

                jpegData = snapshot.EncodeToJPG();
                Destroy(snapshot);
                
                Debug.Log($"[CameraManager] Image captured successfully ({jpegData.Length} bytes)");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CameraManager] Capture failed: {e}");
                ReleaseWebCamTexture();
                return false;
            }
        }

        public byte[] GetJpegData() => jpegData;

        public async UniTask<string> TakePhotoAndExplain(string question)
        {
            Debug.Log($"[CameraManager] TakePhotoAndExplain called with question: {question}");
            
            if (!await Capture())
            {
                Debug.LogError("[CameraManager] Failed to capture photo");
                return "{\"success\": false, \"message\": \"Failed to capture photo\"}";
            }

            // 可选：在问题中嵌入指令，提示AI准确描述识别结果
            // string enhancedQuestion = $"{question}。请详细、准确地描述图像识别的结果，不要省略细节。";
            
            return await Explain(question);
        }

        public async UniTask<string> Explain(string question)
        {
            Debug.Log($"[CameraManager] Explain called with question: {question}");
            
            if (jpegData == null || jpegData.Length == 0)
            {
                Debug.LogError("[CameraManager] Camera buffer is empty");
                return "{\"success\": false, \"message\": \"Camera buffer is empty\"}";
            }

            // 优先使用智谱AI模式（如果配置了API Key）
            if (!string.IsNullOrEmpty(vlApiKey) && !string.IsNullOrEmpty(vlUrl))
            {
                Debug.Log("[CameraManager] Using VL Camera mode (Zhipu AI)");
                return await ExplainWithZhipuAI(question);
            }

            // 否则使用外部explain服务
            if (!string.IsNullOrEmpty(explainUrl))
            {
                Debug.Log("[CameraManager] Using Normal Camera mode (External explain service)");
                return await ExplainWithExternalService(question);
            }

            Debug.LogError("[CameraManager] No explain service configured");
            return "{\"success\": false, \"message\": \"No explain service configured. Please set VL API Key or Explain URL\"}";
        }

        private async UniTask<string> ExplainWithZhipuAI(string question)
        {
            Debug.Log($"[CameraManager] Calling Zhipu AI with model: {vlModel}");
            Debug.Log($"[CameraManager] Image size: {jpegData.Length} bytes");

            string base64Image = System.Convert.ToBase64String(jpegData);
            string imageDataUrl = $"data:image/jpeg;base64,{base64Image}";
            
            // 使用 JsonUtility 构建请求，避免 JSON 转义问题
            // 注意：py-xiaozhi 的消息顺序是 image_url 在前，text 在后
            var requestObj = new VLRequest
            {
                model = vlModel,
                messages = new VLMessage[]
                {
                    new VLMessage
                    {
                        role = "user",
                        content = new VLContent[]
                        {
                            new VLContent
                            {
                                type = "image_url",
                                image_url = new VLImageUrl { url = imageDataUrl }
                            },
                            new VLContent
                            {
                                type = "text",
                                text = string.IsNullOrEmpty(question) ? "图中描绘的是什么景象？请详细描述。" : question
                            }
                        }
                    }
                }
            };
            
            string requestBody = JsonUtility.ToJson(requestObj);
            
            // 构建正确的 URL - 如果 vlUrl 已经包含完整路径则直接使用
            string apiUrl = vlUrl;
            if (!apiUrl.EndsWith("/") && !apiUrl.EndsWith("completions"))
            {
                apiUrl = apiUrl.TrimEnd('/') + "/";
            }
            if (!apiUrl.Contains("chat/completions"))
            {
                apiUrl += "chat/completions";
            }
            
            Debug.Log($"[CameraManager] VL API URL: {apiUrl}");

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {vlApiKey}");
                request.timeout = 30;

                Debug.Log("[CameraManager] Sending request to Zhipu AI...");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"[CameraManager] Zhipu AI response: {responseText}");
                    
                    // 解析响应，提取 content
                    try
                    {
                        var response = JsonUtility.FromJson<VLResponse>(responseText);
                        if (response?.choices != null && response.choices.Length > 0 && response.choices[0].message != null)
                        {
                            string content = response.choices[0].message.content ?? "";
                            // 返回与 py-xiaozhi 一致的格式
                            return $"{{\"success\": true, \"text\": \"{EscapeJsonString(content)}\"}}";
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogWarning($"[CameraManager] Failed to parse VL response: {parseEx.Message}, returning raw response");
                    }
                    
                    return responseText;
                }
                else
                {
                    Debug.LogError($"[CameraManager] Zhipu AI request failed! Status: {request.responseCode}, Error: {request.error}");
                    Debug.LogError($"[CameraManager] Response: {request.downloadHandler?.text ?? "No response"}");
                    return $"{{\"success\": false, \"message\": \"Zhipu AI request failed: {EscapeJsonString(request.error)}\"}}";
                }
            }
        }
        
        /// <summary>
        /// 转义 JSON 字符串中的特殊字符
        /// </summary>
        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private async UniTask<string> ExplainWithExternalService(string question)
        {
            Debug.Log($"[CameraManager] Sending request to: {explainUrl}");
            Debug.Log($"[CameraManager] Image size: {jpegData.Length} bytes");

            // 手动构建multipart/form-data，完全模拟requests.post
            string boundary = "----WebKitFormBoundary" + System.Guid.NewGuid().ToString("N");
            byte[] boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary + "\r\n");
            byte[] middleBoundaryBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "\r\n");
            byte[] endBoundaryBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

            using (var stream = new System.IO.MemoryStream())
            {
                // question字段（第一个字段，前面不加\r\n）
                stream.Write(boundaryBytes, 0, boundaryBytes.Length);
                string questionHeader = $"Content-Disposition: form-data; name=\"question\"\r\n\r\n{question}";
                byte[] questionBytes = Encoding.UTF8.GetBytes(questionHeader);
                stream.Write(questionBytes, 0, questionBytes.Length);

                // file字段（后续字段，前面加\r\n）
                stream.Write(middleBoundaryBytes, 0, middleBoundaryBytes.Length);
                string fileHeader = $"Content-Disposition: form-data; name=\"file\"; filename=\"camera.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n";
                byte[] fileHeaderBytes = Encoding.UTF8.GetBytes(fileHeader);
                stream.Write(fileHeaderBytes, 0, fileHeaderBytes.Length);
                stream.Write(jpegData, 0, jpegData.Length);

                // 结束边界
                stream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);

                byte[] formData = stream.ToArray();

                using (UnityWebRequest request = new UnityWebRequest(explainUrl, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(formData);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 10;

                    string deviceId = AppSettings.Instance.GetMacAddress();
                    string clientId = AppUtility.GetUUid();
                    Debug.Log($"[CameraManager] Device-Id: {deviceId}");
                    Debug.Log($"[CameraManager] Client-Id: {clientId}");
                    
                    // 按照py-xiaozhi的顺序设置headers
                    request.SetRequestHeader("Content-Type", $"multipart/form-data; boundary={boundary}");
                    request.SetRequestHeader("Device-Id", deviceId);
                    request.SetRequestHeader("Client-Id", clientId);

                    if (!string.IsNullOrEmpty(explainToken))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {explainToken}");
                        Debug.Log("[CameraManager] Authorization header added");
                    }

                    Debug.Log("[CameraManager] Sending HTTP request...");
                    try
                    {
                        await request.SendWebRequest();

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log($"[CameraManager] Request successful! Response: {request.downloadHandler.text}");
                            return request.downloadHandler.text;
                        }
                        else
                        {
                            Debug.LogError($"[CameraManager] Request failed! Status: {request.responseCode}, Error: {request.error}");
                            Debug.LogError($"[CameraManager] Response: {request.downloadHandler?.text ?? "No response"}");
                            return $"{{\"success\": false, \"message\": \"Failed to upload photo, status code: {request.responseCode}, error: {request.error}\"}}";
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[CameraManager] Exception during request: {e}");
                        return $"{{\"success\": false, \"message\": \"Request exception: {e.Message}\"}}";
                    }
                }
            }
        }

        private Texture2D ResizeTexture(Texture2D source, int w, int h)
        {
            RenderTexture rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D result = new Texture2D(w, h);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(source);
            return result;
        }

        private void OnDestroy()
        {
            Debug.Log("[CameraManager] OnDestroy called - releasing camera");
            ReleaseCamera();
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[CameraManager] OnApplicationQuit called - releasing camera");
            ReleaseCamera();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Debug.Log("[CameraManager] Application paused - releasing camera");
                ReleaseCamera();
            }
        }

        private void ReleaseCamera()
        {
            ReleaseWebCamTexture();
        }
    }
}

