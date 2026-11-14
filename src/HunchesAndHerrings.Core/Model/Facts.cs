using System.Text.Json.Serialization;

namespace HunchesAndHerrings.Core.Model;

/// <summary>
/// A structured, machine-checkable assertion carried by a clue. Facts drive the suspect
/// matrix and automatic contradiction detection; the clue text is just their prose form.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LocationFact), "location")]
[JsonDerivedType(typeof(OccupancyFact), "occupancy")]
[JsonDerivedType(typeof(TraitFact), "trait")]
[JsonDerivedType(typeof(SceneFact), "scene")]
[JsonDerivedType(typeof(TimeOfDeathFact), "tod")]
[JsonDerivedType(typeof(MotiveFact), "motive")]
[JsonDerivedType(typeof(GenderSightingFact), "gender")]
[JsonDerivedType(typeof(ScheduleFact), "schedule")]
[JsonDerivedType(typeof(WeaponFact), "weapon")]
public abstract record Fact;

/// <summary>Someone was in a room at a given time last night.</summary>
public sealed record LocationFact(string CharacterId, int Slot, string RoomId) : Fact;

/// <summary>Exactly these people (possibly nobody) were in a room at a given time last night.</summary>
public sealed record OccupancyFact(string RoomId, int Slot, IReadOnlyList<string> Occupants) : Fact;

/// <summary>A person's value for a physical trait.</summary>
public sealed record TraitFact(string CharacterId, TraitCategory Category, string Value) : Fact;

/// <summary>What the scene of the crime shows for a trait category.</summary>
public sealed record SceneFact(TraitCategory Category, string Value) : Fact;

/// <summary>When the victim died, as a night slot.</summary>
public sealed record TimeOfDeathFact(int Slot) : Fact;

/// <summary>A person had a reason to want the victim dead.</summary>
public sealed record MotiveFact(string CharacterId, MotiveKind Motive) : Fact;

/// <summary>A partial sighting near the scene: only the figure's sex was made out.</summary>
public sealed record GenderSightingFact(Gender Gender, int Slot, string NearRoomId) : Fact;

/// <summary>Where someone can be found today between two hours (24h clock, end exclusive).</summary>
public sealed record ScheduleFact(string CharacterId, int FromHour, int ToHour, string RoomId) : Fact;

/// <summary>The weapon used.</summary>
public sealed record WeaponFact(string WeaponId) : Fact;
