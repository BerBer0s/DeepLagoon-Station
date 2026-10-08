using System;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using Moq;
using NUnit.Framework;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Tests.Client;

/// <summary>Exercise the compiled network adapter through the real engine methods.</summary>
[TestFixture]
public sealed class NetworkStatusCancellationTest
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static NetConnection Connection()
    {
        var peer = new NetPeer(new NetPeerConfiguration("network-cancellation-test"));
        return (NetConnection) Activator.CreateInstance(typeof(NetConnection), PrivateInstance, null,
            new object[] { peer, new IPEndPoint(IPAddress.Loopback, 1212) }, null)!;
    }

    private static IDictionary Waiters(NetManager manager) =>
        (IDictionary) typeof(NetManager).GetField("_awaitingStatusChange", PrivateInstance)!.GetValue(manager)!;

    private static Task<string> AwaitStatus(NetManager manager, NetConnection connection, CancellationToken token) =>
        (Task<string>) typeof(NetManager).GetMethod("AwaitStatusChange", PrivateInstance)!
            .Invoke(manager, new object[] { connection, token })!;

    [Test]
    public void AlreadyCancelledAttemptDoesNotLeaveAWaiter()
    {
        var manager = new NetManager();
        var task = AwaitStatus(manager, Connection(), new CancellationToken(true));
        Assert.Multiple(() =>
        {
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(Waiters(manager).Count, Is.Zero);
        });
    }

    [Test]
    public void CancellationRemovesAnExistingWaiter()
    {
        var manager = new NetManager();
        using var cancellation = new CancellationTokenSource();
        var task = AwaitStatus(manager, Connection(), cancellation.Token);
        Assert.That(Waiters(manager).Count, Is.EqualTo(1));
        cancellation.Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(task.IsCanceled, Is.True);
            Assert.That(Waiters(manager).Count, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReceivedStatusCanCompleteOrLoseToCancellation(bool cancelled)
    {
        var manager = new NetManager();
        typeof(NetManager).GetField("_logger", PrivateInstance)!.SetValue(manager, new Mock<ISawmill>().Object);
        var connection = Connection();
        var completion = new TaskCompletionSource<string>();
        // Model the losing attempt: the status handler has obtained its waiter,
        // while cancellation has already completed that waiter's task.
        if (cancelled) completion.SetCanceled();
        Waiters(manager).Add(connection, (default(CancellationTokenRegistration), completion));
        var message = (NetIncomingMessage) Activator.CreateInstance(typeof(NetIncomingMessage), true)!;
        typeof(NetIncomingMessage).GetField("m_senderConnection", PrivateInstance)!.SetValue(message, connection);
        message.Write((byte) NetConnectionStatus.Connected);
        message.Write("connected");
        Assert.DoesNotThrow(() => typeof(NetManager).GetMethod("HandleStatusChanged", PrivateInstance)!
            .Invoke(manager, new object[] { null, message }));
        Assert.That(Waiters(manager).Count, Is.Zero);
        if (cancelled)
            Assert.That(completion.Task.IsCanceled, Is.True);
        else
            Assert.That(await completion.Task, Is.EqualTo("connected"));
    }

    [Test]
    public void OldCancellationCannotRemoveANewerWaiterForTheSameConnection()
    {
        var manager = new NetManager();
        var connection = Connection();
        using var oldCancellation = new CancellationTokenSource();
        using var newCancellation = new CancellationTokenSource();
        var oldTask = AwaitStatus(manager, connection, oldCancellation.Token);
        var oldWaiter = ((CancellationTokenRegistration, TaskCompletionSource<string>)) Waiters(manager)[connection]!;
        // Status processing has removed the old waiter, but has not yet disposed
        // its cancellation registration. A new wait may already be published.
        Waiters(manager).Remove(connection);
        var newTask = AwaitStatus(manager, connection, newCancellation.Token);
        oldCancellation.Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(oldTask.IsCanceled, Is.True);
            Assert.That(newTask.IsCompleted, Is.False);
            Assert.That(Waiters(manager).Count, Is.EqualTo(1));
        });
        oldWaiter.Item1.Dispose();
        newCancellation.Cancel();
        Assert.That(Waiters(manager).Count, Is.Zero);
    }
}
