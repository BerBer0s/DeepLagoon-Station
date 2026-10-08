using System.Collections.Generic;
using Content.Client._DeepLagoon.WebUI;
using NUnit.Framework;

namespace Content.Tests.Client;

[TestFixture]
public sealed class WikiNavigationPolicyTest
{
    private static readonly HashSet<string> Pages = new() { "/ru/rules" };

    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/rules", true)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/in_game", true)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/in_game/jobs/engineer#tools", true)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/in_game_other", false)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/home", false)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/in_game?edit=1", false)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/ru/in_game/%2fadmin", false)]
    [TestCase("https://example.com/ru/in_game", false)]
    [TestCase("http://wiki.deep-lagoon-ss14.ru/ru/in_game", false)]
    public void NewArticlesAreAllowedOnlyInPublicGameTree(string url, bool expected)
        => Assert.That(WikiNavigationPolicy.IsPage(url, Pages), Is.EqualTo(expected));

    [TestCase("https://wiki.deep-lagoon-ss14.ru/graphql", true)]
    [TestCase("https://example.com/graphql", false)]
    [TestCase("https://wiki.deep-lagoon-ss14.ru/graphql?query=test", false)]
    public void TreeRequestsStayOnOwnEndpoint(string url, bool expected)
        => Assert.That(WikiNavigationPolicy.IsTreeRequest(url), Is.EqualTo(expected));
}
