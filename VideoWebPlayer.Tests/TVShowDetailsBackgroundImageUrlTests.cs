using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class TVShowDetailsBackgroundImageUrlTests
{
    [Fact]
    public void GetHeaderBackgroundUrl_WithGeneratedBackgroundImage_AppendsAccessToken()
    {
        var componentType = Assembly.Load("VideoWebPlayer")
            .GetTypes()
            .Single(t => t.Name == "TVShowDetails");

        var component = Activator.CreateInstance(componentType)!;

        var client = new VideoWebPlayerClient(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance);
        client.SetAuthorizationToken(new AuthorizationToken { token = "test-token", expires = DateTime.UtcNow.AddHours(1) });
        SetMember(component, "Client", client);

        var episode = new DtoTVShowEpisode { Id = 42, GeneratedBackgroundPictureId = 7 };
        SetMember(component, "selectedEpisode", episode);

        var method = componentType.GetMethod("GetHeaderBackgroundUrl", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Method 'GetHeaderBackgroundUrl' not found.");

        var url = (string)method.Invoke(component, null)!;

        Assert.Equal("/api/episodes/42/background-image?access_token=test-token", url);
    }

    private static void SetMember(object target, string name, object? value)
    {
        var type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        var property = type.GetProperty(name, flags);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(name, flags)
            ?? throw new InvalidOperationException($"Member '{name}' not found on {type.FullName}.");
        field.SetValue(target, value);
    }
}
