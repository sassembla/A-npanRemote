using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChanquoCore
{
    public class ChanquoThreadRunner : MonoBehaviour
    {
        public Hashtable update = new Hashtable();
        private object writeLock = new object();

        public void Add(string id, Action act, ThreadMode mode)
        {
            lock (writeLock)
            {
                switch (mode)
                {
                    case ThreadMode.OnUpdate:
                        update[id] = act;
                        break;
                    default:
                        throw new Exception("unsupported mode:" + mode);
                }
            }
        }

        public void Dispose(List<string> disposedActIds)
        {
            lock (writeLock)
            {
                // この部分はすべてのハンドラの処理を行う必要がある。
                var updateKeys = update.Keys.OfType<string>().ToArray();
                foreach (var key in updateKeys)
                {
                    if (disposedActIds.Contains(key))
                    {
                        update.Remove(key);
                    }
                }
            }
        }

        public void Update()
        {
            var keys = update.Keys.OfType<string>().ToArray();
            foreach (var key in keys)
            {
                var upd = (Action)update[key];
                upd?.Invoke();
            }
        }
    }
}