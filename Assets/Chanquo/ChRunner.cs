using System;
using System.Collections;
using UnityEngine;

namespace Chanquo.v2
{
    public class ChRunner : MonoBehaviour
    {
        private object delayLock = new object();
        private readonly Hashtable typeChanTable = new Hashtable();

        internal void Add<T>(Action pullAct) where T : struct
        {
            lock (delayLock)
            {
                var type = typeof(T);
                typeChanTable[type] = pullAct;
            }
        }

        internal void Remove<T>() where T : struct
        {
            lock (delayLock)
            {
                var type = typeof(T);
                typeChanTable.Remove(type);
            }
        }

        private void Update()
        {
            foreach (var key in typeChanTable.Keys)
            {
                var pull = (Action)typeChanTable[(Type)key];
                pull?.Invoke();
            }
        }
    }
}