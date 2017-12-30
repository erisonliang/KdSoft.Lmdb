﻿using System;
using System.Runtime.InteropServices;

namespace KdSoft.Lmdb
{
    /// <summary>
    /// Statistics for a database or the environment.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = Compile.PackSize)]
    public struct Statistics
    {
        /// <summary>
        /// Size of a database page. This is currently the same for all databases.
        /// </summary>
        public readonly int PageSize;

        /// <summary>
        /// Depth (height) of the B-tree
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Number of internal (non-leaf) pages
        /// </summary>
        public readonly IntPtr BranchPages;

        /// <summary>
        /// Number of leaf pages
        /// </summary>
        public readonly IntPtr LeafPages;

        /// <summary>
        /// Number of overflow pages
        /// </summary>
        public readonly IntPtr OverflowPages;

        /// <summary>
        /// Number of data items
        /// </summary>
        public readonly IntPtr Entries;
    }
}