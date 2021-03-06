using LiteDB;
using System;
using Prime.Base;
using Prime.Core;

namespace Prime.Finance
{
    public class BalanceResult : IEquatable<BalanceResult>
    {
        public BalanceResult()
        {
            Id = ObjectId.NewObjectId();
        }

        public Asset Asset => !AvailableAndReserved.Asset.IsNone() ? AvailableAndReserved.Asset : (!Available.Asset.IsNone() ? Available.Asset : Reserved.Asset);

        [BsonId]
        public ObjectId Id { get; private set; }

        /// <summary>
        /// The available balance for use.
        /// </summary>
        [Bson]
        public Money Available { get; set; }

        /// <summary>
        /// The balance reserved in either trading / withdrawals or creation of orders.
        /// </summary>
        [Bson]
        public Money Reserved { get; set; }

        /// <summary>
        /// The account's value when all balances are available.
        /// The amount of money reserved by trading / withdrawals plus the available balance.
        /// </summary>
        [Bson]
        public Money AvailableAndReserved { get; set; }
        
        [Bson]
        public AssetPosition AssetPosition { get; private set; }

        public Network Network { get; }

        public BalanceResult(INetworkProvider provider) : this(provider.Network) { }

        public BalanceResult(Network network, Money? availableBalance = null) : this()
        {
            Network = network;
            if (availableBalance != null)
            {
                Available = availableBalance.Value;
                AssetPosition = new AssetPosition(network, Available.Asset);
            }
            else
                AssetPosition = new AssetPosition(network, Asset.None);
        }

        public override string ToString()
        {
            return "A: " + Available + " B: " + AvailableAndReserved + " R: " + Reserved;
        }

        public bool MatchNetworkAsset(BalanceResult other)
        {
            if (other == null)
                return false;

            return other?.Network.Id == Network.Id && other?.Asset.Id == Asset.Id;
        }

        public bool Equals(BalanceResult other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BalanceResult) obj);
        }

        public override int GetHashCode()
        {
            return (Id != null ? Id.GetHashCode() : 0);
        }
    }
}