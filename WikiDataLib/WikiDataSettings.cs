using System;
using System.Net.Http;

namespace WikiDataLib
{
    /// <summary>
    /// Process-wide settings for requests to Wikimedia. Call once at startup, before the first request.
    /// </summary>
    public static class WikiDataSettings
    {
        /// <summary>
        /// Identifies your application to Wikimedia, as its User-Agent policy asks
        /// (https://meta.wikimedia.org/wiki/User-Agent_policy), e.g. <c>"MyApp/1.0 (https://example.com; contact@example.com)"</c>.
        /// It is sent before the library's own product token. Unidentified clients get the lowest rate limits.
        /// Pass null to send only the library's User-Agent.
        /// </summary>
        /// <exception cref="ArgumentException">The value is not a valid User-Agent header value.</exception>
        public static void SetUserAgent(string? applicationUserAgent)
        {
            if (string.IsNullOrWhiteSpace(applicationUserAgent))
            {
                WikiHttp.ApplicationUserAgent = null;
                return;
            }

            var value = applicationUserAgent!.Trim();
            using (var probe = new HttpRequestMessage())
            {
                if (!probe.Headers.UserAgent.TryParseAdd(value))
                {
                    throw new ArgumentException("Not a valid User-Agent header value.", nameof(applicationUserAgent));
                }
            }

            WikiHttp.ApplicationUserAgent = value;
        }

        /// <summary>
        /// Optional Wikimedia OAuth 2.0 access token (for example an owner-only consumer token), sent as
        /// <c>Authorization: Bearer</c> on every request. Authenticated clients can get higher rate limits.
        /// Pass null to stop sending it.
        /// </summary>
        public static void SetAccessToken(string? accessToken)
        {
            WikiHttp.AccessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken!.Trim();
        }
    }
}
