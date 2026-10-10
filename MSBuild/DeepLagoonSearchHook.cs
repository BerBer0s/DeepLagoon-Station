// Compiled into Robust.Client by MSBuild/DeepLagoon.Search.targets. The engine spawn windows call this hook
// when Content has registered an implementation (Content.Client/_DeepLagoon/Search/FuzzySearchHookController.cs).
#nullable enable
using System;
using System.Collections.Generic;

namespace Robust.Client.DeepLagoon;

public interface IDeepLagoonSearch
{
    /// <summary>Keeps the matching items and orders them best first; an empty query keeps the order.</summary>
    List<T> Rank<T>(IEnumerable<T> items, string query, Func<T, string?> name, Func<T, string?>? extra);
}

public static class DeepLagoonSearch
{
    public static IDeepLagoonSearch? Impl;
}
