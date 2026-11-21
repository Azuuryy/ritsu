// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.LocalPriority
{
    /// <summary>
    /// The part of the API provider that local profiles drive: which local profile is shown as the player.
    /// In Ritsu this is implemented by <see cref="Offline.OfflineAPIProvider"/>, which never goes online.
    /// </summary>
    public interface ILocalProfileApi
    {
        /// <summary>
        /// The active local profile's user, or null when no local profile is active.
        /// </summary>
        APIUser? ActiveOfflineUser { get; }

        /// <summary>
        /// Invoked when the player asked for the online account, so the profile list can follow.
        /// </summary>
        event Action? OnlineAccountRequested;

        /// <summary>
        /// Show a local profile as the player (scores are saved under it), or none by passing null.
        /// </summary>
        void SetOfflineProfile(APIUser? localUser);

        /// <summary>
        /// Whether local profiles may use a signed-in online account for read-only lookups.
        /// </summary>
        void SetLocalProfilesUseOnlineAccount(bool value);
    }
}
