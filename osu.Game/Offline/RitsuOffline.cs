// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;

namespace osu.Game.Offline
{
    /// <summary>The capabilities of this local-only distribution. Not a user preference.</summary>
    public static class RitsuOffline
    {
        public static bool Enabled => true;
        public const string StorageName = "Ritsu";
        public const string PipeName = "ritsu-offline";
    }

    public sealed class OfflineOperationException : InvalidOperationException
    {
        public OfflineOperationException()
            : base("This Ritsu build operates entirely offline. Online services are unavailable.")
        {
        }
    }
}
