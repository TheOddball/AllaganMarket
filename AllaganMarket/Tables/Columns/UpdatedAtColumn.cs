using System.Globalization;

using AllaganLib.Interface.Grid;
using AllaganLib.Interface.Grid.ColumnFilters;
using AllaganLib.Interface.Services;

using DalaMock.Host.Mediator;

using ImGuiNET;

namespace AllaganMarket.Tables.Columns;

public class UpdatedAtColumn(ImGuiService imGuiService, StringColumnFilter stringColumnFilter)
    : DateTimeColumn<SearchResultConfiguration, SearchResult, MessageBase>(imGuiService, stringColumnFilter)
{
    public override string? DefaultValue { get; set; } = null;

    public override string Key { get; set; } = "UpdatedAt";

    public override string Name { get; set; } = "Updated At";

    public override string? RenderName { get; set; } = null;

    public override int Width { get; set; } = 100;

    public override bool HideFilter { get; set; } = false;

    public override ImGuiTableColumnFlags ColumnFlags { get; set; } = ImGuiTableColumnFlags.None;

    public override string EmptyText { get; set; } = string.Empty;

    public override string HelpText { get; set; } =
        "When the item was last updated by either confirming the item is not undercut or by adjusting the price to be the lowest.";

    public override string Version { get; } = "1.0.0";

    public override string? CurrentValue(SearchResult item)
    {
        if (item.SaleItem != null && item.SaleItem.IsEmpty())
        {
            return null;
        }

        return item.SaleItem?.UpdatedAt.ToString(CultureInfo.CurrentCulture) ?? null;
    }
}
