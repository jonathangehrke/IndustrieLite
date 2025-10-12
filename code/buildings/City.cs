// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

public class MarketOrder
{
    private static int _nextId = 1;
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
        Id = _nextId++;
        Product = product;
        Amount = amount;
        Remaining = amount;
        PricePerUnit = ppu;
    }
}

public partial class City : Building, ITickable
{
    [Export] public string CityName = "Berlin";
    public List<MarketOrder> Orders = new List<MarketOrder>();

    private double _orderAccum = 0.0;
    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private GameTimeManager? _gameTime;

    [Export] public string RezeptIdOverride { get; set; } = ""; // Standard: city_orders
    private RecipeProductionController? _controller;

    [Export] public double AuftragsIntervallSek { get; set; } = 6.0;
    [Export] public double RezeptTickLaengeSek { get; set; } = 6.0;

    [Export] public float ChickenProbability { get; set; } = 0.4f;
    [Export] public float PigProbability { get; set; } = 0.3f;
    [Export] public float EggProbability { get; set; } = 0.2f;
    [Export] public float GrainProbability { get; set; } = 0.1f;

    private string? _letztesProdukt = null;

    private EventHub? _eventHub;
    private readonly AboVerwalter _abos = new();
    string ITickable.Name => "City";

    public City()
    {
        DefaultSize = new Vector2I(4, 4);
        Size = DefaultSize;
        Color = new Color(0.7f, 0.7f, 0.9f);
        rng.Randomize();
    }

    public void Initialize(EventHub? eventHub, GameTimeManager? gameTime, Simulation? simulation)
    {
        _eventHub = eventHub ?? _eventHub;
        _gameTime = gameTime ?? _gameTime;
        if (simulation != null)
        {
            try { simulation.Register(this); } catch { }
        }

        // Subscribe to DayChanged event to remove expired orders
        if (_eventHub != null)
        {
            _abos.Abonniere(
                subscribe: () => _eventHub.DayChanged += OnDayChanged,
                unsubscribe: () => _eventHub.DayChanged -= OnDayChanged
            );
        }
    }

    public override void _Ready()
    {
        base._Ready();
        // Rezeptcontroller initialisieren
        if (_controller == null)
        {
            _controller = new RecipeProductionController();
            _controller.Name = "RecipeProductionController"; // Explicit name for save/load
            _controller.Initialize(_database, null);
            AddChild(_controller);
            var rid = string.IsNullOrEmpty(RezeptIdOverride) ? "city_orders" : RezeptIdOverride;
            _controller.SetzeRezept(rid);
        }
        if (_eventHub == null)
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => "City: EventHub nicht injiziert (UI-Refresh eingeschraenkt)");
        if (_gameTime == null)
            DebugLogger.Log("debug_building", DebugLogger.LogLevel.Warn, () => "City: GameTimeManager nicht injiziert (Kalenderdaten fehlen)");
    }

    public void Tick(double dt)
    {
        ProcessOrderGeneration(dt);
    }

    private void ProcessOrderGeneration(double dt)
    {
        bool createdAny = false;
        double effectiveInterval = (_controller != null && _controller.AktuellesRezept != null) ? RezeptTickLaengeSek : AuftragsIntervallSek;
        _orderAccum += dt;
        while (_orderAccum >= effectiveInterval)
        {
            _orderAccum -= effectiveInterval;
            int cycleCount = 1;
            if (_controller != null && _controller.AktuellesRezept != null)
            {
                cycleCount = _controller.VerarbeiteProduktionsTick(true);
                if (cycleCount <= 0) cycleCount = 1;
            }
            for (int i = 0; i < cycleCount; i++)
            {
                // Produkt so wählen, dass es auf dem aktuellen Level freigeschaltet ist
                if (!TrySelectRandomUnlockedProduct(out var pd))
                {
                    // Nichts freigeschaltet -> diesen Versuch auslassen
                    continue;
                }
                int amount = (int)rng.RandiRange(pd.minAmount, pd.maxAmount);
                double ppu = rng.RandfRange(pd.minPrice, pd.maxPrice);
                var order = new MarketOrder(pd.displayName, amount, ppu);
                var created = _gameTime?.CurrentDate ?? new System.DateTime(2015, 1, 1);
                order.CreatedOn = created;
                order.ExpiresOn = created.AddDays(10);
                Orders.Add(order);
                _letztesProdukt = pd.displayName;
                createdAny = true;
            }
        }
        if (createdAny && _eventHub != null)
            _eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
    }

    private bool IsProductUnlockedForGeneration(string displayName)
    {
        try
        {
            var sc = ServiceContainer.Instance;
            var marketService = sc?.GetNamedService<MarketService>(ServiceNames.MarketService);
            if (marketService == null)
                return true; // Fallback: nichts blockieren, um Early-Game nicht leer zu lassen

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
            if (w <= 0f) return;
            // Nur freigeschaltete Produkte zulassen
            if (!IsProductUnlockedForGeneration(name)) return;
            candidates.Add(((name, minA, maxA, minP, maxP), w));
        }

        float wHuhn = (_letztesProdukt == "Huhn") ? 0f : ChickenProbability;
        float wSchwein = (_letztesProdukt == "Schwein") ? 0f : PigProbability;
        float wEi = (_letztesProdukt == "Ei") ? 0f : EggProbability;
        float wGetreide = (_letztesProdukt == "Getreide") ? 0f : GrainProbability;

        Add("Huhn", 20, 60, 7.0f, 9.0f, wHuhn);
        Add("Schwein", 10, 30, 14.0f, 18.0f, wSchwein);
        Add("Ei", 50, 150, 3.5f, 4.5f, wEi);
        Add("Getreide", 20, 50, 2.0f, 3.0f, wGetreide);

        // Falls durch No-Repeat-Regel alle rausgefallen sind: Regel lockern (aber weiterhin nur freigeschaltete Produkte)
        if (candidates.Count == 0)
        {
            candidates.Clear();
            Add("Huhn", 20, 60, 7.0f, 9.0f, ChickenProbability);
            Add("Schwein", 10, 30, 14.0f, 18.0f, PigProbability);
            Add("Ei", 50, 150, 3.5f, 4.5f, EggProbability);
            Add("Getreide", 20, 50, 2.0f, 3.0f, GrainProbability);
        }

        if (candidates.Count == 0)
        {
            pd = default;
            return false;
        }

        float totalW = 0f;
        foreach (var c in candidates) totalW += c.weight;
        if (totalW <= 0f)
        {
            pd = default;
            return false;
        }

        float r = rng.Randf() * totalW;
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
        _abos.DisposeAll();
        try { Simulation.Instance?.Unregister(this); } catch { }
        base._ExitTree();
    }

    private void OnDayChanged(int year, int month, int day)
    {
        var today = new System.DateTime(year, month, day);
        int removed = Orders.RemoveAll(o => !o.Accepted && !o.Delivered && o.ExpiresOn <= today);
        if (removed > 0 && _eventHub != null)
            _eventHub.EmitSignal(EventHub.SignalName.MarketOrdersChanged);
    }
}
