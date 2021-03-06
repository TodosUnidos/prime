﻿using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Prime.Core;

namespace Prime.Finance.Services.Services.BitStamp
{
    public class BitStampAuthenticator : BaseAuthenticator
    {
        public BitStampAuthenticator(ApiKey apiKey) : base(apiKey)
        {

        }

        public override void RequestModify(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var nonce = GetLongNonce().ToString();
            var customerId = ApiKey.Extra;

            var message = nonce + customerId + ApiKey.Key;

            var signature = HashHMACSHA256Hex(message, ApiKey.Secret).ToUpper();

            request.Content = new FormUrlEncodedContent(new []
            {
                new KeyValuePair<string, string>("key", ApiKey.Key),
                new KeyValuePair<string, string>("nonce", nonce),
                new KeyValuePair<string, string>("signature", signature),
            });
        }
    }
}
