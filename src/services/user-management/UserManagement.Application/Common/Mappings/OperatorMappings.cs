using UserManagement.Application.Common.Dtos;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Common.Mappings;

/// <summary>
/// Hand-written domain -> DTO mappings for the operator/participant read models. Kept explicit
/// (no AutoMapper/Mapster) so the projection is trivially traceable and allocation-free.
/// </summary>
public static class OperatorMappings
{
    public static OperatorDto ToDto(this OperatorUser user) =>
        new(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.IsActive);

    public static IReadOnlyList<OperatorDto> ToDtos(this IEnumerable<OperatorUser> users) =>
        users.Select(ToDto).ToArray();

    public static ParticipantDto ToParticipantDto(this OperatorUser user) =>
        new(user.Id, user.Username);
}
