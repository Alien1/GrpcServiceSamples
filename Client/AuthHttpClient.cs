using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Client
{
    class AuthHttpClient
    {
        private readonly HttpClient _httpClient;

        public AuthHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> Authenticate(string clientId, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"generateJwtToken?clientId={HttpUtility.UrlEncode(clientId)}", UriKind.Relative),
                Method = HttpMethod.Get,
                Version = new Version(2, 0)
            };

            var tokenResponse = await _httpClient.SendAsync(request, cancellationToken);
            tokenResponse.EnsureSuccessStatusCode();

            var token = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            return token;
        }
    }
}
