using Amib.Threading;

using System;

namespace AsmDude2LS
{
    public class AsmThreadPool
    {
        public readonly SmartThreadPool threadPool_;

        private static readonly Lazy<AsmThreadPool> Lazy = new(() => new AsmThreadPool());

        public static AsmThreadPool Instance { get { return Lazy.Value; } }

        private AsmThreadPool()
        {
            //ThreadHelper.ThrowIfNotOnUIThread();
            this.threadPool_ = new SmartThreadPool();
        }

        #region IDisposable Support
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AsmThreadPool()
        {
            this.Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                this.threadPool_.Dispose();
            }
            // free native resources if there are any.
        }

        #endregion
    }
}
