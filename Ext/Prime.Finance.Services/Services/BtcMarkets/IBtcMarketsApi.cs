﻿using System.Threading.Tasks;
using RestEase;

namespace Prime.Finance.Services.Services.BtcMarkets
{
    internal interface IBtcMarketsApi
    {
        [Get("/market/{currencyPair}/tick")]
        Task<BtcMarketsSchema.TickerResponse> GetTickerAsync([Path(UrlEncode = false)] string currencyPair);

        [Get("/market/{currencyPair}/orderbook")]
        Task<BtcMarketsSchema.OrderBookResponse> GetOrderBookAsync([Path(UrlEncode = false)] string currencyPair);
    }
}
