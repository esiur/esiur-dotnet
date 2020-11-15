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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net;
using System.Collections;
using System.Collections.Generic;
using Esiur.Data;
using Esiur.Misc;
using Esiur.Core;

namespace Esiur.Net.HTTP
{
    public class HTTPSession : IDestructible //<T> where T : TClient
    {
        public delegate void SessionModifiedEvent(HTTPSession session, string key, object oldValue, object newValue);
        public delegate void SessionEndedEvent(HTTPSession session);

        private string id;
        private Timer timer;
        private int timeout;
        DateTime creation;
        DateTime lastAction;

        private KeyList<string, object> variables;

        public event SessionEndedEvent OnEnd;
        public event SessionModifiedEvent OnModify;
        public event DestroyedEvent OnDestroy;

        public KeyList<string, object> Variables
        {
            get { return variables; }
        }

        public HTTPSession()
        {
            variables = new KeyList<string, object>();
            variables.OnModified += new KeyList<string, object>.Modified(VariablesModified);
            creation = DateTime.Now;
        }

        internal void Set(string id, int timeout)
        {
            //modified = sessionModifiedEvent;
            //ended = sessionEndEvent;
            this.id = id;

            if (this.timeout != 0)
            {
                this.timeout = timeout;
                timer = new Timer(OnSessionEndTimerCallback, null, TimeSpan.FromSeconds(timeout), TimeSpan.FromSeconds(0));
                creation = DateTime.Now;
            }
        }

        private void OnSessionEndTimerCallback(object o)
        {
            OnEnd?.Invoke(this);
        }

        void VariablesModified(string key, object oldValue, object newValue, KeyList<string, object> sender)
        {
            OnModify?.Invoke(this, key, oldValue, newValue);
        }

        public void Destroy()
        {
            OnDestroy?.Invoke(this);
            timer.Dispose();
            timer = null;
        }

        internal void Refresh()
        {
            lastAction = DateTime.Now;
            timer.Change(TimeSpan.FromSeconds(timeout), TimeSpan.FromSeconds(0));
        }

        public int Timeout // Seconds
        {
            get
            {
                return timeout;
            }
            set
            {
                timeout = value;
                Refresh();
            }
        }

        public string Id
        {
            get { return id; }
        }

        public DateTime LastAction
        {
            get { return lastAction; }
        }
    }

}