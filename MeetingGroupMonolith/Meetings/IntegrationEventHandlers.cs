using Marten;
using MeetingGroupMonolith;

namespace Meetings;

/// <summary>
/// Handles NewUserRegisteredEvent from the Registrations module.
/// Creates a Member document in the Meetings module.
/// Replaces: InboxMessage → ProcessInboxJob → MediatR Notification → handler
/// </summary>
public static class NewUserRegisteredHandler
{
    public static void Handle(NewUserRegisteredEvent message, IDocumentSession session)
    {
        session.Store(new Member
        {
            Id = message.UserId,
            Login = message.Login,
            Email = message.Email,
            FirstName = message.FirstName,
            LastName = message.LastName,
        });
    }
}

/// <summary>
/// Handles MeetingGroupProposalAcceptedEvent from the Administration module.
/// Creates the actual MeetingGroup.
/// </summary>
public static class MeetingGroupProposalAcceptedHandler
{
    public static void Handle(MeetingGroupProposalAcceptedEvent message, IDocumentSession session)
    {
        session.Store(new MeetingGroup
        {
            Id = Guid.NewGuid(),
            Name = message.Name,
            Description = message.Description,
            LocationCity = message.LocationCity,
            LocationCountryCode = message.LocationCountryCode,
            CreatorId = message.ProposalUserId,
            Members = [new MeetingGroupMember { MemberId = message.ProposalUserId, Role = "Organizer", JoinedAt = DateTimeOffset.UtcNow }],
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}

/// <summary>
/// Handles SubscriptionExpirationChangedEvent from the Payments module.
/// </summary>
public static class SubscriptionExpirationChangedHandler
{
    public static async Task Handle(SubscriptionExpirationChangedEvent message, IDocumentSession session)
    {
        var member = await session.LoadAsync<Member>(message.PayerId);
        if (member is null) return;

        member.SubscriptionExpirationDate = message.ExpirationDate;
        session.Store(member);
    }
}
