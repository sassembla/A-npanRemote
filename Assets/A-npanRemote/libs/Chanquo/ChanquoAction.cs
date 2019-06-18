using System;

namespace ChanquoCore
{
    public class ChanquoAction<T1, T2> : IDisposable
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3> : IDisposable
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3, T4> : IDisposable
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;
        private ChanquoAction<T4> cAct4;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3, ChanquoAction<T4> cAct4)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
            this.cAct4 = cAct4;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                    cAct4.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3, T4, T5> : IDisposable
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;
        private ChanquoAction<T4> cAct4;
        private ChanquoAction<T5> cAct5;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3, ChanquoAction<T4> cAct4, ChanquoAction<T5> cAct5)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
            this.cAct4 = cAct4;
            this.cAct5 = cAct5;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                    cAct4.Dispose();
                    cAct5.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3, T4, T5, T6> : IDisposable
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
        where T6 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;
        private ChanquoAction<T4> cAct4;
        private ChanquoAction<T5> cAct5;
        private ChanquoAction<T6> cAct6;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3, ChanquoAction<T4> cAct4, ChanquoAction<T5> cAct5, ChanquoAction<T6> cAct6)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
            this.cAct4 = cAct4;
            this.cAct5 = cAct5;
            this.cAct6 = cAct6;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                    cAct4.Dispose();
                    cAct5.Dispose();
                    cAct6.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3, T4, T5, T6, T7> : IDisposable
            where T1 : IChanquoBase, new()
            where T2 : IChanquoBase, new()
            where T3 : IChanquoBase, new()
            where T4 : IChanquoBase, new()
            where T5 : IChanquoBase, new()
            where T6 : IChanquoBase, new()
            where T7 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;
        private ChanquoAction<T4> cAct4;
        private ChanquoAction<T5> cAct5;
        private ChanquoAction<T6> cAct6;
        private ChanquoAction<T7> cAct7;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3, ChanquoAction<T4> cAct4, ChanquoAction<T5> cAct5, ChanquoAction<T6> cAct6, ChanquoAction<T7> cAct7)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
            this.cAct4 = cAct4;
            this.cAct5 = cAct5;
            this.cAct6 = cAct6;
            this.cAct7 = cAct7;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                    cAct4.Dispose();
                    cAct5.Dispose();
                    cAct6.Dispose();
                    cAct7.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class ChanquoAction<T1, T2, T3, T4, T5, T6, T7, T8> : IDisposable
                where T1 : IChanquoBase, new()
                where T2 : IChanquoBase, new()
                where T3 : IChanquoBase, new()
                where T4 : IChanquoBase, new()
                where T5 : IChanquoBase, new()
                where T6 : IChanquoBase, new()
                where T7 : IChanquoBase, new()
                where T8 : IChanquoBase, new()
    {
        private ChanquoAction<T1> cAct1;
        private ChanquoAction<T2> cAct2;
        private ChanquoAction<T3> cAct3;
        private ChanquoAction<T4> cAct4;
        private ChanquoAction<T5> cAct5;
        private ChanquoAction<T6> cAct6;
        private ChanquoAction<T7> cAct7;
        private ChanquoAction<T8> cAct8;

        public ChanquoAction(ChanquoAction<T1> cAct1, ChanquoAction<T2> cAct2, ChanquoAction<T3> cAct3, ChanquoAction<T4> cAct4, ChanquoAction<T5> cAct5, ChanquoAction<T6> cAct6, ChanquoAction<T7> cAct7, ChanquoAction<T8> cAct8)
        {
            this.cAct1 = cAct1;
            this.cAct2 = cAct2;
            this.cAct3 = cAct3;
            this.cAct4 = cAct4;
            this.cAct5 = cAct5;
            this.cAct6 = cAct6;
            this.cAct7 = cAct7;
            this.cAct8 = cAct8;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cAct1.Dispose();
                    cAct2.Dispose();
                    cAct3.Dispose();
                    cAct4.Dispose();
                    cAct5.Dispose();
                    cAct6.Dispose();
                    cAct7.Dispose();
                    cAct8.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }


    public class ChanquoAction<T> : IDisposable where T : IChanquoBase, new()
    {
        // このインスタンスのactは、存在し続けるだけでChanquo本体から実行される。
        // disposeすると受け取る権利を失う。
        public readonly Action<T, bool> act;
        private Action onDispose;
        public ChanquoAction(Action<T, bool> act)
        {
            this.act = act;
        }

        public void SetOnDispose(Action onDispose)
        {
            this.onDispose = onDispose;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    onDispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoAction() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}