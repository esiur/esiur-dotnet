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

namespace Esiur.Data
{

    public class StringKeyList : IEnumerable<KeyValuePair<string, string>>
    {
        
        //private List<string> m_keys = new List<string>();
        //private List<string> m_values = new List<string>();

        private List<KeyValuePair<string, string>> m_Variables = new List<KeyValuePair<string, string>>();

        private bool allowMultiple;

        public delegate void Modified(string Key, string NewValue);
        public event Modified OnModified;

        public StringKeyList(bool AllowMultipleValues = false)
        {
            allowMultiple = AllowMultipleValues;
        }

        public void Add(string Key, string Value)
        {
            if (OnModified != null)
                OnModified(Key, Value);

            var key = Key.ToLower();

            if (!allowMultiple)
            {
                foreach(var kv in m_Variables)
                {
                    if (kv.Key.ToLower() == key)
                    {
                        m_Variables.Remove(kv);
                        break;
                    }
                }
            }

            m_Variables.Add(new KeyValuePair<string, string>(Key, Value));
        }

        public string this[string Key]
        {
            get
            {
                var key = Key.ToLower();
                foreach (var kv in m_Variables)
                    if (kv.Key.ToLower() == key)
                        return kv.Value;

                return null;
            }
            set
            {
                var key = Key.ToLower();

                var toRemove = m_Variables.Where(x => x.Key.ToLower() == key);

                foreach (var item in toRemove)
                    m_Variables.Remove(item);


                m_Variables.Add(new KeyValuePair<string, string>(Key, value));

                OnModified?.Invoke(Key, value);

            }
        }

        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            //return m_keys.GetEnumerator();
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

        /*
        public string[] Keys
        {
            get 
            { 
                return m_keys.ToArray();
            }
        }


        //public Dictionary<string, string>.ValueCollection Values
        public string[] Values
        {
            get 
            { 
                //return m_Variables.Values; 
                return m_values.ToArray();
            }
        }
        */

        public List<string> GetValues(string Key)
        {
            var key = Key.ToLower();

            List<string> values = new List<string>();

            foreach (var kv in m_Variables)
                if (kv.Key.ToLower() == key)
                    values.Add(kv.Value);
            
            return values;
        }

        public void RemoveAll(string Key)
        {
            while (Remove(Key)){}
        }

        public bool Remove(string Key)
        {
            var key = Key.ToLower();

            foreach(var kv in m_Variables)
            {
                if (kv.Key.ToLower() == key)
                {
                    if (OnModified != null)
                        OnModified(Key, null);
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
        
        public bool ContainsKey(string Key)
        {
            var key = Key.ToLower();
            foreach (var kv in m_Variables)
                if (kv.Key.ToLower() == key)
                    return true;
            return false;
        }

        /*
        public bool ContainsKey(string Key)
        {
            //return m_Variables.ContainsKey(Key);
            return m_keys.Contains(Key.ToLower());
        }
         */

        public bool ContainsValue(string Value)
        {
            var value = Value.ToLower();
            foreach (var kv in m_Variables)
                if (kv.Value.ToLower() == value)
                    return true;
            return false;
        }

        //internal KeyList()
        //{
        //    m_Session = Session;
        //    m_Server = Server;
        //}

    }
}