using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>
/// Builds a complete, solvable mystery from a seed.
/// <para>
/// First a <see cref="CaseModel"/> is chosen: a lone killer, a pair of conspirators, the children acting together,
/// a secret heir, a doctor's poison or blunder, an affair, a killer who struck the wrong person, an impostor, or
/// no murder at all. The cast, the crime and the lies are then built to fit it.
/// </para>
/// <para>
/// Solvability is designed in rather than checked afterwards. Every innocent is given at least one route to
/// elimination at the time of death:
/// <list type="bullet">
/// <item>"Twins" match every scene indicator, so they are guaranteed an alibi (a partner who vouches for them,
/// a third-party sighting, the servants' bell register, or the visitors' book).</item>
/// <item>Everyone else fails at least one indicator, and that trait is revealed by one or more truthful
/// sources (their own word, gossip, or a document in their room).</item>
/// </list>
/// The culprits always lie about where they were when the scene evidence was left, and that lie is contradicted by
/// a witness alone in the claimed room, by physical proof the room was unused, or by the visitors' book.
/// </para>
/// </summary>
public static partial class CaseGenerator
{
    public static CaseFile Generate(int seed, Difficulty difficulty) => Generate(seed, new CaseOptions(difficulty));

    public static CaseFile Generate(int seed, CaseOptions options) => new Builder(seed, options).Build();

    /// <summary>How the culprits' false claim about where they were is exposed.</summary>
    private enum Disproof
    {
        LoneWitness,
        EmptyRoom,
        VisitorsBook,
    }

    /// <summary>Holds all working state for one generation run.</summary>
    private sealed partial class Builder
    {
        private static readonly string[] VictimEveningRooms =
        {
            RoomIds.Drawing, RoomIds.Library, RoomIds.Dining, RoomIds.Study, RoomIds.Billiard, RoomIds.Conservatory,
        };

        private readonly int _seed;
        private readonly CaseOptions _options;
        private readonly Random _rng;
        private readonly DifficultySettings _s;

        private readonly HashSet<string> _usedFirst = new();
        private readonly HashSet<string> _usedSurnames = new();
        private readonly List<Character> _suspects = new();
        private readonly Dictionary<string, RoleDef> _defs = new();
        private readonly List<TraitCategory> _cats = new();
        private readonly Dictionary<TraitCategory, string> _scene = new();
        private readonly Dictionary<string, string[]> _night = new();
        private readonly Dictionary<string, string[]> _day = new();
        private readonly List<Clue> _clues = new();
        private readonly List<Character> _twins = new();
        private readonly List<Character> _others = new();
        private readonly HashSet<string> _reserved = new();
        private readonly List<(Character A, Character B, string Room)> _pairs = new();
        private readonly List<Character> _soloTwins = new();
        private readonly HashSet<(string Speaker, string Target, int Slot)> _forcedGlimpses = new();
        private readonly List<Character> _bellRegisterAlibis = new();
        private readonly Dictionary<(string Speaker, string Target), GossipPlan> _gossip = new();
        private readonly Dictionary<string, List<TraitCategory>> _selfTraits = new();
        private readonly List<(Character Who, TraitCategory Category)> _traitDocs = new();
        private readonly List<string> _keyEvidence = new();
        private readonly List<Character> _culprits = new();
        private readonly List<Secret> _secrets = new();
        private readonly List<Character> _youngsters = new();
        private readonly HashSet<string> _usedPortraits = new();

        private Manor _manor = null!;
        private Character _victim = null!;
        private Room _sceneRoom = null!;
        private Weapon _weapon = null!;
        private Lockdown _lockdown = null!;
        private CaseModel _model;
        private int _variant;
        private MotiveKind _motive;

        /// <summary>Whoever was in the scene at the crime slot and left the evidence: the killer, or in an accident, the meddler.</summary>
        private Character _hand = null!;

        /// <summary>Time of death.</summary>
        private int _ms;

        /// <summary>When the hand was in the scene (and lied about it). Equals <see cref="_ms"/> except in accidents.</summary>
        private int _cs;

