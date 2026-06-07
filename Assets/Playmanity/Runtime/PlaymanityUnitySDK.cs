// Playmanity Unity SDK
// Drop this file into Assets/Playmanity/Runtime/PlaymanityUnitySDK.cs
//
// This file contains the low-level Playmanity bridge client:
// - Live mode: signed JSON messages over the Playmanity Windows named pipe.
// - Editor mock mode: local fake responses so you can test in Unity Play Mode
//   without launching the game from the desktop client.
//
// Requirements for live mode:
// - Windows Editor or Windows Standalone player.
// - Player launched by the Playmanity desktop client so PLAYMANITY_SESSION exists.
// - Unity API Compatibility Level with System.IO.Pipes support.

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define PLAYMANITY_WINDOWS
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if PLAYMANITY_WINDOWS
using System.IO.Pipes;
#endif
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Playmanity.Unity
{
    public enum PlaymanityClientMode
    {
        Auto,
        LivePipe,
        Mock
    }

    [Serializable]
    public sealed class PlaymanityClientOptions
    {
        [Tooltip("In Unity Editor, use mock responses when PLAYMANITY_SESSION is missing.")]
        public bool useMockInEditor = true;

        [Tooltip("Always use mock responses. Useful for UI and ad-flow testing.")]
        public bool forceMock = false;

        [Tooltip("Artificial mock response delay in milliseconds.")]
        public int mockDelayMs = 150;

        [Tooltip("Mock ad image URL. Keep mock://playmanity-ad to use the generated test image.")]
        public string mockAdMediaUrl = "mock://playmanity-ad";

        public string mockUserId = "u_editor_test";
        public string mockUsername = "Unity Editor Tester";
        public string mockEmail = "editor-test@playmanity.local";
    }

    /// <summary>
    /// Simple Unity client for the Playmanity desktop bridge.
    /// It uses the same protocol as the Python tester:
    /// newline-delimited JSON over \\.\pipe\playmanity-auth,
    /// HMAC-SHA256 signed envelopes, millisecond timestamps, and UUID message IDs.
    /// </summary>
    public sealed class PlaymanityClient : IDisposable
    {
        public const string DefaultPipeName = "playmanity-auth";
        public const int DefaultTimeoutMs = 5000;
        public const int ProtocolVersion = 1;

        private static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly string _gameId;
        private readonly string _secret;
        private readonly string _pipeName;
        private readonly PlaymanityClientOptions _options;
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

        private readonly HashSet<string> _mockSeenImpressionMessageIds = new HashSet<string>();
        private readonly HashSet<string> _mockValidNonces = new HashSet<string>();
        private string _mockLastAdId;

#if PLAYMANITY_WINDOWS
        private NamedPipeClientStream _pipe;
#endif
        private StreamReader _reader;
        private StreamWriter _writer;
        private bool _disposed;

        private static readonly Regex SignatureAtEnd =
            new Regex(",\"signature\":\"(?<sig>[0-9a-f]{64})\"\\s*\\}\\s*$");

        public bool IsConnected
        {
            get
            {
#if PLAYMANITY_WINDOWS
                return _pipe != null && _pipe.IsConnected;
#else
                return false;
#endif
            }
        }

        public bool IsMockMode
        {
            get { return ShouldUseMock(); }
        }

        public PlaymanityClientMode ActiveMode
        {
            get { return ShouldUseMock() ? PlaymanityClientMode.Mock : PlaymanityClientMode.LivePipe; }
        }

        public string GameId
        {
            get { return _gameId; }
        }

        public PlaymanityClient(
            string gameId,
            string sessionSecret = null,
            string pipeName = DefaultPipeName,
            PlaymanityClientOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(gameId))
                throw new ArgumentException("gameId is required.", "gameId");

            _gameId = gameId;
            _secret = string.IsNullOrEmpty(sessionSecret)
                ? Environment.GetEnvironmentVariable("PLAYMANITY_SESSION")
                : sessionSecret;
            _pipeName = string.IsNullOrEmpty(pipeName) ? DefaultPipeName : pipeName;
            _options = options ?? new PlaymanityClientOptions();
        }

        /// <summary>
        /// Connects to the Playmanity desktop named pipe. Usually you can skip this;
        /// API methods call it lazily. In mock mode this is a no-op.
        /// </summary>
        public async Task ConnectAsync(
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            if (ShouldUseMock()) return;

#if !PLAYMANITY_WINDOWS
            throw new PlatformNotSupportedException(
                "Playmanity named-pipe SDK currently supports Windows Editor/Standalone only.");
#else
            if (IsConnected) return;

            if (string.IsNullOrEmpty(_secret))
            {
                throw new InvalidOperationException(
                    "PLAYMANITY_SESSION is missing. Launch the game through the Playmanity desktop client, or enable editor mock mode.");
            }

            _pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await Task.Run(delegate { _pipe.Connect(timeoutMs); }, cancellationToken);

            Encoding utf8NoBom = new UTF8Encoding(false);
            _reader = new StreamReader(_pipe, utf8NoBom, false, 4096, true);
            _writer = new StreamWriter(_pipe, utf8NoBom, 4096, true)
            {
                AutoFlush = true,
                NewLine = "\n"
            };
#endif
        }

        public Task<PlaymanityResponse> PingAsync(
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendPayloadAsync("{\"action\":\"ping\"}", timeoutMs, cancellationToken);
        }

        public Task<PlaymanityResponse> GetUserAsync(
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendPayloadAsync("{\"action\":\"getUser\"}", timeoutMs, cancellationToken);
        }

        public Task<PlaymanityResponse> RequestAdAsync(
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendPayloadAsync("{\"action\":\"requestAd\"}", timeoutMs, cancellationToken);
        }

        public Task<PlaymanityResponse> ReportAdImpressionAsync(
            string adId,
            int exposureMs,
            double viewportRatio,
            string messageId = null,
            long? timestampMs = null,
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(adId))
                throw new ArgumentException("adId is required.", "adId");

            messageId = string.IsNullOrEmpty(messageId) ? NewMessageId() : messageId;
            long timestamp = timestampMs ?? NowMs();

            string payload =
                "{\"action\":\"reportAdImpression\"" +
                ",\"adId\":" + JsonString(adId) +
                ",\"messageId\":" + JsonString(messageId) +
                ",\"exposureMs\":" + exposureMs.ToString(CultureInfo.InvariantCulture) +
                ",\"viewportRatio\":" + JsonNumber(viewportRatio) +
                ",\"timestamp\":" + timestamp.ToString(CultureInfo.InvariantCulture) +
                "}";

            return SendPayloadAsync(payload, timeoutMs, cancellationToken);
        }

        public Task<PlaymanityResponse> RequestClickIntentAsync(
            string adId,
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(adId))
                throw new ArgumentException("adId is required.", "adId");

            string payload =
                "{\"action\":\"requestClickIntent\"" +
                ",\"adId\":" + JsonString(adId) +
                "}";

            return SendPayloadAsync(payload, timeoutMs, cancellationToken);
        }

        public Task<PlaymanityResponse> ReportAdClickAsync(
            string adId,
            string nonce,
            int timeoutMs = DefaultTimeoutMs,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(adId))
                throw new ArgumentException("adId is required.", "adId");
            if (string.IsNullOrWhiteSpace(nonce))
                throw new ArgumentException("nonce is required.", "nonce");

            string payload =
                "{\"action\":\"reportAdClick\"" +
                ",\"adId\":" + JsonString(adId) +
                ",\"nonce\":" + JsonString(nonce) +
                "}";

            return SendPayloadAsync(payload, timeoutMs, cancellationToken);
        }

        /// <summary>
        /// Runs the same offline HMAC known-answer check used by the Python tester.
        /// Useful for diagnosing crypto/platform issues.
        /// </summary>
        public static bool HmacSelfTest()
        {
            const string expected =
                "88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b";
            return TimingSafeEquals(HmacSha256Hex("hello", "secret"), expected);
        }

        public static string NewMessageId()
        {
            return Guid.NewGuid().ToString();
        }

        public static long NowMs()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
        }

        private bool ShouldUseMock()
        {
            if (_options.forceMock) return true;

#if UNITY_EDITOR
            if (_options.useMockInEditor && string.IsNullOrEmpty(_secret))
                return true;
#endif

            return false;
        }

        private async Task<PlaymanityResponse> SendPayloadAsync(
            string payloadJson,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (ShouldUseMock())
                return await SendMockPayloadAsync(payloadJson, cancellationToken);

            await ConnectAsync(timeoutMs, cancellationToken);

            await _ioLock.WaitAsync(cancellationToken);
            try
            {
                string requestJson = BuildSignedEnvelope(payloadJson);

                await _writer.WriteLineAsync(requestJson);

                string responseJson = await ReadLineWithTimeoutAsync(timeoutMs, cancellationToken);
                if (string.IsNullOrEmpty(responseJson))
                    throw new IOException("Playmanity bridge closed the pipe.");

                if (!VerifySignedEnvelope(responseJson))
                    throw new PlaymanityException("Response HMAC is invalid.", responseJson);

                PlaymanityEnvelope envelope = JsonUtility.FromJson<PlaymanityEnvelope>(responseJson);
                if (envelope == null || envelope.payload == null)
                    throw new PlaymanityException("Invalid Playmanity response JSON.", responseJson);

                envelope.payload.rawEnvelopeJson = responseJson;
                return envelope.payload;
            }
            finally
            {
                _ioLock.Release();
            }
        }

        private async Task<PlaymanityResponse> SendMockPayloadAsync(
            string payloadJson,
            CancellationToken cancellationToken)
        {
            if (_options.mockDelayMs > 0)
                await Task.Delay(_options.mockDelayMs, cancellationToken);

            PlaymanityPayloadProbe payload = JsonUtility.FromJson<PlaymanityPayloadProbe>(payloadJson);
            if (payload == null || string.IsNullOrEmpty(payload.action))
                return Error("Invalid mock payload", 400);

            switch (payload.action)
            {
                case "ping":
                    return new PlaymanityResponse { success = true };

                case "getUser":
                    return new PlaymanityResponse
                    {
                        success = true,
                        user = new PlaymanityUser
                        {
                            id = _options.mockUserId,
                            username = _options.mockUsername,
                            email = _options.mockEmail,
                            avatarColor = "#8A5CFF",
                            createdAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                            completedOnboarding = true,
                            memberships = new PlaymanityMembership[0]
                        }
                    };

                case "requestAd":
                    _mockLastAdId = "ad_editor_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                    return new PlaymanityResponse
                    {
                        success = true,
                        adId = _mockLastAdId,
                        title = "Playmanity Editor Test Ad",
                        description = "This is a local mock ad so you can test the Unity ad UI in Play Mode.",
                        mediaUrl = string.IsNullOrEmpty(_options.mockAdMediaUrl)
                            ? "mock://playmanity-ad"
                            : _options.mockAdMediaUrl,
                        clickUrl = "https://playmanity.local/editor-test-ad"
                    };

                case "reportAdImpression":
                    if (string.IsNullOrEmpty(payload.adId))
                        return Error("Missing adId", 400);
                    if (!string.IsNullOrEmpty(_mockLastAdId) && payload.adId != _mockLastAdId)
                        return Error("Mock rejected impression for ad that was not last served", 401);
                    if (!string.IsNullOrEmpty(payload.messageId) && _mockSeenImpressionMessageIds.Contains(payload.messageId))
                        return Error("Duplicate messageId", 409);

                    if (!string.IsNullOrEmpty(payload.messageId))
                        _mockSeenImpressionMessageIds.Add(payload.messageId);

                    return new PlaymanityResponse { success = true };

                case "requestClickIntent":
                    if (string.IsNullOrEmpty(payload.adId))
                        return Error("Missing adId", 400);
                    string nonce = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                    _mockValidNonces.Add(nonce);
                    return new PlaymanityResponse { success = true, nonce = nonce };

                case "reportAdClick":
                    if (string.IsNullOrEmpty(payload.nonce))
                        return Error("Missing nonce", 400);
                    if (!_mockValidNonces.Contains(payload.nonce))
                        return Error("Invalid or consumed nonce", 401);

                    _mockValidNonces.Remove(payload.nonce);
                    return new PlaymanityResponse { success = true };

                default:
                    return Error("Unknown action", 400);
            }
        }

        private static PlaymanityResponse Error(string message, int code)
        {
            return new PlaymanityResponse
            {
                success = false,
                error = message,
                errorCode = code
            };
        }

        private async Task<string> ReadLineWithTimeoutAsync(
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            Task<string> readTask = _reader.ReadLineAsync();
            Task delayTask = Task.Delay(timeoutMs, cancellationToken);

            Task completed = await Task.WhenAny(readTask, delayTask);
            if (completed != readTask)
                throw new TimeoutException("Timed out waiting for Playmanity bridge response.");

            return await readTask;
        }

        private string BuildSignedEnvelope(string payloadJson)
        {
            string id = NewMessageId();
            long timestamp = NowMs();

            string unsignedJson =
                "{\"gameId\":" + JsonString(_gameId) +
                ",\"id\":" + JsonString(id) +
                ",\"payload\":" + payloadJson +
                ",\"timestamp\":" + timestamp.ToString(CultureInfo.InvariantCulture) +
                ",\"version\":" + ProtocolVersion.ToString(CultureInfo.InvariantCulture) +
                "}";

            string signature = HmacSha256Hex(unsignedJson, _secret);

            return unsignedJson.Substring(0, unsignedJson.Length - 1) +
                   ",\"signature\":\"" + signature + "\"}";
        }

        private bool VerifySignedEnvelope(string rawJson)
        {
            Match match = SignatureAtEnd.Match(rawJson);
            if (!match.Success) return false;

            string signature = match.Groups["sig"].Value;
            string unsignedJson = rawJson.Substring(0, match.Index) + "}";

            string expected = HmacSha256Hex(unsignedJson, _secret);
            return TimingSafeEquals(signature, expected);
        }

        private static string HmacSha256Hex(string data, string key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool TimingSafeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];

            return diff == 0;
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";

            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('"');

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        private static string JsonNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException("value", "JSON numbers cannot be NaN or Infinity.");

            return value.ToString("0.################", CultureInfo.InvariantCulture);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("PlaymanityClient");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { if (_writer != null) _writer.Dispose(); } catch { }
            try { if (_reader != null) _reader.Dispose(); } catch { }
