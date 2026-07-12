namespace MissionManagement.Domain.Missions;

/// <summary>
/// Component of the Composite pattern. A Path Item belongs to a <see cref="Mission"/> and may sit at
/// the mission root (<see cref="ParentSectionId"/> is null) or inside a containing <see cref="Section"/>.
/// Concrete subtypes: <see cref="Section"/> (inert composite) and <see cref="Challenge"/> (playable leaf).
/// </summary>
public abstract class PathItem
{
    private protected PathItem()
    {
    }

    private protected PathItem(Guid id, Guid missionId, Guid? parentSectionId, int order)
    {
        Id = id;
        MissionId = missionId;
        ParentSectionId = parentSectionId;
        Order = order;
    }

    public Guid Id { get; private protected set; }

    public Guid MissionId { get; private protected set; }

    /// <summary>The containing <see cref="Section"/>, or null when the item sits at the mission root.</summary>
    public Guid? ParentSectionId { get; private protected set; }

    public int Order { get; private protected set; }

    private protected static int NormalizeOrder(int order)
    {
        return DomainText.NormalizeOrder(order, "path_item_order_invalid", "Path item order must be greater than zero.");
    }
}
