using FluentAssertions;
using KulturHub.Domain.Entities;

namespace KulturHub.UnitTests.Domain;

public class InvitationTests
{
    // Rules:
    // - EnsureCanBeUsed returns Ok when the invitation is neither expired nor used.
    // - EnsureCanBeUsed returns Expired when ExpiresAt is in the past.
    // - EnsureCanBeUsed returns AlreadyUsed when UsedBy is set.
    // - Expired takes precedence over AlreadyUsed when both are true.

    private static Invitation BuildInvitation(DateTime expiresAt, Guid? usedBy = null)
        => Invitation.Reconstitute(
            Guid.NewGuid(),
            "K3P-R2A",
            usedBy,
            DateTime.UtcNow.AddDays(-1),
            expiresAt);

    [Fact]
    public void EnsureCanBeUsed_WhenNotExpiredAndNotUsed_ShouldReturnOk()
    {
        var invitation = BuildInvitation(DateTime.UtcNow.AddDays(7));

        invitation.EnsureCanBeUsed().Should().Be(InvitationValidation.Ok);
    }

    [Fact]
    public void EnsureCanBeUsed_WhenExpired_ShouldReturnExpired()
    {
        var invitation = BuildInvitation(DateTime.UtcNow.AddSeconds(-1));

        invitation.EnsureCanBeUsed().Should().Be(InvitationValidation.Expired);
    }

    [Fact]
    public void EnsureCanBeUsed_WhenUsed_ShouldReturnAlreadyUsed()
    {
        var invitation = BuildInvitation(DateTime.UtcNow.AddDays(7), Guid.NewGuid());

        invitation.EnsureCanBeUsed().Should().Be(InvitationValidation.AlreadyUsed);
    }

    [Fact]
    public void EnsureCanBeUsed_WhenExpiredAndUsed_ShouldReturnExpired()
    {
        var invitation = BuildInvitation(DateTime.UtcNow.AddSeconds(-1), Guid.NewGuid());

        invitation.EnsureCanBeUsed().Should().Be(InvitationValidation.Expired);
    }
}
