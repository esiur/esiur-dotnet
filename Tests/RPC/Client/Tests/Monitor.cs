using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace RPC.Client.Tests;

public class PerProcessNetMonitor : IDisposable
{
    private readonly int _pid;
    private TraceEventSession _session;
    private Task _listenTask;
    private long _txBytes;
    private long _rxBytes;
    private volatile bool _running;

    public PerProcessNetMonitor(int pid)
    {
        _pid = pid;
    }

    public void Start()
    {
        // Use a unique session name
        string sessionName = "NetMon_" + Guid.NewGuid();
        _session = new TraceEventSession(sessionName);

        // Enable kernel network provider
        _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

        _running = true;
        _listenTask = Task.Run(() =>
        {
            _session.Source.Kernel.TcpIpRecv += evt =>
            {
                if (evt.ProcessID == _pid)
                    Interlocked.Add(ref _rxBytes, evt.size);
            };

            _session.Source.Kernel.TcpIpSend += evt =>
            {
                if (evt.ProcessID == _pid)
                    Interlocked.Add(ref _txBytes, evt.size);
            };

            // For UDP:
            _session.Source.Kernel.UdpIpRecv += evt =>
            {
                if (evt.ProcessID == _pid)
                    Interlocked.Add(ref _rxBytes, evt.size);
            };

            _session.Source.Kernel.UdpIpSend += evt =>
            {
                if (evt.ProcessID == _pid)
                    Interlocked.Add(ref _txBytes, evt.size);
            };

            _session.Source.Process();
        });
    }

    public (long tx, long rx) GetTotals()
        => (Interlocked.Read(ref _txBytes), Interlocked.Read(ref _rxBytes));

    public (long tx, long rx, long diffTX, long diffRX) GetDiff(long previousTX, long previousRX)
    {
        var ctx = Interlocked.Read(ref _txBytes);
        var crx = Interlocked.Read(ref _rxBytes);

        return (ctx, crx, ctx - previousTX, crx - previousRX);
    }

    public void Stop()
    {
        _running = false;
        _session?.Dispose();
    }

    public void Dispose() => Stop();
}
