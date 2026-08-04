using MsOptions = Microsoft.Extensions.Options.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpsCentral.Controllers.Webhooks;
using OpsCentral.Data;
using OpsCentral.Models.Dto;
using OpsCentral.Models.Entities;
using OpsCentral.Options;

namespace OpsCentral.Tests;

public class JenkinsCallbackControllerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;

    public JenkinsCallbackControllerTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static JenkinsCallbackController CreateController(AppDbContext db, string sharedSecret) =>
        new(db, MsOptions.Create(new JenkinsOptions { CallbackSharedSecret = sharedSecret }));

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenTokenHeaderIsMissing()
    {
        var controller = CreateController(_db, "expected-secret");
        var payload = new JenkinsCallbackPayload { CorrelationId = Guid.NewGuid(), Status = "Succeeded" };

        var result = await controller.Callback(payload, callbackToken: null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_ReturnsUnauthorized_WhenTokenDoesNotMatch()
    {
        var controller = CreateController(_db, "expected-secret");
        var payload = new JenkinsCallbackPayload { CorrelationId = Guid.NewGuid(), Status = "Succeeded" };

        var result = await controller.Callback(payload, callbackToken: "wrong-secret", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Callback_ReturnsNotFound_WhenCorrelationIdIsUnknown()
    {
        var controller = CreateController(_db, "expected-secret");
        var payload = new JenkinsCallbackPayload { CorrelationId = Guid.NewGuid(), Status = "Succeeded" };

        var result = await controller.Callback(payload, callbackToken: "expected-secret", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Callback_UpdatesTheRequest_WhenTokenAndCorrelationIdAreValid()
    {
        var request = new AdActionRequest
        {
            ActionType = AdActionType.Unlock.ToString(),
            Input = "test.user",
            DispatchTarget = DispatchTarget.Jenkins.ToString(),
            Status = JobStatus.Running.ToString()
        };
        _db.AdActionRequests.Add(request);
        await _db.SaveChangesAsync();

        var controller = CreateController(_db, "expected-secret");
        var payload = new JenkinsCallbackPayload
        {
            CorrelationId = request.Id,
            Status = JobStatus.Succeeded.ToString(),
            ActionNote = "Done.",
            BuildUrl = "https://jenkins.example/job/1"
        };

        var result = await controller.Callback(payload, callbackToken: "expected-secret", CancellationToken.None);

        Assert.IsType<OkResult>(result);

        var updated = await _db.AdActionRequests.FindAsync(request.Id);
        Assert.NotNull(updated);
        Assert.Equal(JobStatus.Succeeded.ToString(), updated!.Status);
        Assert.Equal("Done.", updated.ActionNote);
        Assert.Equal("https://jenkins.example/job/1", updated.ExternalJobUrl);
        Assert.NotNull(updated.CallbackReceivedAtUtc);
    }
}