#if PLAYMANITY_WINDOWS
            try { if (_pipe != null) _pipe.Dispose(); } catch { }
#endif
            _ioLock.Dispose();
        }
    }

    [Serializable]
    internal sealed class PlaymanityEnvelope
    {
        public string gameId;
        public string id;
        public PlaymanityResponse payload;
        public long timestamp;
        public int version;
        public string signature;
    }

    [Serializable]
    internal sealed class PlaymanityPayloadProbe
    {
        public string action;
        public string adId;
        public string messageId;
        public int exposureMs;
        public float viewportRatio;
        public long timestamp;
        public string nonce;
    }

    [Serializable]
    public sealed class PlaymanityResponse
    {
        // Common result fields
        public bool success;
        public string error;
        public int errorCode;
        public int retryAfterMs;

        // getUser
        public PlaymanityUser user;

        // requestAd
        public string adId;
        public string title;
        public string description;
        public string mediaUrl;
        public string clickUrl;

        // requestClickIntent
        public string nonce;

        // Debugging / advanced parsing
        [NonSerialized] public string rawEnvelopeJson;

        public bool IsOk
        {
            get { return success; }
        }

        public bool HasError
        {
            get { return !success; }
        }
    }

    [Serializable]
    public sealed class PlaymanityUser
    {
        public string id;
        public string username;
        public string email;
        public string avatarColor;
        public string createdAt;
        public bool completedOnboarding;
        public PlaymanityMembership[] memberships;
    }

    [Serializable]
    public sealed class PlaymanityMembership
    {
        public string id;
        public string role;
        public string communityId;
        public string communitySlug;
        public string communityName;
    }

    public sealed class PlaymanityException : Exception
    {
        public string RawJson { get; private set; }

        public PlaymanityException(string message, string rawJson = null) : base(message)
        {
            RawJson = rawJson;
        }
    }
}
