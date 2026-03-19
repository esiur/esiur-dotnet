using Esiur.Resource;
using System;
using System.Collections.Generic;
using System.Text;

namespace Esiur.Tests.Annotations
{
    [Annotation("Represents a managed service node with load, error count, and enable state. Functions control service operation.")]
    [Annotation("usage_rules", @"1.Choose at most one function per tick.
    2. Use only functions defined in the functions list.
    3. Do not invent properties or functions.
    4. Base the decision only on current property values and annotations.
    5. Keep the service enabled as much as possible")]
    [Resource]
    public partial class ServiceNode
    {
        [Annotation("Current service load percentage from 0 to 100. Values above 80 indicate overload.")]
        [Export] int load;

        [Annotation("Number of recent errors detected in the service. Values above 3 indicate instability. A value of 0 means no reset is needed")]
        [Export] int errorCount;

        [Annotation("True when the service is enabled and allowed to run. False means the service is disabled.")]
        [Export] bool enabled;

        [Annotation("Restart the service when load is very high or when repeated errors indicate instability.")]
        [Export] public void Restart()
        {
            ErrorCount = 0;
            Load = 10;
            Enabled = true;
        }

        [Annotation("Clear recent errors only when ErrorCount is greater than 0 and the service is otherwise stable.")]
        [Export] public void ResetErrors()
        {
            ErrorCount = 0;
        }

        [Annotation("Enable the service when Enabled is false.")]
        [Export] public void Enable()
        {
            Enabled = true;
        }

        [Annotation("Disable the service if it should stop processing requests.")]
        [Export] public void Disable()
        {
            Enabled = false;
        }
    }
}
