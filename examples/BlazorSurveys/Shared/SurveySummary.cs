namespace BlazorSurveys.Shared; 

public record SurveySummary: IExpirable
{
    public Guid Id { get; init; }
    public string Title { get; init; }
    public DateTime ExpiresAt { get; init; }
    public List<string> Options { get; init; }
}