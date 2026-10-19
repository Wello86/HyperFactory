using BrownEvents.Api.Data;
using BrownEvents.Api.Dtos;
using BrownEvents.Api.Models;
using BrownEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrownEvents.Tests;

public class SessionRegistrationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Session CreateSession(AppDbContext context, int capacity)
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

        var session = new Session
        {
            Title = "Test Session",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1),
            Capacity = capacity,
            ConferenceId = conference.Id,
        };
        context.Sessions.Add(session);
        context.SaveChanges();
        return session;
    }

    private static SessionRegistration CreateWaitlistedRegistration(AppDbContext context, Session session, string email, DateTime registeredAt)
    {
        var attendee = new Attendee { FirstName = "Wait", LastName = "Lister", Email = email };
        context.Attendees.Add(attendee);
        context.SaveChanges();

        var registration = new SessionRegistration
        {
            SessionId = session.Id,
            AttendeeId = attendee.Id,
            RegisteredAt = registeredAt,
            Status = "WAITLISTED",
        };
        context.SessionRegistrations.Add(registration);
        context.SaveChanges();
        return registration;
    }

    private class CapturingLogger : ILogger<SessionRegistrationService>
    {
        public List<string> InformationMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                InformationMessages.Add(formatter(state, exception));
            }
        }
    }

    [Fact]
    public async Task RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);

        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);
        var attendee = new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };

        var result = await service.RegisterAttendeeAsync(session.Id, session.Capacity, attendee);

        Assert.Equal(SessionRegistrationOutcome.Success, result.Outcome);
        Assert.Equal(1, context.Attendees.Count());
        Assert.Equal(1, context.SessionRegistrations.Count());
        Assert.Equal(session.Id, result.Registration!.SessionId);
        Assert.Equal("CONFIRMED", result.Registration.Status);
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ReusesExistingAttendee_WhenEmailFound()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);

        var existingAttendee = new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };
        context.Attendees.Add(existingAttendee);
        context.SaveChanges();

        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);
        var duplicateEmailAttendee = new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Other", Email = "ADA@example.com" };

        var result = await service.RegisterAttendeeAsync(session.Id, session.Capacity, duplicateEmailAttendee);

        Assert.Equal(SessionRegistrationOutcome.Success, result.Outcome);
        Assert.Equal(1, context.Attendees.Count());
        Assert.Equal(existingAttendee.Id, result.Registration!.AttendeeId);
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ReturnsAlreadyRegistered_WhenSameEmailRegistersTwice()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var first = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        var second = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        Assert.Equal(SessionRegistrationOutcome.Success, first.Outcome);
        Assert.Equal(SessionRegistrationOutcome.AlreadyRegistered, second.Outcome);
        Assert.Equal(1, context.SessionRegistrations.Count());
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ReturnsSessionFull_WhenCapacityReached()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var first = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        var second = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        Assert.Equal(SessionRegistrationOutcome.Success, first.Outcome);
        Assert.Equal(SessionRegistrationOutcome.SessionFull, second.Outcome);
        Assert.DoesNotContain(context.SessionRegistrations, sr => sr.Status == "WAITLISTED");
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ReturnsSessionFull_WhenCapacityAlreadyReachedBySeededRegistration_ForBrandNewAttendee()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);

        var seededAttendee = new Attendee { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" };
        context.Attendees.Add(seededAttendee);
        context.SaveChanges();
        context.SessionRegistrations.Add(new SessionRegistration
        {
            SessionId = session.Id,
            AttendeeId = seededAttendee.Id,
            RegisteredAt = DateTime.UtcNow,
            Status = "CONFIRMED",
        });
        context.SaveChanges();

        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);
        var result = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        Assert.Equal(SessionRegistrationOutcome.SessionFull, result.Outcome);
        Assert.Equal(1, context.SessionRegistrations.Count());
    }

    [Fact]
    public async Task CancelRegistrationAsync_FreesSlot_ForSubsequentRegistration()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var first = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var cancellation = await service.CancelRegistrationAsync(session.Id, first.Registration!.Id);
        Assert.Equal(SessionCancellationOutcome.Success, cancellation.Outcome);

        var second = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        Assert.Equal(SessionRegistrationOutcome.Success, second.Outcome);
    }

    [Fact]
    public async Task CancelRegistrationAsync_ReturnsNotFound_WhenRegistrationDoesNotExist()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var result = await service.CancelRegistrationAsync(session.Id, registrationId: 999);

        Assert.Equal(SessionCancellationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task CancelRegistrationAsync_ReturnsNotFound_WhenAlreadyCancelled()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var registered = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        await service.CancelRegistrationAsync(session.Id, registered.Registration!.Id);

        var secondCancel = await service.CancelRegistrationAsync(session.Id, registered.Registration.Id);

        Assert.Equal(SessionCancellationOutcome.NotFound, secondCancel.Outcome);
    }

    [Fact]
    public async Task CancelRegistrationAsync_DoesNotPromote_WhenNoWaitlistExists()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var logger = new CapturingLogger();
        var service = new SessionRegistrationService(context, logger);

        var registered = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var cancellation = await service.CancelRegistrationAsync(session.Id, registered.Registration!.Id);

        Assert.Equal(SessionCancellationOutcome.Success, cancellation.Outcome);
        Assert.Empty(logger.InformationMessages);
    }

    [Fact]
    public async Task CancelRegistrationAsync_PromotesEarliestWaitlistedAttendee_InFifoOrder()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var confirmed = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var later = CreateWaitlistedRegistration(context, session, "later@example.com", DateTime.UtcNow.AddMinutes(5));
        var earlier = CreateWaitlistedRegistration(context, session, "earlier@example.com", DateTime.UtcNow.AddMinutes(1));

        var cancellation = await service.CancelRegistrationAsync(session.Id, confirmed.Registration!.Id);
        Assert.Equal(SessionCancellationOutcome.Success, cancellation.Outcome);

        await context.Entry(earlier).ReloadAsync();
        await context.Entry(later).ReloadAsync();

        Assert.Equal("CONFIRMED", earlier.Status);
        Assert.Equal("WAITLISTED", later.Status);
    }

    [Fact]
    public async Task CancelRegistrationAsync_LogsInformation_WhenPromotingWaitlistedAttendee()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var logger = new CapturingLogger();
        var service = new SessionRegistrationService(context, logger);

        var confirmed = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        var waitlisted = CreateWaitlistedRegistration(context, session, "grace@example.com", DateTime.UtcNow);

        await service.CancelRegistrationAsync(session.Id, confirmed.Registration!.Id);

        Assert.Single(logger.InformationMessages);
        Assert.Contains(waitlisted.AttendeeId.ToString(), logger.InformationMessages[0]);
        Assert.Contains(session.Id.ToString(), logger.InformationMessages[0]);
    }

    [Fact]
    public async Task JoinWaitlistAsync_AddsWaitlistedRegistration_WhenSessionAtCapacity()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var result = await service.JoinWaitlistAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        Assert.Equal(WaitlistJoinOutcome.Success, result.Outcome);
        Assert.Equal("WAITLISTED", result.Registration!.Status);
    }

    [Fact]
    public async Task JoinWaitlistAsync_ReturnsSessionNotFull_WhenCapacityNotReached()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var result = await service.JoinWaitlistAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        Assert.Equal(WaitlistJoinOutcome.SessionNotFull, result.Outcome);
        Assert.Equal(0, context.SessionRegistrations.Count());
        Assert.Equal(0, context.Attendees.Count());
    }

    [Fact]
    public async Task JoinWaitlistAsync_ReturnsAlreadyRegistered_WhenAttendeeAlreadyConfirmed()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var result = await service.JoinWaitlistAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        Assert.Equal(WaitlistJoinOutcome.AlreadyRegistered, result.Outcome);
    }

    [Fact]
    public async Task JoinWaitlistAsync_ReturnsAlreadyWaitlisted_WhenAttendeeAlreadyWaitlisted()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        await service.JoinWaitlistAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        var result = await service.JoinWaitlistAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        Assert.Equal(WaitlistJoinOutcome.AlreadyWaitlisted, result.Outcome);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCountAndCurrentAttendeeRegistration_MatchingEmailCaseInsensitively()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        var registered = await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com" });

        var summary = await service.GetSummaryAsync(session.Id, "ADA@EXAMPLE.COM");

        Assert.Equal(2, summary.RegisteredCount);
        Assert.Equal(registered.Registration!.Id, summary.CurrentAttendeeRegistrationId);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsNullCurrentAttendeeRegistration_WhenEmailNotProvided()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });

        var summary = await service.GetSummaryAsync(session.Id, email: null);

        Assert.Equal(1, summary.RegisteredCount);
        Assert.Null(summary.CurrentAttendeeRegistrationId);
    }

    [Fact]
    public async Task GetSummaryAsync_IncludesWaitlistCount()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 1);
        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);

        await service.RegisterAttendeeAsync(session.Id, session.Capacity,
            new AttendeeRegistrationRequestDto { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" });
        CreateWaitlistedRegistration(context, session, "grace@example.com", DateTime.UtcNow);
        CreateWaitlistedRegistration(context, session, "linus@example.com", DateTime.UtcNow.AddMinutes(1));

        var summary = await service.GetSummaryAsync(session.Id, email: null);

        Assert.Equal(2, summary.WaitlistCount);
    }

    [Fact]
    public async Task RegisterAttendeeAsync_ThrowsWhenCancelled()
    {
        using var context = CreateContext();
        var session = CreateSession(context, capacity: 5);

        var service = new SessionRegistrationService(context, NullLogger<SessionRegistrationService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RegisterAttendeeAsync(session.Id, session.Capacity,
                new AttendeeRegistrationRequestDto { Email = "a@b.com", FirstName = "A", LastName = "B" }, cts.Token));
    }
}
