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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Linq;

namespace Esiur.Data;


public class StringKeyList : IEnumerable<KeyValuePair<string, string>>
{

    private List<KeyValuePair<string, string>> m_Variables = new List<KeyValuePair<string, string>>();

    private bool allowMultiple;

    public delegate void Modified(string key, string newValue);
    public event Modified OnModified;

    public StringKeyList(bool allowMultipleValues = false)
    {
        allowMultiple = allowMultipleValues;
    }

    public void Add(string key, string value)
    {
        if (OnModified != null)
            OnModified(key, value);

        key = key.ToLower();

        if (!allowMultiple)
        {
            foreach (var kv in m_Variables)
            {
                if (kv.Key.ToLower() == key)
                {
                    m_Variables.Remove(kv);
                    break;
                }
            }
        }

        m_Variables.Add(new KeyValuePair<string, string>(key, value));
    }

    public string this[string key]
    {
        get
        {
            key = key.ToLower();
            foreach (var kv in m_Variables)
                if (kv.Key.ToLower() == key)
                    return kv.Value;

            return null;
        }
        set
        {
            key = key.ToLower();

            var toRemove = m_Variables.Where(x => x.Key.ToLower() == key).ToArray();

            foreach (var item in toRemove)
                m_Variables.Remove(item);


            m_Variables.Add(new KeyValuePair<string, string>(key, value));

            OnModified?.Invoke(key, value);

        }
    }

    IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
    {
        return m_Variables.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return m_Variables.GetEnumerator();
    }

    public void Clear()
    {
        if (OnModified != null)
            OnModified(null, null);

        m_Variables.Clear();

    }


    public List<string> GetValues(string Key)
    {
        var key = Key.ToLower();

        List<string> values = new List<string>();

        foreach (var kv in m_Variables)
            if (kv.Key.ToLower() == key)
                values.Add(kv.Value);

        return values;
    }

    public void RemoveAll(string key)
    {
        while (Remove(key)) { }
    }

    public bool Remove(string key)
    {
        key = key.ToLower();

        foreach (var kv in m_Variables)
        {
            if (kv.Key.ToLower() == key)
            {
                if (OnModified != null)
                    OnModified(key, null);
                m_Variables.Remove(kv);
                return true;
            }
        }

        return false;
    }

    public int Count
    {
        get { return m_Variables.Count; }
    }

    public bool ContainsKey(string key)
    {
        key = key.ToLower();
        foreach (var kv in m_Variables)
            if (kv.Key.ToLower() == key)
                return true;
        return false;
    }


    public bool ContainsValue(string value)
    {
        value = value.ToLower();
        foreach (var kv in m_Variables)
            if (kv.Value.ToLower() == value)
                return true;
        return false;
    }


}