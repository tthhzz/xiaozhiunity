using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XiaoZhi.Unity.Mcp
{
    /// <summary>
    /// MCP Server Implementation for Unity
    /// Reference: https://modelcontextprotocol.io/specification/2024-11-05
    /// </summary>
    public class McpServer
    {
        private static McpServer _instance;
        public static McpServer Instance => _instance ??= new McpServer();

        private Func<string, UniTask> _sendCallback;
        private readonly List<McpTool> _tools = new();

        public void SetSendCallback(Func<string, UniTask> callback)
        {
            _sendCallback = callback;
        }

        public void RegisterTools()
        {
            _tools.Clear();
            
            // Register take_photo tool
            _tools.Add(new McpTool(
                "take_photo",
                "【拍照识图】当用户提到：拍照、拍张照、照张相、看一下、看看、帮我看、这是什么、识别时调用。" +
                "功能：拍照并分析图片内容，回答用户关于图片的问题。" +
                "参数：question - 用户想了解的关于图片的问题",
                new Dictionary<string, McpPropertyInfo>
                {
                    { "question", new McpPropertyInfo { Type = "string", Description = "用户想了解的关于图片的问题" } }
                },
                new[] { "question" },
                TakePhotoAsync
            ));
            
            Debug.Log($"[McpServer] Registered {_tools.Count} tools");
        }

        private async UniTask<string> TakePhotoAsync(Dictionary<string, object> arguments)
        {
            var question = arguments.TryGetValue("question", out var q) ? q?.ToString() : "这是什么?";
            Debug.Log($"[McpServer] take_photo called with question: {question}");
            
            try
            {
                var result = await CameraManager.Instance.TakePhotoAndExplain(question);
                Debug.Log($"[McpServer] take_photo result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpServer] take_photo error: {e}");
                return JsonConvert.SerializeObject(new { success = false, message = e.Message });
            }
        }

        public async UniTask ParseMessage(JObject message)
        {
            try
            {
                // Check JSONRPC version
                if (message["jsonrpc"]?.ToString() != "2.0")
                {
                    Debug.LogError($"[McpServer] Invalid JSONRPC version: {message["jsonrpc"]}");
                    return;
                }

                var method = message["method"]?.ToString();
                if (string.IsNullOrEmpty(method))
                {
                    Debug.LogError("[McpServer] Missing method");
                    return;
                }

                // Ignore notifications
                if (method.StartsWith("notifications"))
                {
                    Debug.Log($"[McpServer] Ignoring notification: {method}");
                    return;
                }

                var id = message["id"];
                if (id == null)
                {
                    Debug.LogError($"[McpServer] Invalid id for method: {method}");
                    return;
                }

                var idValue = id.Value<int>();
                var paramsObj = message["params"] as JObject ?? new JObject();

                Debug.Log($"[McpServer] Processing method: {method}, ID: {idValue}");

                switch (method)
                {
                    case "initialize":
                        await HandleInitialize(idValue, paramsObj);
                        break;
                    case "tools/list":
                        await HandleToolsList(idValue, paramsObj);
                        break;
                    case "tools/call":
                        await HandleToolCall(idValue, paramsObj);
                        break;
                    default:
                        Debug.LogWarning($"[McpServer] Method not implemented: {method}");
                        await ReplyError(idValue, $"Method not implemented: {method}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpServer] Error parsing MCP message: {e}");
            }
        }

        private async UniTask HandleInitialize(int id, JObject parameters)
        {
            // Parse capabilities (vision config)
            var capabilities = parameters["capabilities"] as JObject;
            if (capabilities != null)
            {
                var vision = capabilities["vision"] as JObject;
                if (vision != null)
                {
                    var url = vision["url"]?.ToString();
                    var token = vision["token"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        CameraManager.Instance.SetExplainUrl(url);
                        if (!string.IsNullOrEmpty(token))
                        {
                            CameraManager.Instance.SetExplainToken(token);
                        }
                        Debug.Log($"[McpServer] Vision service configured: {url}");
                    }
                }
            }

            var result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { tools = new { } },
                serverInfo = new
                {
                    name = "xiaozhi-unity",
                    version = AppUtility.GetVersion()
                }
            };

            await ReplyResult(id, JObject.FromObject(result));
        }

        private async UniTask HandleToolsList(int id, JObject parameters)
        {
            var toolsJson = new JArray();
            foreach (var tool in _tools)
            {
                toolsJson.Add(tool.ToJson());
            }

            var result = new JObject
            {
                ["tools"] = toolsJson
            };

            await ReplyResult(id, result);
        }

        private async UniTask HandleToolCall(int id, JObject parameters)
        {
            var toolName = parameters["name"]?.ToString();
            if (string.IsNullOrEmpty(toolName))
            {
                await ReplyError(id, "Missing tool name");
                return;
            }

            Debug.Log($"[McpServer] Calling tool: {toolName}");

            McpTool tool = null;
            foreach (var t in _tools)
            {
                if (t.Name == toolName)
                {
                    tool = t;
                    break;
                }
            }

            if (tool == null)
            {
                await ReplyError(id, $"Unknown tool: {toolName}");
                return;
            }

            var arguments = parameters["arguments"] as JObject ?? new JObject();
            var argsDict = new Dictionary<string, object>();
            foreach (var prop in arguments.Properties())
            {
                argsDict[prop.Name] = prop.Value.ToObject<object>();
            }

            try
            {
                var resultText = await tool.CallAsync(argsDict);
                var result = new JObject
                {
                    ["content"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "text",
                            ["text"] = resultText
                        }
                    },
                    ["isError"] = false
                };
                await ReplyResult(id, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[McpServer] Tool {toolName} failed: {e}");
                await ReplyError(id, e.Message);
            }
        }

        private async UniTask ReplyResult(int id, JObject result)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            };

            Debug.Log($"[McpServer] Sending result for ID={id}");
            
            if (_sendCallback != null)
            {
                await _sendCallback(response.ToString(Formatting.None));
            }
            else
            {
                Debug.LogError("[McpServer] Send callback not set!");
            }
        }

        private async UniTask ReplyError(int id, string message)
        {
            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["message"] = message
                }
            };

            Debug.LogError($"[McpServer] Sending error for ID={id}: {message}");
            
            if (_sendCallback != null)
            {
                await _sendCallback(response.ToString(Formatting.None));
            }
        }
    }

    public class McpPropertyInfo
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public object Default { get; set; }
    }

    public class McpTool
    {
        public string Name { get; }
        public string Description { get; }
        private readonly Dictionary<string, McpPropertyInfo> _properties;
        private readonly string[] _required;
        private readonly Func<Dictionary<string, object>, UniTask<string>> _callback;

        public McpTool(
            string name,
            string description,
            Dictionary<string, McpPropertyInfo> properties,
            string[] required,
            Func<Dictionary<string, object>, UniTask<string>> callback)
        {
            Name = name;
            Description = description;
            _properties = properties;
            _required = required;
            _callback = callback;
        }

        public JObject ToJson()
        {
            var propsJson = new JObject();
            foreach (var prop in _properties)
            {
                var propObj = new JObject { ["type"] = prop.Value.Type };
                if (!string.IsNullOrEmpty(prop.Value.Description))
                {
                    propObj["description"] = prop.Value.Description;
                }
                propsJson[prop.Key] = propObj;
            }

            return new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["inputSchema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = propsJson,
                    ["required"] = new JArray(_required)
                }
            };
        }

        public async UniTask<string> CallAsync(Dictionary<string, object> arguments)
        {
            return await _callback(arguments);
        }
    }
}
