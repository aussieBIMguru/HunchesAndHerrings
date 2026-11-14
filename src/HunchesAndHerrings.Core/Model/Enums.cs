namespace HunchesAndHerrings.Core.Model;

/// <summary>Levels of the manor, from the cellar up.</summary>
public enum Floor
{
    Cellar = 0,
    Ground = 1,
    Upper = 2,
}

/// <summary>Gender of a character; drives names, pronouns and portraits.</summary>
public enum Gender
{
    Male,
    Female,
}

/// <summary>Social standing within the household.</summary>
public enum Station
{
    Servant,
    Family,
    Guest,
}

/// <summary>The part a character plays in the household.</summary>
public enum RoleKind
{
    Master,
    Butler,
    Housekeeper,
    Cook,
    Gardener,
    Maid,
    Valet,
    Governess,
    Spouse,
    Heir,
    Ward,
    Colonel,
    Doctor,
    Reverend,
    Solicitor,
    BusinessPartner,
    Companion,
}

/// <summary>A physical characteristic that scene evidence can test against.</summary>
public enum TraitCategory
{
    Smoking,
    Handedness,
    ShoeSize,
    Allergy,
    Strength,
}

/// <summary>What, if anything, a character smokes.</summary>
public enum SmokeKind
{
    None,
    Pipe,
    Cigar,
    Cigarette,
}

/// <summary>Dominant hand.</summary>
public enum Handedness
{
    Right,
    Left,
}

/// <summary>Boot size, as read from a print.</summary>
public enum ShoeSize
{
    Small,
    Medium,
    Large,
}

/// <summary>Something a character reacts badly to.</summary>
public enum Allergen
{
    None,
    Lilies,
    Cats,
    Horsehair,
    Dust,
}

/// <summary>Why someone might have wanted the victim dead.</summary>
public enum MotiveKind
{
    None,
    Inheritance,
    Debt,
    Dismissal,
    Jealousy,
    Blackmail,
    Revenge,
    Disinheritance,
}

/// <summary>Interview subjects the inspector can raise.</summary>
public enum Topic
{
    LastNight,
    Sightings,
    Self,
    Victim,
    AboutPerson,
    Whereabouts,
}

/// <summary>Where a piece of evidence came from.</summary>
public enum ClueKind
{
    Scene,
    Document,
    Statement,
    Observation,
    Report,
}

/// <summary>Game difficulty.</summary>
public enum Difficulty
{
    Novice,
    Inspector,
    Master,
}
