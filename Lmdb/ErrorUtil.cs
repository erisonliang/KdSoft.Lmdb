﻿using System.Runtime.CompilerServices;
using KdSoft.Lmdb.Interop;

namespace KdSoft.Lmdb
{
    /// <summary>
    /// Helpers for native error handling.
    /// </summary>
    public static class ErrorUtil
    {
        // keep in sync with user defined codes in DbRetCode
#pragma warning disable CA1825 // Avoid zero-length array allocations.
        static string[] dotNetStr = {
           "Item count for PutMultiple exceeds buffer size.",
        };
#pragma warning restore CA1825 // Avoid zero-length array allocations.

        public static string DotNetStr(DbRetCode ret) {
            if (ret > DotNetLowError && ret <= (DotNetLowError + dotNetStr.Length))
                return dotNetStr[(int)ret - (int)DotNetLowError - 1];
            else
                return UnknownStr(ret);
        }

        public static string UnknownStr(DbRetCode ret) {
            return $"Unknown LMDB error code: {ret}.";
        }

        public const DbRetCode MdbLowError = (DbRetCode)(-30800);
        public const DbRetCode DotNetLowError = (DbRetCode)(-41000);
        public const DbRetCode TooManyFixedItems = DotNetLowError + 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CheckRetCode(DbRetCode ret) {
            if (ret != DbRetCode.SUCCESS) {
                string errStr;
                if (ret > MdbLowError && ret <= DbRetCode.LAST_ERRCODE)
                    errStr = DbLib.mdb_strerror(ret);
                else
                    errStr = DotNetStr(ret);
                throw new LmdbException(ret, errStr);
            }
        }
    }
}
