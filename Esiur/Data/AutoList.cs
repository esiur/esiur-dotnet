﻿/*
 
Copyright (c) 2017 Ahmed Kh. Zamil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Esiur.Core;
using System.Reflection;

namespace Esiur.Data;

public class AutoList<T, ST> : IEnumerable<T>, ICollection, ICollection<T>
{

    private readonly object syncRoot = new object();
    private List<T> list = new List<T>();

    public delegate void Modified(ST sender, int index, T oldValue, T newValue);
    public delegate void Added(ST sender, T value);
    public delegate void Removed(ST sender, T value);
    public delegate void Cleared(ST sender);


    public event Modified OnModified;
    public event Removed OnRemoved;
    public event Cleared OnCleared;
    public event Added OnAdd;

    bool removableList;


    public ST State { get; set; }
    /*
    IOrderedEnumerable<T> OrderBy<T, TK>(Func<T, TK> keySelector)
    {
        return list.OrderBy<T,TK>(keySelector);
    }
    */

    public void Sort()
    {
        lock (syncRoot)
            list.Sort();
    }

    public void Sort(IComparer<T> comparer)
    {
        lock (syncRoot)
            list.Sort(comparer);
    }

    public void Sort(Comparison<T> comparison)
    {
        lock (syncRoot)
            list.Sort(comparison);
    }

    public IEnumerable<T> Where(Func<T, bool> predicate)
    {
        return list.Where(predicate);
    }

    /// <summary>
    /// Convert AutoList to array
    /// </summary>
    /// <returns>Array</returns>
    public T[] ToArray()
    {
        //    list.OrderBy()
        return list.ToArray();
    }

    /// <summary>
    /// Create a new instance of AutoList
    /// </summary>
    /// <param name="state">State object to be included when an event is raised.</param>
    public AutoList(ST state = default(ST))
    {
        State = state;
#if NETSTANDARD
        removableList = (typeof(IDestructible).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()));
#else
                        removableList = (typeof(IDestructible).IsAssignableFrom(typeof(T)));
#endif
    }

    /// <summary>
    /// Create a new instance of AutoList
    /// </summary>
    /// <param name="values">Populate the list with items</param>
    /// <returns></returns>
    public AutoList(ST state, T[] values)
    {
        State = state;
#if NETSTANDARD
        removableList = (typeof(IDestructible).GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()));
#else
                                        removableList = (typeof(IDestructible).IsAssignableFrom(typeof(T)));
#endif

        AddRange(values);
    }

    /// <summary>
    /// Synchronization lock of the list
    /// </summary>
    public object SyncRoot
    {
        get
        {
            return syncRoot;
        }
    }

    /// <summary>
    /// First item in the list
    /// </summary>
    public T First()
    {
        return list.First();
    }

    /// <summary>
    /// Get an item at a specified index
    /// </summary>
    public T this[int index]
    {
        get
        {
            return list[index];
        }
        set
        {
            var oldValue = list[index];

            if (removableList)
            {
                if (oldValue != null)
                    ((IDestructible)oldValue).OnDestroy -= ItemDestroyed;
                if (value != null)
                    ((IDestructible)value).OnDestroy += ItemDestroyed;
            }

            lock (syncRoot)
                list[index] = value;

            OnModified?.Invoke(State, index, oldValue, value);
        }
    }

    /// <summary>
    /// Add item to the list
    /// </summary>
    public void Add(T value)
    {
        if (removableList)
            if (value != null)
                ((IDestructible)value).OnDestroy += ItemDestroyed;

        lock (syncRoot)
            list.Add(value);

        OnAdd?.Invoke(State, value);
    }

    /// <summary>
    /// Add an array of items to the list
    /// </summary>
    public void AddRange(T[] values)
    {
        foreach (var v in values)
            Add(v);
    }

    private void ItemDestroyed(object sender)
    {
        Remove((T)sender);
    }

    /// <summary>
    /// Clear the list
    /// </summary>
    public void Clear()
    {
        if (removableList)
            foreach (IDestructible v in list)
                if (v != null)
                    v.OnDestroy -= ItemDestroyed;

        lock (syncRoot)
            list.Clear();

        OnCleared?.Invoke(State);
    }

    /// <summary>
    /// Remove an item from the list
    /// <param name="value">Item to remove</param>
    /// </summary>
    public bool Remove(T value)
    {
        if (!list.Contains(value))
            return false;

        if (removableList)
            if (value != null)
                ((IDestructible)value).OnDestroy -= ItemDestroyed;

        lock (syncRoot)
            list.Remove(value);

        OnRemoved?.Invoke(State, value);

        return true;
    }

    /// <summary>
    /// Number of items in the list
    /// </summary>
    public int Count
    {
        get { return list.Count; }
    }

    public bool IsSynchronized => (list as ICollection).IsSynchronized;

    public bool IsReadOnly => false;


    /// <summary>
    /// Check if an item exists in the list
    /// </summary>
    /// <param name="value">Item to check if exists</param>
    public bool Contains(T value)
    {
        return list.Contains(value);
    }

    /// <summary>
    /// Check if any item of the given array is in the list
    /// </summary>
    /// <param name="values">Array of items</param>
    public bool ContainsAny(T[] values)
    {
        foreach (var v in values)
            if (list.Contains(v))
                return true;
        return false;
    }

    /// <summary>
    /// Check if any item of the given list is in the list
    /// </summary>
    /// <param name="values">List of items</param>
    public bool ContainsAny(AutoList<T, ST> values)
    {
        foreach (var v in values)
            if (list.Contains((T)v))
                return true;
        return false;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)list).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)list).GetEnumerator();
    }

    public void CopyTo(Array array, int index)
    {
        lock (syncRoot)
            (list as ICollection).CopyTo(array, index);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (syncRoot)
            list.CopyTo(array, arrayIndex);
    }

    //bool ICollection<T>.Remove(T item)
    //{
    //    lock(syncRoot)
    //        return Remove(item);
    //}
}
