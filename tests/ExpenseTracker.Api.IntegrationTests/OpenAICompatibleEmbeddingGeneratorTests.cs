using System.Net;
using System.Text;
using ExpenseTracker.Infrastructure.Ai;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

public class OpenAICompatibleEmbeddingGeneratorTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (OpenAICompatibleEmbeddingGenerator, StubHandler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/") };
        return (new OpenAICompatibleEmbeddingGenerator(http, "gemini-embedding-001"), handler);
    }

    [Fact]
    public async Task Reads_vectors_from_a_response_without_usage_in_index_order()
    {
        // Gemini's shape: no "usage" field, which crashed the SDK-based generator.
        var (generator, handler) = Create(HttpStatusCode.OK,
            """{"object":"list","data":[{"index":1,"embedding":[0.3,0.4]},{"index":0,"embedding":[0.1,0.2]}],"model":"gemini-embedding-001"}""");

        var result = await generator.GenerateAsync(["kahve", "taksi"]);

        result.Select(e => e.Vector.ToArray()).Should().BeEquivalentTo(new[] { new[] { 0.1f, 0.2f }, new[] { 0.3f, 0.4f } },
            o => o.WithStrictOrdering());
        handler.RequestUri!.ToString().Should().Be("https://generativelanguage.googleapis.com/v1beta/openai/embeddings");
        handler.RequestBody.Should().Contain("\"model\":\"gemini-embedding-001\"").And.Contain("\"input\":[\"kahve\",\"taksi\"]");
    }

    [Fact]
    public async Task Provider_errors_keep_their_status_code()
    {
        var (generator, _) = Create(HttpStatusCode.TooManyRequests, """{"error":{"message":"quota"}}""");

        var act = () => generator.GenerateAsync(["x"]);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
