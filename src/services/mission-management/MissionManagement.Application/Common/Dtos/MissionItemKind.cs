namespace MissionManagement.Application.Common.Dtos;

/// <summary>Discriminator for the polymorphic mission path-item contracts (Section vs Challenge).</summary>
public static class MissionItemKind
{
    public const string Section = "Section";
    public const string Challenge = "Challenge";
}
