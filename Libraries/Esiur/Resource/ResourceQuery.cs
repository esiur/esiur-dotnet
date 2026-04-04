using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Esiur.Resource;

public class ResourceQuery : IQueryable<IResource>
{
    public Type ElementType => throw new NotImplementedException();

    public Expression Expression => throw new NotImplementedException();

    public IQueryProvider Provider => throw new NotImplementedException();

    public IEnumerator<IResource> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
