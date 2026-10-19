using BrownEvents.Api.Data;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrownEvents.Tests;

public class RegistrationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Conference CreateConference(AppDbContext context)
    {
        var conference = new Conference
        {
            Title = "Test Conf",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = "UPCOMING"
        };
        context.Conferences.Add(conference);
        context.SaveChanges();
        return conference;
    }

    [Fact]
    public async Task RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);

        var service = new RegistrationService(context);
        var attendee = new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

        var registration = await service.RegisterAttendeeAsync(conference.Id, attendee);

        Assert.Equal(1, context.Attendees.Count());
        Assert.Equal(1, context.Registrations.Count());
        Assert.Equal(conference.Id, registration.ConferenceId);
        Assert.Equal("CONFIRMED", registration.Status);
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ReusesExistingAttendee_WhenEmailFound()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);

        var existingAttendee = new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };
        context.Attendees.Add(existingAttendee);
        context.SaveChanges();

        var service = new RegistrationService(context);
        var duplicateEmailAttendee = new Attendee { FirstName = "Ada", LastName = "Other", Email = "ada@example.com" };

        var registration = await service.RegisterAttendeeAsync(conference.Id, duplicateEmailAttendee);

        Assert.Equal(1, context.Attendees.Count());
        Assert.Equal(1, context.Registrations.Count());
        Assert.Equal(existingAttendee.Id, registration.AttendeeId);
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ThrowsWhenCancelled()
    {
        using var context = CreateContext();
        var conf = new Conference { Title = "C1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1), Status = "UPCOMING" };
        context.Conferences.Add(conf);
        await context.SaveChangesAsync();

        var service = new RegistrationService(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RegisterAttendeeAsync(conf.Id, new Attendee { Email = "a@b.com", FirstName = "A", LastName = "B" }, cts.Token));
    }

    [Fact]
    public async Task GetByAttendeeEmailAsync_ReturnsRegistration_WhenAttendeeHasOne()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var email = $"{Guid.NewGuid()}@example.com";

        var service = new RegistrationService(context);
        await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });

        var registrations = await service.GetByAttendeeEmailAsync(email);

        Assert.Single(registrations);
        Assert.Equal(conference.Id, registrations[0].ConferenceId);
    }

    [Fact]
    public async Task GetByAttendeeEmailAsync_ReturnsAllRegistrations_AcrossMultipleConferences()
    {
        using var context = CreateContext();
        var conferenceOne = CreateConference(context);
        var conferenceTwo = CreateConference(context);
        var email = $"{Guid.NewGuid()}@example.com";

        var service = new RegistrationService(context);
        await service.RegisterAttendeeAsync(conferenceOne.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });
        await service.RegisterAttendeeAsync(conferenceTwo.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });

        var registrations = await service.GetByAttendeeEmailAsync(email);

        Assert.Equal(2, registrations.Count);
        Assert.Contains(registrations, r => r.ConferenceId == conferenceOne.Id);
        Assert.Contains(registrations, r => r.ConferenceId == conferenceTwo.Id);
    }

    [Fact]
    public async Task GetByAttendeeEmailAsync_MatchesEmailCaseInsensitively()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var email = $"{Guid.NewGuid()}@Example.com";

        var service = new RegistrationService(context);
        await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });

        var registrations = await service.GetByAttendeeEmailAsync(email.ToUpperInvariant());

        Assert.Single(registrations);
    }

    [Fact]
    public async Task GetByAttendeeEmailAsync_ReturnsEmptyList_WhenAttendeeHasNoRegistrations()
    {
        using var context = CreateContext();
        var service = new RegistrationService(context);

        var registrations = await service.GetByAttendeeEmailAsync($"{Guid.NewGuid()}@example.com");

        Assert.Empty(registrations);
    }

    [Fact]
    public async Task AttendeeExistsAsync_ReturnsTrue_WhenEmailMatchesCaseInsensitively()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var email = $"{Guid.NewGuid()}@Example.com";

        var service = new RegistrationService(context);
        await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = email });

        var exists = await service.AttendeeExistsAsync(email.ToUpperInvariant());

        Assert.True(exists);
    }

    [Fact]
    public async Task AttendeeExistsAsync_ReturnsFalse_WhenNoAttendeeMatches()
    {
        using var context = CreateContext();
        var service = new RegistrationService(context);

        var exists = await service.AttendeeExistsAsync($"{Guid.NewGuid()}@example.com");

        Assert.False(exists);
    }

    [Fact]
    public async Task CancelRegistrationAsync_SetsStatusToCancelled_OnSuccess()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var service = new RegistrationService(context);
        var registration = await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var result = await service.CancelRegistrationAsync(conference.Id, registration.Id);

        Assert.Equal(RegistrationCancellationOutcome.Success, result.Outcome);
        var updated = await context.Registrations.FindAsync(registration.Id);
        Assert.Equal("CANCELLED", updated!.Status);
    }

    [Fact]
    public async Task CancelRegistrationAsync_ReturnsNotFound_WhenRegistrationIdDoesNotExist()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var service = new RegistrationService(context);

        var result = await service.CancelRegistrationAsync(conference.Id, 9999);

        Assert.Equal(RegistrationCancellationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task CancelRegistrationAsync_ReturnsNotFound_WhenConferenceIdDoesNotMatch()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var otherConference = CreateConference(context);
        var service = new RegistrationService(context);
        var registration = await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var result = await service.CancelRegistrationAsync(otherConference.Id, registration.Id);

        Assert.Equal(RegistrationCancellationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task CancelRegistrationAsync_ReturnsNotFound_WhenAlreadyCancelled()
    {
        using var context = CreateContext();
        var conference = CreateConference(context);
        var service = new RegistrationService(context);
        var registration = await service.RegisterAttendeeAsync(conference.Id, new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        await service.CancelRegistrationAsync(conference.Id, registration.Id);

        var result = await service.CancelRegistrationAsync(conference.Id, registration.Id);

        Assert.Equal(RegistrationCancellationOutcome.NotFound, result.Outcome);
    }
}
