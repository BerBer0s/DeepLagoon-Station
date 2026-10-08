using System.IO;
using System.Linq;
using Content.Client.Lobby;
using Content.Server.Preferences.Managers;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._DeepLagoon;
[TestFixture, NonParallelizable]
public sealed class HeadshotTransferTests
{
    [Test]
    public async Task UploadReadReplaceAndRejectForeignSlotOverGameChannel()
    {
        await using var pair=await PoolManager.GetServerClient(new PoolSettings {Destructive=true,InLobby=true});
        var client=pair.Client;var server=pair.Server;
        using var picture=new Image<Rgba32>(32,32);using var png=new MemoryStream();picture.SaveAsPng(png);var original=png.ToArray();
        using var jpeg=new MemoryStream();picture.SaveAsJpeg(jpeg);var replacement=jpeg.ToArray();
        int slot=0;string firstId="";byte[]? read=null;string error="";
        await client.WaitPost(()=>
        {
            slot=client.ResolveDependency<IClientPreferencesManager>().Preferences!.SelectedCharacterIndex;
            var net=client.ResolveDependency<IEntityManager>().EntityNetManager!;var request=Guid.NewGuid();
            net.SendSystemNetworkMessage(new HeadshotBeginEvent(request,slot,original.Length));
            net.SendSystemNetworkMessage(new HeadshotUploadEvent(request,0,original));
        });
        await pair.RunTicksSync(200);
        await server.WaitAssertion(()=>
        {
            var session=server.ResolveDependency<Robust.Server.Player.IPlayerManager>().Sessions.Single();
            var profile=(HumanoidCharacterProfile)server.ResolveDependency<IServerPreferencesManager>().GetPreferences(session.UserId).GetProfile(slot);
            firstId=profile.HeadshotId;Assert.That(Guid.TryParse(firstId,out _),Is.True);

        });
        await client.WaitPost(()=>client.ResolveDependency<IEntityManager>().System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Read(slot,null,(bytes,_,_,message)=>{read=bytes;error=message;}));
        await pair.RunTicksSync(20);
        Assert.That(error,Is.Empty);Assert.That(read,Is.EqualTo(original));
        await client.WaitPost(()=>
        {
            var net=client.ResolveDependency<IEntityManager>().EntityNetManager!;var request=Guid.NewGuid();
            net.SendSystemNetworkMessage(new HeadshotBeginEvent(request,slot,replacement.Length));net.SendSystemNetworkMessage(new HeadshotUploadEvent(request,0,replacement));
        });
        await pair.RunTicksSync(200);
        await server.WaitAssertion(()=>
        {
            var session=server.ResolveDependency<Robust.Server.Player.IPlayerManager>().Sessions.Single();
            var profile=(HumanoidCharacterProfile)server.ResolveDependency<IServerPreferencesManager>().GetPreferences(session.UserId).GetProfile(slot);
            Assert.That(profile.HeadshotId,Is.EqualTo(firstId));

        });
        read=null;
        await client.WaitPost(()=>client.ResolveDependency<IEntityManager>().System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Read(slot,null,(bytes,_,_,message)=>{read=bytes;error=message;}));
        await pair.RunTicksSync(20);Assert.That(error,Is.Empty);Assert.That(read,Is.EqualTo(replacement));
        await client.WaitPost(()=>
        {
            var net=client.ResolveDependency<IEntityManager>().EntityNetManager!;var request=Guid.NewGuid();
            net.SendSystemNetworkMessage(new HeadshotBeginEvent(request,999,original.Length));net.SendSystemNetworkMessage(new HeadshotUploadEvent(request,0,original));
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(()=>
        {
            var session=server.ResolveDependency<Robust.Server.Player.IPlayerManager>().Sessions.Single();
            var profile=(HumanoidCharacterProfile)server.ResolveDependency<IServerPreferencesManager>().GetPreferences(session.UserId).GetProfile(slot);
            Assert.That(profile.HeadshotId,Is.EqualTo(firstId));
        });
        await pair.CleanReturnAsync();
    }
}
