using System.Text.Json.Serialization;

namespace BlazorSurveys.Shared;

public record Survey : IExpirable
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; }
    public DateTime ExpiresAt { get; init; }
    public List<string> Options { get; init; } = new();
    public List<SurveyAnswer> Answers { get; init; } = new();

    public SurveySummary ToSummary() => new()
    {
        Id = Id,
        Title = Title,
        Options = Options,
        ExpiresAt = ExpiresAt
    };
}

public record SurveyAnswer
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SurveyId { get; init; }
    public string Option { get; init; }
}