/*
 
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Core;
using System.Reflection;
using System.Dynamic;

namespace Esiur.Data;

//public class Map : IEnumerable<KeyValuePair<object, object>>
//{
//    private Dictionary<object, object> dic = new();

//    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
//    {
//        return dic.GetEnumerator();
//    }

//    IEnumerator IEnumerable.GetEnumerator()
//    {
//        return dic.GetEnumerator();
//    }
//}

public interface IMap
{
    public void Add(object key, object value);
    public void Remove(object key);
    public void Clear();
    public bool ContainsKey(object key);
    public object[] Serialize();
}

public class Map<KT, VT> : IEnumerable<KeyValuePair<KT, VT>>, IMap
{

    private Dictionary<KT, VT> dic = new Dictionary<KT, VT>();
    private object syncRoot = new object();

    // Change map types
    public Map<NewKeyType, NewValueType> Select<NewKeyType, NewValueType>
                                        (Func<KeyValuePair<KT, VT>, KeyValuePair<NewKeyType, NewValueType>> selector)
    {
        var rt = new Map<NewKeyType, NewValueType>();
        foreach (var kv in dic)
        {
            var nt = selector(kv);
            rt.dic.Add(nt.Key, nt.Value);
        }

        return rt;
    }

    public bool ContainsKey(KT key)
    {
        return dic.ContainsKey(key);
    }

    public override string ToString()
    {
        var rt = "";
        foreach (var kv in dic)
            rt += kv.Key + ": " + kv.Value.ToString() + " \r\n";

        return rt.TrimEnd('\r', '\n');
    }

    public Map(Map<KT, VT> source)
    {
        dic = source.dic;
    }
    public Map()
    {

    }

    public static Map<KT, VT> FromMap(Map<KT, VT> source, Type destinationType)
    {
        var rt = Activator.CreateInstance(destinationType) as Map<KT, VT>;
        rt.dic = source.dic;
        return rt;
    }

    //public static T FromStructure<T>(Map<KT, VT> source) where T : Map<KT, VT>
    //{
    //    var rt = Activator.CreateInstance<T>();
    //    rt.dic = source.dic;
    //    return rt;
    //}

    // public static explicit operator Map<string, object>(ExpandoObject obj) => FromDynamic(obj);

    public static Map<string, object> FromDynamic(ExpandoObject obj)
    {
        var rt = new Map<string, object>();
        foreach (var kv in obj)
            rt[kv.Key] = kv.Value;
        return rt;
    }

    public static Map<KT, VT> FromDictionary(Dictionary<KT, VT> source)
    {
        var rt = new Map<KT, VT>();
        rt.dic = source;
        return rt;
    }

    public static Map<string, object> FromObject(object obj)
    {
        var type = obj.GetType();

        var st = new Map<string, object>();

        var pi = type.GetTypeInfo().GetProperties().Where(x => x.CanRead);
        foreach (var p in pi)
            st[p.Name] = p.GetValue(obj);

        var fi = type.GetTypeInfo().GetFields().Where(x => x.IsPublic);
        foreach (var f in fi)
            st[f.Name] = f.GetValue(obj);

        return st;


        //    if (obj is Structure)
        //        return obj as Structure;
        //    else //if (Codec.IsAnonymous(type))
        //    {
        //        var st = new Structure();

        //        var pi = type.GetTypeInfo().GetProperties().Where(x => x.CanRead);
        //        foreach (var p in pi)
        //            st[p.Name] = p.GetValue(obj);

        //        var fi = type.GetTypeInfo().GetFields().Where(x => x.IsPublic);
        //        foreach (var f in fi)
        //            st[f.Name] = f.GetValue(obj);

        //        return st;
        //    }
        //    //else
        //    //  return null;
    }

    public IEnumerator<KeyValuePair<KT, VT>> GetEnumerator()
    {
        return dic.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return dic.GetEnumerator();
    }

    public int Length
    {
        get { return dic.Count; }
    }

    public KeyValuePair<KT, VT> At(int index)
    {
        return dic.ElementAt(index);
    }

    public object SyncRoot
    {
        get { return syncRoot; }
    }

    public KT[] GetKeys() => dic.Keys.ToArray();//GetKeys()
                                                //{
                                                //  return dic.Keys.ToArray();
                                                //}

    public void Add(KT key, VT value)
    {
        if (dic.ContainsKey(key))
            dic[key] = value;
        else
            dic.Add(key, value);
    }

    public void Add(object key, object value)
    {
        Add((KT)key, (VT)value);
    }

    public void Remove(object key)
    {
        Remove((KT)key);
    }

    public void Clear()
    {
        dic.Clear();
    }

    public bool ContainsKey(object key)
    {
        return ContainsKey((KT)key);
    }

    public object[] Serialize()
    {
        var rt = new List<object>();
        foreach (var kv in dic)
        {
            rt.Add(kv.Key);
            rt.Add(kv.Value);
        }

        return rt.ToArray();
    }

    public VT this[KT index]
    {
        get
        {
            if (dic.ContainsKey(index))
                return dic[index];
            else
                return default;
        }
        set
        {
            if (dic.ContainsKey(index))
                dic[index] = value;
            else
                dic.Add(index, value);
        }
    }

}
