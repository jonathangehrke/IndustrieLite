// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public class MarketOrder
{
    private static int nextId = 1;
    public int Id;
    public string Product;
    public int Amount;
    public int Remaining;
    public double PricePerUnit;
    public bool Accepted = false;
    public bool Delivered = false;
    public System.DateTime CreatedOn;
    public System.DateTime ExpiresOn;

    public MarketOrder(string product, int amount, double ppu)
    {
        this.Id = nextId++;
        this.Product = product;
        this.Amount = amount;
        this.Remaining = amount;
        this.PricePerUnit = ppu;
    }
}

public partial class City : Building, ITickable
{
    [Export]
    public string CityName = "Berlin";
    public List<MarketOrder> Orders = new List<MarketOrder>();

    private double orderAccum = 0.0;
    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private GameTimeManager? gameTime;

    [Export]
    public string RezeptIdOverride { get; set; } = ""; // Standard: city_orders

    private RecipeProductionController? controller;

    [Export]
    public double AuftragsIntervallSek { get; set; } = 6.0;

    [Export]
    public double RezeptTickLaengeSek { get; set; } = 6.0;

    [Export]
    public float ChickenProbability { get; set; } = 0.4f;

    [Export]
    public float PigProbability { get; set; } = 0.3f;

    [Export]
    public float EggProbability { get; set; } = 0.2f;

    [Export]
    public float GrainProbability { get; set; } = 0.1f;

    private string? letztesProdukt = null;

    private EventHub? eventHub;
    private readonly AboVerwalter abos = new();

    string ITickable.Name => "City";

    public City()
    {
        this.DefaultSize = new Vector2I(4, 4);
        this.Size = this.DefaultSize;
        this.Color = new Color(0.7f, 0.7f, 0.9f);
        this.rng.Randomize();
    }

    public void Initialize(EventHub? eventHub, GameTimeManager? gameTime, Simulation? simulation)
    {
        this.eventHub = eventHub ?? this.eventHub;
        this.gameTime = gameTime ?? this.gameTime;
        if (simulation != null)
        {
            try
            {
                simulation.Register(this);
            }
            catch
            {
            }
        }

        // Subscribe to DayChanged event to remove expired orders
        if (this.eventHub != null)
        {
            this.abos.Abonniere(
                subscribe: () => this.eventHub.DayChanged += this.OnDayChanged,
                unsubscribe: () => this.eventHub.DayChanged -= this.OnDayChanged);
        }
    }

    public override void _Ready()
    {
        base._Ready();
        // Rezeptcontroller initialisieren
        if (this.controller == null)
        {
            this.controller = new RecipeProductionController();
            this.controller.Name = "RecipeProductionController"; // Explicit name for save/load
            this.controller.Initialize(this.database, null);
            this.AddChild(this.controller);
            var rid = string.IsNullOrEmpty(this.RezeptIdOverride) ? "city_orders" : this.RezeptIdOverride;
            this.controller.SetzeRezept(rid);
        }
        if (this.eventHub == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => "City: EventHub nicht injiziert (UI-Refresh eingeschraenkt)");
        }

