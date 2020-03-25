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
using Esyur.Data;
using Esyur.Misc;
using Esyur.Core;
using System.Reflection;

namespace Esyur.Data
{
    public class Structure : IEnumerable<KeyValuePair<string, object>>
    {

        public struct StructureMetadata
        {
            public string[] Keys;
            public DataType[] Types;
        }

        private Dictionary<string, object> dic = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private object syncRoot = new object();


        public bool ContainsKey(string key)
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

        public Structure(Structure source)
        {
            dic = source.dic;
        }
        public Structure()
        {

        }

        public static Structure FromStructure(Structure source, Type destinationType)
        {
            var rt = Activator.CreateInstance(destinationType) as Structure;
            rt.dic = source.dic;
            return rt;
        }

        public static T FromStructure<T>(Structure source) where T : Structure
        {
            var rt = Activator.CreateInstance<T>();
            rt.dic = source.dic;
            return rt;
        }

        public static Structure FromObject(object obj)
        {
            var type = obj.GetType();

            if (obj is Structure)
                return obj as Structure;
            else //if (Codec.IsAnonymous(type))
            {
                var st = new Structure();

                var pi = type.GetTypeInfo().GetProperties().Where(x=>x.CanRead);
                foreach (var p in pi)
                    st[p.Name] = p.GetValue(obj);

                return st;
            }
            //else
              //  return null;
        }
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
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

        public KeyValuePair<string, object> At(int index)
        {
            return dic.ElementAt(index);
        }

        public object SyncRoot
        {
            get { return syncRoot; }
        }

        public string[] GetKeys() => dic.Keys.ToArray();//GetKeys()
        //{
          //  return dic.Keys.ToArray();
        //}
        
        public object this[string index]
        {
            get
            {
                if (dic.ContainsKey(index))
                    return dic[index];
                else
                    return null;
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
}
