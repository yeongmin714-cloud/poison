using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectName.Core
{
    /// <summary>
    /// Telegram Bot 알림 유틸리티
    /// Resources/TelegramConfig.json에서 설정 로드
    /// </summary>
    public static class TelegramNotifier
    {
        private class Config
        {
            public string botToken;
            public string chatId;
            public bool enabled = true;
        }

        private static Config _config;
        private static bool _configLoaded;
        private const string ConfigPath = "TelegramConfig";

        /// <summary>
        /// 설정 로드 (Resources/TelegramConfig.json)
        /// </summary>
        private static void LoadConfig()
        {
            if (_configLoaded) return;

            var textAsset = Resources.Load<TextAsset>(ConfigPath);
            if (textAsset != null)
            {
                try
                {
                    _config = JsonUtility.FromJson<Config>(textAsset.text);
                    _configLoaded = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TelegramNotifier] Config parse error: {e.Message}");
                    _config = new Config();
                }
            }
            else
            {
                _config = new Config();
                Debug.LogWarning($"[TelegramNotifier] Config not found at Resources/{ConfigPath}.json");
            }
        }

        /// <summary>
        /// 텔레그램 메시지 전송 (비동기)
        /// </summary>
        /// <param name="message">전송할 메시지 (Markdown 지원)</param>
        /// <param name="onComplete">완료 콜백 (success, errorMessage)</param>
        public static void SendMessage(string message, Action<bool, string> onComplete = null)
        {
            LoadConfig();

            if (_config == null || !_config.enabled)
            {
                onComplete?.Invoke(false, "Telegram notifier disabled or config missing");
                return;
            }

            if (string.IsNullOrEmpty(_config.botToken) || _config.botToken == "YOUR_BOT_TOKEN_HERE")
            {
                onComplete?.Invoke(false, "Bot token not configured");
                return;
            }

            if (string.IsNullOrEmpty(_config.chatId))
            {
                onComplete?.Invoke(false, "Chat ID not configured");
                return;
            }

            CoroutineRunner.Instance.StartCoroutine(SendMessageRoutine(message, onComplete));
        }

        /// <summary>
        /// 동기 전송 (에디터/빌드 후 알림용)
        /// </summary>
        public static bool SendMessageSync(string message, out string error)
        {
            LoadConfig();
            error = "";

            if (_config == null || !_config.enabled || string.IsNullOrEmpty(_config.botToken) || _config.botToken == "YOUR_BOT_TOKEN_HERE")
            {
                error = "Not configured";
                return false;
            }

            string url = $"https://api.telegram.org/bot{_config.botToken}/sendMessage";
            string payload = $"{{\"chat_id\":\"{_config.chatId}\",\"text\":\"{EscapeJson(message)}\",\"parse_mode\":\"Markdown\"}}";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var asyncOp = request.SendWebRequest();
                while (!asyncOp.isDone) { }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return true;
                }
                else
                {
                    error = request.error;
                    return false;
                }
            }
        }

        private static IEnumerator SendMessageRoutine(string message, Action<bool, string> onComplete)
        {
            string url = $"https://api.telegram.org/bot{_config.botToken}/sendMessage";
            string payload = $"{{\"chat_id\":\"{_config.chatId}\",\"text\":\"{EscapeJson(message)}\",\"parse_mode\":\"Markdown\"}}";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(true, null);
                }
                else
                {
                    onComplete?.Invoke(false, request.error);
                }
            }
        }

        private static string EscapeJson(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }

    /// <summary>
    /// 정적 클래스에서 코루틴 실행용 MonoBehaviour 헬퍼
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[TelegramNotifier_Runner]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }
    }
}