        if (this.gameTime == null)
        {
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => "City: GameTimeManager nicht injiziert (Kalenderdaten fehlen)");
        }
    }

    public void Tick(double dt)
    {
        this.ProcessOrderGeneration(dt);
    }

    private void ProcessOrderGeneration(double dt)
    {
        bool createdAny = false;
        double effectiveInterval = (this.controller != null && this.controller.AktuellesRezept != null) ? this.RezeptTickLaengeSek : this.AuftragsIntervallSek;
        this.orderAccum += dt;
        while (this.orderAccum >= effectiveInterval)
        {
            this.orderAccum -= effectiveInterval;
            int cycleCount = 1;
            if (this.controller != null && this.controller.AktuellesRezept != null)
            {
                cycleCount = this.controller.VerarbeiteProduktionsTick(true);
                if (cycleCount <= 0)
                {
                    cycleCount = 1;
                }
            }
            for (int i = 0; i < cycleCount; i++)
            {
                // Produkt so wählen, dass es auf dem aktuellen Level freigeschaltet ist
                if (!this.TrySelectRandomUnlockedProduct(out var pd))
                {
                    // Nichts freigeschaltet -> diesen Versuch auslassen
                    continue;
                }
                int amount = (int)this.rng.RandiRange(pd.minAmount, pd.maxAmount);
                double ppu = this.rng.RandfRange(pd.minPrice, pd.maxPrice);
                var order = new MarketOrder(pd.displayName, amount, ppu);
                var created = this.gameTime?.CurrentDate ?? new System.DateTime(2015, 1, 1);
                order.CreatedOn = created;
                order.ExpiresOn = created.AddDays(10);
                this.Orders.Add(order);
                this.letztesProdukt = pd.displayName;
                createdAny = true;
            }
        }
        if (createdAny && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }

    private bool IsProductUnlockedForGeneration(string displayName)
    {
        try
        {
            var sc = ServiceContainer.Instance;
            var marketService = sc?.GetNamedService<MarketService>(ServiceNames.MarketService);
            if (marketService == null)
            {
                return true; // Fallback: nichts blockieren, um Early-Game nicht leer zu lassen
            }

            var id = marketService.NormalizeProductName(displayName);
            return marketService.IsProductUnlocked(id);
        }
        catch
        {
            return true; // Fail-open
        }
    }

    private bool TrySelectRandomUnlockedProduct(out (string displayName, int minAmount, int maxAmount, float minPrice, float maxPrice) pd)
    {
        // Kandidaten mit Gewichten (No-Repeat-Regel berücksichtigen)
        var candidates = new System.Collections.Generic.List<((string name, int minA, int maxA, float minP, float maxP) data, float weight)>();
        void Add(string name, int minA, int maxA, float minP, float maxP, float w)
        {
            if (w <= 0f)
            {
                return;
            }
            // Nur freigeschaltete Produkte zulassen
            if (!this.IsProductUnlockedForGeneration(name))
            {
                return;
            }

            candidates.Add(((name, minA, maxA, minP, maxP), w));
        }

        float wHuhn = string.Equals(this.letztesProdukt, "Huhn", System.StringComparison.Ordinal) ? 0f : this.ChickenProbability;
        float wSchwein = string.Equals(this.letztesProdukt, "Schwein", System.StringComparison.Ordinal) ? 0f : this.PigProbability;
        float wEi = string.Equals(this.letztesProdukt, "Ei", System.StringComparison.Ordinal) ? 0f : this.EggProbability;
        float wGetreide = string.Equals(this.letztesProdukt, "Getreide", System.StringComparison.Ordinal) ? 0f : this.GrainProbability;

        Add("Huhn", 20, 60, 7.0f, 9.0f, wHuhn);
        Add("Schwein", 10, 30, 14.0f, 18.0f, wSchwein);
        Add("Ei", 50, 150, 3.5f, 4.5f, wEi);
        Add("Getreide", 20, 50, 2.0f, 3.0f, wGetreide);

        // Falls durch No-Repeat-Regel alle rausgefallen sind: Regel lockern (aber weiterhin nur freigeschaltete Produkte)
        if (candidates.Count == 0)
        {
            candidates.Clear();
            Add("Huhn", 20, 60, 7.0f, 9.0f, this.ChickenProbability);
            Add("Schwein", 10, 30, 14.0f, 18.0f, this.PigProbability);
            Add("Ei", 50, 150, 3.5f, 4.5f, this.EggProbability);
            Add("Getreide", 20, 50, 2.0f, 3.0f, this.GrainProbability);
        }

        if (candidates.Count == 0)
        {
            pd = default;
            return false;
        }

        float totalW = 0f;
        foreach (var c in candidates)
        {
            totalW += c.weight;
        }

        if (totalW <= 0f)
        {
            pd = default;
            return false;
        }

        float r = this.rng.Randf() * totalW;
        foreach (var c in candidates)
        {
            if (r < c.weight)
            {
                pd = (c.data.name, c.data.minA, c.data.maxA, c.data.minP, c.data.maxP);
                return true;
            }
            r -= c.weight;
        }

        // Fallback: letzter Kandidat
        var last = candidates[candidates.Count - 1];
        pd = (last.data.name, last.data.minA, last.data.maxA, last.data.minP, last.data.maxP);
        return true;
    }

    public override void _ExitTree()
    {
        this.abos.DisposeAll();
        try
        {
            Simulation.Instance?.Unregister(this);
        }
        catch
        {
        }
        base._ExitTree();
    }

    private void OnDayChanged(int year, int month, int day)
    {
        var today = new System.DateTime(year, month, day);
        int removed = this.Orders.RemoveAll(o => !o.Accepted && !o.Delivered && o.ExpiresOn <= today);
        if (removed > 0 && this.eventHub != null)
        {
            this.eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
        }
    }
}
