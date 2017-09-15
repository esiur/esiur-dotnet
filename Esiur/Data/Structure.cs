using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Engine;

namespace Esiur.Data
{
    public class Structure : IEnumerable<KeyValuePair<string, object>>
    {

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
                rt += kv.Key + ": " + kv.Value.ToString() + "\r\n";

            return rt.TrimEnd('\r', '\n');
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

        public string[] GetKeys()
        {
            return dic.Keys.ToArray();
        }

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
