using System;
using System.Collections.Generic;
using System.Threading;

namespace RawDiskReadPOC
{
    /// <summary>Sometimes, computations involving allocation of <see cref="IPartitionClusterData"/> items
    /// can be convoluted. They may require several delegates and callbacks and maybe asynchronous calls in
    /// the future. Thus, it is not easy to define a disposal strategy that fit everyone needs. This class is
    /// a container that will accumulate such objects that are intended to be disposed at once. The top
    /// level caller is expected to create this object, then let involved methods add items to the object
    /// as they are bound to the computation. Later the top caller is expected to dispose the whole batch.
    /// This doesn't prevent intermediate methods to perform early disposal should they wish.</summary>
    /// <remarks>The asynchronous model is not implemented yet.</remarks>
    internal class PartitionDataDisposableBatch :
        // List<IPartitionClusterData>,
        IDisposable
    {
        private const int StorageCountAlert = 512;
        private bool _detached;
        private bool _disposing;
        private IPartitionClusterDataDisposedDelegate _dispositionHandler;
        private static object _globalLock = new object();
        private bool _inUse;
        private unsafe Dictionary<IPartitionClusterData, int> _storage =
            new Dictionary<IPartitionClusterData, int>();
        [ThreadStatic()]
        private static Stack<PartitionDataDisposableBatch> _threadStack = new Stack<PartitionDataDisposableBatch>();

        private PartitionDataDisposableBatch()
        {
            _dispositionHandler = HandlePartitionClusterDataDisposal;
            _inUse = true;
        }

        internal bool Detached
        {
            get { return _detached; }
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

        internal void Attach()
        {
            if (!_detached) {
                throw new InvalidOperationException();
            }
            _threadStack.Push(this);
            _detached = false;
        }

        internal static PartitionDataDisposableBatch CreateNew(bool detached = false)
        {
            PartitionDataDisposableBatch result = new PartitionDataDisposableBatch();
            result._detached = detached;
            if (!detached) {
                _threadStack.Push(result);
            }
            return result;
        }

        internal void Detach()
        {
            if (_detached) {
                throw new InvalidOperationException("Already detached.");
            }
            if (1 >= _threadStack.Count) {
                throw new InvalidOperationException("Can't detach last batch.");
            }
            if (!object.ReferenceEquals(_threadStack.Peek(), this)) {
                throw new InvalidOperationException("Can't detach non topmost batch.");
            }
            _threadStack.Pop();
            _detached = true;
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
            if (FeaturesContext.DataPoolChecksEnabled) {
                if (StorageCountAlert < _storage.Count) {
                    throw new ApplicationException();
                }
            }
        }

        private void HandlePartitionClusterDataDisposal(IPartitionClusterData disposed)
        {
            if (_disposing) { return; }
            if (!_storage.Remove(disposed)) {
                throw new ArgumentException();
            }
        }
    }
}
