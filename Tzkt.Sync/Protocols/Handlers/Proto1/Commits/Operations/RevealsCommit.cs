﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

using Tzkt.Data.Models;
using Tzkt.Data.Models.Base;

namespace Tzkt.Sync.Protocols.Proto1
{
    class RevealsCommit : ProtocolCommit
    {
        public List<RevealOperation> Reveals { get; protected set; }
        public Dictionary<string, string> PubKeys { get; protected set; }

        public RevealsCommit(ProtocolHandler protocol, List<ICommit> commits) : base(protocol, commits) { }

        public override async Task Init()
        {
            var block = await Cache.GetCurrentBlockAsync();
            block.Baker ??= (Data.Models.Delegate)await Cache.GetAccountAsync(block.BakerId);

            Reveals = await Db.RevealOps.Where(x => x.Level == block.Level).ToListAsync();
            foreach (var op in Reveals)
            {
                op.Block = block;
                op.Sender = await Cache.GetAccountAsync(op.SenderId);
                op.Sender.Delegate ??= (Data.Models.Delegate)await Cache.GetAccountAsync(op.Sender.DelegateId);
            }
        }

        public override async Task Init(IBlock block)
        {
            var rawBlock = block as RawBlock;
            var parsedBlock = FindCommit<BlockCommit>().Block;
            parsedBlock.Baker ??= (Data.Models.Delegate)await Cache.GetAccountAsync(parsedBlock.BakerId);

            Reveals = new List<RevealOperation>();
            PubKeys = new Dictionary<string, string>(4);
            foreach (var op in rawBlock.Operations[3])
            {
                foreach (var content in op.Contents.Where(x => x is RawRevealContent))
                {
                    var reveal = content as RawRevealContent;
                    var sender = await Cache.GetAccountAsync(reveal.Source);
                    sender.Delegate ??= (Data.Models.Delegate)await Cache.GetAccountAsync(sender.DelegateId);

                    PubKeys[reveal.Source] = reveal.PublicKey;

                    Reveals.Add(new RevealOperation
                    {
                        OpHash = op.Hash,
                        Block = parsedBlock,
                        Timestamp = parsedBlock.Timestamp,
                        BakerFee = reveal.Fee,
                        Counter = reveal.Counter,
                        GasLimit = reveal.GasLimit,
                        StorageLimit = reveal.StorageLimit,
                        Sender = sender,
                        Status = reveal.Metadata.Result.Status switch
                        {
                            "applied" => OperationStatus.Applied,
                            _ => throw new NotImplementedException()
                        }
                    });
                }
            }
        }

        public override Task Apply()
        {
            if (Reveals == null)
                throw new Exception("Commit is not initialized");

            foreach (var reveal in Reveals)
            {
                #region entities
                var block = reveal.Block;
                var blockBaker = block.Baker;

                var sender = reveal.Sender;
                var senderDelegate = sender.Delegate ?? sender as Data.Models.Delegate;

                //Db.TryAttach(block);
                Db.TryAttach(blockBaker);

                Db.TryAttach(sender);
                Db.TryAttach(senderDelegate);
                #endregion

                #region apply operation
                sender.Balance -= reveal.BakerFee;
                if (senderDelegate != null) senderDelegate.StakingBalance -= reveal.BakerFee;
                blockBaker.FrozenFees += reveal.BakerFee;

                sender.Operations |= Operations.Reveals;
                block.Operations |= Operations.Reveals;
                
                sender.Counter = Math.Max(sender.Counter, reveal.Counter);
                #endregion

                #region apply result
                if (reveal.Status == OperationStatus.Applied)
                {
                    if (sender is User user)
                        user.PublicKey = PubKeys[sender.Address];
                }
                #endregion

                Db.RevealOps.Add(reveal);
            }

            return Task.CompletedTask;
        }

        public override async Task Revert()
        {
            if (Reveals == null)
                throw new Exception("Commit is not initialized");

            foreach (var reveal in Reveals)
            {
                #region entities
                var block = reveal.Block;
                var blockBaker = block.Baker;

                var sender = reveal.Sender;
                var senderDelegate = sender.Delegate ?? sender as Data.Models.Delegate;

                //Db.TryAttach(block);
                Db.TryAttach(blockBaker);

                Db.TryAttach(sender);
                Db.TryAttach(senderDelegate);
                #endregion

                #region revert result
                if (reveal.Status == OperationStatus.Applied)
                {
                    if (sender is User user)
                        user.PublicKey = null;
                }
                #endregion

                #region revert operation
                sender.Balance += reveal.BakerFee;
                if (senderDelegate != null) senderDelegate.StakingBalance += reveal.BakerFee;
                blockBaker.FrozenFees -= reveal.BakerFee;

                if (!await Db.RevealOps.AnyAsync(x => x.SenderId == sender.Id && x.Counter < reveal.Counter))
                    sender.Operations &= ~Operations.Delegations;

                sender.Counter = Math.Min(sender.Counter, reveal.Counter - 1);
                #endregion

                if (sender.Operations == Operations.None && sender.Counter > 0)
                {
                    Db.Accounts.Remove(sender);
                    Cache.RemoveAccount(sender);
                }

                Db.RevealOps.Remove(reveal);
            }
        }

        #region static
        public static async Task<RevealsCommit> Create(ProtocolHandler protocol, List<ICommit> commits, RawBlock rawBlock)
        {
            var commit = new RevealsCommit(protocol, commits);
            await commit.Init(rawBlock);
            return commit;
        }

        public static async Task<RevealsCommit> Create(ProtocolHandler protocol, List<ICommit> commits)
        {
            var commit = new RevealsCommit(protocol, commits);
            await commit.Init();
            return commit;
        }
        #endregion
    }
}