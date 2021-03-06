using System;
using System.Collections.Generic;
using System.Data;
using Dapper;

namespace Vertica.Integration.Infrastructure.Database
{
    internal class DbSession : IDbSession
    {
        private readonly IDbConnection _connection;
        private readonly Stack<IDbTransaction> _transactions;

        public DbSession(IDbConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            _connection = connection;
            _connection.Open();

            _transactions = new Stack<IDbTransaction>(capacity: 3);
        }

        public IDbTransaction BeginTransaction(IsolationLevel? isolationLevel = null)
        {
            IDbTransaction transaction = isolationLevel.HasValue
                ? _connection.BeginTransaction(isolationLevel.Value)
                : _connection.BeginTransaction();

            _transactions.Push(transaction);

            return new TransactionScope(transaction, () => _transactions.Pop());
        }

        public int Execute(string sql, dynamic param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return SqlMapper.Execute(_connection, sql, param, CurrentTransaction, commandTimeout, commandType);
        }

        public T ExecuteScalar<T>(string sql, dynamic param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            return SqlMapper.ExecuteScalar<T>(_connection, sql, param, CurrentTransaction, commandTimeout, commandType);
        }

	    public IEnumerable<dynamic> Query(string sql, dynamic param = null)
	    {
		    return SqlMapper.Query(_connection, sql, param, CurrentTransaction);
	    }

	    public IEnumerable<T> Query<T>(string sql, dynamic param = null)
        {
            return SqlMapper.Query<T>(_connection, sql, param, CurrentTransaction);
        }

		public IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, object param = null, string splitOn = "Id")
		{
			return _connection.Query(sql, map, param, CurrentTransaction, splitOn: splitOn);
		}

		public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param = null, string splitOn = "Id")
		{
			return _connection.Query(sql, map, param, CurrentTransaction, splitOn: splitOn);
		}

		public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param = null, string splitOn = "Id")
		{
			return _connection.Query(sql, map, param, CurrentTransaction, splitOn: splitOn);
		}

	    public SqlMapper.GridReader QueryMultiple(string sql, dynamic param = null, int? commandTimeout = null, CommandType? commandType = null)
	    {
			return SqlMapper.QueryMultiple(_connection, sql, param, CurrentTransaction, commandTimeout, commandType);
	    }

	    public IDbConnection Connection => _connection;

	    public virtual void Dispose()
        {
            _connection.Dispose();
        }

        public IDbTransaction CurrentTransaction => _transactions.Count > 0 ? _transactions.Peek() : null;

	    private class TransactionScope : IDbTransaction
        {
            private readonly IDbTransaction _transaction;
            private readonly Action _beforeDispose;

            public TransactionScope(IDbTransaction transaction, Action beforeDispose)
            {
                _transaction = transaction;
                _beforeDispose = beforeDispose;
            }

            public void Dispose()
            {
                _beforeDispose();
                _transaction.Dispose();
            }

            public void Commit()
            {
                _transaction.Commit();
            }

            public void Rollback()
            {
                _transaction.Rollback();
            }

            public IDbConnection Connection => _transaction.Connection;

	        public IsolationLevel IsolationLevel => _transaction.IsolationLevel;
        }
    }
}