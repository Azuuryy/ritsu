// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;

namespace osu.Game.Offline
{
    public static class RitsuOffline
    {
        public static bool Enabled => true;
        public const string StorageName = "Ritsu";
        public const string PipeName = "ritsu-offline";
    }

    public sealed class OfflineOperationException : InvalidOperationException
    {
        public OfflineOperationException()
            : base("This online service is unavailable in Ritsu.")
        {
        }
    }
}
