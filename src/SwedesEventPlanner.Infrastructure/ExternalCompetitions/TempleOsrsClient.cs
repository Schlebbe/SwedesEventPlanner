using System.Net.Http.Headers;
using SwedesEventPlanner.Application.ExternalCompetitions;

namespace SwedesEventPlanner.Infrastructure.ExternalCompetitions;

public sealed class TempleOsrsClient(
    HttpClient httpClient) : ITempleOsrsClient
{
    public async Task<TempleOsrsCompetitionInfo> GetCompetitionInfoAsync(
        string competitionId,
        CancellationToken cancellationToken)
    {
        var path = $"competition_info_v2.php?id={Uri.EscapeDataString(competitionId)}&details=1";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return TempleOsrsCompetitionInfoParser.Parse(stream);
    }

    public static void ConfigureHttpClient(HttpClient client, TempleOsrsOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 120));
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