        private string _claimRoom = string.Empty;
        private Disproof _disproof;
        private Character? _loneWitness;
        private Character? _genderWitness;
        private Character? _finder;
        private TraitCategory? _handLieCategory;
        private AccidentKind? _accident;
        private Character? _intended;
        private Character? _secretHeir;
        private Character? _newcomer;
        private Character? _lover;
        private (TraitCategory Category, string Value, Character Owner)? _herring;
        private RivalPlan? _rival;
        private int _counter;

        public Builder(int seed, CaseOptions options)
        {
            _seed = seed;
            _options = options;
            _rng = new Random(seed);
            _s = options.Resolve();
        }

        private IEnumerable<Character> Innocents => _suspects.Where(c => !_culprits.Contains(c));

        private IEnumerable<Character> Everyone => _suspects.Append(_victim);

        private bool IsCulprit(Character c) => _culprits.Contains(c);

        /// <summary>People who lie about the crime slot: the culprits, or in an accident, the meddler.</summary>
        private IEnumerable<Character> SlotLiars => _model == CaseModel.Accident ? new[] { _hand } : _culprits;

        public CaseFile Build()
        {
            _manor = ManorLayout.Create(NameBank.HouseName(_rng));
            ChooseModel();
            _lockdown = _rng.Pick(StoryBank.Lockdowns);
            CreateCast();
            AssignTraits();
            AssignMotives();
            PlanCrime();
            ConformTraits();
            PlaceNight();
            PlanSecrets();
            BuildDaySchedule();
            BuildYoungsterDay();
            PlanKnowledge();

            BuildSceneClues();
            foreach (Character c in _suspects)
            {
                BuildLastNight(c);
                BuildSightings(c);
                BuildSelf(c);
                BuildVictimTopic(c);
                BuildWhereabouts(c);
            }

            BuildGossip();
            BuildDocuments();
            BuildStoryClues();
            BuildSecretClues();
            BuildHerring();
            BuildYoungsterClues();

            var eliminations = BuildEliminations();
            FlagKeyClues(eliminations);
            BuildRival();

            string sergeantSurname = NameBank.TakeSurname(_rng, _usedSurnames);
            var style = (SergeantStyle)_rng.Next(3);

            return new CaseFile
            {
                Seed = _seed,
                Options = _options,
                Settings = _s,
                Manor = _manor,
                Title = BuildTitle(),
                Victim = _victim,
                Suspects = _suspects,
                Model = _model,
                CulpritIds = _culprits.Select(c => c.Id).ToList(),
                MurdererId = _model == CaseModel.Accident ? string.Empty : _hand.Id,
                CaseMotive = _motive,
                IntendedVictimId = _intended?.Id,
                CrimeSceneId = _sceneRoom.Id,
                Weapon = _weapon,
                MurderSlot = _ms,
                CrimeSlot = _cs,
                SceneCategories = _cats,
                SceneValues = _scene,
                Night = _night,
                Day = _day,
                Clues = _clues,
                KeyEvidenceIds = _keyEvidence.Distinct().ToList(),
                Eliminations = eliminations,
                ClaimRoomId = _claimRoom,
                Lockdown = _lockdown,
                Briefing = BuildBriefing(),
                Truth = BuildTruth(),
                MotiveDetail = MotiveSentence(),
                SergeantName = $"Sergeant {sergeantSurname}",
                SergeantStyle = style,
                Rival = _rival,
                Children = _youngsters,
            };
        }

        // ================================================================== model

        private void ChooseModel()
        {
            if (_options.Model is { } chosen)
            {
                _model = chosen;
            }
            else
            {
                (CaseModel Model, double Weight)[] weights = _s.Difficulty switch
                {
                    Difficulty.Novice => new[]
                    {
                        (CaseModel.LoneKiller, 4.0), (CaseModel.SecretHeir, 1.0), (CaseModel.Affair, 1.0), (CaseModel.DoctorPoison, 1.0),
                        (CaseModel.MistakenIdentity, 1.0), (CaseModel.Impostor, 1.0), (CaseModel.Conspiracy, 0.7),
                    },
                    Difficulty.Inspector => new[]
                    {
                        (CaseModel.LoneKiller, 3.0), (CaseModel.Conspiracy, 1.0), (CaseModel.FamilyPact, 1.0), (CaseModel.HiddenSecrets, 1.0),
                        (CaseModel.SecretHeir, 1.0), (CaseModel.Accident, 0.8), (CaseModel.DoctorPoison, 1.0), (CaseModel.DoctorNegligence, 0.8),
                        (CaseModel.Affair, 1.0), (CaseModel.MistakenIdentity, 1.0), (CaseModel.Impostor, 1.0),
                    },
                    _ => Enum.GetValues<CaseModel>().Select(m => (m, m == CaseModel.LoneKiller ? 2.0 : 1.5)).ToArray(),
                };

                double roll = _rng.NextDouble() * weights.Sum(w => w.Weight);
                _model = weights[^1].Model;
                foreach (var (model, weight) in weights)
                {
                    roll -= weight;
                    if (roll < 0)
                    {
                        _model = model;
                        break;
                    }
                }
            }

            _variant = _rng.Next(3);
        }

