﻿using System;
using System.Configuration;
using Npgsql;
using NUnit.Framework;
using WB.Core.GenericSubdomains.Portable;
using WB.Infrastructure.Native.Storage.Postgre;

namespace WB.Tests.Integration
{
    internal abstract class NpgsqlTestContext
    {
        protected static NpgsqlConnectionStringBuilder connectionStringBuilder;
        protected static string TestConnectionString;
        private static string databaseName;

        protected IUnitOfWork UnitOfWork;

        [OneTimeSetUp]
        public void Context()
        {
            TestConnectionString = TestsConfigurationManager.ConnectionString;
            databaseName = "testdb_" + Guid.NewGuid().FormatGuid();
            connectionStringBuilder = new NpgsqlConnectionStringBuilder(TestConnectionString)
            {
                Database = databaseName
            };

            using var connection = new NpgsqlConnection(TestConnectionString);
            connection.Open();
            var command = $@"CREATE DATABASE {databaseName} ENCODING = 'UTF8'";
            using (var sqlCommand = connection.CreateCommand())
            {
                sqlCommand.CommandText = command;
                sqlCommand.ExecuteNonQuery();
            }
            connection.Close();
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (UnitOfWork != null)
            {
                UnitOfWork.AcceptChanges();
                UnitOfWork.Dispose();
            }

            //pgSqlConnection.Close();

            using var connection = new NpgsqlConnection(TestConnectionString);
            connection.Open();
            var command = string.Format(
                @"SELECT pg_terminate_backend (pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = '{0}'; DROP DATABASE {0};",
                databaseName);
            using (var sqlCommand = connection.CreateCommand())
            {
                sqlCommand.CommandText = command;
                sqlCommand.ExecuteNonQuery();
            }
            connection.Close();
        }
    }
}
