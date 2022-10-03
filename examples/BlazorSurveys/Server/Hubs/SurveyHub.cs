using Microsoft.AspNetCore.SignalR;
using BlazorSurveys.Shared;

namespace BlazorSurveys.Server.Hubs; 

public interface ISurveyHub
{
    Task SurveyAdded(SurveySummary survey);
    Task SurveyUpdated(Survey survey);
}

public class SurveyHub: Hub<ISurveyHub>
{
    // No need to implement here the methods defined by ISurveyHub, their purpose is simply
    // to provide a strongly typed interface.
    // Users of IHubContext still have to decide to whom should the events be sent
    // as in: await this.hubContext.Clients.All.SendSurveyUpdated(survey);

    // These 2 methods will be called from the client
    public async Task JoinSurveyGroup(Guid surveyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, surveyId.ToString());
    }
    public async Task LeaveSurveyGroup(Guid surveyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, surveyId.ToString());
    }
}