using System.Net;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModApiServiceTests
{
    [Fact]
    public async Task GetTagsAsyncParsesTagIdsReturnedAsStrings()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler("""
            {
              "statuscode": "200",
              "tags": [
                { "tagid": "1328", "name": "Content", "color": "#92C96AFF" }
              ]
            }
            """));
        var service = new ModApiService(httpClient);

        var tags = await service.GetTagsAsync();

        var tag = Assert.Single(tags);
        Assert.Equal(1328, tag.TagId);
        Assert.Equal("Content", tag.Name);
    }

    private sealed class StubHttpMessageHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
