using System;
using System.Globalization;

using AllaganLib.Data.Interfaces;

using AllaganMarket.GameInterop;
using AllaganMarket.Interfaces;

using FFXIVClientStructs.FFXIV.Client.Game;

using Lumina;
using Lumina.Data;

namespace AllaganMarket.Models;

public class SaleItem : IDebuggable, IEquatable<SaleItem>, ICsv
{
    public SaleItem(
        InventoryItem inventoryItem,
        RetainerMarketItemPrice? retainerMarketItemPrice,
        ulong retainerId,
        uint worldId)
    {
        this.RetainerId = retainerId;
        this.WorldId = worldId;
        this.ItemId = inventoryItem.ItemId;
        this.IsHq = inventoryItem.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
        this.Quantity = inventoryItem.Quantity;
        this.UnitPrice = retainerMarketItemPrice?.UnitPrice ?? 0;
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    public SaleItem(InventoryItem inventoryItem, uint unitPrice, ulong retainerId, uint worldId)
    {
        this.RetainerId = retainerId;
        this.WorldId = worldId;
        this.ItemId = inventoryItem.ItemId;
        this.IsHq = inventoryItem.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
        this.Quantity = inventoryItem.Quantity;
        this.UnitPrice = unitPrice;
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    public SaleItem(ulong retainerId, uint worldId, uint itemId, bool isHq, uint quantity, uint unitPrice)
    {
        this.RetainerId = retainerId;
        this.WorldId = worldId;
        this.ItemId = itemId;
        this.IsHq = isHq;
        this.Quantity = quantity;
        this.UnitPrice = unitPrice;
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    public SaleItem(ulong retainerId, uint worldId)
    {
        this.RetainerId = retainerId;
        this.WorldId = worldId;
        this.ItemId = 0;
        this.IsHq = false;
        this.Quantity = 0;
        this.UnitPrice = 0;
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    public SaleItem(ulong retainerId)
    {
        this.RetainerId = retainerId;
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SaleItem"/> class.
    /// Do not remove, used for JSON construction.
    /// </summary>
    public SaleItem()
    {
        this.ListedAt = DateTime.Now;
        this.UpdatedAt = DateTime.Now;
    }

    public ulong RetainerId { get; set; }

    public uint WorldId { get; set; }

    public uint ItemId { get; set; }

    public bool IsHq { get; set; }

    public uint Quantity { get; set; }

    public uint UnitPrice { get; set; }

    public uint? UndercutBy { get; set; }

    public DateTime ListedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public uint Total => this.Quantity * this.UnitPrice;

    public static string[] GetHeaders()
    {
        return
        [
            "Retainer ID",
            "World ID",
            "Item ID",
            "Is HQ?",
            "Quantity",
            "Unit Price",
            "Undercut By?",
            "Listed At",
            "Updated At"
        ];
    }

    public bool IsEmpty()
    {
        return this.ItemId == 0;
    }

    public uint RecommendedUnitPrice(uint undercutBy = 5)
    {
        if (this.UndercutBy is null or 0)
        {
            return this.UnitPrice;
        }

        return this.UnitPrice - (this.UndercutBy ?? 0) - undercutBy;
    }

    public bool NeedsUpdate(int updatePeriodMinutes)
    {
        return DateTime.Now > this.UpdatedAt + TimeSpan.FromMinutes(updatePeriodMinutes);
    }

    public DateTime NextUpdateDate(int updatePeriodMinutes)
    {
        if (this.NeedsUpdate(updatePeriodMinutes))
        {
            return DateTime.Now;
        }

        return this.UpdatedAt + TimeSpan.FromMinutes(updatePeriodMinutes);
    }

    public string AsDebugString()
    {
        return
            $"Retainer ID: {this.RetainerId}, World ID: {this.WorldId}, Item ID: {this.ItemId}, Is HQ: {this.IsHq}, Quantity: {this.Quantity}, Unit Price: {this.UnitPrice}";
    }

    public bool Equals(SaleItem? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return this.RetainerId == other.RetainerId && this.WorldId == other.WorldId && this.ItemId == other.ItemId &&
               this.IsHq == other.IsHq &&
               this.Quantity == other.Quantity && this.UnitPrice == other.UnitPrice;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return this.Equals((SaleItem)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.RetainerId, this.WorldId, this.ItemId, this.IsHq, this.Quantity, this.UnitPrice);
    }

    public void FromCsv(string[] lineData)
    {
        this.RetainerId = Convert.ToUInt64(lineData[0]);
        this.WorldId = Convert.ToUInt32(lineData[1]);
        this.ItemId = Convert.ToUInt32(lineData[2]);
        this.IsHq = lineData[3] == "1";
        this.Quantity = Convert.ToUInt32(lineData[4], CultureInfo.InvariantCulture);
        this.UnitPrice = Convert.ToUInt32(lineData[5], CultureInfo.InvariantCulture);
        this.UndercutBy = lineData[6] == string.Empty
                              ? null
                              : Convert.ToUInt32(lineData[6], CultureInfo.InvariantCulture);
        this.ListedAt = DateTime.Parse(lineData[7], CultureInfo.InvariantCulture);
        this.UpdatedAt = DateTime.Parse(lineData[8], CultureInfo.InvariantCulture);
    }

    public string[] ToCsv()
    {
        return
        [
            this.RetainerId.ToString(),
            this.WorldId.ToString(),
            this.ItemId.ToString(),
            this.IsHq ? "1" : "0",
            this.Quantity.ToString(CultureInfo.InvariantCulture),
            this.UnitPrice.ToString(CultureInfo.InvariantCulture),
            this.UndercutBy?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            this.ListedAt.ToString(CultureInfo.InvariantCulture),
            this.UpdatedAt.ToString(CultureInfo.InvariantCulture)
        ];
    }

    public bool IncludeInCsv()
    {
        return true;
    }

    public void PopulateData(GameData gameData, Language language)
    {
    }
}
