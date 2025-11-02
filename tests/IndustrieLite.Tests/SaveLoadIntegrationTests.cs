// SPDX-License-Identifier: MIT
using Godot;
using Xunit;

public class SaveLoadIntegrationTests
{
    [Fact(Skip="Requires Godot runtime (Node types)")]
    public void RoundTrip_Semantics_Equal_With_ChickenFarm_Stock()
    {
        // Arrange
        var land = new LandManager { GridW = 10, GridH = 10 };
        land.ResetAllLandFalse();
        land.SetOwnedCell(new Vector2I(2, 3), true);
        land.SetOwnedCell(new Vector2I(4, 5), true);

        var economy = new EconomyManager();
        economy.SetMoney(1234.5);

        var buildings = new BuildingManager();
        var farm = new ChickenFarm { GridPos = new Vector2I(4, 5) };
        farm.SetInventoryAmount(ChickenFarm.MainResourceId, 12);
        buildings.Buildings.Add(farm);

        var svc = new SaveLoadService();

        // Act
        string diff;
        var equal = svc.RoundTripSemanticsEqual(land, buildings, economy, out diff);

        // Assert
        Assert.True(equal, diff);
    }
}
