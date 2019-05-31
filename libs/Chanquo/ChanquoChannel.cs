using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ChanquoCore
{
    public struct PullActAndId
    {
        public readonly Action pullAct;
        public readonly string id;
        public PullActAndId(Action pullAct, string id)
        {
            this.pullAct = pullAct;
            this.id = id;
        }
    }

    public class ChanquoChannel : IDisposable
    {
        private ConcurrentQueue<IChanquoBase> queue = new ConcurrentQueue<IChanquoBase>();
        private Hashtable selectActTable = new Hashtable();
        private Hashtable nonUnityThreadSelectActTable = new Hashtable();
        private Hashtable lastActTable = new Hashtable();
        private object actTableLock = new object();
        private readonly Action<List<string>> leftFromChanquo;

        public ChanquoChannel(Action<List<string>> leftFromChanquo)
        {
            this.leftFromChanquo = leftFromChanquo;
        }
        public void Send<T>(T data) where T : IChanquoBase, new()
        {
            queue.Enqueue(data);
            foreach (var id in nonUnityThreadSelectActTable)
            {
                ((Action)nonUnityThreadSelectActTable[id])?.Invoke();
            }
        }

        public T Dequeue<T>() where T : IChanquoBase, new()
        {
            if (queue.Count == 0)
            {
                return default(T);
            }

            if (disposedValue)
            {
                return default(T);
            }

            IChanquoBase result;
            queue.TryDequeue(out result);
            return (T)result;
        }

        public void Remove(string id)
        {
            lock (actTableLock)
            {
                if (selectActTable.ContainsKey(id))
                {
                    selectActTable.Remove(id);
                }

                if (nonUnityThreadSelectActTable.ContainsKey(id))
                {
                    nonUnityThreadSelectActTable.Remove(id);
                }

                if (lastActTable.ContainsKey(id))
                {
                    lastActTable.Remove(id);
                }
            }
        }

        public PullActAndId AddSelectAction<T>(ChanquoAction<T> selectAct) where T : IChanquoBase, new()
        {
            if (disposedValue)
            {
                return new PullActAndId();
            }

            var id = Guid.NewGuid().ToString();
            Action pullAct = () =>
            {
                var count = queue.Count;
                for (var i = 0; i < count; i++)
                {
                    selectAct.act(Dequeue<T>(), true);
                }
            };

            Action lastAct = () =>
            {
                selectAct.act(new T(), false);
            };

            lock (actTableLock)
            {
                selectActTable[id] = pullAct;
                lastActTable[id] = lastAct;
            }

            return new PullActAndId(pullAct, id);
        }

        public void AddNonUnityThreadSelectAct<T>(ChanquoAction<T> selectAct) where T : IChanquoBase, new()
        {
            var id = Guid.NewGuid().ToString();
            Action pullAct = () =>
            {
                var count = queue.Count;
                for (var i = 0; i < count; i++)
                {
                    selectAct.act(Dequeue<T>(), true);
                }
            };

            Action lastAct = () =>
            {
                selectAct.act(new T(), false);
            };

            lock (actTableLock)
            {
                nonUnityThreadSelectActTable[id] = pullAct;
                lastActTable[id] = lastAct;
            }
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    var pullActIds = new List<string>();
                    foreach (var key in lastActTable.Keys)
                    {
                        pullActIds.Add((string)key);
                    }

                    // これが実行されたら、もうこのchannelは停止する。登録されている全てのreceiverを消す。
                    lock (actTableLock)
                    {

                        // すべてのレシーバーに対してok = falseを送り出さないといけない。
                        foreach (var id in pullActIds)
                        {
                            if (selectActTable.ContainsKey(id))
                            {
                                selectActTable.Remove(id);
                            }

                            if (nonUnityThreadSelectActTable.ContainsKey(id))
                            {
                                nonUnityThreadSelectActTable.Remove(id);
                            }

                            ((Action)lastActTable[id])?.Invoke();
                            lastActTable.Remove(id);
                        }
                    }

                    // chanque自体の管理から抜ける
                    leftFromChanquo(pullActIds);

                    queue = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~ChanquoChannel() {
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