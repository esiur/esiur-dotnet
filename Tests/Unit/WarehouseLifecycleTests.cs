using Esiur.Core;
using Esiur.Resource;
using Esiur.Stores;

namespace Esiur.Tests.Unit;

public sealed class WarehouseLifecycleTests
{
    [Fact]
    public async Task Close_AllowsWarehouseToBeOpenedAgain()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());

        Assert.False(warehouse.IsOpen);
        Assert.True(await warehouse.Open());
        Assert.True(warehouse.IsOpen);
        Assert.False(await warehouse.Open());

        Assert.True(await warehouse.Close());
        Assert.False(warehouse.IsOpen);

        Assert.True(await warehouse.Open());
        Assert.True(warehouse.IsOpen);
        Assert.True(await warehouse.Close());
        Assert.False(warehouse.IsOpen);
    }

    [Fact]
    public async Task Close_WaitsForAnInProgressOpenBeforeTerminatingResources()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());
        var resource = await warehouse.Put("sys/blocking", new BlockingResource());

        var open = warehouse.Open();
        await resource.InitializeStarted.WaitAsync(TimeSpan.FromSeconds(2));

        var close = warehouse.Close();
        await Task.Delay(25);
        Assert.False(resource.TerminateStarted.IsCompleted);

        resource.ReleaseInitialize();

        Assert.True(await open);
        Assert.True(await close);
        Assert.True(resource.TerminateStarted.IsCompletedSuccessfully);
        Assert.False(warehouse.IsOpen);
    }

    [Fact]
    public async Task Close_WaitsForEachShutdownPhaseBeforeAdvancingOrReopening()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());

        var terminateReply = new AsyncReply<bool>();
        var systemTerminatedReply = new AsyncReply<bool>();
        var terminateBlocker = await warehouse.Put(
            "sys/terminate-blocker",
            new ControlledLifecycleResource(terminateReply: terminateReply));
        var systemTerminatedBlocker = await warehouse.Put(
            "sys/system-terminated-blocker",
            new ControlledLifecycleResource(systemTerminatedReply: systemTerminatedReply));

        Assert.True(await warehouse.Open());

        var close = Observe(warehouse.Close());
        await Task.WhenAll(
                terminateBlocker.TerminateStarted,
                systemTerminatedBlocker.TerminateStarted)
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(terminateBlocker.SystemTerminatedStarted.IsCompleted);
        Assert.False(systemTerminatedBlocker.SystemTerminatedStarted.IsCompleted);

        terminateReply.Trigger(true);

        await Task.WhenAll(
                terminateBlocker.SystemTerminatedStarted,
                systemTerminatedBlocker.SystemTerminatedStarted)
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(close.IsCompleted);

        var reopen = Observe(warehouse.Open());
        await Task.Delay(25);
        Assert.False(reopen.IsCompleted);

        systemTerminatedReply.Trigger(true);

        Assert.True(await close);
        Assert.True(await reopen);
        Assert.True(await warehouse.Close());
    }

    [Fact]
    public async Task Close_SynchronousThrowDoesNotSkipOtherResourcesOrSecondPhase()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());

        var expected = new InvalidOperationException("terminate failed synchronously");
        var throwing = await warehouse.Put(
            "sys/throwing",
            new ControlledLifecycleResource(terminateException: expected));
        var other = await warehouse.Put(
            "sys/other",
            new ControlledLifecycleResource());

        Assert.True(await warehouse.Open());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Observe(warehouse.Close()));

        Assert.Same(expected, exception);
        Assert.True(other.TerminateStarted.IsCompletedSuccessfully);
        Assert.True(throwing.SystemTerminatedStarted.IsCompletedSuccessfully);
        Assert.True(other.SystemTerminatedStarted.IsCompletedSuccessfully);
        Assert.False(warehouse.IsOpen);
        Assert.False(await warehouse.Close());
    }

    [Fact]
    public async Task Close_AsyncErrorWaitsForEveryOtherReplyToSettle()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());

        var failingReply = new AsyncReply<bool>();
        var blockingReply = new AsyncReply<bool>();
        var failing = await warehouse.Put(
            "sys/failing",
            new ControlledLifecycleResource(systemTerminatedReply: failingReply));
        var blocking = await warehouse.Put(
            "sys/blocking",
            new ControlledLifecycleResource(systemTerminatedReply: blockingReply));

        Assert.True(await warehouse.Open());

        var close = Observe(warehouse.Close());
        await Task.WhenAll(failing.SystemTerminatedStarted, blocking.SystemTerminatedStarted)
            .WaitAsync(TimeSpan.FromSeconds(2));

        var expected = new InvalidOperationException("system termination failed asynchronously");
        failingReply.TriggerError(expected);

        Assert.False(close.IsCompleted);

        blockingReply.Trigger(true);

        var exception = await Assert.ThrowsAsync<AsyncException>(() => close);
        Assert.Same(expected, exception.InnerException);
        Assert.True(failing.TerminateStarted.IsCompletedSuccessfully);
        Assert.True(blocking.TerminateStarted.IsCompletedSuccessfully);
        Assert.False(warehouse.IsOpen);
        Assert.False(await warehouse.Close());
    }

    [Fact]
    public async Task Close_ReturnsFalseAfterBothPhasesWhenAResourceReturnsFalse()
    {
        var warehouse = new Warehouse();
        await warehouse.Put("sys", new MemoryStore());

        var resource = await warehouse.Put(
            "sys/false-result",
            new ControlledLifecycleResource(
                terminateReply: new AsyncReply<bool>(false)));

        Assert.True(await warehouse.Open());

        Assert.False(await warehouse.Close());
        Assert.True(resource.SystemTerminatedStarted.IsCompletedSuccessfully);
        Assert.False(warehouse.IsOpen);
    }

    private static async Task<bool> Observe(AsyncReply<bool> reply) => await reply;

    private sealed class ControlledLifecycleResource : IResource
    {
        private readonly AsyncReply<bool>? terminateReply;
        private readonly AsyncReply<bool>? systemTerminatedReply;
        private readonly Exception? terminateException;
        private readonly TaskCompletionSource terminateStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource systemTerminatedStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ControlledLifecycleResource(
            AsyncReply<bool>? terminateReply = null,
            AsyncReply<bool>? systemTerminatedReply = null,
            Exception? terminateException = null)
        {
            this.terminateReply = terminateReply;
            this.systemTerminatedReply = systemTerminatedReply;
            this.terminateException = terminateException;
        }

        public event DestroyedEvent? OnDestroy;
        public Instance? Instance { get; set; }
        public Task TerminateStarted => terminateStarted.Task;
        public Task SystemTerminatedStarted => systemTerminatedStarted.Task;

        public AsyncReply<bool> Handle(
            ResourceOperation operation,
            IResourceContext? context = null)
        {
            if (operation == ResourceOperation.Terminate)
            {
                terminateStarted.TrySetResult();
                if (terminateException != null)
                    throw terminateException;

                return terminateReply ?? new AsyncReply<bool>(true);
            }

            if (operation == ResourceOperation.SystemTerminated)
            {
                systemTerminatedStarted.TrySetResult();
                return systemTerminatedReply ?? new AsyncReply<bool>(true);
            }

            return new AsyncReply<bool>(true);
        }

        public void Destroy() => OnDestroy?.Invoke(this);
    }

    private sealed class BlockingResource : IResource
    {
        private readonly AsyncReply<bool> initialize = new();
        private readonly TaskCompletionSource initializeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource terminateStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public event DestroyedEvent? OnDestroy;
        public Instance? Instance { get; set; }
        public Task InitializeStarted => initializeStarted.Task;
        public Task TerminateStarted => terminateStarted.Task;

        public void ReleaseInitialize() => initialize.Trigger(true);

        public AsyncReply<bool> Handle(
            ResourceOperation operation,
            IResourceContext? context = null)
        {
            if (operation == ResourceOperation.Initialize)
            {
                initializeStarted.TrySetResult();
                return initialize;
            }

            if (operation == ResourceOperation.Terminate)
                terminateStarted.TrySetResult();

            return new AsyncReply<bool>(true);
        }

        public void Destroy() => OnDestroy?.Invoke(this);
    }
}
