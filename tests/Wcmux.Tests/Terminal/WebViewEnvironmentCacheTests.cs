using System.Reflection;
using Wcmux.App.Terminal;

namespace Wcmux.Tests.Terminal;

/// <summary>
/// Tests for <see cref="WebViewEnvironmentCache"/>.
/// Structural tests verify the API surface. Integration tests (requiring
/// the WebView2 runtime) verify actual singleton behavior at runtime.
/// </summary>
public class WebViewEnvironmentCacheTests
{
    [Fact]
    public void GetOrCreateAsync_ReturnsTask()
    {
        // Verify the method exists and returns Task<CoreWebView2Environment>
        var method = typeof(WebViewEnvironmentCache).GetMethod(
            "GetOrCreateAsync",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(
            method!.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
            "GetOrCreateAsync should return a Task<T>");
    }

    [Fact]
    public void Reset_MethodExists()
    {
        // Verify Reset is an internal static method
        var method = typeof(WebViewEnvironmentCache).GetMethod(
            "Reset",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(method!.IsAssembly || method.IsFamilyOrAssembly,
            "Reset should be internal");
    }

    [Fact]
    public void Class_IsStaticSingleton()
    {
        // Verify the class is static (abstract + sealed in IL)
        var type = typeof(WebViewEnvironmentCache);
        Assert.True(type.IsAbstract && type.IsSealed,
            "WebViewEnvironmentCache should be a static class");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetOrCreateAsync_ReturnsSameInstance_WhenCalledConcurrently()
    {
        // This test requires the WebView2 runtime to be installed.
        // It verifies the singleton guarantee under concurrent access.
        try
        {
            WebViewEnvironmentCache.Reset();

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => WebViewEnvironmentCache.GetOrCreateAsync())
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // All should be the same instance
            Assert.All(results, env => Assert.Same(results[0], env));
        }
        finally
        {
            WebViewEnvironmentCache.Reset();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Reset_ClearsCachedEnvironment()
    {
        // After Reset(), a new call should create a fresh environment.
        try
        {
            var first = await WebViewEnvironmentCache.GetOrCreateAsync();
            WebViewEnvironmentCache.Reset();
            var second = await WebViewEnvironmentCache.GetOrCreateAsync();

            // They should be different instances after reset
            Assert.NotSame(first, second);
        }
        finally
        {
            WebViewEnvironmentCache.Reset();
        }
    }
}
