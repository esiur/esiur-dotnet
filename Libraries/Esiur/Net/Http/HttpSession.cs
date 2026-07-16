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

namespace Esiur.Net.Http;
public class HttpSession : IDestructible //<T> where T : TClient
{
    public delegate void SessionModifiedEvent(HttpSession session, string key, object oldValue, object newValue);
    public delegate void SessionEndedEvent(HttpSession session);

    private string id;
    private Timer timer;
    private int timeout;
    private readonly object timerLock = new object();
    private long timerGeneration;
    private bool destroyed;
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

    public HttpSession()
    {
        variables = new KeyList<string, object>();
        variables.OnModified += new KeyList<string, object>.Modified(VariablesModified);
        creation = DateTime.Now;
        lastAction = creation;
    }

    internal void Set(string id, int timeout)
    {
        //modified = sessionModifiedEvent;
        //ended = sessionEndEvent;
        if (timeout < 0)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        lock (timerLock)
        {
            if (destroyed)
                throw new ObjectDisposedException(nameof(HttpSession));

            this.id = id;
            this.timeout = timeout;
            creation = DateTime.Now;
            lastAction = creation;
            ScheduleTimerLocked();
        }
    }

    private void OnSessionEndTimerCallback(object o)
    {
        SessionEndedEvent onEnd;

        lock (timerLock)
        {
            if (destroyed || !(o is long generation) || generation != timerGeneration)
                return;

            timer?.Dispose();
            timer = null;
            onEnd = OnEnd;
        }

        onEnd?.Invoke(this);
    }

    void VariablesModified(string key, object oldValue, object newValue, KeyList<string, object> sender)
    {
        OnModify?.Invoke(this, key, oldValue, newValue);
    }

    public void Destroy()
    {
        DestroyedEvent onDestroy;

        lock (timerLock)
        {
            if (destroyed)
                return;

            destroyed = true;
            timerGeneration++;
            timer?.Dispose();
            timer = null;
            variables.OnModified -= VariablesModified;
            onDestroy = OnDestroy;
            OnDestroy = null;
            OnEnd = null;
            OnModify = null;
        }

        onDestroy?.Invoke(this);
    }

    internal void Refresh()
    {
        lock (timerLock)
        {
            if (destroyed)
                return;

            lastAction = DateTime.Now;
            ScheduleTimerLocked();
        }
    }

    private void ScheduleTimerLocked()
    {
        timerGeneration++;
        timer?.Dispose();
        timer = null;

        if (timeout <= 0)
            return;

        var generation = timerGeneration;
        timer = new Timer(
            OnSessionEndTimerCallback,
            generation,
            TimeSpan.FromSeconds(timeout),
            System.Threading.Timeout.InfiniteTimeSpan);
    }

    public int Timeout // Seconds
    {
        get
        {
            return timeout;
        }
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            lock (timerLock)
            {
                if (destroyed)
                    return;

                timeout = value;
                lastAction = DateTime.Now;
                ScheduleTimerLocked();
            }
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

    internal bool IsDestroyed
    {
        get
        {
            lock (timerLock)
                return destroyed;
        }
    }
}

