﻿using LiteDB;
using Prime.Base;
using Prime.Core;

namespace Prime.Finance.Services.Services.Tidex
{
    /// <author email="scaruana_prime@outlook.com">Sean Caruana</author>
    /// <author email="yasko.alexander@gmail.com">Alexander Yasko</author>
    // https://tidex.com/exchange/public-api
    public partial class TidexProviderTiLiWe : Common.CommonProviderTiLiWe<ITidexApi>
    {
        private const string TidexApiVersion = "3";
        private const string TidexApiUrlPublic = "https://api.tidex.com/api/" + TidexApiVersion;
        private const string TidexApiUrlPrivate = "https://api.tidex.com/tapi";

        private static readonly ObjectId IdHash = "prime:tidex".GetObjectIdHashCode();

        public override Network Network { get; } = Networks.I.Get("Tidex");
        public override ObjectId Id => IdHash;
        protected override RestApiClientProvider<ITidexApi> ApiProviderPublic { get; }
        protected override RestApiClientProvider<ITidexApi> ApiProviderPrivate { get; }

        public TidexProviderTiLiWe()
        {
            ApiProviderPublic = new RestApiClientProvider<ITidexApi>(TidexApiUrlPublic, this, (k) => null);
            ApiProviderPrivate = new RestApiClientProvider<ITidexApi>(TidexApiUrlPrivate, this, (k) => new TidexAuthenticator(k).GetRequestModifierAsync);
        }
    }
}
