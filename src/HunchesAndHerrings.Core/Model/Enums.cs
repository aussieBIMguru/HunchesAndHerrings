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
    Child,
    Constable,
    RivalDetective,

    /// <summary>A young child of the house: a witness, never a suspect.</summary>
    YoungChild,
}

/// <summary>
/// A characteristic that scene evidence can test a person against. The full catalogue, with every value
/// and all the prose for it, lives in <see cref="TraitCatalog"/>.
/// </summary>
public enum TraitCategory
{
    Smoking,
    Handedness,
    ShoeSize,
    Allergy,
    Strength,
    Height,
    HairColour,
    Scent,
    Spectacles,
    Gait,
    Gloves,
    SignetRing,
    Snuff,
    BootNails,
    Sweets,
    ClothColour,
    Rouge,
    HairOil,
    Buttons,
    Knots,
    Medicine,
    Literacy,
    Ink,
    Keys,
    Dog,
    Scratches,
    Accent,
    Candle,
    Tremor,
    Riding,
    Botany,
    Vertigo,
    WalkingStick,
    WatchChain,
    GoldTooth,
    Cyclist,
    Tattoo,
    Pianist,
    Photography,
    Stammer,
    Sleep,
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
    Affair,
    Silence,
    Recognition,
    Negligence,
    Imposture,
    Insurance,
    Theft,

    /// <summary>Not a motive at all: the death was an accident.</summary>
    Accident,
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

    /// <summary>Confronting someone with evidence of their secret.</summary>
    Confront,

    /// <summary>Comparing notes with the rival detective.</summary>
    CompareNotes,

    /// <summary>Asking the rival detective for their theory.</summary>
    RivalTheory,
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

/// <summary>The shape of the story behind a case: who did it, how many of them, and why.</summary>
public enum CaseModel
{
    /// <summary>One killer, one victim.</summary>
    LoneKiller,

    /// <summary>Two people planned it together; one struck, the other gave a false alibi.</summary>
    Conspiracy,

    /// <summary>The victim's children acted together over the will.</summary>
    FamilyPact,

    /// <summary>One killer, but the whole household hides secrets, and the motive is one of them.</summary>
    HiddenSecrets,

    /// <summary>A servant is secretly the victim's child.</summary>
    SecretHeir,

    /// <summary>No murder at all: an accident that someone made look like one.</summary>
    Accident,

    /// <summary>The family doctor poisoned the victim.</summary>
    DoctorPoison,

    /// <summary>The family doctor gave a fatal dose in error and is hiding it.</summary>
    DoctorNegligence,

    /// <summary>An affair lies behind the killing.</summary>
    Affair,

    /// <summary>The killer meant to kill somebody else.</summary>
    MistakenIdentity,

    /// <summary>Someone in the house isn't who they claim to be, and the victim found out.</summary>
    Impostor,
}

/// <summary>How a scene value tests a person's value.</summary>
public enum TraitRule
{
    /// <summary>The person must have the value the scene shows.</summary>
    Match,

    /// <summary>Anyone who has the value the scene shows is ruled out (an allergen, a fear of heights).</summary>
    Avoid,
}

/// <summary>What kind of death a trait can be evidenced at.</summary>
public enum TraitFit
{
    Any,
    Blow,
    Poison,
}
