using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Telegram 봇 API 연동 유틸리티.
/// 게임 내 중요 이벤트(오류, QA 실패, 빌드 상태 등)를 텔레그램 채널/그룹에 전송합니다.
/// 환경 변수 TELEGRAM_BOT_TOKEN 및 TELEGRAM_CHAT_ID에서 보안 정보를 로드합니다.
/// </summary>
public static class TelegramNotifier
{
    /// <summary>Bot Token (env: TELEGRAM_BOT_TOKEN)</summary>
    private static string BotToken => Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";

    /// <summary>Target Chat ID (env: TELEGRAM_CHAT_ID)</summary>
    private static string ChatId => Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "";

    /// <summary>
    /// 텔레그램 메시지를 전송합니다 (fire-and-forget).
    /// </summary>
    /// <param name="message">전송할 텍스트</param>
    public static void SendMessage(string message)
    {
        if (string.IsNullOrEmpty(BotToken) || string.IsNullOrEmpty(ChatId))
        {
            Debug.LogWarning("[TelegramNotifier] Token 또는 Chat ID가 설정되지 않았습니다. (env: TELEGRAM_BOT_TOKEN, TELEGRAM_CHAT_ID)");
            return;
        }
        CoroutineRunner.Instance.StartCoroutine(SendMessageRoutine(message));
    }

    /// <summary>
    /// 텔레그램 메시지를 전송하는 내부 코루틴.
    /// </summary>
    private static IEnumerator SendMessageRoutine(string message)
    {
        string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
        var req = new UnityWebRequest(url, "POST");
        byte[] body = Encoding.UTF8.GetBytes($"chat_id={ChatId}&text={Uri.EscapeDataString(message)}");
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[TelegramNotifier] 전송 실패: {req.error}");
        }
        else
        {
            Debug.Log("[TelegramNotifier] 메시지 전송 성공");
        }
    }

    /// <summary>
    /// 코루틴 실행을 위한 지속적인 GameObject 러너.
    /// </summary>
    private class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("TelegramNotifierRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }
    }
}
