using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Localization;

[TestFixture]
public sealed class RussianFallbackTest
{
    [Test]
    public async Task RussianMessagesUseEnglishFallbackOnClientAndServer()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() => VerifyLocalization(pair.Server.ResolveDependency<ILocalizationManager>()));
        await pair.Client.WaitAssertion(() => VerifyLocalization(pair.Client.ResolveDependency<ILocalizationManager>()));

        await pair.CleanReturnAsync();
    }

    private static void VerifyLocalization(ILocalizationManager loc)
    {
        Assert.Multiple(() =>
        {
            Assert.That(loc.DefaultCulture?.Name, Is.EqualTo("ru-RU"));
            Assert.That(loc.GetString("dl-character-setup-characters"), Is.EqualTo("Персонажи"));
            Assert.That(loc.GetEntityData("GridMagnet").Name, Is.EqualTo("гридовый магнит"));

            // These messages only exist in English and exercise content functions in the fallback bundle.
            Assert.That(loc.GetString("contraband-job-plural", ("job", "engineer")), Is.EqualTo("engineers"));
            Assert.That(loc.GetString("zzzz-fmt-power-watts", ("divided", 1.5), ("places", 1)),
                Is.EqualTo("1.5 kW"));
        });
    }
}
