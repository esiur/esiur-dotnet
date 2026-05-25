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

namespace Esiur.Resource;

public enum ResourceOperation : int
{
    /// <summary>
    /// Resource is requested to open/start its active work.
    /// </summary>
    Open = 0,

    /// <summary>
    /// Resource is requested to initialize.
    /// </summary>
    Initialize = 1,


    /// <summary>
    /// Resource configuration should be applied or refreshed.
    /// </summary>
    Configure = 2,

    /// <summary>
    /// Resource is requested to close/stop active work, but may remain loaded.
    /// </summary>
    Close = 3,

    /// <summary>
    /// Resource is requested to terminate and release its resources.
    /// </summary>
    Terminate = 4,

    /// <summary>
    /// Runtime has finished initializing the system/resource graph.
    /// Safe point for resolving dependencies on other resources.
    /// </summary>
    SystemReady = 5,

    /// <summary>
    /// Runtime is about to reload the system or resource graph.
    /// Resource should prepare for reload.
    /// </summary>
    SystemReloading = 6,

    /// <summary>
    /// Runtime has finished reloading the system or resource graph.
    /// Resource may rebind dependencies or refresh state.
    /// </summary>
    SystemReloaded = 7,

    /// <summary>
    /// Runtime is preparing to shut down the system.
    /// Resource should stop background activity gracefully.
    /// </summary>
    SystemTerminating = 8,

    /// <summary>
    /// Runtime has completed system shutdown/termination.
    /// Usually used only for final notifications.
    /// </summary>
    SystemTerminated = 8,

    /// <summary>
    /// Resource should persist its current state if supported.
    /// </summary>
    Save = 9,

    /// <summary>
    /// Resource should reload its state from its backing store if supported.
    /// </summary>
    Load = 10,

    /// <summary>
    /// Resource should pause active work without releasing all resources.
    /// </summary>
    Pause = 11,

    /// <summary>
    /// Resource should resume work after pause.
    /// </summary>
    Resume = 12
}
