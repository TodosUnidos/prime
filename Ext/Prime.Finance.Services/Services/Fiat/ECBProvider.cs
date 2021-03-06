﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using LiteDB;
using Prime.Base;
using Prime.Core;

namespace Prime.Finance.Services.Services.Fiat
{
    public class EcbProvider : IPublicPricingProvider
    {
        public Version Version { get; } = new Version(1, 0, 0);
        private static readonly ObjectId IdHash = "prime:ECB:PROVIDER".GetObjectIdHashCode();
        private static readonly Network NetworkStatic = Networks.I.Get("ECB (Fiat)");
        private static readonly IRateLimiter Limiter = new NoRateLimits();
        private static readonly string _title = "European Central Bank";

        public ObjectId Id => IdHash;
        public IRateLimiter RateLimiter => Limiter;
        public bool IsDirect => true;
        public char? CommonPairSeparator { get; }

        public Task<bool> TestPublicApiAsync(NetworkProviderContext context)
        {
            return Task.Run(() => true);
        }

        public IAssetCodeConverter GetAssetCodeConverter()
        {
            return null;
        }

        public Network Network => NetworkStatic;
        public bool Disabled => false;
        public int Priority => 1;
        public string AggregatorName => null;
        public string Title => _title;

        private DateTime _date;
        private DateTime _lastRefresh;
        private Dictionary<AssetPair, decimal> _lastRates = new Dictionary<AssetPair, decimal>();
        private readonly UniqueList<AssetPair> _pairs = new UniqueList<AssetPair>();
        private readonly object _lock = new object();

        public static Asset Euro = "EUR".ToAssetRaw();

        public Task<Dictionary<AssetPair, decimal>> GetRatesAsync()
        {
            var t = new Task<Dictionary<AssetPair, decimal>>(() =>
            {
                lock (_lock)
                {
                    if (_lastRefresh.IsWithinTheLast(TimeSpan.FromMinutes(1)))
                        return _lastRates;
                    return _lastRates = GetXmlRates();
                }
            });
            t.Start();
            return t;
        }

        private Dictionary<AssetPair, decimal> GetXmlRates()
        {
            var returnList = new Dictionary<AssetPair, decimal>();

            var date = string.Empty;
            using (var xmlr = XmlReader.Create("http://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml"))
            {
                xmlr.ReadToFollowing("Cube");
                while (xmlr.Read())
                {
                    if (xmlr.NodeType != XmlNodeType.Element)
                        continue;

                    if (xmlr.GetAttribute("time") != null)
                        date = xmlr.GetAttribute("time");
                    else
                    {
                        var pair = new AssetPair(xmlr.GetAttribute("currency").ToAssetRaw(), Euro);
                        returnList.Add(pair, decimal.Parse(xmlr.GetAttribute("rate"), CultureInfo.InvariantCulture));
                    }
                }

                _date = DateTime.Parse(date);
            }
            _lastRefresh = DateTime.UtcNow;
            return returnList;
        }

        public async Task<decimal> GetMultiplierAsync(AssetPair pair)
        {
            var rates = await GetRatesAsync().ConfigureAwait(false);
            return rates.Get(pair, 0);
        }

        public async Task<AssetPairs> GetAssetPairsAsync(NetworkProviderContext context)
        {
            if (_pairs.Any())
                return new AssetPairs(_pairs);

            var rates = await GetRatesAsync().ConfigureAwait(false);

            lock (_lock)
            {
                var pairs = new AssetPairs(rates.Select(x => x.Key));
                _pairs.Clear();
                _pairs.AddRange(pairs);
                return pairs;
            }
        }

        private static readonly PricingFeatures StaticPricingFeatures = new PricingFeatures(false, true);
        public PricingFeatures PricingFeatures => StaticPricingFeatures;

        public async Task<MarketPrices> GetPricingAsync(PublicPricesContext context)
        {
            var rates = await GetRatesAsync().ConfigureAwait(false);

            var lp = new MarketPrices();

            foreach (var pair in context.Pairs)
            {
                var rate = rates.FirstOrDefault(x => Equals(x.Key, pair));
                if (rate.Key == null)
                    continue;

                lp.Add(new MarketPrice(Network, rate.Key, rate.Value));
            }

            return lp;
        }

        public async Task<MarketPrice> GetPriceAsync(PublicPriceContext context)
        {
            var r = await GetPricingAsync(new PublicPricesContext(new List<AssetPair>() {context.Pair}, context.L)).ConfigureAwait(false);
            return r.FirstOrDefault();
        }
    }
}
