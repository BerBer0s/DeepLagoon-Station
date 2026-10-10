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

            // Imported Russian messages use Russian numeric formatting.
            Assert.That(loc.GetString("zzzz-fmt-power-watts", ("divided", 1.5), ("places", 1)),
                Is.EqualTo("1,5 кВт"));

            // The pack's version requires an extra variable, so this message keeps the English fallback.
            // Exercise both English number formatting and grammar functions in that fallback bundle.
            Assert.That(loc.GetString("lathe-menu-material-amount-missing",
                    ("amount", 1.5), ("unit", "sheet"), ("material", "steel"), ("missingAmount", 2.5)),
                Is.EqualTo("1.5 sheets of steel ([color=red]2.5 sheets missing[/color])"));
        });
    }
}
