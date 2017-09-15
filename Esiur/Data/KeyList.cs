using System;
using System.IO;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Esiur.Engine;

namespace Esiur.Data
{

    public class KeyList<KT, T> : IEnumerable
    {
        private readonly object syncRoot = new object();
        private Dictionary<KT, T> dic;

        public delegate void Modified(KT key, T oldValue, T newValue);
        public delegate void Added(T value);
        public delegate void Removed(KT key, T value);
        public delegate void Cleared();

        public event Modified OnModified;
        public event Removed OnRemoved;
        public event Cleared OnCleared;
        public event Added OnAdd;

        bool removableList;

        public object SyncRoot
        {
            get
            {
                return syncRoot;
            }
        }

        public T Take(KT key)
        {
            if (dic.ContainsKey(key))
            {
                var v = dic[key];
                Remove(key);
                return v;
            }
            else
                return default(T);
        }

        public void Sort(Func<KeyValuePair<KT, T>, object> keySelector)
        {
            dic = dic.OrderBy(keySelector).ToDictionary(x => x.Key, x => x.Value);
        }

        public T[] ToArray()
        {
            var a = new T[Count];
            dic.Values.CopyTo(a, 0);
            return a;
        }

        public void Add(KT key, T value)
        {
            lock (syncRoot)
            {
                if (removableList)
                    if (value != null)
                        ((IDestructible)value).OnDestroy += ItemDestroyed;

                if (dic.ContainsKey(key))
                {
                    var oldValue = dic[key];
                    if (removableList)
                        if (oldValue != null)
                            ((IDestructible)oldValue).OnDestroy -= ItemDestroyed;
                    dic[key] = value;
                    if (OnModified != null)
                        OnModified(key, oldValue, value);
                }
                else
                {
                    dic.Add(key, value);

                    if (OnAdd != null)
                        OnAdd(value);
                }
            }
        }

        private void ItemDestroyed(object sender)
        {
            RemoveValue((T)sender);
        }

        public void RemoveValue(T value)
        {
            var toRemove = new List<KT>();
            foreach (var kv in dic)
                if (kv.Value.Equals(value))
                    toRemove.Add(kv.Key);

            foreach (var k in toRemove)
                Remove(k);
        }

        public T this[KT key]
        {
            get
            {
                if (dic.ContainsKey(key))
                    return dic[key];
                else
                    return default(T);
            }
            set
            {
                 Add(key, value);
            }
        }

        public IEnumerator GetEnumerator()
        {
            return dic.GetEnumerator();
        }

        public void Clear()
        {
            if (removableList)
                foreach (IDestructible v in dic.Values)
                    if (v != null)
                        v.OnDestroy -= ItemDestroyed;

            lock (syncRoot)
                dic.Clear();

            if (OnCleared != null)
                OnCleared();
        }

        public Dictionary<KT, T>.KeyCollection Keys
        {
            get { return dic.Keys; }
        }

        public Dictionary<KT, T>.ValueCollection Values
        {
            get
            {
                return dic.Values;
            }
        }

        public void Remove(KT key)
        {
            if (!dic.ContainsKey(key))
                return;

            var value = dic[key];

            if (removableList)
                if (value != null)
                    ((IDestructible)value).OnDestroy -= ItemDestroyed;

            lock (syncRoot)
                dic.Remove(key);

            if (OnRemoved != null)
                OnRemoved(key, value);
        }

        public int Count
        {
            get { return dic.Count; }
        }
        public bool Contains(KT Key)
        {
            return dic.ContainsKey(Key);
        }
        public bool ContainsKey(KT Key)
        {
            return dic.ContainsKey(Key);
        }
        public bool ContainsValue(T Value)
        {
            return dic.ContainsValue(Value);
        }

        public KeyList()
        {
            #if NETSTANDARD1_5
                        removableList = (typeof(IDestructible).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()));
            #else
                        removableList = (typeof(IDestructible).IsAssignableFrom(typeof(T)));
            #endif

            if (typeof(KT) == typeof(string))
                dic = (Dictionary<KT, T>)(object)new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            else
                dic = new Dictionary<KT, T>();
        }
    }
}