using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esiur.Net.Sockets;
using System.Net;
using System.Collections;
using Esiur.Misc;
using Esiur.Data;

namespace Esiur.Net.TCP
{
    public class TCPConnection: NetworkConnection 
    {

        private KeyList<string, object> variables = new KeyList<string, object>();


        public KeyList<string, object> Variables
        {
            get
            {
                return variables;
            }
        }
    }
}