        // ================================================================== cast

        private void CreateCast()
        {
            Gender vg = _rng.Chance(0.65) ? Gender.Male : Gender.Female;
            string family = NameBank.TakeSurname(_rng, _usedSurnames);
            string first = NameBank.TakeFirst(_rng, vg, _usedFirst);
            string title = vg == Gender.Male ? "Lord" : "Lady";
            _victim = new Character
            {
                Id = "victim",
                FirstName = first,
                Surname = family,
                Title = title,
                ShortName = $"{title} {family}",
                Gender = vg,
                Role = RoleKind.Master,
                Station = Station.Family,
                RoleLabel = vg == Gender.Male ? "master of the house" : "mistress of the house",
                IsVictim = true,
                PersonalRoomId = RoomIds.Master,
                PortraitSeed = _rng.Next(),
            };

            int n = _s.SuspectCount;
            bool doctorModel = _model is CaseModel.DoctorPoison or CaseModel.DoctorNegligence;
            bool visitingDoctor = doctorModel && _lockdown.AllowsVisitors && _rng.Chance(0.5);

            var required = new List<RoleDef>();
            if (_model == CaseModel.Affair)
            {
                required.Add(RoleCatalog.Get(RoleKind.Spouse));
            }

            if (_model == CaseModel.FamilyPact)
            {
                bool newcomer = _variant == 1;
                int children = Math.Clamp(n >= 6 ? _rng.Next(2, 4) : 2, 2, Math.Max(2, n - 2 - (newcomer ? 1 : 0)));
                for (int i = 0; i < children; i++)
                {
                    required.Add(RoleCatalog.Get(RoleKind.Child));
                }

                if (newcomer)
                {
                    required.Add(RoleCatalog.Get(_rng.Chance(0.5) ? RoleKind.Ward : RoleKind.Companion));
                }
            }

            if (doctorModel && !visitingDoctor)
            {
                required.Add(RoleCatalog.Get(RoleKind.Doctor));
            }

            if (_model == CaseModel.Impostor && required.All(r => r.Station != Station.Guest))
            {
                required.Add(RoleCatalog.Get(_rng.Pick(new[] { RoleKind.Colonel, RoleKind.BusinessPartner, RoleKind.Companion, RoleKind.Reverend })));
            }

            bool hasSpouse = required.Any(r => r.Kind == RoleKind.Spouse) || _rng.Chance(0.75);
            if (hasSpouse && required.All(r => r.Kind != RoleKind.Spouse))
            {
                required.Insert(0, RoleCatalog.Get(RoleKind.Spouse));
            }

            int gentry = Math.Clamp(_rng.Next(2, 5), required.Count, Math.Max(required.Count, n - 1));
            var roles = new List<RoleDef>(required);
            var pool = RoleCatalog.Gentry
                .Where(r => r.Kind != RoleKind.Spouse && r.Kind != RoleKind.Child && roles.All(x => x.Kind != r.Kind))
                .Where(r => !(visitingDoctor && r.Kind == RoleKind.Doctor))
                .ToList();
            pool.Add(RoleCatalog.Get(RoleKind.Child));
            roles.AddRange(_rng.Shuffled(pool).Take(Math.Max(0, gentry - roles.Count)));
            roles.AddRange(_rng.Shuffled(RoleCatalog.Servants).Take(Math.Max(1, n - roles.Count)));

            var guestRooms = new Queue<string>(new[] { RoomIds.Blue, RoomIds.Rose, RoomIds.Green });
            for (int i = 0; i < roles.Count; i++)
            {
                Character c = MakeCharacter(roles[i], i, family, visitor: false);
                if (string.IsNullOrEmpty(c.PersonalRoomId))
                {
                    c.PersonalRoomId = guestRooms.Count > 0 ? guestRooms.Dequeue() : RoomIds.Green;
                }

                _suspects.Add(c);
                _defs[c.Id] = roles[i];
            }

            LabelChildren();
            CreateYoungsters(family);

            // Callers from outside the house.
            if (_lockdown.AllowsVisitors)
            {
                var visitorRoles = new List<RoleDef>();
                if (visitingDoctor)
                {
                    visitorRoles.Add(RoleCatalog.Visitor(RoleKind.Doctor));
                }

                int wanted = _s.Difficulty == Difficulty.Novice ? 1 : _rng.Next(1, 3);
                var available = _rng.Shuffled(RoleCatalog.Visitors.Where(v => _suspects.All(s => s.Role != v.Kind) && visitorRoles.All(x => x.Kind != v.Kind)));
                visitorRoles.AddRange(available.Take(Math.Max(0, wanted - visitorRoles.Count)));
                foreach (RoleDef def in visitorRoles)
                {
                    Character v = MakeCharacter(def, _suspects.Count, family, visitor: true);
                    _suspects.Add(v);
                    _defs[v.Id] = def;
                }
            }
        }

