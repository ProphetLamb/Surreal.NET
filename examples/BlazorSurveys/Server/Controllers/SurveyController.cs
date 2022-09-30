using BlazorSurveys.Server.Hubs;
using BlazorSurveys.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SurrealDB.Abstractions;
using SurrealDB.Models;

namespace BlazorSurveys.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveyController : ControllerBase
{
    private readonly IDatabase _db;
    private readonly IHubContext<SurveyHub, ISurveyHub> _hub;
    public SurveyController(IDatabase db, IHubContext<SurveyHub, ISurveyHub> surveyHub)
    {
        _db = db;
        _hub = surveyHub;
    }

    [HttpGet]
    public async Task<IEnumerable<SurveySummary>> GetSurveys()
    {
        await _db.Open();
        return (await _db.Select("survey")).TryGetResult(out Result res)
            ? res.GetArray<Survey>().Select(static s => s.ToSummary())
            : Enumerable.Empty<SurveySummary>();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetSurvey(Guid id)
    {
        return (await _db.Select($"survey:{id}")).TryGetResult(out Result res)
            ? new JsonResult(res.GetObject<Survey>())
            : NotFound();
    }

    // Note an [ApiController] will automatically return a 400 response if any
    // of the data annotation valiadations defined in AddSurveyModel fails
    [HttpPut]
    public async Task<ActionResult> AddSurvey([FromBody] AddSurveyModel addSurveyModel)
    {
        await _db.Open();
        Survey survey = new()
        {
            Title = addSurveyModel.Title,
            ExpiresAt = DateTime.Now.AddMinutes(addSurveyModel.Minutes ?? 10),
            Options = addSurveyModel.Options.Select(o => o.OptionValue).ToList()
        };

        if (!(await _db.Create($"survey:{survey.Id}", survey)).TryGetResult(out _))
        {
            return BadRequest();
        }

        await _hub.Clients.All.SurveyAdded(survey.ToSummary());
        return new JsonResult(survey);

    }

    [HttpPost("{surveyId}/answer")]
    public async Task<ActionResult> AnswerSurvey(Guid surveyId, [FromBody] SurveyAnswer answer)
    {
        await _db.Open();
        Thing thing = $"survey:{surveyId}";
        if (!(await _db.Query($"SELECT id,ExpiresAt FROM {thing}")).TryGetResult(out Result expired))
        {
            return NotFound();
        }

        if ((expired.GetObject<Expirable>() as IExpirable)?.IsExpired ?? true)
        {
            return BadRequest("This survey has expired");
        }

        SurveyAnswer a = new() { SurveyId = surveyId, Option = answer.Option };
        if ((await _db.Query(
                $"UPDATE {thing} PATCH $data RETURN AFTER",
                new Dictionary<string, object?> { ["data"] = new object[] { new { op = "add", path = "Answers/0", value = a }, }, }
            )).TryGetResult(out Result updated)
         && updated.GetObject<Survey>() is { } survey)
        {
            // Notify anyone connected to the survey group
            // ofc sending the entire survey all the time is inefficient, but enough in this tutorial
            await _hub.Clients.Group(surveyId.ToString()).SurveyUpdated(survey);

            return new JsonResult(survey);
        }

        return BadRequest("Unable to update the survey");
    }
}