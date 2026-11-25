using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the holder of the agent factory: runs lease the current factory, and a replaced factory is disposed only after
/// its last run has ended.
/// </summary>
[TestFixture]
internal class HarnessAgentFactoryHolderTest
{
    private const string kReason = "The AI settings are invalid: test.";

    /// <summary>
    /// Verifies that a holder without factory refuses runs with the reason that no provider is configured.
    /// </summary>
    [Test]
    public void Acquire_WithoutFactory_ThrowsNotConfigured()
    {
        using AgentFactoryHolder holder = new();

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => holder.Acquire());

        Assert.That(exception!.Message, Is.EqualTo(AgentFactoryHolder.kNotConfiguredReason));
        Assert.That(holder.HasFactory, Is.False);
    }

    /// <summary>
    /// Verifies that a run gets the factory that is set.
    /// </summary>
    [Test]
    public void Acquire_WithFactory_ReturnsFactory()
    {
        FwoAgentFactory factory = CreateFactory(out _);
        using AgentFactoryHolder holder = new(factory);

        using AgentFactoryLease lease = holder.Acquire();

        Assert.That(lease.Factory, Is.SameAs(factory));
        Assert.That(holder.HasFactory, Is.True);
    }

    /// <summary>
    /// Verifies that a factory replaced while a run uses it stays usable until that run ends, while new runs get the
    /// new factory right away.
    /// </summary>
    [Test]
    public void SetFactory_WhileLeased_DisposesOldFactoryAfterLastRun()
    {
        FwoAgentFactory oldFactory = CreateFactory(out ScriptedChatClient oldClient);
        FwoAgentFactory newFactory = CreateFactory(out ScriptedChatClient newClient);
        using AgentFactoryHolder holder = new(oldFactory);
        AgentFactoryLease firstRun = holder.Acquire();
        AgentFactoryLease secondRun = holder.Acquire();

        holder.SetFactory(newFactory);
        using AgentFactoryLease newRun = holder.Acquire();
        firstRun.Dispose();
        int disposeCountWhileSecondRunIsRunning = oldClient.DisposeCount;
        secondRun.Dispose();

        Assert.That(newRun.Factory, Is.SameAs(newFactory));
        Assert.That(disposeCountWhileSecondRunIsRunning, Is.Zero);
        Assert.That(oldClient.DisposeCount, Is.EqualTo(1));
        Assert.That(newClient.DisposeCount, Is.Zero);
    }

    /// <summary>
    /// Verifies that a factory no run uses is disposed as soon as it is replaced.
    /// </summary>
    [Test]
    public void SetFactory_WithoutLease_DisposesOldFactoryRightAway()
    {
        FwoAgentFactory oldFactory = CreateFactory(out ScriptedChatClient oldClient);
        using AgentFactoryHolder holder = new(oldFactory);

        holder.SetFactory(CreateFactory(out _));

        Assert.That(oldClient.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that removing the factory makes runs fail with the given reason, and disposes the factory once its run
    /// has ended.
    /// </summary>
    [Test]
    public void SetUnavailable_RefusesNewRunsWithReason()
    {
        FwoAgentFactory factory = CreateFactory(out ScriptedChatClient client);
        using AgentFactoryHolder holder = new(factory);
        AgentFactoryLease runningRun = holder.Acquire();

        holder.SetUnavailable(kReason);
        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => holder.Acquire());
        int disposeCountWhileRunning = client.DisposeCount;
        runningRun.Dispose();

        Assert.That(exception!.Message, Is.EqualTo(kReason));
        Assert.That(holder.UnavailableReason, Is.EqualTo(kReason));
        Assert.That(holder.HasFactory, Is.False);
        Assert.That(disposeCountWhileRunning, Is.Zero);
        Assert.That(client.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a lease disposed twice ends the run only once, so that the factory is not disposed early.
    /// </summary>
    [Test]
    public void LeaseDispose_Twice_ReleasesOnce()
    {
        FwoAgentFactory factory = CreateFactory(out ScriptedChatClient client);
        using AgentFactoryHolder holder = new(factory);
        AgentFactoryLease firstRun = holder.Acquire();
        AgentFactoryLease secondRun = holder.Acquire();

        firstRun.Dispose();
        firstRun.Dispose();
        holder.SetUnavailable(kReason);
        int disposeCountWhileSecondRunIsRunning = client.DisposeCount;
        secondRun.Dispose();

        Assert.That(disposeCountWhileSecondRunIsRunning, Is.Zero);
        Assert.That(client.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that disposing the holder disposes its factory, and that it refuses runs and new factories afterwards,
    /// disposing a factory it is given.
    /// </summary>
    [Test]
    public void Dispose_DisposesFactoryAndRefusesFurtherUse()
    {
        FwoAgentFactory factory = CreateFactory(out ScriptedChatClient client);
        FwoAgentFactory lateFactory = CreateFactory(out ScriptedChatClient lateClient);
        AgentFactoryHolder holder = new(factory);

        holder.Dispose();
        holder.Dispose();

        Assert.That(client.DisposeCount, Is.EqualTo(1));
        Assert.Throws<ObjectDisposedException>(() => holder.Acquire());
        Assert.Throws<ObjectDisposedException>(() => holder.SetFactory(lateFactory));
        Assert.That(lateClient.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that the holder refuses a missing factory and a reason without text.
    /// </summary>
    [Test]
    public void SetFactoryAndSetUnavailable_WithoutValue_Throw()
    {
        using AgentFactoryHolder holder = new();

        Assert.Throws<ArgumentNullException>(() => holder.SetFactory(null!));
        Assert.Throws<ArgumentException>(() => holder.SetUnavailable(" "));
    }

    /// <summary>
    /// Creates a factory with a scripted chat client, which counts how often the factory disposes it.
    /// </summary>
    private static FwoAgentFactory CreateFactory(out ScriptedChatClient chatClient)
    {
        chatClient = new ScriptedChatClient();
        return new FwoAgentFactory(chatClient, HarnessTestSetup.kContextWindowTokens, new AgentSettings());
    }
}
