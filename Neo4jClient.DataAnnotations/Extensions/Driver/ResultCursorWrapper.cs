﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace Neo4jClient.DataAnnotations.Extensions.Driver
{
    public class ResultCursorWrapper : BaseWrapper<IResultCursor>, IResultCursor
    {



        //public ResultCursorWrapper(IStatementResultCursor statementResultCursor) : base(statementResultCursor) { }

        //public IReadOnlyList<string> Keys => WrappedItem.Keys;

        //public IRecord Current => StatementResultWrapper.GetRecord(WrappedItem.Current);

        //public Task<IResultSummary> ConsumeAsync()
        //{
        //    return WrappedItem.ConsumeAsync();
        //}

        //public Task<bool> FetchAsync()
        //{
        //    return WrappedItem.FetchAsync();
        //}

        //public async Task<IRecord> PeekAsync()
        //{
        //    return StatementResultWrapper.GetRecord(await WrappedItem.PeekAsync());
        //}

        //public Task<IResultSummary> SummaryAsync()
        //{
        //    return WrappedItem.SummaryAsync();
        //}
        public ResultCursorWrapper(IResultCursor item) : base(item)
        {
        }

        public Task<string[]> KeysAsync()
        {
            return WrappedItem.KeysAsync();
        }

        public Task<IResultSummary> ConsumeAsync()
        {
            return WrappedItem.ConsumeAsync();
        }
        protected internal static IRecord GetRecord(IRecord record)
        {
            if (record != null && !(record is RecordWrapper))
            {
                return new RecordWrapper(record);
            }

            return record;
        }

        public async Task<IRecord> PeekAsync()
        {
            return GetRecord(await WrappedItem.PeekAsync());
        }

        public Task<bool> FetchAsync()
        {
            return WrappedItem.FetchAsync();
        }

        public IRecord Current => GetRecord(WrappedItem.Current);
    }
}