        private Character MakeCharacter(RoleDef def, int index, string family, bool visitor)
        {
            Gender g = def.Kind == RoleKind.Spouse
                ? (_victim.IsMale ? Gender.Female : Gender.Male)
                : _rng.Chance(def.MaleChance) ? Gender.Male : Gender.Female;
            bool male = g == Gender.Male;
            Portrait? portrait = PortraitCatalog.Pick(g, def.Kind, _rng, _usedPortraits);
            string first = portrait?.FirstNames is { Length: > 0 } firsts
                ? TakeFrom(firsts, _usedFirst)
                : NameBank.TakeFirst(_rng, g, _usedFirst);
            string surname = def.Kind is RoleKind.Spouse or RoleKind.Heir or RoleKind.Child
                ? family
                : portrait?.Surnames is { Length: > 0 } surnames
                    ? TakeFrom(surnames, _usedSurnames)
                    : NameBank.TakeSurname(_rng, _usedSurnames);

            var (title, shortName, label) = def.Kind switch
            {
                RoleKind.Butler => (string.Empty, surname, "the butler"),
                RoleKind.Housekeeper => ("Mrs", $"Mrs {surname}", "the housekeeper"),
                RoleKind.Cook => (male ? "Mr" : "Mrs", male ? $"Mr {surname}" : $"Mrs {surname}", "the cook"),
                RoleKind.Gardener => (string.Empty, surname, "the gardener"),
                RoleKind.Maid => (string.Empty, first, "the housemaid"),
                RoleKind.Valet => (string.Empty, surname, "the valet"),
                RoleKind.Governess => (male ? "Mr" : "Miss", male ? $"Mr {surname}" : $"Miss {surname}", male ? "the tutor" : "the governess"),
                RoleKind.Spouse => (male ? "Mr" : "Lady", male ? $"Mr {first}" : $"Lady {first}", male ? "the widower" : "the widow"),
                RoleKind.Heir => (male ? "Mr" : "Miss", male ? $"Mr {first}" : $"Miss {first}", male ? "the nephew and heir" : "the niece and heir"),
                RoleKind.Child => (male ? "Mr" : "Miss", male ? $"Mr {first}" : $"Miss {first}", male ? "the son" : "the daughter"),
                RoleKind.Ward => (male ? "Mr" : "Miss", male ? $"Mr {surname}" : $"Miss {surname}", "the ward"),
                RoleKind.Colonel => ("Colonel", $"Colonel {surname}", "an old army friend"),
                RoleKind.Doctor => ("Dr", $"Dr {surname}", visitor ? "the family doctor, who calls at the house" : "the family physician, staying the week"),
                RoleKind.Reverend => ("Reverend", $"Reverend {surname}", visitor ? "the vicar, who calls at the house" : "the vicar"),
                RoleKind.Solicitor => ("Mr", $"Mr {surname}", visitor ? "the family solicitor, who calls at the house" : "the family solicitor"),
                RoleKind.BusinessPartner => (male ? "Mr" : "Mrs", male ? $"Mr {surname}" : $"Mrs {surname}", "a business partner"),
                RoleKind.Companion => ("Miss", $"Miss {surname}", "a lady's companion"),
                RoleKind.Constable => ("Constable", $"Constable {surname}", "the village constable"),
                _ => (string.Empty, surname, "a guest"),
            };

            if (portrait?.Label is { } own)
            {
                label = visitor ? $"{own}, who calls at the house" : own;
            }

            return new Character
            {
                Id = $"p{index}",
                FirstName = first,
                Surname = surname,
                Title = title,
                ShortName = shortName,
                Gender = g,
                Role = def.Kind,
                Station = def.Station,
                RoleLabel = label,
                PersonalRoomId = def.PersonalRoomId,
                PortraitSeed = _rng.Next(),
                IsVisitor = visitor,
                PortraitId = portrait?.Id,
            };
        }

