﻿

using AllaganLib.Data.Service;
using AllaganLib.Interface.Widgets;
using AllaganLib.Shared.Extensions;

using AllaganMarket.Settings;

using DalaMock.Host.Mediator;

namespace AllaganMarket.Windows;

using System;
using System.Globalization;
using System.Linq;
using DalaMock.Shared.Interfaces;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using System.Numerics;
using Extensions;
using Filtering;
using Humanizer;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Models;
using Services;
using Services.Interfaces;

public class MainWindow : ExtendedWindow, IDisposable
{
    public enum MainWindowTab
    {
        CurrentlySelling,
        SalesHistory
    }

    private readonly ExcelSheet<Item> itemSheet;
    private readonly IPluginLog pluginLog;
    private readonly SaleFilter saleFilter;
    private readonly NumberFormatInfo gilNumberFormat;
    private readonly ItemUpdatePeriodSetting itemUpdatePeriodSetting;
    private readonly ICommandManager commandManager;
    private readonly IFont font;
    private readonly CsvLoaderService csvLoaderService;
    private readonly IFileDialogManager fileDialogManager;
    private readonly ExcelSheet<World> worldSheet;
    private bool filterMenuOpen;

    public MainWindow(
        MediatorService mediatorService,
        ImGuiService imGuiService,
        Configuration configuration,
        ITextureProvider textureProvider,
        ConfigWindow configWindow,
        IPluginLog pluginLog,
        RetainerMarketService retainerMarketService,
        SaleTrackerService saleTrackerService,
        ICharacterMonitorService characterMonitorService,
        IDataManager dataManager,
        SaleFilter saleFilter,
        NumberFormatInfo gilNumberFormat,
        ItemUpdatePeriodSetting itemUpdatePeriodSetting,
        ICommandManager commandManager,
        IFont font,
        CsvLoaderService csvLoaderService,
        IFileDialogManager fileDialogManager)
        : base(mediatorService, imGuiService, "Allagan Market##AllaganMarkets")
    {
        this.pluginLog = pluginLog;
        this.saleFilter = saleFilter;
        this.gilNumberFormat = gilNumberFormat;
        this.itemUpdatePeriodSetting = itemUpdatePeriodSetting;
        this.commandManager = commandManager;
        this.font = font;
        this.csvLoaderService = csvLoaderService;
        this.fileDialogManager = fileDialogManager;
        this.Configuration = configuration;
        this.TextureProvider = textureProvider;
        this.ConfigWindow = configWindow;
        this.RetainerMarketService = retainerMarketService;
        this.SaleTrackerService = saleTrackerService;
        this.CharacterMonitorService = characterMonitorService;
        this.DataManager = dataManager;
        this.itemSheet = this.DataManager.GetExcelSheet<Item>()!;
        this.worldSheet = this.DataManager.GetExcelSheet<World>()!;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.Size = new Vector2(600, 600);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.MenuBar;
        this.SaleTrackerService.SnapshotCreated += this.SnapshotCreated;
    }

    private void SnapshotCreated()
    {
        this.saleFilter.RequestRefresh();
    }

    public Configuration Configuration { get; }

    public ITextureProvider TextureProvider { get; }

    public ConfigWindow ConfigWindow { get; }

    public RetainerMarketService RetainerMarketService { get; }

    public SaleTrackerService SaleTrackerService { get; }

    public ICharacterMonitorService CharacterMonitorService { get; }

    public IDataManager DataManager { get; }

    private MainWindowTab SelectedTab { get; set; }

    public void Dispose()
    {
        this.SaleTrackerService.SnapshotCreated -= this.SnapshotCreated;
    }

