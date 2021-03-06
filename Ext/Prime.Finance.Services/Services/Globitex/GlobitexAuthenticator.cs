﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Prime.Core;

namespace Prime.Finance.Services.Services.Globitex
{
    public class GlobitexAuthenticator : BaseAuthenticator
    {

        public GlobitexAuthenticator(ApiKey apiKey) : base(apiKey)
        {
        }

        private readonly List<string> _getEndpointsWithAccountParam = new List<string>()
        {
            "/trading/orders/active",
            "/trading/trades"
        };

        public override void RequestModify(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers;
            var nonce = (long)(DateTime.UtcNow.ToUnixTimeStamp() * 1000); // Milliseconds.
            var parameters = request.Content?.ReadAsStringAsync()?.Result;

            if (!string.IsNullOrWhiteSpace(request.RequestUri.Query))
            {
                parameters = request.RequestUri.Query.TrimStart('?');
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/trading/new_order"))
            {
                parameters = string.IsNullOrWhiteSpace(parameters) ? $"account={ApiKey.Extra}" : $"{parameters}&account={ApiKey.Extra}";
                request.Content = new StringContent(parameters, Encoding.UTF8, "application/x-www-form-urlencoded");
            }
            else if (_getEndpointsWithAccountParam.Exists(x => request.RequestUri.AbsolutePath.EndsWith(x)))
            {
                parameters = string.IsNullOrWhiteSpace(parameters) ? $"account={ApiKey.Extra}" : $"account={ApiKey.Extra}&{parameters}";
                request.RequestUri = new Uri($"https://{request.RequestUri.Host}{request.RequestUri.AbsolutePath}?{parameters}");
            }

            var message = string.IsNullOrWhiteSpace(parameters) ? $"{ApiKey.Key}&{nonce}{request.RequestUri.AbsolutePath}" : $"{ApiKey.Key}&{nonce}{request.RequestUri.AbsolutePath}?{parameters}";

            var signature = HashHMACSHA512Hex(message, ApiKey.Secret);

            headers.Add("X-API-Key", ApiKey.Key);
            headers.Add("X-Nonce", nonce.ToString());
            headers.Add("X-Signature", signature.ToLower());
        }
    }
}
