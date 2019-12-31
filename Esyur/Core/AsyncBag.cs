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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esyur.Core
{
    public class AsyncBag: AsyncReply
    {

        protected List<AsyncReply> replies = new List<AsyncReply>();
        List<object> results = new List<object>();

        int count = 0;
        bool sealedBag = false;

        
        public AsyncBag Then(Action<object[]> callback)
        {
            base.Then(new Action<object>(o => callback((object[])o)));
            return this;
        }

        public new AsyncAwaiter<object[]> GetAwaiter()
        {
            return new AsyncAwaiter<object[]>(this);
        }


        public void Seal()
        {
            if (sealedBag)
                return;

            sealedBag = true;

            if (results.Count == 0)
                Trigger(new object[0]);

            for (var i = 0; i < results.Count; i++)
            //foreach(var reply in results.Keys)
            {
                var k = replies[i];// results.Keys.ElementAt(i);
                var index = i;

                k.Then((r) =>
                {
                    results[index] = r;
                    count++;
                    if (count == results.Count)
                        Trigger(results.ToArray());
                });
            }
        }

        public void Add(AsyncReply reply)
        {
            if (!sealedBag)
            {
                results.Add(null);
                replies.Add(reply);
            }
        }

        public void AddBag(AsyncBag bag)
        {
            foreach (var r in bag.replies)
                Add(r);
        }

      

        public AsyncBag()
        {

        }

        public AsyncBag(object[] results)
            : base(results)
        {

        }

    }
}
