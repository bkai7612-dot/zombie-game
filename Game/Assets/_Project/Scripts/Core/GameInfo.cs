namespace UZS.Core
{
    /// <summary>
    /// Branding and compatibility constants. The display title is deliberately kept here (and in
    /// the editor bootstrapper's PlayerSettings step) so the game can be renamed without touching gameplay.
    /// </summary>
    public static class GameInfo
    {
        public const string DisplayTitle = "Untitled Zombie Survival";
        public const string CompanyName = "UZS Dev";
        public const string ExecutableName = "UntitledZombieSurvival";

        /// <summary>Bumped whenever the network protocol or rules change. Clients with a different value are rejected.</summary>
        public const string ProtocolVersion = "0.1.0";

        public const int MaxPlayers = 4;
        public const int StartingPoints = 500;
        public const ushort DefaultDirectPort = 7777;
    }
}