        private string TakeFrom(string[] pool, HashSet<string> used)
        {
            var free = pool.Where(n => !used.Contains(n)).ToList();
            string name = free.Count > 0 ? _rng.Pick(free) : _rng.Pick(pool);
            used.Add(name);
            return name;
        }

        /// <summary>"The elder son", "the younger daughter" and so on, once we know how many there are.</summary>
        private void LabelChildren()
        {
            foreach (var group in _suspects.Where(c => c.Role == RoleKind.Child).GroupBy(c => c.Gender))
            {
                var list = group.ToList();
                string noun = group.Key == Gender.Male ? "son" : "daughter";
                for (int i = 0; i < list.Count; i++)
                {
                    list[i].RoleLabel = list.Count switch
                    {
                        1 => $"the {noun}",
                        2 => i == 0 ? $"the elder {noun}" : $"the younger {noun}",
                        _ => i == 0 ? $"the eldest {noun}" : i == list.Count - 1 ? $"the youngest {noun}" : $"the second {noun}",
                    };
                }
            }
        }

        private void AssignTraits()
        {
            foreach (Character c in Everyone)
            {
                foreach (TraitDef def in TraitCatalog.All)
                {
                    c.Traits.Set(def.Category, def.Assign(c, _rng));
                }

                if (c.Traits.Frail && _rng.Chance(0.5))
                {
                    c.Traits.Set(TraitCategory.HairColour, "Grey");
                }

                if (c.PortraitId is null)
                {
                    PortraitCatalog.Assign(c, _rng, _usedPortraits);
                }
                else
                {
                    PortraitCatalog.ApplyLooks(c);
                }
            }

            foreach (Character k in _youngsters)
            {
                foreach (TraitDef def in TraitCatalog.All)
                {
                    k.Traits.Set(def.Category, def.Values[0].Id);
                }

                PortraitCatalog.Assign(k, _rng, _usedPortraits);
            }
        }

        private void AssignMotives()
        {
            foreach (Character c in _suspects)
            {
                c.Motive = !c.IsVisitor && _rng.Chance(0.6) ? _rng.Pick(_defs[c.Id].Motives) : MotiveKind.None;
            }
        }

        // ================================================================== clue helpers

        private Clue AddClue(ClueKind kind, string title, string text)
        {
            var clue = new Clue { Id = $"c{_counter++}", Kind = kind, Title = title, Text = text };
            _clues.Add(clue);
            return clue;
        }

        private string Room(string id) => _manor.NameOf(id);

        /// <summary>"in the Library", or "away from the house".</summary>
        private string In(string id) => id == RoomIds.Away ? "away from the house" : $"in the {Room(id)}";

        private List<Character> PeopleAt(string roomId, int slot, Character except) =>
            roomId == RoomIds.Away
                ? new List<Character>()
                : Everyone.Where(p => p != except && _night[p.Id][slot] == roomId).ToList();

        private static void AddUnique<T>(List<T> list, T item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        private Character? Find(string id) => _suspects.FirstOrDefault(s => s.Id == id);
    }
}
