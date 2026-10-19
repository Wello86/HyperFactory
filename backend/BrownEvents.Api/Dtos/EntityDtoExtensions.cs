using System;
using System.Linq;
using BrownEvents.Api.Models;

namespace BrownEvents.Api.Dtos;

public static class EntityDtoExtensions
{
    public static ConferenceDto ToDto(this Conference conference) => new()
    {
        Id = conference.Id,
        Title = conference.Title,
        Description = conference.Description,
        Location = conference.Location,
        Venue = conference.Venue,
        StartDate = conference.StartDate,
        EndDate = conference.EndDate,
        Status = conference.Status,
        MaxAttendees = conference.MaxAttendees,
        OrganizerName = conference.OrganizerName,
        Website = conference.Website,
    };

    public static SpeakerDto ToDto(this Speaker speaker) => new()
    {
        Id = speaker.Id,
        FirstName = speaker.FirstName,
        LastName = speaker.LastName,
        Bio = speaker.Bio,
        Email = speaker.Email,
    };

    public static SpeakerProfileDto ToProfileDto(this Speaker speaker) => new()
    {
        Id = speaker.Id,
        FirstName = speaker.FirstName,
        LastName = speaker.LastName,
        Bio = speaker.Bio,
        Email = speaker.Email,
        Sessions = speaker.Sessions.Select(s => new SpeakerSessionDto
        {
            SessionId = s.Id,
            Title = s.Title,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            ConferenceId = s.ConferenceId,
            ConferenceTitle = s.Conference.Title,
        }).ToList(),
    };

    public static RoomDto ToDto(this Room room) => new()
    {
        Id = room.Id,
        Name = room.Name,
        Capacity = room.Capacity,
        Location = room.Location,
    };

    public static SessionDto ToDto(this Session session, int registeredCount = 0, int waitlistCount = 0, int? currentAttendeeRegistrationId = null) => new()
    {
        Id = session.Id,
        Title = session.Title,
        Description = session.Description,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        Capacity = session.Capacity,
        ConferenceId = session.ConferenceId,
        SpeakerId = session.SpeakerId,
        RoomId = session.RoomId,
        Speaker = session.Speaker?.ToDto(),
        Room = session.Room?.ToDto(),
        RegisteredCount = registeredCount,
        WaitlistCount = waitlistCount,
        CurrentAttendeeRegistrationId = currentAttendeeRegistrationId,
    };

    public static AttendeeDto ToDto(this Attendee attendee) => new()
    {
        Id = attendee.Id,
        FirstName = attendee.FirstName,
        LastName = attendee.LastName,
        Email = attendee.Email,
    };

    public static RegistrationDto ToDto(this Registration registration) => new()
    {
        Id = registration.Id,
        RegisteredAt = registration.RegisteredAt,
        Status = registration.Status,
        ConferenceId = registration.ConferenceId,
        AttendeeId = registration.AttendeeId,
        Attendee = registration.Attendee.ToDto(),
        ConferenceTitle = registration.Conference.Title,
        ConferenceStartDate = registration.Conference.StartDate,
        ConferenceEndDate = registration.Conference.EndDate,
    };

    public static SessionRegistrationDto ToDto(this SessionRegistration registration) => new()
    {
        Id = registration.Id,
        RegisteredAt = registration.RegisteredAt,
        Status = registration.Status,
        SessionId = registration.SessionId,
        AttendeeId = registration.AttendeeId,
        Attendee = registration.Attendee.ToDto(),
    };

    public static PagedResult<TDto> ToDto<TEntity, TDto>(this PagedResult<TEntity> source, Func<TEntity, TDto> map) => new()
    {
        Data = source.Data.Select(map).ToList(),
        Page = source.Page,
        PageSize = source.PageSize,
        TotalCount = source.TotalCount,
    };
}
