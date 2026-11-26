// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
namespace osu.Game.LocalPriority
{
    /// <summary>
    /// Local profiles: whether the network may be used at all. (Which requests may be sent is decided by the online patch,
    /// in <see cref="Online.API.APIRequest"/>.)
    /// </summary>
    public static partial class LocalPriorityPolicy
    {
        /// <summary>
        /// The name of the local profiles feature (its settings tab).
        /// </summary>
        public const string GAME_NAME = @"Ritsu";

        /// <summary>
        /// Whether online resources (avatars, beatmap covers, audio previews, the beatmap metadata cache) may be fetched.
        /// </summary>
        public static bool AllowOnlineResources => NetworkAllowed;
    }
}