    public override void Draw()
    {
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Configuration"))
                {
                    this.MediatorService.Publish(new OpenWindowMessage(typeof(ConfigWindow)));
                }

                if (ImGui.MenuItem("Report a Issue"))
                {
                    "https://github.com/Critical-Impact/AllaganMarket".OpenBrowser();
                }

                if (ImGui.MenuItem("Ko-Fi"))
                {
                    "https://ko-fi.com/critical_impact".OpenBrowser();
                }


                if (ImGui.MenuItem("Close"))
                {
                    this.IsOpen = false;
                }

                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("Export"))
            {
                if (ImGui.MenuItem("Export Current Sales (CSV)"))
                {
                    this.fileDialogManager.SaveFileDialog("Select a save location",
                                                          "*.csv",
                                                          "Current Sales.csv",
                                                          ".csv",
                                                          (success, fileName) =>
                                                          {
                                                              if (success)
                                                              {
                                                                  this.csvLoaderService.ToCsv(
                                                                      this.Configuration.SaleItems
                                                                          .SelectMany(c => c.Value).ToList(),
                                                                      fileName,
                                                                      true);
                                                              }
                                                          });
                }

                if (ImGui.MenuItem("Export History (CSV)"))
                {
                    this.fileDialogManager.SaveFileDialog("Select a save location",
                                                          "*.csv",
                                                          "Sales History.csv",
                                                          ".csv",
                                                          (success, fileName) =>
                                                          {
                                                              if (success)
                                                              {
                                                                  this.csvLoaderService.ToCsv(
                                                                      this.Configuration.Sales
                                                                          .SelectMany(c => c.Value).ToList(),
                                                                      fileName,
                                                                      true);
                                                              }
                                                          });
                }

                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }
        this.DrawCharacterSideBar();
        ImGui.SameLine();
        this.DrawSalesPane();
        ImGui.SameLine();
        this.DrawFilterSideBar();
    }

    private string? DrawCharacterRightClickMenu(Character character)
    {
        using var popup = ImRaii.Popup("rc_" + character.CharacterId);
        if (!popup)
        {
            return null;
        }

        ImGui.Text(character.Name);
        ImGui.Separator();
        if (ImGui.Selectable("Delete Character"))
        {
            return "Delete Character?##dc_" + character.CharacterId;
        }

        return null;
    }

    private void DrawCharacterDeleteConfirmPopup(Character character)
    {
        var open = true;
        using var popupModal = ImRaii.PopupModal(
            "Delete Character?##dc_" + character.CharacterId,
            ref open,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        if (!popupModal)
        {
            return;
        }

        ImGui.TextUnformatted(
            "Are you sure you want to delete this character? Any retainers owned by this retainer will also be removed.");
        if (ImGui.Button("Confirm"))
        {
            this.CharacterMonitorService.RemoveCharacter(character.CharacterId);
            if (character.CharacterId == this.saleFilter.CharacterId)
            {
                this.saleFilter.CharacterId = null;
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawCharacterSideBar()
    {
        using (var characters = ImRaii.Child("characters", new Vector2(200, 0)))
        {
            if (characters.Success)
            {
                using (var mainSection = ImRaii.Child("mainSection", new Vector2(0, -30)))
                {
                    if (mainSection)
                    {
                        if (this.CharacterMonitorService.Characters.Count == 0)
                        {
                            ImGui.TextWrapped("No characters found. Please login.");
                        }

                        var worlds = this.CharacterMonitorService.GetWorldIds(CharacterType.Character);
                        if (worlds.Count == 0)
                        {
                            ImGui.TextWrapped("No worlds found. Please login.");
                        }
                        foreach (var worldId in worlds)
                        {
                            var world = this.worldSheet.GetRow(worldId)!;
                            if (ImGui.Selectable(
                                    world.Name.AsReadOnly().ExtractText(),
                                    this.saleFilter.WorldId == worldId))
                            {
                                this.SwitchWorld(worldId);
                            }

                            if (ImGui.IsItemHovered())
                            {
                                using (var tooltip = ImRaii.Tooltip())
                                {
                                    if (tooltip)
                                    {
                                        ImGui.Text("Left Click: Select/Unselect");
                                    }
                                }
                            }

                            using (ImRaii.PushIndent())
                            {
                                foreach (var character in this.CharacterMonitorService.GetCharactersByType(
                                             CharacterType.Character,
                                             worldId))
                                {
                                    if (ImGui.Selectable(
                                            character.Name,
                                            this.saleFilter.CharacterId == character.CharacterId))
                                    {
                                        this.SwitchCharacter(character.CharacterId);
                                    }

                                    if (ImGui.IsItemHovered())
                                    {
                                        using (var tooltip = ImRaii.Tooltip())
                                        {
                                            if (tooltip)
                                            {
                                                ImGui.Text("Left Click: Select/Unselect");
                                                ImGui.Text("Right Click: Menu");
                                            }
                                        }

                                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                        {
                                            ImGui.OpenPopup("rc_" + character.CharacterId);
                                        }
                                    }

                                    var result = this.DrawCharacterRightClickMenu(character);
                                    if (result != null)
                                    {
                                        ImGui.OpenPopup(result);
                                    }

                                    this.DrawCharacterDeleteConfirmPopup(character);

                                    ImGui.Separator();
                                    using (ImRaii.PushIndent())
                                    {
                                        foreach (var retainer in this.CharacterMonitorService.GetOwnedCharacters(
                                                     character.CharacterId,
                                                     CharacterType.Retainer))
                                        {
                                            if (ImGui.Selectable(
                                                    retainer.Name,
                                                    this.saleFilter.CharacterId == retainer.CharacterId))
                                            {
                                                this.SwitchCharacter(retainer.CharacterId);
                                            }

                                            if (ImGui.IsItemHovered())
                                            {
                                                using (var tooltip = ImRaii.Tooltip())
                                                {
                                                    if (tooltip)
                                                    {
                                                        ImGui.Text("Left Click: Select/Unselect");
                                                        ImGui.Text("Right Click: Menu");
                                                    }
                                                }

                                                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                                                {
                                                    ImGui.OpenPopup("rc_" + retainer.CharacterId);
                                                }
                                            }

                                            var retainerResult = this.DrawCharacterRightClickMenu(retainer);
                                            if (retainerResult != null)
                                            {
                                                ImGui.OpenPopup(retainerResult);
                                            }

                                            this.DrawCharacterDeleteConfirmPopup(retainer);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                using (var bottomBar = ImRaii.Child(
                           "bottomBar",
                           new Vector2(0, 0),
                           false,
                           ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (bottomBar)
                    {
                        ImGui.Separator();
                    }
                }
            }
        }
    }

    private void DrawSalesPane()
    {
        using (var saleItems = ImRaii.Child("saleItems", new Vector2(this.filterMenuOpen ? -200 : 0, 0)))
        {
            if (saleItems)
            {
                using (var mainSection = ImRaii.Child("mainSection", new Vector2(0, -30)))
                {
                    if (mainSection)
                    {
                        using (var tabBar = ImRaii.TabBar("mainTabs"))
                        {
                            if (tabBar)
                            {
                                using (var currentSales = ImRaii.TabItem("Currently Selling"))
                                {
                                    if (currentSales)
                                    {
                                        this.ImGuiService.HoverTooltip(
                                            "These are the items you currently have for sale.");
                                        this.SelectedTab = MainWindowTab.CurrentlySelling;
                                        this.DrawButtonBar();
                                        this.DrawCurrentlySelling();
                                    }
                                }
                                

                                using (var recentSales = ImRaii.TabItem("Sale History"))
                                {
                                    if (recentSales)
                                    {
                                        this.ImGuiService.HoverTooltip("These are the items that you have sold.");
                                        this.SelectedTab = MainWindowTab.SalesHistory;
                                        this.DrawButtonBar();
                                        this.DrawSalesHistory();
                                    }
                                }

                                this.ImGuiService.HoverTooltip("These are the items that you have sold.");
                            }
                        }
                    }
                }

                using (var bottomBar = ImRaii.Child("bottomBar", new Vector2(0, 0), false))
                {
                    if (bottomBar)
                    {
                        ImGui.Separator();
                        if (this.RetainerMarketService.InBadState)
                        {
                            ImGui.PushTextWrapPos();
                            ImGui.Text(
                                "It looks like you opened your retainer before this plugin loaded. Please go back to the retainer list and reopen the retainer. The only time this happens is when the plugin is updated or you have just installed the plugin.");
                            ImGui.PopTextWrapPos();
                        }

                        if (this.SelectedTab == MainWindowTab.CurrentlySelling)
                        {
                            ImGui.AlignTextToFramePadding();
                            ImGui.Text(
                                $"{this.saleFilter.GetSaleItems().Count} items for sale on {this.GetSelectedName()} worth {this.saleFilter.AggregateSalesTotalGil.ToString("C", this.gilNumberFormat)}");
                        }
                        else if (this.SelectedTab == MainWindowTab.SalesHistory)
                        {
                            ImGui.AlignTextToFramePadding();
                            ImGui.Text(
                                $"{this.saleFilter.GetSoldItems().Count} items sold on {this.GetSelectedName()} worth {this.saleFilter.AggregateSoldTotalGil.ToString("C", this.gilNumberFormat)}");
                        }

                        var selectedGil = this.GetSelectedGil();
                        if (selectedGil != null)
                        {
                            ImGui.SameLine();
                            ImGui.Text(
                                $"{this.GetSelectedName()} has {selectedGil.Value.ToString("C", this.gilNumberFormat)} stored.");
                        }
                    }
                }
            }
        }
    }

    private void DrawBooleanTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using (var tooltip = ImRaii.Tooltip())
            {
                if (tooltip)
                {
                    ImGui.Text(fieldText);
                }
            }
        }
    }

    private void DrawStringTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using (var tooltip = ImRaii.Tooltip())
            {
                if (tooltip)
                {
                    ImGui.Text(fieldText);
                    ImGui.Text("When searching the following operators can be used to compare: ");
                    ImGui.Separator();
                    ImGui.Text("=, for exact comparisons");
                    ImGui.Text("!, for inequality comparisons");
                    ImGui.Text("||, search multiple expressions using OR");
                    ImGui.Text("&&, search multiple expressions using AND");
                }
            }
        }
    }

    private void DrawNumericTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using (var tooltip = ImRaii.Tooltip())
            {
                if (tooltip)
                {
                    ImGui.Text(fieldText);
                    ImGui.Text("When searching the following operators can be used to compare: ");
                    ImGui.Separator();
                    ImGui.Text(">, >=, <, <=, =, for numeric comparisons");
                    ImGui.Text("=, for exact comparisons");
                    ImGui.Text("!, for inequality comparisons");
                    ImGui.Text("||, search multiple expressions using OR");
                    ImGui.Text("&&, search multiple expressions using AND");
                }
            }
        }
    }

    private void DrawDateTooltip(string fieldText)
    {
        ImGui.Text("?");
        if (ImGui.IsItemHovered())
        {
            using (var tooltip = ImRaii.Tooltip())
            {
                if (tooltip)
                {
                    ImGui.Text(fieldText);
                    ImGui.Text("When searching the following operators can be used to compare: ");
                    ImGui.Separator();
                    ImGui.Text("2 hours ago, finds all entries 2 hours and greater");
                    ImGui.Text(">, >=, <, <=, =, for date comparisons");
                    ImGui.Text("=, for exact comparisons");
                    ImGui.Text("!, for inequality comparisons");
                    ImGui.Text("||, search multiple expressions using OR");
                    ImGui.Text("&&, search multiple expressions using AND");
                }
            }
        }
    }

    private void DrawFilterSideBar()
    {
        using (var filterMenu = ImRaii.Child("filterMenu", new Vector2(0, 0)))
        {
            if (filterMenu.Success)
            {
                using (var searchMain = ImRaii.Child("searchMain", new Vector2(0, -30)))
                {
                    if (searchMain)
                    {
                        ImGui.Text("Search");
                        ImGui.Separator();
                        ImGui.Text("Name");
                        var itemName = this.saleFilter.ItemName ?? "";
                        if (ImGui.InputText("##nameFilter", ref itemName, 200))
                        {
                            var newItemName = itemName == "" ? null : itemName;
                            if (this.saleFilter.ItemName != newItemName)
                            {
                                this.saleFilter.ItemName = newItemName;
                            }
                        }

                        ImGui.SameLine();
                        this.DrawStringTooltip("Search against the name of the item listed.");

                        string isHq;
                        switch (this.saleFilter.IsHq)
                        {
                            case null:
                                isHq = "N/A";
                                break;
                            case false:
                                isHq = "No";
                                break;
                            case true:
                                isHq = "Yes";
                                break;
                        }

                        ImGui.Text("Is HQ?");
                        using (var combo = ImRaii.Combo("##isHq", isHq))
                        {
                            if (combo)
                            {
                                if (ImGui.Selectable("N/A", isHq == "N/A"))
                                {
                                    this.saleFilter.IsHq = null;
                                }

                                if (ImGui.Selectable("No", isHq == "No"))
                                {
                                    this.saleFilter.IsHq = false;
                                }

                                if (ImGui.Selectable("Yes", isHq == "Yes"))
                                {
                                    this.saleFilter.IsHq = true;
                                }
                            }
                        }

                        ImGui.SameLine();
                        this.DrawBooleanTooltip("Search against the quality of the item listed.");

                        string needsUpdate;
                        switch (this.saleFilter.NeedUpdating)
                        {
                            case null:
                                needsUpdate = "N/A";
                                break;
                            case false:
                                needsUpdate = "No";
                                break;
                            case true:
                                needsUpdate = "Yes";
                                break;
                        }

                        ImGui.Text("Needs Update?");
                        using (var combo = ImRaii.Combo("##needsUpdate", needsUpdate))
                        {
                            if (combo)
                            {
                                if (ImGui.Selectable("N/A", isHq == "N/A"))
                                {
                                    this.saleFilter.NeedUpdating = null;
                                }

                                if (ImGui.Selectable("No", isHq == "No"))
                                {
                                    this.saleFilter.NeedUpdating = false;
                                }

                                if (ImGui.Selectable("Yes", isHq == "Yes"))
                                {
                                    this.saleFilter.NeedUpdating = true;
                                }
                            }
                        }

                        ImGui.SameLine();
                        this.DrawBooleanTooltip("Does this item need to be updated? Checking to see if you are undercut in your retainer will reset the last updated flag on an item.");

                        ImGui.Text("Quantity:");
                        var quantity = this.saleFilter.Quantity ?? "";
                        if (ImGui.InputText("##quantityFilter", ref quantity, 200))
                        {
                            var newQuantity = quantity == "" ? null : quantity;
                            if (this.saleFilter.Quantity != newQuantity)
                            {
                                this.saleFilter.Quantity = quantity;
                            }
                        }

                        ImGui.SameLine();
                        this.DrawNumericTooltip("Search against the quantity of the item listed.");

                        ImGui.Text("Unit Price:");
                        var unitPrice = this.saleFilter.UnitPrice ?? "";
                        if (ImGui.InputText("##unitPriceFilter", ref unitPrice, 200))
                        {
                            var newUnitPrice = unitPrice == "" ? null : unitPrice;
                            if (this.saleFilter.UnitPrice != newUnitPrice)
                            {
                                this.saleFilter.UnitPrice = unitPrice;
                            }
                        }

                        ImGui.SameLine();
                        this.DrawNumericTooltip("Search against the unit price of the item listed.");

                        ImGui.Text("Total:");
                        var total = this.saleFilter.Total ?? "";
                        if (ImGui.InputText("##totalFilter", ref total, 200))
                        {
                            var newTotal = total == "" ? null : total;
                            if (this.saleFilter.Total != newTotal)
                            {
                                this.saleFilter.Total = total;
                            }
                        }

                        ImGui.SameLine();
                        this.DrawNumericTooltip("Search against the total of the item listed.");

                        if (this.SelectedTab == MainWindowTab.CurrentlySelling)
                        {
                            ImGui.Text("Listed At:");
                            var listedAt = this.saleFilter.ListedAt ?? "";
                            if (ImGui.InputText("##listedAt", ref listedAt, 300))
                            {
                                var newListedAt = listedAt == "" ? null : listedAt;
                                if (this.saleFilter.ListedAt != newListedAt)
                                {
                                    this.saleFilter.ListedAt = newListedAt;
                                }
                            }

                            ImGui.SameLine();
                            this.DrawDateTooltip("Search against the listed at date of the item.");

                            ImGui.Text("Updated At:");
                            var updatedAt = this.saleFilter.UpdatedAt ?? "";
                            if (ImGui.InputText("##updatedAt", ref updatedAt, 300))
                            {
                                var newUpdatedAt = updatedAt == "" ? null : updatedAt;
                                if (this.saleFilter.UpdatedAt != newUpdatedAt)
                                {
                                    this.saleFilter.UpdatedAt = newUpdatedAt;
                                }
                            }

                            ImGui.SameLine();
                            this.DrawDateTooltip("Search against the updated at date of the item.");
                        }
                        else
                        {
                            ImGui.Text("Sold At:");
                            var soldAt = this.saleFilter.SoldAt ?? "";
                            if (ImGui.InputText("##soldAt", ref soldAt, 300))
                            {
                                var newSoldAt = soldAt == "" ? null : soldAt;
                                if (this.saleFilter.SoldAt != newSoldAt)
                                {
                                    this.saleFilter.SoldAt = newSoldAt;
                                }
                            }

                            ImGui.SameLine();
                            this.DrawDateTooltip("Search against the sold at date of the item.");
                        }
                    }
                }

                using (var bottomBar = ImRaii.Child(
                           "bottomBar",
                           new Vector2(0, 0),
                           false,
                           ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (bottomBar)
                    {
                        ImGui.Separator();
                        using (var iconFont = ImRaii.PushFont(this.font.IconFont))
                        {
                            using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f))
                            {
                                if (ImGui.Button(FontAwesomeIcon.Times.ToIconString(), new Vector2(22, 22)))
                                {
                                    this.saleFilter.Clear();
                                }

                                iconFont.Pop();
                                this.ImGuiService.HoverTooltip("Clear Search");
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawCurrentlySelling()
    {
        var widthAvailable = ImGui.GetContentRegionAvail().X;
        var totalItems = Math.Floor(widthAvailable / 300);
        using (var currentlySelling = ImRaii.Child("currentlySelling", new Vector2(0, 0), false))
        {
            if (currentlySelling)
            {
                var retainerItems = this.saleFilter.GetSaleItems();
                var iterateList = retainerItems.Select(c => c).SortByRetainerMarketOrder(this.itemSheet).ToList();
                var total = 0;
                for (var index = 0; index < iterateList.Count; index++)
                {
                    var saleItem = iterateList[index];
                    if (total >= totalItems)
                    {
                        total = 0;
                    }
                    else if (index != 0)
                    {
                        ImGui.SameLine();
                    }

                    total++;
                    this.DrawSaleItem(saleItem, index);
                }
            }
        }
    }

    private void DrawSalesHistory()
    {
        ImRaii.IEndObject saleItems;
        var widthAvailable = ImGui.GetContentRegionAvail().X;
        var totalItems = Math.Floor(widthAvailable / 300);

        var retainerItems = this.saleFilter.GetSoldItems();
        var total = 0;
        if (retainerItems.Count == 0)
        {
            ImGui.Text("No sales yet, get out there and make some gil!");
        }

        for (var index = 0; index < retainerItems.Count; index++)
        {
            var saleItem = retainerItems[index];
            if (total >= totalItems)
            {
                total = 0;
            }
            else if (index != 0 && index != retainerItems.Count)
            {
                ImGui.SameLine();
            }

            total++;
            using (var itemChild = ImRaii.Child(
                       $"item_{index}",
                       new Vector2(300, 80),
                       true,
                       ImGuiWindowFlags.NoScrollbar))
            {
                if (itemChild)
                {
                    var item = this.itemSheet.GetRow(saleItem.ItemId)!;
                    using (var iconChild = ImRaii.Child(
                               $"icon_{index}",
                               new Vector2(64, 64),
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (iconChild)
                        {
                            var icon = this.TextureProvider.GetFromGameIcon(
                                new GameIconLookup(item.Icon, saleItem.IsHq));

                            ImGui.Image(icon.GetWrapOrEmpty().ImGuiHandle, new Vector2(64, 64));
                        }
                    }

                    ImGui.SameLine();
                    using (var infoChild = ImRaii.Child(
                               $"info_{index}",
                               new Vector2(0, 0),
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (infoChild)
                        {
                            ImGui.Text(item.Name.AsReadOnly().ExtractText());
                            ImGui.PushTextWrapPos();
                            ImGui.Text(
                                $"{saleItem.Quantity} at {saleItem.UnitPrice.ToString("C", this.gilNumberFormat)} ({saleItem.TotalIncTax.ToString("C", this.gilNumberFormat)})");
                            ImGui.Text($"Sold on {saleItem.SoldAt.ToString(CultureInfo.CurrentCulture)}");
                            ImGui.PopTextWrapPos();
                        }
                    }
                }
            }
        }
    }

    public void DrawSaleItem(SaleItem saleItem, int index)
    {
        using (var id = ImRaii.PushId(index))
        {
            var needsUpdate = true;

            using (var itemChild = ImRaii.Child(
                       $"item_{index}",
                       new Vector2(300, 90),
                       true,
                       ImGuiWindowFlags.NoScrollbar))
            {
                if (itemChild)
                {
                    //Empty slot
                    if (saleItem.ItemId == 0)
                    {
                        ImGui.Text("Empty Slot");
                        return;
                    }
                    var item = this.itemSheet.GetRow(saleItem.ItemId)!;
                    var character = this.CharacterMonitorService.GetCharacterById(saleItem.RetainerId);
                    if (character == null)
                    {
                        ImGui.Text("Unknown Character");
                        return;
                    }

                    var retainerWorld = this.worldSheet.GetRow(character.WorldId)!;
                    using (var iconChild = ImRaii.Child(
                               $"icon_{index}",
                               new Vector2(64, 64),
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (iconChild)
                        {
                            var icon = this.TextureProvider.GetFromGameIcon(
                                new GameIconLookup(item.Icon, saleItem.IsHq));
                            ImGui.Image(icon.GetWrapOrEmpty().ImGuiHandle, new Vector2(64, 64));
                        }
                    }

                    bool undercutHovered = false;

                    ImGui.SameLine();
                    using (var infoChild = ImRaii.Child(
                               $"info_{index}",
                               new Vector2(0, 0),
                               false,
                               ImGuiWindowFlags.NoScrollbar))
                    {
                        if (infoChild)
                        {
                            if (saleItem.UndercutBy != null && saleItem.UndercutBy != 0)
                            {
                                var startPosition = ImGui.GetCursorPos();
                                ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - 16);
                                ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - 16);
                                ImGui.Image(
                                    this.TextureProvider.GetFromGameIcon(new GameIconLookup(61575)).GetWrapOrEmpty()
                                        .ImGuiHandle,
                                    new Vector2(16, 16));
                                undercutHovered = ImGui.IsItemHovered();
                                this.ImGuiService.HoverTooltip($"You have been undercut on this item by {SeIconChar.Gil.ToIconString()} {saleItem.UndercutBy}");
                                ImGui.SetCursorPos(startPosition);
                            }

                            ImGui.Text(item.Name.AsReadOnly().ExtractText());
                            ImGui.PushTextWrapPos();
                            ImGui.Text(
                                $"{saleItem.Quantity} at {saleItem.UnitPrice.ToString("C", this.gilNumberFormat)} ({saleItem.Total.ToString("C", this.gilNumberFormat)})");
                            ImGui.Text($"Listed: {saleItem.ListedAt.Humanize(false)}");

                            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow, saleItem.NeedsUpdate(this.itemUpdatePeriodSetting.CurrentValue(this.Configuration))))
                            {
                                ImGui.Text($"Updated: {saleItem.UpdatedAt.Humanize(false)}");
                            }

                            ImGui.PopTextWrapPos();
                        }
                    }

                    if (!undercutHovered && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
                    {
                        using (var tooltip = ImRaii.Tooltip())
                        {
                            if (tooltip)
                            {
                                ImGui.Text($"{item.Name.AsReadOnly().ExtractText()}");
                                ImGui.Text($"{character?.Name ?? "Unknown"}");
                                ImGui.Text($"{retainerWorld.Name.AsReadOnly().ExtractText()}");
                                ImGui.Text(
                                    $"{saleItem.Quantity} at {saleItem.UnitPrice.ToString("C", this.gilNumberFormat)} ({saleItem.Total.ToString("C", this.gilNumberFormat)})");
                                ImGui.Text($"Listed: {saleItem.ListedAt.Humanize()}");
                                ImGui.Text($"Updated: {saleItem.UpdatedAt.Humanize()}");
                            }
                        }
                    }
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenOverlapped) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("RightClick");
            }

            using (var popup = ImRaii.Popup("RightClick"))
            {
                if (popup)
                {
                    if (ImGui.Selectable("More Information"))
                    {
                        this.commandManager.ProcessCommand("/moreinfo " + saleItem.ItemId);
                    }
                }
            }
        }
    }

    private void DrawButtonBar()
    {
        using (var buttonBar = ImRaii.Child(
                   "buttonBar",
                   new Vector2(0, 22 + (ImGui.GetStyle().CellPadding.Y * 2)),
                   false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (buttonBar)
            {
                var search = this.saleFilter.ItemName ?? "";
                var searchWidth = ImGui.CalcTextSize("Search").X + ImGui.GetStyle().ItemSpacing.X;
                ImGui.SetNextItemWidth(searchWidth);
                this.ImGuiService.VerticalCenter();
                ImGui.LabelText("", "Search:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                this.ImGuiService.VerticalCenter();
                if (ImGui.InputText("##searchBox", ref search, 200))
                {
                    var newItemName = search == "" ? null : search;
                    if (this.saleFilter.ItemName != newItemName)
                    {
                        this.saleFilter.ItemName = newItemName;
                    }
                }

                ImGui.SameLine();
                this.ImGuiService.VerticalCenter();
                using (var iconFont = ImRaii.PushFont(this.font.IconFont))
                {
                    using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f))
                    {
                        if (ImGui.Button(FontAwesomeIcon.Times.ToIconString(), new Vector2(22, 22)))
                        {
                            this.saleFilter.Clear();
                        }

                        iconFont.Pop();
                        this.ImGuiService.HoverTooltip("Clear Search");
                    }
                }

                ImGui.SameLine();

                var showEmptySlots = this.saleFilter.ShowEmpty ?? false;
                if (ImGui.Checkbox("Show empty slots:", ref showEmptySlots))
                {
                    this.saleFilter.ShowEmpty = showEmptySlots;
                }

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetWindowSize().X - 24 - ImGui.GetStyle().CellPadding.X);
                using (var iconFont = ImRaii.PushFont(this.font.IconFont))
                {
                    using (var frameRounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 4f))
                    {
                        if (ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(22, 22)))
                        {
                            this.filterMenuOpen = !this.filterMenuOpen;
                        }

                        iconFont.Pop();
                        this.ImGuiService.HoverTooltip("Toggle Search Bar");
                    }
                }

                ImGui.Separator();
            }
        }
    }

    private void SwitchCharacter(ulong newCharacterId)
    {
        if (this.saleFilter.CharacterId == newCharacterId)
        {
            this.saleFilter.CharacterId = null;
        }
        else
        {
            this.saleFilter.CharacterId = newCharacterId;
            this.saleFilter.WorldId = null;
        }
    }

    private void SwitchWorld(uint newWorldId)
    {
        if (this.saleFilter.WorldId == newWorldId)
        {
            this.saleFilter.WorldId = null;
        }
        else
        {
            this.saleFilter.WorldId = newWorldId;
            this.saleFilter.CharacterId = null;
        }
    }

    public string GetSelectedName()
    {
        if (this.saleFilter.CharacterId == null && this.saleFilter.WorldId == null)
        {
            return "All Retainers/Worlds";
        }

        if (this.saleFilter.CharacterId != null)
        {
            return this.CharacterMonitorService.GetCharacterById(this.saleFilter.CharacterId.Value)?.Name ??
                   "Unknown Character/Retainer";
        }

        if (this.saleFilter.WorldId != null)
        {
            return this.worldSheet.GetRow((uint)this.saleFilter.WorldId)?.Name.AsReadOnly().ExtractText() ??
                   "Unknown World";
        }

        return "Unknown";
    }

    public uint? GetSelectedGil()
    {
        if (this.saleFilter.CharacterId != null)
        {
            return this.SaleTrackerService.GetRetainerGil(this.saleFilter.CharacterId.Value);
        }

        return null;
    }

    public void VerticalCenter(string text)
    {
        var offset = (ImGui.GetWindowSize().Y - ImGui.CalcTextSize(text).Y) / 2.0f;
        ImGui.SetCursorPosY(offset);
        ImGui.TextUnformatted(text);
    }
}
