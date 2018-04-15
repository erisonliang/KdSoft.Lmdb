﻿using System;
using System.Collections.Generic;

namespace KdSoft.Lmdb
{
    /// <summary>
    /// Transaction that allows creation/opening of databases. Only one of these can be active at a time.
    /// </summary>
    public class DatabaseTransaction: Transaction
    {
        readonly Dictionary<string, Database> committedDatabases;
        readonly Action<Database> committedDisposed;
        readonly List<Database> newDatabases = new List<Database>();

        internal DatabaseTransaction(
            IntPtr txn,
            Transaction parent,
            Action<IntPtr> closed,
            Dictionary<string, Database> committedDatabases,
            Action<Database> committedDisposed
        ) : base(txn, parent, closed) {
            this.committedDatabases = committedDatabases;
            this.committedDisposed = committedDisposed;
        }

        readonly object dbLock = new object();

        (uint dbi, IntPtr handle, IntPtr env) OpenDatabaseInternal(string name, uint options, DbLibCompareFunction compare) {
            // we won't allow database name conflicts, even before the new database is committed
            if (committedDatabases.ContainsKey(name))
                throw new LmdbException($"Database '{name}' exists already.");
            var handle = CheckDisposed();
            var ret = DbLib.mdb_dbi_open(handle, name, options, out uint dbi);
            ErrorUtil.CheckRetCode(ret);

            var env = DbLib.mdb_txn_env(handle);

            if (compare != null) {
                ret = DbLib.mdb_set_compare(handle, dbi, compare);
                if (ret != DbRetCode.SUCCESS)
                    DbLib.mdb_dbi_close(env, dbi);
                ErrorUtil.CheckRetCode(ret);
            }

            return (dbi, handle, env);
        }

        /// <summary>
        /// Open a database in the environment.
        /// A database handle denotes the name and parameters of a database, independently of whether such a database exists.
        /// The database handle may be discarded by calling mdb_dbi_close().
        /// The old database handle is returned if the database was already open.
        /// The handle may only be closed once.
        /// The database handle will be private to the current transaction until the transaction is successfully committed.
        /// If the transaction is aborted the handle will be closed automatically.
        /// After a successful commit the handle will reside in the shared environment, and may be used by other transactions.
        /// This function must not be called from multiple concurrent transactions in the same process.
        /// A transaction that uses this function must finish (either commit or abort) before any other transaction in the process
        /// may use this function.
        /// To use named databases(with name != NULL), mdb_env_set_maxdbs() must be called before opening the environment.
        /// Database names are keys in the unnamed database, and may be read but not written.
        /// </summary>
        /// <param name="name">Database name. Can be <c>null</c> for the default database.</param>
        /// <param name="config">Database configuration instance.</param>
        /// <returns></returns>
        public Database OpenDatabase(string name, DatabaseConfiguration config) {
            lock (rscLock) {
                lock (dbLock) {
                    var (dbi, handle, env) = OpenDatabaseInternal(name, unchecked((uint)config.Options), config.LibCompare);

                    var result = new Database(dbi, env, name, NewDatabaseDisposed, config);
                    newDatabases.Add(result);
                    return result;
                }
            }
        }

        /// <summary>
        /// Open a multi-value database in the environment.
        /// A database handle denotes the name and parameters of a database, independently of whether such a database exists.
        /// The database handle may be discarded by calling mdb_dbi_close().
        /// The old database handle is returned if the database was already open.
        /// The handle may only be closed once.
        /// The database handle will be private to the current transaction until the transaction is successfully committed.
        /// If the transaction is aborted the handle will be closed automatically.
        /// After a successful commit the handle will reside in the shared environment, and may be used by other transactions.
        /// This function must not be called from multiple concurrent transactions in the same process.
        /// A transaction that uses this function must finish (either commit or abort) before any other transaction in the process
        /// may use this function.
        /// To use named databases(with name != NULL), mdb_env_set_maxdbs() must be called before opening the environment.
        /// Database names are keys in the unnamed database, and may be read but not written.
        /// </summary>
        /// <param name="name">Database name. Can be <c>null</c> for the default database.</param>
        /// <param name="config">Database configuration instance.</param>
        /// <returns></returns>
        public MultiValueDatabase OpenMultiValueDatabase(string name, MultiValueDatabaseConfiguration config) {
            uint options = unchecked((uint)config.Options | (uint)config.DupOptions | DbLibConstants.MDB_DUPSORT /* to make sure */);
            lock (rscLock) {
                lock (dbLock) {
                    var (dbi, handle, env) = OpenDatabaseInternal(name, options, config.LibCompare);
                    if (config.LibDupCompare != null) {
                        var ret = DbLib.mdb_set_dupsort(handle, dbi, config.LibDupCompare);
                        if (ret != DbRetCode.SUCCESS)
                            DbLib.mdb_dbi_close(env, dbi);
                        ErrorUtil.CheckRetCode(ret);
                    }

                    var result = new MultiValueDatabase(dbi, env, name, NewDatabaseDisposed, config);
                    newDatabases.Add(result);
                    return result;
                }
            }
        }

        void NewDatabaseDisposed(Database db) {
            lock (dbLock) {
                newDatabases.Remove(db);
            }
        }

        // part of Commit()
        protected override void Committed() {
            base.Committed();
            lock (dbLock) {
                foreach (var newDb in newDatabases) {
                    committedDatabases.Add(newDb.Name, newDb);
                    newDb.Disposed = committedDisposed;
                }
                newDatabases.Clear();
            }

        }

        // part of Dispose() / Abort()
        protected override void ReleaseManagedResources(bool forCommit = false) {
            base.ReleaseManagedResources(forCommit);
            if (!forCommit) {
                lock (dbLock) {
                    foreach (var newDb in newDatabases)
                        newDb.ClearHandle();
                }
            }
        }

        // part of Dispose() / Abort()
        protected override void Cleanup() {
            base.Cleanup();
            lock (dbLock) {
                newDatabases.Clear();
            }
        }
    }
}
