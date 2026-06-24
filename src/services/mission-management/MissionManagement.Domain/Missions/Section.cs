using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>
/// The inert composite of the Composite pattern. A Section only carries a Title, an Order and an
/// ordered list of child <see cref="PathItem"/>s (Sections and Challenges, recursively). It never
/// holds game semantics and may be empty.
/// </summary>
public sealed class Section : PathItem
{
    private readonly List<PathItem> _children = new();

    private Section()
    {
    }

    private Section(Guid id, Guid missionId, Guid? parentSectionId, int order, string title)
        : base(id, missionId, parentSectionId, order)
    {
        Title = title;
    }

    public string Title { get; private set; } = string.Empty;

    public IReadOnlyList<PathItem> Children => _children;

    public static Section Create(
        Guid id,
        Guid missionId,
        Guid? parentSectionId,
        int order,
        string title,
        IReadOnlyList<PathItem>? children = null)
    {
        var section = new Section(
            id,
            missionId,
            parentSectionId,
            NormalizeOrder(order),
            DomainText.NormalizeRequired(title, "section_title_required", "Section title is required.", 120));

        section.SetChildren(children ?? Array.Empty<PathItem>());

        return section;
    }

    /// <summary>Attaches an already-constructed child during in-memory rehydration. Skips re-validation.</summary>
    internal void AttachChild(PathItem child)
    {
        _children.Add(child);
    }

    internal void SortChildren()
    {
        _children.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    private void SetChildren(IReadOnlyList<PathItem> children)
    {
        var ordered = children.OrderBy(child => child.Order).ToList();

        var duplicateOrder = ordered
            .GroupBy(child => child.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "path_item_order_duplicate",
                $"Path item order '{duplicateOrder.Key}' is duplicated among siblings.",
                UmbralFailureCategory.Validation);
        }

        _children.Clear();
        _children.AddRange(ordered);
    }
}
