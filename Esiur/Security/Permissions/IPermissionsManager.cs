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

using Esiur.Data;
using Esiur.Engine;
using Esiur.Net;
using Esiur.Resource;
using Esiur.Resource.Template;
using Esiur.Security.Authority;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esiur.Security.Permissions
{
    public interface IPermissionsManager
    {
        /// <summary>
        /// Check for permission.
        /// </summary>
        /// <param name="resource">IResource.</param>
        /// <param name="session">Caller sessions.</param>
        /// <param name="action">Action type</param>
        /// <param name="member">Function, property or event to check for permission.</param>
        /// <param name="inquirer">Permission inquirer object.</param>
        /// <returns>Allowed or denined.</returns>
        Ruling Applicable(IResource resource, Session session, ActionType action, MemberTemplate member, object inquirer = null);

        bool Initialize(Structure settings, IResource resource);

        Structure Settings { get; }
    }
}
