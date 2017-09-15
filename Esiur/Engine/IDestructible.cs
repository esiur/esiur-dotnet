using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Engine
{
    public delegate void DestroyedEvent(object sender);

    public interface IDestructible
    {
        event DestroyedEvent OnDestroy;
        void Destroy();
    }
}
