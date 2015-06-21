using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Sync;
using OneDrive.Api;
using OneDrive.Configuration;

namespace OneDrive
{
    public class OneDriveCredentials
    {
        private readonly IConfigurationRetriever _configurationRetriever;
        private readonly ILiveAuthenticationApi _liveAuthenticationApi;
        private readonly ILogger _logger;
        private readonly string _syncAccountId;
        private readonly Token _accessToken;

        public OneDriveCredentials(IConfigurationRetriever configurationRetriever, ILiveAuthenticationApi liveAuthenticationApi, ILogger logger, SyncTarget target)
        {
            _configurationRetriever = configurationRetriever;
            _liveAuthenticationApi = liveAuthenticationApi;
            _logger = logger;
            _syncAccountId = target.Id;

            var syncAccount = configurationRetriever.GetSyncAccount(target.Id);
            _accessToken = syncAccount.AccessToken;
        }

        public async Task<string> GetAccessToken(CancellationToken cancellationToken)
        {
            // Give a buffer around the expiration time
            if (_accessToken.ExpiresAt <= DateTime.Now.AddMinutes(-11))
            {
                _logger.Debug("Access token expired at {0}, getting a new one", _accessToken.ExpiresAt);

                await RefreshToken(cancellationToken);
            }

            return _accessToken.AccessToken;
        }

        private async Task RefreshToken(CancellationToken cancellationToken)
        {
            var config = _configurationRetriever.GetGeneralConfiguration();
            var now = DateTime.Now;
            var refreshToken = await _liveAuthenticationApi.RefreshToken(_accessToken.RefresToken, Constants.OneDriveRedirectUrl, config.OneDriveClientId, config.OneDriveClientSecret, cancellationToken);

            var expiresAt = now.AddSeconds(refreshToken.expires_in);
            _logger.Debug("Refreshed access token, this one expires in {0} seconds, at {1}", refreshToken.expires_in, expiresAt);

            _accessToken.AccessToken = refreshToken.access_token;
            _accessToken.ExpiresAt = expiresAt;
            _accessToken.RefresToken = refreshToken.refresh_token;

            var syncAccount = _configurationRetriever.GetSyncAccount(_syncAccountId);
            syncAccount.AccessToken = _accessToken;

            _configurationRetriever.AddSyncAccount(syncAccount);
        }
    }
}
