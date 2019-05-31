using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ChanquoCore
{
    public class Chanquo
    {
        private static Chanquo _chanq;

        private Hashtable channelTable = new Hashtable();
        private GameObject go = new GameObject("ChanquoThreadRunner");
        private ChanquoThreadRunner runner = null;
        private Thread unityThread;
        private object channelWriteLock = new object();

        private ChanquoChannel AddChannel<T>() where T : IChanquoBase, new()
        {
            var key = typeof(T);
            if (channelTable.ContainsKey(key))
            {
                return (ChanquoChannel)channelTable[key];
            }

            // generate new channel.
            var chan = new ChanquoChannel(
                actIds =>
                {
                    lock (channelWriteLock)
                    {
                        channelTable.Remove(typeof(T));
                    }

                    runner.Dispose(actIds);
                }
            );

            lock (channelWriteLock)
            {
                channelTable[typeof(T)] = chan;
            }
            return chan;
        }

        private ChanquoAction<T> AddReceiver<T>(Action<T, bool> act, ThreadMode mode = ThreadMode.Default) where T : IChanquoBase, new()
        {
            // ここで関連付けを行う。
            ChanquoChannel chan;

            var key = typeof(T);
            if (channelTable.ContainsKey(key))
            {
                chan = (ChanquoChannel)channelTable[key];
            }
            else
            {
                chan = new ChanquoChannel(
                    actIds =>
                    {
                        lock (channelWriteLock)
                        {
                            channelTable.Remove(typeof(T));
                        }

                        runner.Dispose(actIds);
                    }
                );

                lock (channelWriteLock)
                {
                    channelTable[typeof(T)] = chan;
                }
            }

            var chanquoAct = new ChanquoAction<T>(act);
            var pullActAndId = chan.AddSelectAction<T>(chanquoAct);
            chanquoAct.SetOnDispose(() =>
            {
                chan.Remove(pullActAndId.id);
                runner.Dispose(new List<string> { pullActAndId.id });
            });

            if (Thread.CurrentThread == unityThread)
            {
                switch (mode)
                {
                    case ThreadMode.Default:
                        runner.Add(pullActAndId.id, pullActAndId.pullAct, ThreadMode.OnUpdate);
                        break;
                    default:
                        runner.Add(pullActAndId.id, pullActAndId.pullAct, mode);
                        break;
                }
            }
            else
            {
                // receiver is not running on the UnityThread.
                chan.AddNonUnityThreadSelectAct(chanquoAct);

                Task.Delay(TimeSpan.FromTicks(1)).ContinueWith(o =>
                {
                    T s;
                    while ((s = chan.Dequeue<T>()) != null)
                    {
                        chanquoAct.act?.Invoke(s, true);
                    }
                });
            }

            return chanquoAct;
        }

        // GameObjectの制約の関係で、初期化はMainThreadからしかできなそう。
        static Chanquo()
        {
            _chanq = new Chanquo();
            _chanq.runner = _chanq.go.AddComponent<ChanquoThreadRunner>();
            GameObject.DontDestroyOnLoad(_chanq.go);
            _chanq.unityThread = Thread.CurrentThread;
        }


        public static ChanquoChannel MakeChannel<T>() where T : IChanquoBase, new()
        {
            return _chanq.AddChannel<T>();
        }


        public static T Receive<T>() where T : IChanquoBase, new()
        {
            if (_chanq.channelTable.ContainsKey(typeof(T)))
            {
                var chan = (ChanquoChannel)_chanq.channelTable[typeof(T)];
                return chan.Dequeue<T>();
            }

            var newChan = _chanq.AddChannel<T>();
            return newChan.Dequeue<T>();// return null val.
        }

        public static ChanquoAction<T1> Select<T1>(Action<T1, bool> act, ThreadMode mode = ThreadMode.Default) where T1 : IChanquoBase, new()
        {
            return _chanq.AddReceiver(act, mode);
        }

        public static ChanquoAction<T1, T2> Select<T1, T2>(Action<T1, bool> act1, Action<T2, bool> act2, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            return new ChanquoAction<T1, T2>(cAct1, cAct2);
        }

        public static ChanquoAction<T1, T2, T3> Select<T1, T2, T3>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            return new ChanquoAction<T1, T2, T3>(cAct1, cAct2, cAct3);
        }

        public static ChanquoAction<T1, T2, T3, T4> Select<T1, T2, T3, T4>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, Action<T4, bool> act4, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            var cAct4 = _chanq.AddReceiver(act4, mode);
            return new ChanquoAction<T1, T2, T3, T4>(cAct1, cAct2, cAct3, cAct4);
        }

        public static ChanquoAction<T1, T2, T3, T4, T5> Select<T1, T2, T3, T4, T5>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, Action<T4, bool> act4, Action<T5, bool> act5, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            var cAct4 = _chanq.AddReceiver(act4, mode);
            var cAct5 = _chanq.AddReceiver(act5, mode);
            return new ChanquoAction<T1, T2, T3, T4, T5>(cAct1, cAct2, cAct3, cAct4, cAct5);
        }

        public static ChanquoAction<T1, T2, T3, T4, T5, T6> Select<T1, T2, T3, T4, T5, T6>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, Action<T4, bool> act4, Action<T5, bool> act5, Action<T6, bool> act6, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
        where T6 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            var cAct4 = _chanq.AddReceiver(act4, mode);
            var cAct5 = _chanq.AddReceiver(act5, mode);
            var cAct6 = _chanq.AddReceiver(act6, mode);
            return new ChanquoAction<T1, T2, T3, T4, T5, T6>(cAct1, cAct2, cAct3, cAct4, cAct5, cAct6);
        }

        public static ChanquoAction<T1, T2, T3, T4, T5, T6, T7> Select<T1, T2, T3, T4, T5, T6, T7>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, Action<T4, bool> act4, Action<T5, bool> act5, Action<T6, bool> act6, Action<T7, bool> act7, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
        where T6 : IChanquoBase, new()
        where T7 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            var cAct4 = _chanq.AddReceiver(act4, mode);
            var cAct5 = _chanq.AddReceiver(act5, mode);
            var cAct6 = _chanq.AddReceiver(act6, mode);
            var cAct7 = _chanq.AddReceiver(act7, mode);
            return new ChanquoAction<T1, T2, T3, T4, T5, T6, T7>(cAct1, cAct2, cAct3, cAct4, cAct5, cAct6, cAct7);
        }

        public static ChanquoAction<T1, T2, T3, T4, T5, T6, T7, T8> Select<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, bool> act1, Action<T2, bool> act2, Action<T3, bool> act3, Action<T4, bool> act4, Action<T5, bool> act5, Action<T6, bool> act6, Action<T7, bool> act7, Action<T8, bool> act8, ThreadMode mode = ThreadMode.Default)
        where T1 : IChanquoBase, new()
        where T2 : IChanquoBase, new()
        where T3 : IChanquoBase, new()
        where T4 : IChanquoBase, new()
        where T5 : IChanquoBase, new()
        where T6 : IChanquoBase, new()
        where T7 : IChanquoBase, new()
        where T8 : IChanquoBase, new()
        {
            var cAct1 = _chanq.AddReceiver(act1, mode);
            var cAct2 = _chanq.AddReceiver(act2, mode);
            var cAct3 = _chanq.AddReceiver(act3, mode);
            var cAct4 = _chanq.AddReceiver(act4, mode);
            var cAct5 = _chanq.AddReceiver(act5, mode);
            var cAct6 = _chanq.AddReceiver(act6, mode);
            var cAct7 = _chanq.AddReceiver(act7, mode);
            var cAct8 = _chanq.AddReceiver(act8, mode);
            return new ChanquoAction<T1, T2, T3, T4, T5, T6, T7, T8>(cAct1, cAct2, cAct3, cAct4, cAct5, cAct6, cAct7, cAct8);
        }
    }
}