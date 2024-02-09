﻿using System;

namespace Tzkt.Api.Models
{
    public class BakingOperation : Operation
    {
        /// <summary>
        /// Type of the operation, `baking` - an operation which contains brief information about a baked (produced) block (synthetic type)
        /// </summary>
        public override string Type => OpTypes.Baking;

        /// <summary>
        /// Unique ID of the operation, stored in the MvKT indexer database
        /// </summary>
        public override long Id { get; set; }

        /// <summary>
        /// Height of the block from the genesis
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Datetime at which the block is claimed to have been created (ISO 8601, e.g. `2020-02-20T02:40:57Z`)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Block hash
        /// </summary>
        public string Block { get; set; }

        /// <summary>
        /// Baker who proposed the block payload
        /// </summary>
        public Alias Proposer { get; set; }

        /// <summary>
        /// Baker who produced the block
        /// </summary>
        public Alias Producer { get; set; }

        /// <summary>
        /// Round at which the block payload was proposed
        /// </summary>
        public int PayloadRound { get; set; }

        /// <summary>
        /// Round at which the block was produced
        /// </summary>
        public int BlockRound { get; set; }

        /// <summary>
        /// Security deposit frozen on the baker's account for producing the block (micro tez)
        /// </summary>
        public long Deposit { get; set; }

        /// <summary>
        /// Fixed reward paid to the payload proposer (micro tez) on baker's liquid balance
        /// (i.e. they are not frozen and can be spent immediately).
        /// </summary>
        public long RewardLiquid { get; set; }

        /// <summary>
        /// Fixed reward paid to the payload proposer (micro tez) on baker's staked balance (i.e. they are frozen).
        /// </summary>
        public long RewardStakedOwn { get; set; }

        /// <summary>
        /// Fixed reward paid to the payload proposer (micro tez) on baker's external staked balance
        /// (i.e. they are frozen and belong to stakers and can be withdrawn by unstaking).
        /// </summary>
        public long RewardStakedShared { get; set; }

        /// <summary>
        /// Bonus reward paid to the block producer (micro tez) on baker's liquid balance
        /// (i.e. they are not frozen and can be spent immediately).
        /// </summary>
        public long BonusLiquid { get; set; }

        /// <summary>
        /// Bonus reward paid to the block producer (micro tez) on baker's staked balance (i.e. they are frozen).
        /// </summary>
        public long BonusStakedOwn { get; set; }

        /// <summary>
        /// Bonus reward paid to the block producer (micro tez) on baker's external staked balance
        /// (i.e. they are frozen and belong to stakers and can be withdrawn by unstaking).
        /// </summary>
        public long BonusStakedShared { get; set; }

        /// <summary>
        /// Total fee gathered from operations, included into the block
        /// </summary>
        public long Fees { get; set; }

        #region injecting
        /// <summary>
        /// Injected historical quote at the time of operation
        /// </summary>
        public QuoteShort Quote { get; set; }
        #endregion

        #region deprecated
        /// <summary>
        /// [DEPRECATED]
        /// </summary>
        public long Reward => RewardLiquid + RewardStakedOwn + RewardStakedShared;

        /// <summary>
        /// [DEPRECATED]
        /// </summary>
        public long Bonus => BonusLiquid + BonusStakedOwn + BonusStakedShared;

        /// <summary>
        /// [DEPRECATED]
        /// </summary>
        public Alias Baker => Producer;

        /// <summary>
        /// [DEPRECATED]
        /// </summary>
        public int Priority => BlockRound;
        #endregion
    }
}
