using System;
using System.Text;

namespace UZS.Core
{
    /// <summary>
    /// Data a client sends when connecting. Only identity/cosmetic fields - never points, weapons or prices.
    /// Encoded as newline-separated UTF-8 so it needs no serializer.
    /// </summary>
    public sealed class ConnectionPayload
    {
        public const int MaxBytes = 512;
        public const int MaxNameLength = 20;
        public const int MaxKeyLength = 64;

        public string Version = GameInfo.ProtocolVersion;
        /// <summary>Stable identity used to restore a player's state if they reconnect during the same match.</summary>
        public string PlayerKey = string.Empty;
        public string DisplayName = "Survivor";
        public int CharacterId;

        public byte[] Encode()
        {
            var text = string.Join("\n", Clean(Version, 16), Clean(PlayerKey, MaxKeyLength), SanitizeName(DisplayName),
                CharacterCatalog.Clamp(CharacterId).ToString());
            return Encoding.UTF8.GetBytes(text);
        }

        public static bool TryDecode(byte[] bytes, out ConnectionPayload payload)
        {
            payload = null;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes) return false;
            string text;
            try { text = Encoding.UTF8.GetString(bytes); }
            catch (ArgumentException) { return false; }

            var parts = text.Split('\n');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[3], out int character)) return false;

            payload = new ConnectionPayload
            {
                Version = Clean(parts[0], 16),
                PlayerKey = Clean(parts[1], MaxKeyLength),
                DisplayName = SanitizeName(parts[2]),
                CharacterId = CharacterCatalog.Clamp(character),
            };
            return payload.PlayerKey.Length > 0;
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Survivor";
            var sb = new StringBuilder(MaxNameLength);
            foreach (char c in name)
            {
                if (sb.Length >= MaxNameLength) break;
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.') sb.Append(c);
            }
            var result = sb.ToString().Trim();
            return result.Length == 0 ? "Survivor" : result;
        }

        private static string Clean(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(Math.Min(s.Length, max));
            foreach (char c in s)
            {
                if (sb.Length >= max) break;
                if (c >= 33 && c <= 126) sb.Append(c);
            }
            return sb.ToString();
        }
    }

    public enum ApprovalResult
    {
        Approved,
        VersionMismatch,
        SessionFull,
        MatchOver,
        SinglePlayerOnly,
        BadPayload,
    }

    public static class ApprovalRules
    {
        public static ApprovalResult Evaluate(ConnectionPayload payload, bool payloadValid, bool isLocalHost, bool singlePlayer,
            int connectedPlayers, MatchPhase phase)
        {
            if (!payloadValid || payload == null) return ApprovalResult.BadPayload;
            if (isLocalHost) return ApprovalResult.Approved;
            if (singlePlayer) return ApprovalResult.SinglePlayerOnly;
            if (payload.Version != GameInfo.ProtocolVersion) return ApprovalResult.VersionMismatch;
            if (connectedPlayers >= GameInfo.MaxPlayers) return ApprovalResult.SessionFull;
            if (phase == MatchPhase.Victory || phase == MatchPhase.Defeat) return ApprovalResult.MatchOver;
            return ApprovalResult.Approved;
        }

        public static string Describe(ApprovalResult r)
        {
            switch (r)
            {
                case ApprovalResult.VersionMismatch: return "The host is running a different game version.";
                case ApprovalResult.SessionFull: return "The session is full.";
                case ApprovalResult.MatchOver: return "That match has already ended.";
                case ApprovalResult.SinglePlayerOnly: return "That game is single-player.";
                case ApprovalResult.BadPayload: return "Connection data was invalid.";
                default: return string.Empty;
            }
        }
    }
}
