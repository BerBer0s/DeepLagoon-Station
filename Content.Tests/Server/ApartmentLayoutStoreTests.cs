using System;
using System.IO;
using Content.Server._DeepLagoon.Apartments;
using Content.Shared._DeepLagoon.Apartments;
using NUnit.Framework;

namespace Content.Tests.Server;

[TestFixture]
public sealed class ApartmentLayoutStoreTests
{
    [Test]
    public void ReopeningPreservesDeltaAndStaleRevisionCannotOverwriteIt()
    {
#if USE_SYSTEM_SQLITE
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
#else
        SQLitePCL.Batteries_V2.Init();
#endif
        var path = Path.Combine(Path.GetTempPath(), $"dl-apartment-test-{Guid.NewGuid():N}.db");
        var owner = Guid.NewGuid();
        try
        {
            using (var store = new ApartmentLayoutStore(path))
            {
                var delta = new ApartmentDelta { Template = "studio", TemplateVersion = 3, Revision = 1, Removed = new() { "base_table" } };
                Assert.That(store.Save(owner, delta, 0), Is.True);
                Assert.That(store.Save(owner, new ApartmentDelta { Revision = 1 }, 0), Is.False);
                delta.Revision = 2;
                Assert.That(store.Save(owner, delta, 1), Is.True);
                Assert.That(store.Load(Guid.NewGuid()), Is.Null);
            }
            using var reopened = new ApartmentLayoutStore(path);
            var loaded = reopened.Load(owner)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Revision, Is.EqualTo(2));
                Assert.That(loaded.TemplateVersion, Is.EqualTo(3));
                Assert.That(loaded.Removed, Is.EqualTo(new[] { "base_table" }));
            });
        }
        finally { File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
    }
}
