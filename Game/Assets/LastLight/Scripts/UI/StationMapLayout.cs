using UnityEngine;

/// <summary>
/// Room rectangles of the two-floor station in world X/Z (metres), measured from the built walls.
/// Shared by the HUD room title and the in-game map.
/// </summary>
public static class StationMapLayout
{
    public readonly struct Room
    {
        public readonly string Label;
        public readonly Rect Area; // x = world X, y = world Z
        public readonly bool Corridor;
        public Room(string label, float xMin, float zMin, float xMax, float zMax, bool corridor = false)
        {
            Label = label;
            Area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
            Corridor = corridor;
        }
    }

    /// <summary>World extent drawn on the map (building plus both stair wings).</summary>
    public static readonly Rect Extent = Rect.MinMaxRect(-15f, -19.5f, 15f, 19.5f);

    /// <summary>Floor 2 starts above this height.</summary>
    public const float UpperFloorHeight = 2.6f;

    public static readonly Room[] Ground =
    {
        new Room("02  ARMORY", -15f, -12f, -1.5f, -4f),
        new Room("04  POWER / RELAY A", -15f, -4f, -1.5f, 4f),
        new Room("05  STORAGE", -15f, 4f, -1.5f, 12f),
        new Room("01  ARRIVAL", 1.5f, -12f, 15f, 0f),
        new Room("10  EVACUATION", 1.5f, 0f, 15f, 12f),
        new Room("CORRIDOR", -1.5f, -12f, 1.5f, 12f, true),
        new Room("STAIR A", -3f, -19.5f, 3f, -12f, true),
        new Room("STAIR B", -3f, 12f, 3f, 19.5f, true),
    };

    public static readonly Room[] Upper =
    {
        new Room("03  MEDICAL", -15f, -12f, -1.5f, -4f),
        new Room("08  SECURITY", -15f, -4f, -1.5f, 4f),
        new Room("07  ROBOT WORKSHOP", -15f, 4f, -1.5f, 12f),
        new Room("09  CREW QUARTERS", 1.5f, -12f, 15f, 0f),
        new Room("06  MAIN LAB / RELAY B", 1.5f, 0f, 15f, 12f),
        new Room("CORRIDOR", -1.5f, -12f, 1.5f, 12f, true),
        new Room("STAIR A", -3f, -19.5f, 3f, -12f, true),
        new Room("STAIR B", -3f, 12f, 3f, 19.5f, true),
    };

    public static bool IsUpper(Vector3 position) => position.y > UpperFloorHeight;

    public static Room[] FloorOf(Vector3 position) => IsUpper(position) ? Upper : Ground;

    /// <summary>Named room containing the position, or empty for corridors/stairs/outside.</summary>
    public static string RoomAt(Vector3 position)
    {
        Vector2 p = new Vector2(position.x, position.z);
        foreach (Room room in FloorOf(position))
            if (!room.Corridor && room.Area.Contains(p)) return room.Label;
        return string.Empty;
    }
}
