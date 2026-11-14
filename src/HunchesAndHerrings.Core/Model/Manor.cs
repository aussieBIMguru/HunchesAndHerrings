namespace HunchesAndHerrings.Core.Model;

/// <summary>Axis-aligned rectangle of a room on its floor plan, in plan units (640 x 400 per floor).</summary>
public sealed record RoomRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

/// <summary>A single room or space within the manor.</summary>
public sealed class Room
{
    public Room(string id, string name, Floor floor, RoomRect bounds, string description, bool isPassage = false, bool isOutdoors = false)
    {
        Id = id;
        Name = name;
        Floor = floor;
        Bounds = bounds;
        Description = description;
        IsPassage = isPassage;
        IsOutdoors = isOutdoors;
    }

    public string Id { get; }
    public string Name { get; }
    public Floor Floor { get; }
    public RoomRect Bounds { get; }
    public string Description { get; }

    /// <summary>Corridors and landings: people pass through but nothing happens here.</summary>
    public bool IsPassage { get; }

    public bool IsOutdoors { get; }
}

/// <summary>A doorway or staircase between two rooms.</summary>
public sealed record Connection(string A, string B, bool IsStairs)
{
    public bool Touches(string id) => A == id || B == id;

    public string Other(string id) => A == id ? B : A;
}

/// <summary>A walking route through the house and the minutes it costs.</summary>
public sealed record Route(IReadOnlyList<string> Path, int Minutes);

/// <summary>The house itself: rooms, how they connect and how to walk between them.</summary>
public sealed class Manor
{
    private readonly Dictionary<string, Room> _rooms;

    public Manor(string name, IEnumerable<Room> rooms, IEnumerable<Connection> connections)
    {
        Name = name;
        Rooms = rooms.ToList();
        Connections = connections.ToList();
        _rooms = Rooms.ToDictionary(r => r.Id);
    }

    public string Name { get; }
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Connection> Connections { get; }

    public Room this[string id] => _rooms[id];

    public bool Contains(string id) => _rooms.ContainsKey(id);

    /// <summary>Display name for a room id, tolerating the special "away" location.</summary>
    public string NameOf(string id) =>
        _rooms.TryGetValue(id, out Room? room) ? room.Name
        : id == RoomIds.Away ? "away from the house"
        : id;

    public IEnumerable<Room> OnFloor(Floor floor) => Rooms.Where(r => r.Floor == floor);

    public IEnumerable<(Room Room, bool IsStairs)> Neighbours(string id) =>
        Connections.Where(c => c.Touches(id)).Select(c => (Room: this[c.Other(id)], c.IsStairs));

    public bool AreAdjacent(string a, string b) => a != b && Connections.Any(c => c.Touches(a) && c.Touches(b));

    public bool HasStairs(string id) => Connections.Any(c => c.IsStairs && c.Touches(id));

    /// <summary>Finds the quickest walking route between two rooms (Dijkstra), or null if unreachable.</summary>
    public Route? FindRoute(string from, string to)
    {
        if (!_rooms.ContainsKey(from) || !_rooms.ContainsKey(to))
        {
            return null;
        }

        if (from == to)
        {
            return new Route(new[] { from }, 0);
        }

        var dist = Rooms.ToDictionary(r => r.Id, _ => int.MaxValue);
        var prev = new Dictionary<string, string>();
        var open = new PriorityQueue<string, int>();
        dist[from] = 0;
        open.Enqueue(from, 0);

        while (open.TryDequeue(out string? current, out int d))
        {
            if (d > dist[current])
            {
                continue;
            }

            if (current == to)
            {
                break;
            }

            foreach ((Room next, bool isStairs) in Neighbours(current))
            {
                int nd = d + (isStairs ? GameRules.StairsMinutes : GameRules.StepMinutes);
                if (nd < dist[next.Id])
                {
                    dist[next.Id] = nd;
                    prev[next.Id] = current;
                    open.Enqueue(next.Id, nd);
                }
            }
        }

        if (dist[to] == int.MaxValue)
        {
            return null;
        }

        var path = new List<string> { to };
        string step = to;
        while (prev.TryGetValue(step, out string? back))
        {
            path.Add(back);
            step = back;
        }

        path.Reverse();
        return new Route(path, dist[to]);
    }
}
