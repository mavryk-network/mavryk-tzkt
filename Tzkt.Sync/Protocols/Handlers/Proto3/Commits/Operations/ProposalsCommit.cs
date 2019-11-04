﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Tzkt.Data.Models;

namespace Tzkt.Sync.Protocols.Proto3
{
    class ProposalsCommit : ProtocolCommit
    {
        public List<ProposalOperation> ProposalOperations { get; private set; }

        ProposalsCommit(ProtocolHandler protocol) : base(protocol) { }

        public async Task Init(Block block, RawOperation op, RawProposalContent content)
        {
            ProposalOperations = new List<ProposalOperation>(4);
            foreach (var proposal in content.Proposals)
            {
                ProposalOperations.Add(new ProposalOperation
                {
                    Id = await Cache.NextCounterAsync(),
                    Block = block,
                    Level = block.Level,
                    Timestamp = block.Timestamp,
                    OpHash = op.Hash,
                    Sender = await Cache.GetDelegateAsync(content.Source),
                    Period = await Cache.GetCurrentVotingPeriodAsync(),
                    Proposal = await Cache.GetOrSetProposalAsync(proposal, async () => new Proposal
                    {
                        Hash = proposal,
                        Initiator = await Cache.GetDelegateAsync(content.Source),
                        ProposalPeriod = (ProposalPeriod)await Cache.GetCurrentVotingPeriodAsync(),
                        Status = ProposalStatus.Active
                    })
                });
            }
        }

        public async Task Init(Block block, ProposalOperation proposal)
        {
            proposal.Block ??= block;
            proposal.Sender ??= (Data.Models.Delegate)await Cache.GetAccountAsync(proposal.SenderId);
            proposal.Period ??= await Cache.GetCurrentVotingPeriodAsync();
            proposal.Proposal ??= await Cache.GetProposalAsync(proposal.ProposalId);

            ProposalOperations = new List<ProposalOperation> { proposal };
        }

        public override Task Apply()
        {
            foreach (var proposalOp in ProposalOperations)
            {
                #region entities
                var block = proposalOp.Block;
                var sender = proposalOp.Sender;
                var proposal = proposalOp.Proposal;

                //Db.TryAttach(block);
                Db.TryAttach(sender);
                Db.TryAttach(proposal);
                #endregion

                #region apply operation
                proposal.Likes++;

                sender.Operations |= Operations.Proposals;
                block.Operations |= Operations.Proposals;
                #endregion

                Db.ProposalOps.Add(proposalOp);
            }

            return Task.CompletedTask;
        }

        public override async Task Revert()
        {
            foreach (var proposalOp in ProposalOperations)
            {
                #region entities
                //var block = proposal.Block;
                var sender = proposalOp.Sender;
                var proposal = proposalOp.Proposal;

                //Db.TryAttach(block);
                Db.TryAttach(sender);
                Db.TryAttach(proposal);
                #endregion

                #region revert operation
                proposal.Likes--;

                if (!await Db.ProposalOps.AnyAsync(x => x.SenderId == sender.Id && x.Id < proposalOp.Id))
                    sender.Operations &= ~Operations.Proposals;
                #endregion

                if (proposal.Likes == 0)
                {
                    Db.Proposals.Remove(proposal);
                    Cache.RemoveProposal(proposal);
                }

                Db.ProposalOps.Remove(proposalOp);
            }
        }

        #region static
        public static async Task<ProposalsCommit> Apply(ProtocolHandler proto, Block block, RawOperation op, RawProposalContent content)
        {
            var commit = new ProposalsCommit(proto);
            await commit.Init(block, op, content);
            await commit.Apply();

            return commit;
        }

        public static async Task<ProposalsCommit> Revert(ProtocolHandler proto, Block block, ProposalOperation op)
        {
            var commit = new ProposalsCommit(proto);
            await commit.Init(block, op);
            await commit.Revert();

            return commit;
        }
        #endregion
    }
}