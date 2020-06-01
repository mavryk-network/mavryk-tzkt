﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;

using Tzkt.Api.Models;
using Tzkt.Api.Repositories;
using Tzkt.Api.Services.Cache;
using Tzkt.Api.Services.Metadata;

namespace Tzkt.Api.Controllers
{
    [ApiController]
    [Route("v1/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly AccountRepository Accounts;
        private readonly BalanceHistoryRepository History;
        private readonly ReportRepository Reports;
        private readonly StateCache State;

        public AccountsController(AccountRepository accounts, BalanceHistoryRepository history, ReportRepository reports, StateCache state)
        {
            Accounts = accounts;
            History = history;
            Reports = reports;
            State = state;
        }

        /// <summary>
        /// Get accounts
        /// </summary>
        /// <remarks>
        /// Returns a list of accounts.
        /// </remarks>
        /// <param name="type">Filters accounts by type (`user`, `delegate`, `contract`).</param>
        /// <param name="kind">Filter accounts by contract kind (`delegator_contract` or `smart_contract`)</param>
        /// <param name="select">Specify comma-separated list of fields to include into response or leave it undefined to return full object. If you select single field, response will be an array of values in both `.fields` and `.values` modes.</param>
        /// <param name="sort">Sorts delegators by specified field. Supported fields: `id` (default), `balance`, `firstActivity`, `lastActivity`, `numTransactions`, `numContracts`.</param>
        /// <param name="offset">Specifies which or how many items should be skipped</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Account>>> Get(
            AccountTypeParameter type,
            ContractKindParameter kind,
            SelectParameter select,
            SortParameter sort,
            OffsetParameter offset,
            [Range(0, 10000)] int limit = 100)
        {
            #region validate
            if (sort != null && !sort.Validate("id", "balance", "firstActivity", "lastActivity", "numTransactions", "numContracts"))
                return new BadRequest($"{nameof(sort)}", "Sorting by the specified field is not allowed.");
            #endregion

            #region optimize
            if (kind?.Eq != null && type == null)
                type = new AccountTypeParameter { Eq = 2 };
            #endregion
            
            if (select == null)
                return Ok(await Accounts.Get(type, kind, sort, offset, limit));

            if (select.Values != null)
            {
                if (select.Values.Length == 1)
                    return Ok(await Accounts.Get(type, kind, sort, offset, limit, select.Values[0]));
                else
                    return Ok(await Accounts.Get(type, kind, sort, offset, limit, select.Values));
            }
            else
            {
                if (select.Fields.Length == 1)
                    return Ok(await Accounts.Get(type, kind, sort, offset, limit, select.Fields[0]));
                else
                {
                    return Ok(new SelectionResponse
                    {
                        Cols = select.Fields,
                        Rows = await Accounts.Get(type, kind, sort, offset, limit, select.Fields)
                    });
                }
            }
        }

        /// <summary>
        /// Get accounts count
        /// </summary>
        /// <remarks>
        /// Returns a number of accounts.
        /// </remarks>
        /// <param name="type">Filters accounts by type (`user`, `delegate`, `contract`).</param>
        /// <param name="kind">Filter accounts by contract kind (`delegator_contract` or `smart_contract`)</param>
        /// <returns></returns>
        [HttpGet("count")]
        public Task<int> GetCount(AccountTypeParameter type, ContractKindParameter kind)
        {
            #region optimize
            if (type == null && kind == null)
                return Task.FromResult(State.GetState().AccountsCount);

            if (kind?.Eq != null && type == null)
                type = new AccountTypeParameter { Eq = 2 };
            #endregion

            return Accounts.GetCount(type, kind);
        }

        /// <summary>
        /// Get account by address
        /// </summary>
        /// <remarks>
        /// Returns an account with the specified address.
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <returns></returns>
        [HttpGet("{address}")]
        public Task<Account> GetByAddress([Address] string address)
        {
            return Accounts.Get(address);
        }

        /// <summary>
        /// Get account contracts
        /// </summary>
        /// <remarks>
        /// Returns a list of contracts created by (or related to) the specified account.
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="sort">Sorts contracts by specified field. Supported fields: `id` (default, desc), `balance`.</param>
        /// <param name="offset">Specifies which or how many items should be skipped</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <returns></returns>
        [HttpGet("{address}/contracts")]
        public async Task<ActionResult<IEnumerable<RelatedContract>>> GetContracts(
            [Address] string address,
            SortParameter sort,
            OffsetParameter offset,
            [Range(0, 10000)] int limit = 100)
        {
            #region validate
            if (sort != null && !sort.Validate("id", "balance"))
                return new BadRequest($"{nameof(sort)}", "Sorting by the specified field is not allowed.");
            #endregion

            return Ok(await Accounts.GetRelatedContracts(address, sort, offset, limit));
        }

        /// <summary>
        /// Get account delegators
        /// </summary>
        /// <remarks>
        /// Returns a list of accounts delegated to the specified account.
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="type">Filters delegators by type (`user`, `delegate`, `contract`).</param>
        /// <param name="balance">Filters delegators by balance.</param>
        /// <param name="delegationLevel">Number of items to skip</param>
        /// <param name="sort">Sorts delegators by specified field. Supported fields: `delegationLevel` (default, desc), `balance`.</param>
        /// <param name="offset">Specifies which or how many items should be skipped</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <returns></returns>
        [HttpGet("{address}/delegators")]
        public async Task<ActionResult<IEnumerable<Delegator>>> GetDelegators(
            [Address] string address,
            AccountTypeParameter type,
            Int64Parameter balance,
            Int32Parameter delegationLevel,
            SortParameter sort,
            OffsetParameter offset,
            [Range(0, 10000)] int limit = 100)
        {
            #region validate
            if (balance != null)
            {
                if (balance.Eqx != null)
                    return new BadRequest($"{nameof(balance)}.eqx", "This parameter doesn't support .eqx mode.");

                if (balance.Nex != null)
                    return new BadRequest($"{nameof(balance)}.nex", "This parameter doesn't support .nex mode.");
            }

            if (delegationLevel != null)
            {
                if (delegationLevel.Eqx != null)
                    return new BadRequest($"{nameof(delegationLevel)}.eqx", "This parameter doesn't support .eqx mode.");

                if (delegationLevel.Nex != null)
                    return new BadRequest($"{nameof(delegationLevel)}.nex", "This parameter doesn't support .nex mode.");
            }

            if (sort != null && !sort.Validate("balance", "delegationLevel"))
                return new BadRequest($"{nameof(sort)}", "Sorting by the specified field is not allowed.");
            #endregion

            return Ok(await Accounts.GetDelegators(address, type, balance, delegationLevel, sort, offset, limit));
        }

        /// <summary>
        /// Get account operations
        /// </summary>
        /// <remarks>
        /// Returns a list of operations related to the specified account.
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="from">Start of the datetime range to filter by (ISO 8601, e.g. 2019-11-31)</param>
        /// <param name="to">End of the datetime range to filter by (ISO 8601, e.g. 2019-12-31)</param>
        /// <param name="type">Comma separated list of operation types to return (endorsement, ballot, proposal, activation, double_baking, double_endorsing, nonce_revelation, delegation, origination, transaction, reveal, migration, revelation_penalty, baking)</param>
        /// <param name="lastId">Id of the last operation received, which is used as an offset for pagination</param>
        /// <param name="limit">Number of items to return</param>
        /// <param name="sort">Sort mode (0 - ascending, 1 - descending)</param>
        /// <returns></returns>
        [HttpGet("{address}/operations")]
        public Task<IEnumerable<Operation>> GetOperations(
            [Address] string address,
            DateTime? from,
            DateTime? to,
            string type,
            [Min(0)] int lastId = 0,
            [Range(0, 1000)] int limit = 100,
            SortMode sort = SortMode.Descending)
        {
            var types = type != null ? new HashSet<string>(type.Split(',')) : OpTypes.DefaultSet;

            return from != null || to != null
                ? Accounts.GetOperations(address, from ?? DateTime.MinValue, to ?? DateTime.MaxValue, types, sort, lastId, limit)
                : Accounts.GetOperations(address, types, sort, lastId, limit);
        }

        /// <summary>
        /// Get account metadata
        /// </summary>
        /// <remarks>
        /// Returns metadata of the specified account (alias, logo, website, contacts, etc).
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <returns></returns>
        [HttpGet("{address}/metadata")]
        public Task<AccountMetadata> GetMetadata([Address] string address)
        {
            return Accounts.GetMetadata(address);
        }

        [OpenApiIgnore]
        [HttpGet("{address}/profile")]
        public Task<Account> GetProfile([Address] string address, string type, [Range(0, 1000)] int n = 20, SortMode sort = SortMode.Descending)
        {
            var types = type != null ? new HashSet<string>(type.Split(',')) : OpTypes.DefaultSet;

            return Accounts.GetProfile(address, types, sort, n);
        }

        /// <summary>
        /// Get account report
        /// </summary>
        /// <remarks>
        /// Exports account balance report in .csv format
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="from">Start of the datetime range to filter by (ISO 8601, e.g. 2019-11-31)</param>
        /// <param name="to">End of the datetime range to filter by (ISO 8601, e.g. 2019-12-31)</param>
        /// <returns></returns>
        [HttpGet("{address}/report")]
        public async Task<FileStreamResult> GetBalanceReport(
            [Address] string address,
            DateTime? from,
            DateTime? to)
        {
            var _from = from ?? DateTime.MinValue;
            var _to = to ?? DateTime.MaxValue;

            var stream = new MemoryStream();
            var csv = new StreamWriter(stream);

            await Reports.Write(csv, address, _from, _to, 257_000);

            stream.Seek(0, SeekOrigin.Begin);
            return new FileStreamResult(stream, "text/csv")
            {
                FileDownloadName = $"{address[..9]}..{address[^6..]}_{_from.ToShortDateString()}-{_to.ToShortDateString()}.csv"
            };
        }

        /// <summary>
        /// Get balance history
        /// </summary>
        /// <remarks>
        /// Returns time series with historical balances (only changes, without duplicates).
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="step">Step of the time series, for example if `step = 1000` you will get balances at blocks `1000, 2000, 3000, ...`.</param>
        /// <param name="select">Specify comma-separated list of fields to include into response or leave it undefined to return full object. If you select single field, response will be an array of values in both `.fields` and `.values` modes.</param>
        /// <param name="sort">Sorts historical balances by specified field. Supported fields: `level`.</param>
        /// <param name="offset">Specifies which or how many items should be skipped</param>
        /// <param name="limit">Maximum number of items to return</param>
        /// <returns></returns>
        [HttpGet("{address}/balance_history")]
        public async Task<ActionResult<IEnumerable<HistoricalBalance>>> GetBalanceHistory(
            [Address] string address,
            [Min(1)] int? step,
            SelectParameter select,
            SortParameter sort,
            [Min(0)] int offset = 0,
            [Range(0, 10000)] int limit = 100)
        {
            #region validate
            if (sort != null && !sort.Validate("level"))
                return new BadRequest($"{nameof(sort)}", "Sorting by the specified field is not allowed.");
            #endregion

            if (select == null)
                return Ok(await History.Get(address, step ?? 1, sort, offset, limit));

            if (select.Values != null)
            {
                if (select.Values.Length == 1)
                    return Ok(await History.Get(address, step ?? 1, sort, offset, limit, select.Values[0]));
                else
                    return Ok(await History.Get(address, step ?? 1, sort, offset, limit, select.Values));
            }
            else
            {
                if (select.Fields.Length == 1)
                    return Ok(await History.Get(address, step ?? 1, sort, offset, limit, select.Fields[0]));
                else
                {
                    return Ok(new SelectionResponse
                    {
                        Cols = select.Fields,
                        Rows = await History.Get(address, step ?? 1, sort, offset, limit, select.Fields)
                    });
                }
            }
        }

        /// <summary>
        /// Get balance at level
        /// </summary>
        /// <remarks>
        /// Returns account balance at the specified block
        /// </remarks>
        /// <param name="address">Account address (starting with tz or KT)</param>
        /// <param name="level">Block height at which you want to know account balance</param>
        /// <returns></returns>
        [HttpGet("{address}/balance_history/{level}")]
        public Task<long> GetBalanceHistoryByLevel([Address] string address, int level)
        {
            return History.Get(address, level);
        }
    }
}
