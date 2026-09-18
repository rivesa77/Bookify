namespace Bookify.Infrastructure.Tests.Support
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Text;

    internal sealed class RecordingHttpHandler : HttpMessageHandler
    {
        internal List<CapturedRequest> Requests { get; } = [];

        internal Queue<HttpResponseMessage> Responses { get; } = new();

        internal Exception? Failure { get; set; }

        internal static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                body,
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.Authorization,
                cancellationToken));

            if (Failure is not null)
            {
                throw Failure;
            }

            return Responses.Dequeue();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (HttpResponseMessage response in Responses)
                {
                    response.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        internal sealed record CapturedRequest(
            HttpMethod Method,
            Uri Uri,
            string? Body,
            string? ContentType,
            AuthenticationHeaderValue? Authorization,
            CancellationToken CancellationToken);
    }
}
