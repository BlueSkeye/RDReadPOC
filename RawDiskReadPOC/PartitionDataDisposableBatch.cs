﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace RawDiskReadPOC
{
    /// <summary>Sometimes, computations  involving allocation of <see cref="IPartitionClusterData"/> items
    /// can be convoluted, involving several delegates and callbacks and maybe asynchronous calls in the
    /// future. Thus, it is not easy to define a disposal strategy that fit everyone needs. This class is
    /// a container that will accumulate such objects that are intended to be disposed at once. The top
    /// level caller is expected to create this object, then let involved methods add items to the object
    /// as they are bound to the computation. Later the top caller is expected to dispose the whole batch.
    /// This doesn't prevent intermediate methods to perform early disposal should they wish.</summary>
    /// <remarks>The asynchronous model is not implemented yet.</remarks>
    internal class PartitionDataDisposableBatch :
        // List<IPartitionClusterData>,
        IDisposable
    {
        private PartitionDataDisposableBatch()
        {
            _dispositionHandler = HandlePartitionClusterDataDisposal;
            _inUse = true;
        }

        /// <summary>For debugging purpose. Could be unused.</summary>
        internal unsafe void AssertConsistency()
        {
            foreach(IPartitionClusterData item in _storage.Keys) {
                if (null == item.Data) {
                    throw new ApplicationException();
                }
            }
        }

        internal static PartitionDataDisposableBatch CreateNew()
        {
            PartitionDataDisposableBatch result = new PartitionDataDisposableBatch();
            _threadStack.Push(result);
            return result;
        }

        public void Dispose()
        {
            PartitionDataDisposableBatch candidate = _threadStack.Peek();
            if (!object.ReferenceEquals(candidate, this)) {
                throw new ApplicationException();
            }
            _threadStack.Pop();
            _disposing = true;
            foreach (IPartitionClusterData item in _storage.Keys) {
                item.Dispose();
            }
            _storage = null;
            _inUse = false;
        }

        /// <summary>This method is a shortcut. It should be invoked in context where the calller is sure
        /// there is already a batch available, otherwise the call will fail.</summary>
        /// <returns></returns>
        internal static PartitionDataDisposableBatch GetCurrent()
        {
            if (0 == _threadStack.Count) {
                throw new InvalidOperationException();
            }
            return _threadStack.Peek();
        }

        internal static PartitionDataDisposableBatch GetCurrent(out bool owner)
        {
            if (0 == _threadStack.Count) {
                owner = true;
                return CreateNew();
            }
            owner = false;
            return _threadStack.Peek();
        }

        internal void Register(IPartitionClusterData data)
        {
            if (null == data) {
                throw new ArgumentNullException();
            }
            data.Disposed += _dispositionHandler;
            _storage.Add(data, 0);
        }

        private void HandlePartitionClusterDataDisposal(IPartitionClusterData disposed)
        {
            if (!_storage.ContainsKey(disposed)) {
                throw new ArgumentException();
            }
            if (_disposing) { return; }
            _storage.Remove(disposed);
        }

        private bool _disposing;
        private IPartitionClusterDataDisposedDelegate _dispositionHandler;
        private static object _globalLock = new object();
        private bool _inUse;
        private unsafe Dictionary<IPartitionClusterData, int> _storage =
            new Dictionary<IPartitionClusterData, int>();
        [ThreadStatic()]
        private static Stack<PartitionDataDisposableBatch> _threadStack = new Stack<PartitionDataDisposableBatch>();
    }
}