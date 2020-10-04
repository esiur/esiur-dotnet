using System;
using System.Collections.Generic;
using System.Text;

namespace Esyur.Net
{
    public interface INetworkReceiver<T>
    {
        void NetworkClose(T sender);
        void NetworkReceive(T sender, NetworkBuffer buffer);
        //void NetworkError(T sender);

        void NetworkConnect(T sender);
    }
}
