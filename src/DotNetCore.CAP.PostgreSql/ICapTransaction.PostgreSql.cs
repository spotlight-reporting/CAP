﻿// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotNetCore.CAP.Transport;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace DotNetCore.CAP
{
    public class PostgreSqlCapTransaction : CapTransactionBase
    {
        public PostgreSqlCapTransaction(IDispatcher dispatcher) : base(dispatcher)
        {
        }

        public override void Commit()
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Commit();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    dbContextTransaction.Commit();
                    break;
            }

            Flush();
        }

#pragma warning disable CS1988  // isn't async if one of the older frameworks, that's life
        public override async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Commit();
                    break;
                case IDbContextTransaction dbContextTransaction:
#if NETSTANDARD2_0 || NETFRAMEWORK
                    dbContextTransaction.Commit();
#else
                    await dbContextTransaction.CommitAsync(cancellationToken);
#endif
                    break;
            }

            Flush();
        }
#pragma warning restore CS1988

        public override void Rollback()
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Rollback();
                    break;
                case IDbContextTransaction dbContextTransaction:
                    dbContextTransaction.Rollback();
                    break;
            }
        }

#pragma warning disable CS1988  // isn't async if one of the older frameworks, that's life
        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Debug.Assert(DbTransaction != null);

            switch (DbTransaction)
            {
                case IDbTransaction dbTransaction:
                    dbTransaction.Rollback();
                    break;
                case IDbContextTransaction dbContextTransaction:
#if NETSTANDARD2_0 || NETFRAMEWORK
                    dbContextTransaction.Rollback();
#else
                    await dbContextTransaction.RollbackAsync(cancellationToken);
#endif
                    break;
            }
        }
#pragma warning restore CS1988

        public override void Dispose()
        {
            (DbTransaction as IDisposable)?.Dispose();
            DbTransaction = null;
        }
    }

    public static class CapTransactionExtensions
    {
        public static ICapTransaction Begin(this ICapTransaction transaction,
            IDbTransaction dbTransaction, bool autoCommit = false)
        {
            transaction.DbTransaction = dbTransaction;
            transaction.AutoCommit = autoCommit;

            return transaction;
        }

        public static ICapTransaction Begin(this ICapTransaction transaction,
            IDbContextTransaction dbTransaction, bool autoCommit = false)
        {
            transaction.DbTransaction = dbTransaction;
            transaction.AutoCommit = autoCommit;

            return transaction;
        }

        /// <summary>
        /// Start the CAP transaction
        /// </summary>
        /// <param name="dbConnection">The <see cref="IDbConnection" />.</param>
        /// <param name="publisher">The <see cref="ICapPublisher" />.</param>
        /// <param name="autoCommit">Whether the transaction is automatically committed when the message is published</param>
        /// <returns>The <see cref="ICapTransaction" /> object.</returns>
        public static ICapTransaction BeginTransaction(this IDbConnection dbConnection,
            ICapPublisher publisher, bool autoCommit = false)
        {
            if (dbConnection.State == ConnectionState.Closed) dbConnection.Open();

            var dbTransaction = dbConnection.BeginTransaction();
            publisher.Transaction.Value = ActivatorUtilities.CreateInstance<PostgreSqlCapTransaction>(publisher.ServiceProvider);
            return publisher.Transaction.Value.Begin(dbTransaction, autoCommit);
        }

        /// <summary>
        /// Start the CAP transaction
        /// </summary>
        /// <param name="database">The <see cref="DatabaseFacade" />.</param>
        /// <param name="publisher">The <see cref="ICapPublisher" />.</param>
        /// <param name="autoCommit">Whether the transaction is automatically committed when the message is published</param>
        /// <returns>The <see cref="IDbContextTransaction" /> of EF DbContext transaction object.</returns>
        public static IDbContextTransaction BeginTransaction(this DatabaseFacade database,
            ICapPublisher publisher, bool autoCommit = false)
        {
            var trans = database.BeginTransaction();
            publisher.Transaction.Value = ActivatorUtilities.CreateInstance<PostgreSqlCapTransaction>(publisher.ServiceProvider);
            var capTrans = publisher.Transaction.Value.Begin(trans, autoCommit);
            return new CapEFDbTransaction(capTrans);
        }
    }
}