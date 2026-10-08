using System;
using System.IO;
using Content.Shared.Administration;
using NUnit.Framework;

namespace Content.Tests.Shared.Administration;

[TestFixture]
public sealed class ComponentPermissionsTest
{
    [TestCase("addcomp")]
    [TestCase("addcompc")]
    [TestCase("rmcomp")]
    [TestCase("rmcompc")]
    [TestCase("vv")]
    [TestCase("vvread")]
    [TestCase("vvwrite")]
    [TestCase("vvinvoke")]
    public void OrdinaryAdminsAndHostsCanEditComponents(string command)
    {
        var permissions = new AdminCommandPermissions();
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "../../Resources/engineCommandPerms.yml"));
        permissions.LoadPermissionsFromStream(stream);
        Assert.Multiple(() =>
        {
            foreach (var flag in new[] { AdminFlags.Admin, AdminFlags.Host, AdminFlags.VarEdit })
                Assert.That(permissions.CanCommand(command, new AdminData { Active = true, Flags = flag }), Is.True, flag.ToString());
            Assert.That(permissions.CanCommand(command, null), Is.False);
            Assert.That(permissions.CanCommand(command, new AdminData { Active = true, Flags = AdminFlags.Moderator }), Is.False);
            Assert.That(permissions.CanCommand(command, new AdminData { Active = false, Flags = AdminFlags.Admin }), Is.False);
        });
    }
}
