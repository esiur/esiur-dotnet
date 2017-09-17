using Esiur.Data;
using Esiur.Engine;
using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

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
            Info["name"] = "Zamil";
            Name = "Ahmed";
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
