using System.Text.Json;
using FluentAssertions;
using KulturHub.Application.Features.Platform.Memberships.InviteMembership;
using KulturHub.Application.Features.Platform.Memberships.ListMemberships;
using KulturHub.Domain.Memberships;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.JsonSerialization;

public class MembershipResponseJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void MembershipResponse_ShouldSerializeStatusAsNumber()
    {
        var response = new MembershipResponse(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FullName: "Max Mustermann",
            Email: "max@example.com",
            InvitedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DecidedAt: new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
            Status: MembershipStatus.Accepted);

        var json = JsonSerializer.Serialize(response, Options);

        using var document = JsonDocument.Parse(json);
        var statusElement = document.RootElement.GetProperty("status");
        statusElement.ValueKind.Should().Be(JsonValueKind.Number);
        statusElement.GetInt32().Should().Be((int)MembershipStatus.Accepted);
    }

    [Fact]
    public void InviteMembershipResponse_ShouldSerializeStatusAsNumber()
    {
        var response = new InviteMembershipResponse(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FullName: "Erika Musterfrau",
            Email: "erika@example.com",
            InvitedAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DecidedAt: null,
            Status: MembershipStatus.Pending);

        var json = JsonSerializer.Serialize(response, Options);

        using var document = JsonDocument.Parse(json);
        var statusElement = document.RootElement.GetProperty("status");
        statusElement.ValueKind.Should().Be(JsonValueKind.Number);
        statusElement.GetInt32().Should().Be((int)MembershipStatus.Pending);
    }

    [Theory]
    [InlineData(MembershipStatus.Pending, 0)]
    [InlineData(MembershipStatus.Accepted, 1)]
    [InlineData(MembershipStatus.Rejected, 2)]
    public void MembershipStatus_ShouldSerializeAsCorrespondingIntValue(MembershipStatus status, int expected)
    {
        var response = new MembershipResponse(
            Id: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            FullName: "Test",
            Email: "test@example.com",
            InvitedAt: DateTime.UtcNow,
            DecidedAt: null,
            Status: status);

        var json = JsonSerializer.Serialize(response, Options);

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("status").GetInt32().Should().Be(expected);
    }
}