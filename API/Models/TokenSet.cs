namespace Api.Models
{
    /// <summary>
    /// Represents a set of OAuth tokens for a user and provider.
    /// </summary>
    public class TokenSet
    {
        private long _tokenSetId;
        private string _provider;
        private string _providerUserId;
        private string _refreshToken;
        private string _scope;
        private DateTime _accessExpiresAt;
        private DateTime _updatedAt;
        private string _sessionId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenSet"/> class.
        /// </summary>
        /// <param name="tokenSetId">The unique token set identifier.</param>
        /// <param name="provider">The OAuth provider name.</param>
        /// <param name="providerUserId">The user ID at the provider.</param>
        /// <param name="refreshToken">The encrypted refresh token.</param>
        /// <param name="scope">The OAuth scopes.</param>
        /// <param name="accessExpiresAt">The UTC access token expiration date and time.</param>
        /// <param name="updatedAt">The UTC date and time the token set was last updated.</param>
        /// <param name="sessionId">The associated session ID.</param>
        public TokenSet(long tokenSetId, string provider, string providerUserId, string refreshToken,
            string scope, DateTime accessExpiresAt, DateTime updatedAt, string sessionId)
        {
            TokenSetId = tokenSetId;
            Provider = provider;
            ProviderUserId = providerUserId;
            RefreshToken = refreshToken;
            Scope = scope;
            AccessExpiresAt = accessExpiresAt;
            UpdatedAt = updatedAt;
            SessionId = sessionId;
        }

        /// <summary>
        /// Gets or sets the unique token set identifier.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if value is not positive.</exception>
        public long TokenSetId
        {
            get => _tokenSetId;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("TokenSetId must be positive.", nameof(value));
                }

                _tokenSetId = value;
            }
        }

        /// <summary>
        /// Gets or sets the OAuth provider name.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if value is null or empty.</exception>
        public string Provider
        {
            get => _provider;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Provider cannot be null or empty.", nameof(value));
                }

                _provider = value;
            }
        }

        /// <summary>
        /// Gets or sets the user ID at the provider.
        /// </summary>
        public string ProviderUserId
        {
            get => _providerUserId;
            set => _providerUserId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the encrypted refresh token.
        /// </summary>
        public string RefreshToken
        {
            get => _refreshToken;
            set => _refreshToken = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the OAuth scopes.
        /// </summary>
        public string Scope
        {
            get => _scope;
            set => _scope = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the UTC access token expiration date and time.
        /// </summary>
        public DateTime AccessExpiresAt
        {
            get => _accessExpiresAt;
            set => _accessExpiresAt = value;
        }

        /// <summary>
        /// Gets or sets the UTC date and time the token set was last updated.
        /// </summary>
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => _updatedAt = value;
        }

        /// <summary>
        /// Gets or sets the associated session ID.
        /// </summary>
        public string SessionId
        {
            get => _sessionId;
            set => _sessionId = value ?? string.Empty;
        }
    }
}