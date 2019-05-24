using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Test
{
    class MyObject : IResource
    {
        public Instance Instance { get; set; }

        public event DestroyedEvent OnDestroy;
        [ResourceEvent]
        public event ResourceEventHanlder LevelUp;
        [ResourceEvent]
        public event ResourceEventHanlder LevelDown;
        
        public void Destroy()
        {

        }
        public MyObject()
        {
            Info = new Structure();
            Info["size"] = 200;
            Info["age"] = 28;
            Info["name"] = "Esiur";
            Name = "Esiur Project";
            Level = 5;
        }

        public AsyncReply<bool> Trigger(ResourceTrigger trigger)
        {
            return new AsyncReply<bool>();
        }

        [ResourceFunction]
        public int Add(int value)
        {
            Level += value;
            LevelUp?.Invoke(null, "going up", value);
            return Level;
        }

        [ResourceFunction]
        public int Subtract(int value)
        {
            Level -= value;
            LevelDown?.Invoke(null, "going down", value);
            return Level;
        }

        [ResourceFunction]
        public IEnumerable<string> Enum(int count)
        {
            var msg = new string[] { "Have you throught what if a function has multiple returns ?", "So you can return chunks of IO operation that not yet finished.", "Also, what about the progress ?", "This is an example of both.", "Use it anyway you like" };

            for (var i = 0; i < count; i++)
            {
                Thread.Sleep(2000);
                yield return msg[(int)(i % msg.Length)];
            }
        }

        [ResourceFunction]
        public AsyncReply<string> Stream(int count)
        {
            var reply = new AsyncReply<string>();
            var msg = new object[] { "Have you throught what if a function has multiple returns ?", "So you can return chunks of IO operation that not yet finished.", "Also, what about the progress ?", "This is an example of both.", "Use it anyway you like" };
            Timer timer = null;
            var msgCounter = 0;

            timer = new Timer((x) =>
            {
                
                reply.TriggerProgress(AsyncReply.ProgressType.Execution,  count, 22);

                if (count % 2 == 0 && msgCounter < msg.Length)
                    reply.TriggerChunk(msg[msgCounter++]);

                count--;
                if (count <= 0)
                {
                    timer.Dispose();
                    reply.Trigger("Done");
                }
                
            }, null, 10, 3000);
            
            return reply;
        }

        [ResourceProperty]
        public Structure Info
        {
            get;
            set;
        }

        [ResourceProperty]
        public string Name
        {
            get;
            set;
        }

        [ResourceProperty]
        public MyObject Me
        {
            get
            {
                return this;
            }
        }

        int level;
        [ResourceProperty]
        public int Level
        {
            get { return level; }
            set
            {
                level = value;
                Instance?.Modified();
            }
        }
    }


}
