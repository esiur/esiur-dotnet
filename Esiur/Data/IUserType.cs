using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Data;
public interface IUserType
{
    object Get();
    void Set(object value);
}

