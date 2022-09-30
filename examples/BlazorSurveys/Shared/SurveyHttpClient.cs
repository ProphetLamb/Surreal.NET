using System.Net.Http.Json;

namespace BlazorSurveys.Shared; 

// See https://docs.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-5.0#typed-httpclient
// TODO: consider error handling!
// Possibly configure a library like Polly to centralize error handling policies: https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory
public class SurveyHttpClient
{
    private readonly HttpClient http;

    public SurveyHttpClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<SurveySummary[]> GetSurveys()
    {
        return await http.GetFromJsonAsync<SurveySummary[]>("api/survey");
    }

    public async Task<Survey> GetSurvey(Guid surveyId)
    {
        return await http.GetFromJsonAsync<Survey>($"api/survey/{surveyId}");
    }

    public async Task<HttpResponseMessage> AddSurvey(AddSurveyModel survey)
    {
        return await http.PutAsJsonAsync<AddSurveyModel>("api/survey", survey);
    }

    public async Task<HttpResponseMessage> AnswerSurvey(Guid surveyId, SurveyAnswer answer)
    {
        return await http.PostAsJsonAsync<SurveyAnswer>($"api/survey/{surveyId}/answer", answer);
    }

}