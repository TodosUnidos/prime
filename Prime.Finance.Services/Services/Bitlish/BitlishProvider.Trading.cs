﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Prime.Core;
using RestEase;

namespace Prime.Finance.Services.Services.Bitlish
{
    public partial class BitlishProvider : IOrderLimitProvider
    {
        private void CheckResponseErrors<T>(Response<T> rawResponse, [CallerMemberName] string method = "Unknown")
        {
            if (!rawResponse.ResponseMessage.IsSuccessStatusCode)
            {
                var reason = rawResponse.ResponseMessage.ReasonPhrase;

                if (rawResponse.TryGetContent(out BitlishSchema.ErrorResponse baseResponse))
                {
                    throw new ApiResponseException($"Error Code: {baseResponse.errcode} - {baseResponse.msg.TrimEnd('.')}", this, method);
                }

                throw new ApiResponseException($"HTTP error {rawResponse.ResponseMessage.StatusCode} {(string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})")}", this, method);
            }
        }

        public async Task<PlacedOrderLimitResponse> PlaceOrderLimitAsync(PlaceOrderLimitContext context)
        {
            if (context.Pair == null)
                throw new MarketNotSpecifiedException(this);

            var api = ApiProvider.GetApi(context);

            var side = context.IsBuy ? "bid" : "ask";

            var authenticationRaw = await api.AuthenticateUserAsync();

            CheckResponseErrors(authenticationRaw);

            var authentication = authenticationRaw.GetContent();

            var rRaw = await api.PlaceNewOrderAsync(authentication.token, context.Pair.ToTicker(this).ToLower(), side, context.Quantity.ToDecimalValue(), context.Rate.ToDecimalValue()).ConfigureAwait(false);

            CheckResponseErrors(rRaw);

            var r = rRaw.GetContent();

            return new PlacedOrderLimitResponse(r.id);
        }

        public Task<TradeOrdersResponse> GetOrdersHistory(TradeOrdersContext context)
        {
            throw new NotImplementedException();
        }

        public async Task<TradeOrderStatusResponse> GetOrderStatusAsync(RemoteMarketIdContext context)
        {
            var api = ApiProvider.GetApi(context);

            var authenticationRaw = await api.AuthenticateUserAsync();

            CheckResponseErrors(authenticationRaw);

            var authentication = authenticationRaw.GetContent();

            var orderRaw = await api.QueryOrderAsync(authentication.token, context.RemoteGroupId).ConfigureAwait(false);

            CheckResponseErrors(orderRaw);

            var order = orderRaw.GetContent();
            
            var isBuy = order.dir.IndexOf("bid", StringComparison.OrdinalIgnoreCase) >= 0;
            var isOpen = order.state.IndexOf("new", StringComparison.OrdinalIgnoreCase) >= 0;

            return new TradeOrderStatusResponse(Network, order.id, isBuy, isOpen, false)
            {
                TradeOrderStatus =
                {
                    AmountInitial = order.amount,
                    AmountRemaining = order.amount_free,
                    Rate = order.price,
                    Market = order.pair_id.ToAssetPair(this)
                }
            };
        }

        public Task<OpenOrdersResponse> GetOpenOrdersAsync(OpenOrdersContext context)
        {
            throw new NotImplementedException();
        }

        public Task<OrderMarketResponse> GetMarketFromOrderAsync(RemoteIdContext context)
        {
            throw new NotImplementedException();
        }

        public Task<WithdrawalPlacementResult> PlaceWithdrawalAsync(WithdrawalPlacementContext context)
        {
            throw new NotImplementedException();
        }

        public MinimumTradeVolume[] MinimumTradeVolume => throw new NotImplementedException();

        private static readonly OrderLimitFeatures OrderFeatures = new OrderLimitFeatures(false, CanGetOrderMarket.WithinOrderStatus);
        public OrderLimitFeatures OrderLimitFeatures => OrderFeatures;

        public bool IsWithdrawalFeeIncluded => throw new NotImplementedException();
    }
}
