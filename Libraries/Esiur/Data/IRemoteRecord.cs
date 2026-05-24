using Esiur.Data.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data
{
    public interface IRemoteRecord: IRecord
    {
        public RemoteTypeDef TypeDef { get; }
    }
}
