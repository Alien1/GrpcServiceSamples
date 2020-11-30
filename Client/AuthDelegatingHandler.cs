using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Client
{
    class AuthDelegatingHandler : DelegatingHandler
    {
        private readonly ITokenStorage _tokenStorage;
        private readonly AuthHttpClient _httpClient;

        public AuthDelegatingHandler(ITokenStorage tokenStorage, AuthHttpClient httpClient)
        {
            _tokenStorage = tokenStorage;
            _httpClient = httpClient;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_tokenStorage.Token != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.Token);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("Omg. Token probably expired");
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _tokenStorage.Token = await _httpClient.Authenticate(Environment.UserName, cancellationToken); //TODO DI client id accessor
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStorage.Token);
                    response = await base.SendAsync(request, cancellationToken);
                }
            }

            return response;
        }
    }
}
