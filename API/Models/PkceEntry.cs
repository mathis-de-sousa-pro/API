namespace Api.Models
{
    /// <summary>
    /// Represents a PKCE entry for OAuth authentication.
    /// </summary>
    public class PkceEntry
    {
        private string _state;
        private string _codeVerifier;
        private string _codeChallenge;
        private DateTime _expiresAt;

        /// <summary>
        /// Initializes a new instance of the <see cref="PkceEntry"/> class.
        /// </summary>
        /// <param name="state">The PKCE state value.</param>
        /// <param name="codeVerifier">The code verifier.</param>
        /// <param name="codeChallenge">The code challenge.</param>
        /// <param name="expiresAt">The UTC expiration date and time.</param>
        public PkceEntry(string state, string codeVerifier, string codeChallenge, DateTime expiresAt)
        {
            State = state;
            CodeVerifier = codeVerifier;
            CodeChallenge = codeChallenge;
            ExpiresAt = expiresAt;
        }

        /// <summary>
        /// Gets or sets the PKCE state value.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if value is null or empty.</exception>
        public string State
        {
            get { return _state; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("State cannot be null or empty.", nameof(value));
                }
                _state = value;
            }
        }

        /// <summary>
        /// Gets or sets the code verifier.
        /// </summary>
        public string CodeVerifier
        {
            get { return _codeVerifier; }
            set { _codeVerifier = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the code challenge.
        /// </summary>
        public string CodeChallenge
        {
            get { return _codeChallenge; }
            set { _codeChallenge = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the UTC expiration date and time.
        /// </summary>
        public DateTime ExpiresAt
        {
            get { return _expiresAt; }
            set { _expiresAt = value; }
        }

        /// <summary>
        /// Determines whether the PKCE entry is expired.
        /// </summary>
        /// <param name="nowUtc">The current UTC date and time.</param>
        /// <returns>True if expired; otherwise, false.</returns>
        public bool IsExpired(DateTime nowUtc)
        {
            return nowUtc >= _expiresAt;
        }
    }
}