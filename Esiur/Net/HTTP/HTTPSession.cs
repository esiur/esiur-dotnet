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
using Esiur.Engine;

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

        void VariablesModified(string key, object oldValue, object newValue)
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