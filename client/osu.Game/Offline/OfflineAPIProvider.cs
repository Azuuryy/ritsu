// Copyright (c) 2026 Ritsu contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;

namespace osu.Game.Offline
{
    /// <summary>Production offline provider, with no authentication or transport objects.</summary>
    public partial class OfflineAPIProvider : CompositeComponent, IAPIProvider
    {
        private readonly LocalUserState localUserState;
        private readonly Bindable<APIState> state = new Bindable<APIState>(APIState.Offline);

        public OfflineAPIProvider(OsuConfigManager config)
        {
            config.SetValue(OsuSetting.Token, string.Empty);
            localUserState = new LocalUserState(this, config);
            AddInternal(localUserState);
        }

        public IBindable<APIUser> LocalUser => localUserState.User;
        public void SetLocalProfile(string username) => localUserState.SetOfflineProfile(username);
        public ILocalUserState LocalUserState => localUserState;
        public string ScoreProcessingNoticeUrl => string.Empty;
        public Language Language => Language.en;
        public string AccessToken => string.Empty;
        public Guid SessionIdentifier { get; } = Guid.NewGuid();
        public bool IsLoggedIn => false;
        public string ProvidedUsername => string.Empty;
        public EndpointConfiguration Endpoints { get; } = new OfflineEndpointConfiguration();
        public int APIVersion => 20220705;
        public Exception? LastLoginError { get; private set; }
        public IBindable<APIState> State => state;
        public IBindable<string?> UserFacingOutageMessage { get; } = new Bindable<string?>();
        public SessionVerificationMethod? SessionVerificationMethod => null;
        public INotificationsClient NotificationsClient { get; } = new OfflineNotificationsClient();

        public void Queue(APIRequest request) => reject(request);
        public void Perform(APIRequest request) => reject(request);

        public Task PerformAsync(APIRequest request)
        {
            reject(request);
            // IAPIProvider reports request failure via APIRequest.Failure, including
            // callers that intentionally do not await this dispatch task.
            return Task.CompletedTask;
        }

        private void reject(APIRequest request)
        {
            request.AttachAPI(this);
            request.Fail(new OfflineOperationException());
        }

        public void Login(string username, string password) => LastLoginError = new OfflineOperationException();
        public void AuthenticateSecondFactor(string code) => LastLoginError = new OfflineOperationException();
        public void Logout() { }
        public void RefreshLocalUser() { }
        void IAPIProvider.Schedule(Action action) => Schedule(action);
        public IHubClientConnector? GetHubConnector(string clientName, string endpoint) => null;
        public IChatClient GetChatClient() => new OfflineChatClient();
        public RegistrationRequest.RegistrationRequestErrors? CreateAccount(string email, string username, string password) => throw new OfflineOperationException();

        private sealed class OfflineNotificationsClient : INotificationsClient
        {
            public IBindable<bool> IsConnected { get; } = new BindableBool();
            public event Action<SocketMessage>? MessageReceived { add { } remove { } }
            public Task SendAsync(SocketMessage message, CancellationToken? cancellationToken = null) => Task.FromException(new OfflineOperationException());
        }

        private sealed class OfflineChatClient : IChatClient
        {
            public event Action<Channel>? ChannelJoined { add { } remove { } }
            public event Action<Channel>? ChannelParted { add { } remove { } }
            public event Action<List<Message>>? NewMessages { add { } remove { } }
            public event Action? PresenceReceived;
            public void RequestPresence() => PresenceReceived?.Invoke();
            public void Dispose() => PresenceReceived = null;
        }
    }

    public sealed class OfflineEndpointConfiguration : EndpointConfiguration
    {
        public OfflineEndpointConfiguration()
        {
            APIUrl = WebsiteUrl = SpectatorUrl = MultiplayerUrl = MetadataUrl = BeatmapSubmissionServiceUrl = "https://offline.invalid";
            APIClientID = APIClientSecret = string.Empty;
        }
    }
}
