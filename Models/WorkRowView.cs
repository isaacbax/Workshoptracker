namespace DesignSheet.Models;

public sealed class WorkRowView
{
    public bool IsSeparator { get; init; }
    public string SeparatorLabel { get; init; } = "";
    public WorkRow? Row { get; init; }

    public static WorkRowView Separator(string label) => new()
    {
        IsSeparator = true,
        SeparatorLabel = label,
        Row = null
    };

    public static WorkRowView Item(WorkRow row) => new()
    {
        IsSeparator = false,
        SeparatorLabel = "",
        Row = row
    };
}
