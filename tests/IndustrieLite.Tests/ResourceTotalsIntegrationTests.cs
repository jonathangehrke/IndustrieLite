// SPDX-License-Identifier: MIT
using System.Reflection;
using Godot;
using Xunit;

public class ResourceTotalsIntegrationTests
{
    [Fact(Skip="Requires Godot StringName runtime (engine)")]
    public void Totals_Aggregate_From_Managers_And_Inventories()
    {
        // Arrange: ResourceManager mit Produktion
        var rm = new ResourceManager();
        rm.SetProduction(new StringName("power"), 10);
        rm.SetProduction(new StringName("water"), 5);

        // Arrange: BuildingManager mit einer ChickenFarm (Inventar gesetzt)
        var bm = new BuildingManager();
        var farm = new ChickenFarm();
        ((IHasInventory)farm).SetInventoryAmount(ChickenFarm.MainResourceId, 7f);
        bm.Buildings.Add(farm);

        // System under test: ResourceTotalsService mit manuell injizierten Feldern
        var rts = new ResourceTotalsService();
        SetPrivate(rts, "resourceManager", rm);
        SetPrivate(rts, "buildingManager", bm);
        // resourceRegistry bleibt null -> Fallback auf Default-IDs (inkl. "chickens")

        // Act
        var totals = rts.GetTotals();

        // Assert: Power/Water Produktion
        var power = (Godot.Collections.Dictionary)totals["power"];
        var water = (Godot.Collections.Dictionary)totals["water"];
        Assert.Equal(10d, (double)power["prod_ps"]);
        Assert.Equal(5d, (double)water["prod_ps"]);

        // Assert: Chicken-Bestand aus Inventar
        var chickens = (Godot.Collections.Dictionary)totals["chickens"];
        Assert.Equal(7d, (double)chickens["stock"]);
    }

    private static void SetPrivate(object obj, string fieldName, object value)
    {
        var fi = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(fi);
        fi!.SetValue(obj, value);
    }
}
