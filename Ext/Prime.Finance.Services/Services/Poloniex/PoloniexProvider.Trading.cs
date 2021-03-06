﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prime.Core;
using Prime.Finance;

namespace Prime.Finance.Services.Services.Poloniex
{
    // https://poloniex.com/support/api/
    /// <author email="yasko.alexander@gmail.com">Alexander Yasko</author>
    public partial class PoloniexProvider : IOrderLimitProvider, IWithdrawalPlacementProvider
    {
        public async Task<TradeOrdersResponse> GetOrdersHistory(TradeOrdersContext context)
        {
            var historyOrders = (await GetOrdersHistory((NetworkProviderPrivateContext) context).ConfigureAwait(false)).ToList();
            var openOrders = (await GetOpenOrders(context).ConfigureAwait(false)).ToList();
            
            return new TradeOrdersResponse(historyOrders.Concat(openOrders))
            {
                ApiHitsCount = 2
            };
        }

        public async Task<OpenOrdersResponse> GetOpenOrdersAsync(OpenOrdersContext context)
        {
            var openOrders = await GetOpenOrders(context, context.Market).ConfigureAwait(false);
            
            return new OpenOrdersResponse(openOrders);
        }

        public async Task<PlacedOrderLimitResponse> PlaceOrderLimitAsync(PlaceOrderLimitContext context)
        {
            var buy = context.IsBuy;
            var api = ApiProvider.GetApi(context);
            var pairCode = context.Pair.ToTicker(this);

            var body = CreatePoloniexBody(buy ? PoloniexBodyType.LimitOrderBuy : PoloniexBodyType.LimitOrderSell);
            body.Add("currencyPair", pairCode);
            body.Add("rate", context.Rate.ToDecimalValue());
            body.Add("amount", context.Quantity.ToDecimalValue());

            var rRaw = await api.PlaceOrderLimitAsync(body).ConfigureAwait(false);
            CheckResponseErrors(rRaw);

            var r = rRaw.GetContent();

            return new PlacedOrderLimitResponse(r.orderNumber, r.resultingTrades.Select(x => x.tradeID));
        }

        public async Task<OrderCancelationResponse> CancelOrderAsync(RemoteIdContext context)
        {
            var api = ApiProvider.GetApi(context);

            var body = CreatePoloniexBody(PoloniexBodyType.CancelOrder);
            body.Add("orderNumber", context.RemoteGroupId);

            var rRaw = await api.CancelOrderAsync(body).ConfigureAwait(false);
            CheckResponseErrors(rRaw);

            var r = rRaw.GetContent();

            return new OrderCancelationResponse(r.success);
        }

        public async Task<TradeOrderStatusResponse> GetOrderStatusAsync(RemoteMarketIdContext context)
        {
            var historyOrders = (await GetOrdersHistory(context).ConfigureAwait(false)).ToList();
            var openOrders = (await GetOpenOrders(context).ConfigureAwait(false)).ToList();

            var allOrders = historyOrders.Concat(openOrders);

            var order = allOrders.FirstOrDefault(x => x.RemoteOrderId.Equals(context.RemoteGroupId));
            if(order == null)
                throw new NoTradeOrderException(context, this);

            return new TradeOrderStatusResponse(order)
            {
                ApiHitsCount = 2
            };
        }

        /// <summary>
        /// Returns orders history, open orders are not present in this list.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task<IEnumerable<TradeOrderStatus>> GetOrdersHistory(NetworkProviderPrivateContext context)
        {
            var api = ApiProvider.GetApi(context);

            var body = CreatePoloniexBody(PoloniexBodyType.ReturnTradeHistory);
            body.Add("currencyPair", "all");
            body.Add("limit", 10000);
            body.Add("start", DateTime.UtcNow.AddYears(-5).ToUnixTimeStamp());

            var rRaw = await api.GetTradeHistoryAsync(body).ConfigureAwait(false);
            CheckResponseErrors(rRaw);

            if(rRaw.TryGetContent<PoloniexSchema.MarketTradeOrdersResponse, object[]>(out var rArray) && rArray.Length == 0)
                return new List<TradeOrderStatus>();

            var r = rRaw.GetContent();

            var historyOrders = new List<TradeOrderStatus>();

            foreach (var rMarketOrder in r)
            {
                var market = rMarketOrder.Key.ToAssetPair(this);

                foreach (var rOrder in rMarketOrder.Value)
                {
                    var isBuy = rOrder.type.Equals("buy", StringComparison.OrdinalIgnoreCase);

                    historyOrders.Add(new TradeOrderStatus(Network, rOrder.orderNumber.ToString(), isBuy, false, false)
                    {
                        Market = market,
                        AmountInitial = rOrder.amount,
                        Rate = rOrder.rate
                    });
                }
            }

            return historyOrders;
        }

        /// <summary>
        /// Returns list of currently open orders.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="market"></param>
        /// <returns></returns>
        private async Task<IEnumerable<TradeOrderStatus>> GetOpenOrders(NetworkProviderPrivateContext context, AssetPair market = null)
        {
            var api = ApiProvider.GetApi(context);

            var body = CreatePoloniexBody(PoloniexBodyType.ReturnOpenOrders);
            body.Add("currencyPair", market != null ? market.ToTicker(this) : "all");

            var rRaw = await api.GetOpenOrdersAsync(body).ConfigureAwait(false);
            CheckResponseErrors(rRaw);

            var r = rRaw.GetContent();

            var openOrders = new List<TradeOrderStatus>();

            foreach (var rMarketOrders in r)
            {
                var orderMarket = rMarketOrders.Key.ToAssetPair(this);

                foreach (var rOrder in rMarketOrders.Value)
                {
                    var isBuy = rOrder.type.Equals("buy", StringComparison.OrdinalIgnoreCase);
                    openOrders.Add(new TradeOrderStatus(Network, rOrder.orderNumber.ToString(), isBuy, true, false)
                    {
                        Market = orderMarket,
                        AmountInitial = rOrder.amount,
                        Rate = rOrder.rate
                    });
                }
            }

            return openOrders;
        }

        public Task<OrderMarketResponse> GetMarketFromOrderAsync(RemoteIdContext context) => Task.FromResult<OrderMarketResponse>(null);

        public MinimumTradeVolume[] MinimumTradeVolume => throw new NotImplementedException();

        private static readonly OrderLimitFeatures OrderFeatures = new OrderLimitFeatures(false, CanGetOrderMarket.WithinOrderStatus);
        public OrderLimitFeatures OrderLimitFeatures => OrderFeatures;

        public bool IsWithdrawalFeeIncluded => throw new NotImplementedException();

        public async Task<WithdrawalPlacementResult> PlaceWithdrawalAsync(WithdrawalPlacementContext context)
        {
            var api = ApiProvider.GetApi(context);

            var body = CreatePoloniexBody(PoloniexBodyType.Withdraw);
            body.Add("currency", context.Amount.Asset.ToRemoteCode(this));
            body.Add("amount", context.Amount.ToDecimalValue());
            body.Add("address", context.Address.Address);

            if (context.HasDescription)
                body.Add("paymentId", context.Description);

            var rRaw = await api.WithdrawAsync(body).ConfigureAwait(false);
            CheckResponseErrors(rRaw);

            return new WithdrawalPlacementResult();
        }
    }
}
