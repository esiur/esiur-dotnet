using Esiur.Core;
using Esiur.Resource;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Esiur.Data
{
    public class VarList<T> :  IEnumerable<T>, ICollection, ICollection<T>
    {
        string propertyName;
        IResource resource;

        List<T> list = new List<T>();

        public VarList(IResource resource, [CallerMemberName] string propertyName = "")
        {
            this.resource = resource;
            this.propertyName = propertyName;
        }

        public VarList()
        {

        }

        public int Count => list.Count;

        public bool IsReadOnly => false;

        public bool IsSynchronized => true;


        public object SyncRoot { get; } = new object();

        public void Add(T item)
        {
            list.Add(item);

            resource?.Instance?.Modified(propertyName);
        }

        public void Clear()
        {
            list.Clear();
            resource?.Instance?.Modified(propertyName);
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
                list.CopyTo(array, arrayIndex);
        }

        public void CopyTo(Array array, int index)
        {
            lock (SyncRoot)
                (list as ICollection).CopyTo(array, index);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public bool Remove(T item)
        {
            if ( list.Remove(item))
            {
                resource?.Instance?.Modified(propertyName);
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }


        public T this[int index]
        {
            get
            {
                return list[index];
            }
            set
            {
                //var oldValue = list[index];

                lock (SyncRoot)
                    list[index] = value;

                resource?.Instance?.Modified(propertyName);

            }
        }
    }
}
