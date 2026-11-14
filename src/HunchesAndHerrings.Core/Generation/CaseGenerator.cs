using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

/// <summary>
/// Builds a complete, solvable mystery from a seed.
/// <para>
/// Solvability is designed in rather than checked afterwards. Every innocent is given at least one
/// route to elimination:
/// <list type="bullet">
/// <item>"Twins" match every scene trait, so they are guaranteed an alibi (a partner who vouches for them,
/// a third-party sighting, or an impartial document).</item>
/// <item>Everyone else fails at least one scene trait, and that trait is revealed by one or more truthful
/// sources (their own word, gossip, or a document in their room).</item>
/// </list>
/// The murderer always lies about where they were, and that lie is contradicted either by a witness who
/// was alone in the claimed room or by physical proof the room was unused.
/// </para>
/// </summary>
public static class CaseGenerator
{
    public static CaseFile Generate(int seed, Difficulty difficulty) => Generate(seed, new CaseOptions(difficulty));

    public static CaseFile Generate(int seed, CaseOptions options) => new Builder(seed, options).Build();

    /// <summary>Holds all working state for one generation run.</summary>
    private sealed class Builder
    {
        private static readonly TraitCategory[] AllCategories = Enum.GetValues<TraitCategory>();
        private static readonly Allergen[] RealAllergens = { Allergen.Lilies, Allergen.Cats, Allergen.Horsehair, Allergen.Dust };
        private static readonly SmokeKind[] Smokes = { SmokeKind.Pipe, SmokeKind.Cigar, SmokeKind.Cigarette };

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

        private Manor _manor = null!;
        private Character _victim = null!;
        private Character _murderer = null!;
        private Room _sceneRoom = null!;
        private Weapon _weapon = null!;
        private int _ms;
        private string _claimRoom = string.Empty;
        private bool _emptyRoomMode;
        private Character? _loneWitness;
        private Character? _liar;
        private int _liarSlot;
        private Character? _genderWitness;
        private TraitCategory? _murdererLieCategory;
        private int _counter;

        public Builder(int seed, CaseOptions options)
        {
            _seed = seed;
            _options = options;
            _rng = new Random(seed);
            _s = options.Resolve();
        }

        private IEnumerable<Character> Innocents => _suspects.Where(c => c != _murderer);

        private IEnumerable<Character> Everyone => _suspects.Append(_victim);

        public CaseFile Build()
        {
            _manor = ManorLayout.Create(NameBank.HouseName(_rng));
            CreateCast();
            AssignTraits();
            AssignMotives();
            PlanCrime();
            ConformTraits();
            PlaceNight();
            PlanSecretLiar();
            BuildDaySchedule();
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

            var eliminations = BuildEliminations();
            FlagKeyClues(eliminations);

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
                MurdererId = _murderer.Id,
                CrimeSceneId = _sceneRoom.Id,
                Weapon = _weapon,
                MurderSlot = _ms,
                SceneCategories = _cats,
                SceneValues = _scene,
                Night = _night,
                Day = _day,
                Clues = _clues,
                KeyEvidenceIds = _keyEvidence.Distinct().ToList(),
                Eliminations = eliminations,
                ClaimRoomId = _claimRoom,
                Briefing = BuildBriefing(),
                Truth = BuildTruth(),
                MotiveDetail = TextBank.MotiveDetail(_murderer.Motive, _murderer, _victim),
                SergeantName = $"Sergeant {sergeantSurname}",
                SergeantStyle = style,
            };
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
            bool hasSpouse = _rng.Chance(0.75);
            int gentry = Math.Min(_rng.Next(2, 5), hasSpouse ? 4 : 3);
            gentry = Math.Min(gentry, n - 2);

            var roles = new List<RoleDef>();
            if (hasSpouse)
            {
                roles.Add(RoleCatalog.Get(RoleKind.Spouse));
            }

            roles.AddRange(_rng.Shuffled(RoleCatalog.Gentry.Where(r => r.Kind != RoleKind.Spouse)).Take(gentry - (hasSpouse ? 1 : 0)));
            roles.AddRange(_rng.Shuffled(RoleCatalog.Servants).Take(n - gentry));

            var guestRooms = new Queue<string>(new[] { RoomIds.Blue, RoomIds.Rose, RoomIds.Green });
            for (int i = 0; i < roles.Count; i++)
            {
                Character c = MakeCharacter(roles[i], i, family);
                if (string.IsNullOrEmpty(c.PersonalRoomId))
                {
                    c.PersonalRoomId = guestRooms.Count > 0 ? guestRooms.Dequeue() : RoomIds.Green;
                }

                _suspects.Add(c);
                _defs[c.Id] = roles[i];
            }
        }

        private Character MakeCharacter(RoleDef def, int index, string family)
        {
            Gender g = def.Kind == RoleKind.Spouse
                ? (_victim.IsMale ? Gender.Female : Gender.Male)
                : _rng.Chance(def.MaleChance) ? Gender.Male : Gender.Female;
            bool male = g == Gender.Male;
            string first = NameBank.TakeFirst(_rng, g, _usedFirst);
            string surname = def.Kind is RoleKind.Spouse or RoleKind.Heir ? family : NameBank.TakeSurname(_rng, _usedSurnames);

            var (title, shortName, label) = def.Kind switch
            {
                RoleKind.Butler => (string.Empty, surname, "the butler"),
                RoleKind.Housekeeper => ("Mrs", $"Mrs {surname}", "the housekeeper"),
                RoleKind.Cook => (male ? "Mr" : "Mrs", male ? $"Mr {surname}" : $"Mrs {surname}", "the cook"),
                RoleKind.Gardener => (string.Empty, surname, "the gardener"),
                RoleKind.Maid => (string.Empty, first, "the housemaid"),
                RoleKind.Valet => (string.Empty, surname, "the valet"),
                RoleKind.Governess => ("Miss", $"Miss {surname}", "the governess"),
                RoleKind.Spouse => (male ? "Mr" : "Lady", male ? $"Mr {first}" : $"Lady {first}", male ? "the widower" : "the widow"),
                RoleKind.Heir => (male ? "Mr" : "Miss", male ? $"Mr {first}" : $"Miss {first}", male ? "the nephew and heir" : "the niece and heir"),
                RoleKind.Ward => (male ? "Mr" : "Miss", male ? $"Mr {surname}" : $"Miss {surname}", "the ward"),
                RoleKind.Colonel => ("Colonel", $"Colonel {surname}", "an old army friend"),
                RoleKind.Doctor => ("Dr", $"Dr {surname}", "the family physician"),
                RoleKind.Reverend => ("Reverend", $"Reverend {surname}", "the vicar"),
                RoleKind.Solicitor => ("Mr", $"Mr {surname}", "the family solicitor"),
                RoleKind.BusinessPartner => (male ? "Mr" : "Mrs", male ? $"Mr {surname}" : $"Mrs {surname}", "a business partner"),
                RoleKind.Companion => ("Miss", $"Miss {surname}", "a lady's companion"),
                _ => (string.Empty, surname, "a guest"),
            };

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
            };
        }

        private void AssignTraits()
        {
            foreach (Character c in _suspects)
            {
                Traits t = c.Traits;
                bool male = c.IsMale;
                t.Smoke = _rng.Chance(male ? 0.6 : 0.25)
                    ? (male ? _rng.Pick(Smokes) : SmokeKind.Cigarette)
                    : SmokeKind.None;
                t.Hand = _rng.Chance(0.2) ? Handedness.Left : Handedness.Right;
                double r = _rng.NextDouble();
                t.Shoe = male
                    ? (r < 0.1 ? ShoeSize.Small : r < 0.55 ? ShoeSize.Medium : ShoeSize.Large)
                    : (r < 0.55 ? ShoeSize.Small : r < 0.95 ? ShoeSize.Medium : ShoeSize.Large);
                t.Allergy = _rng.Chance(0.4) ? _rng.Pick(RealAllergens) : Allergen.None;
                t.Frail = _rng.Chance(_defs[c.Id].FrailChance);
            }
        }

        private void AssignMotives()
        {
            foreach (Character c in _suspects)
            {
                c.Motive = _rng.Chance(0.65) ? _rng.Pick(_defs[c.Id].Motives) : MotiveKind.None;
            }
        }

        // ================================================================== crime

        private void PlanCrime()
        {
            _murderer = _rng.Pick(_suspects);
            if (_murderer.Motive == MotiveKind.None)
            {
                _murderer.Motive = _rng.Pick(_defs[_murderer.Id].Motives);
            }

            _cats.AddRange(_rng.Shuffled(AllCategories).Take(2));
            _ms = _rng.Next(1, 3);

            var sceneCandidates = _manor.Rooms.Where(r => !r.IsPassage && r.Id != RoomIds.Hall).ToList();
            _sceneRoom = _rng.Pick(sceneCandidates);

            bool heavy = _cats.Contains(TraitCategory.Strength);
            var weapons = WeaponCatalog.All.Where(w => !heavy || w.Heavy).ToList();
            _weapon = _rng.Pick(weapons);
        }

        /// <summary>
        /// Makes the murderer match the scene, splits the innocents into alibi-dependent "twins" and
        /// trait-excluded "others", and adjusts traits so both groups behave as intended.
        /// </summary>
        private void ConformTraits()
        {
            Traits m = _murderer.Traits;
            foreach (TraitCategory cat in _cats)
            {
                switch (cat)
                {
                    case TraitCategory.Smoking:
                        if (m.Smoke == SmokeKind.None)
                        {
                            m.Smoke = _murderer.IsMale ? _rng.Pick(Smokes) : SmokeKind.Cigarette;
                        }

                        _scene[cat] = m.Smoke.ToString();
                        break;
                    case TraitCategory.Handedness:
                        m.Hand = Handedness.Left;
                        _scene[cat] = m.Hand.ToString();
                        break;
                    case TraitCategory.ShoeSize:
                        _scene[cat] = m.Shoe.ToString();
                        break;
                    case TraitCategory.Allergy:
                        var options = RealAllergens.Where(a => a != m.Allergy).ToList();
                        _scene[cat] = _rng.Pick(options).ToString();
                        break;
                    case TraitCategory.Strength:
                        m.Frail = false;
                        _scene[cat] = TraitRules.Heavy;
                        break;
                }
            }

            // Twins work best when they could plausibly share the murderer's traits (e.g. pipe smokers).
            bool genderedSmoke = _cats.Contains(TraitCategory.Smoking) && _scene[TraitCategory.Smoking] != nameof(SmokeKind.Cigarette);
            var innocents = _rng.Shuffled(Innocents)
                .OrderBy(c => genderedSmoke && c.Gender != _murderer.Gender ? 1 : 0)
                .ToList();

            int twinCount = Math.Clamp(_s.AlibiTwins, 1, innocents.Count - 1);
            _twins.AddRange(innocents.Take(twinCount));
            _others.AddRange(innocents.Skip(twinCount));

            foreach (Character t in _twins)
            {
                MakeMatch(t);
            }

            foreach (Character o in _others)
            {
                if (!IsTraitExcluded(o))
                {
                    MakeExcluded(o, _rng.Pick(_cats));
                }
            }
        }

        private bool IsTraitExcluded(Character c) =>
            _cats.Any(cat => TraitRules.Excludes(cat, _scene[cat], c.Traits.ValueOf(cat)));

        private void MakeMatch(Character c)
        {
            Traits t = c.Traits;
            foreach (TraitCategory cat in _cats)
            {
                switch (cat)
                {
                    case TraitCategory.Smoking: t.Smoke = Enum.Parse<SmokeKind>(_scene[cat]); break;
                    case TraitCategory.Handedness: t.Hand = Handedness.Left; break;
                    case TraitCategory.ShoeSize: t.Shoe = Enum.Parse<ShoeSize>(_scene[cat]); break;
                    case TraitCategory.Allergy:
                        if (t.Allergy.ToString() == _scene[cat])
                        {
                            t.Allergy = Allergen.None;
                        }

                        break;
                    case TraitCategory.Strength: t.Frail = false; break;
                }
            }
        }

        private void MakeExcluded(Character c, TraitCategory cat)
        {
            Traits t = c.Traits;
            switch (cat)
            {
                case TraitCategory.Smoking: t.Smoke = SmokeKind.None; break;
                case TraitCategory.Handedness: t.Hand = Handedness.Right; break;
                case TraitCategory.ShoeSize:
                    var sizes = Enum.GetValues<ShoeSize>().Where(s => s.ToString() != _scene[cat]).ToList();
                    t.Shoe = _rng.Pick(sizes);
                    break;
                case TraitCategory.Allergy: t.Allergy = Enum.Parse<Allergen>(_scene[cat]); break;
                case TraitCategory.Strength: t.Frail = true; break;
            }
        }

        // ================================================================== last night

        private List<string> NightRooms(Character c)
        {
            var rooms = _defs[c.Id].NightRooms.ToList();
            if (!rooms.Contains(c.PersonalRoomId))
            {
                rooms.Add(c.PersonalRoomId);
            }

            return rooms;
        }

        private string PickRoom(IEnumerable<string> preferred, Func<string, bool> allowed)
        {
            var options = preferred.Where(allowed).Distinct().ToList();
            if (options.Count == 0)
            {
                options = ManorLayout.SharedRooms.Where(allowed).ToList();
            }

            return options.Count > 0 ? _rng.Pick(options) : RoomIds.Library;
        }

        /// <summary>
        /// Decides everyone's true whereabouts for the three night slots, building in the alibi
        /// structures that make the case solvable.
        /// </summary>
        private void PlaceNight()
        {
            foreach (Character c in Everyone)
            {
                _night[c.Id] = new string[NightSlots.Count];
            }

            string scene = _sceneRoom.Id;
            _night[_murderer.Id][_ms] = scene;

            // 1. Where the murderer will claim to have been, and what disproves it.
            _emptyRoomMode = true;
            if (_others.Count > 0 && _rng.Chance(0.6))
            {
                Character witness = _rng.Pick(_others);
                var rooms = NightRooms(witness).Where(r => ManorLayout.SharedRooms.Contains(r) && r != scene).ToList();
                if (rooms.Count > 0)
                {
                    _loneWitness = witness;
                    _claimRoom = _rng.Pick(rooms);
                    _night[witness.Id][_ms] = _claimRoom;
                    _emptyRoomMode = false;
                }
            }

            if (_emptyRoomMode)
            {
                var rooms = NightRooms(_murderer).Where(r => ManorLayout.ProvablyEmptyRooms.Contains(r) && r != scene).ToList();
                if (rooms.Count == 0)
                {
                    rooms = ManorLayout.ProvablyEmptyRooms.Where(r => r != scene).ToList();
                }

                _claimRoom = _rng.Pick(rooms);
                _reserved.Add(_claimRoom);
            }

            // 2. Twins get paired up, or a solo alibi.
            var forbiddenAtMurder = new HashSet<string>(_reserved) { scene, _claimRoom };
            bool IsFree(Character c) => _night[c.Id][_ms] is null;

            foreach (Character t in _twins)
            {
                if (!IsFree(t))
                {
                    continue;
                }

                Character? partner = _rng.Shuffled(Innocents.Where(p => p != t && IsFree(p)))
                    .OrderBy(p => _twins.Contains(p) ? 1 : 0)
                    .FirstOrDefault();

                if (partner is not null)
                {
                    var common = NightRooms(t).Intersect(NightRooms(partner)).Where(ManorLayout.SharedRooms.Contains).ToList();
                    var either = NightRooms(t).Union(NightRooms(partner)).Where(ManorLayout.SharedRooms.Contains).ToList();
                    string room = PickRoom(common.Count > 0 ? common : either, r => !forbiddenAtMurder.Contains(r));
                    _night[t.Id][_ms] = room;
                    _night[partner.Id][_ms] = room;
                    _pairs.Add((t, partner, room));
                }
                else
                {
                    _night[t.Id][_ms] = PickRoom(NightRooms(t), r => !forbiddenAtMurder.Contains(r));
                    _soloTwins.Add(t);
                }
            }

            // 3. Everyone else at the time of the murder.
            foreach (Character c in _suspects.Where(IsFree))
            {
                _night[c.Id][_ms] = PickRoom(NightRooms(c), r => !forbiddenAtMurder.Contains(r));
            }

            // 4. The other slots. Innocents never go into the scene; nobody enters a provably empty room.
            foreach (Character c in _suspects)
            {
                for (int s = 0; s < NightSlots.Count; s++)
                {
                    if (_night[c.Id][s] is not null)
                    {
                        continue;
                    }

                    bool isMurderer = c == _murderer;
                    _night[c.Id][s] = PickRoom(NightRooms(c), r => !_reserved.Contains(r) && (r != scene || (isMurderer && s < _ms)));
                }
            }

            // 5. The victim: about the house early, then in the scene from the murder onwards.
            for (int s = 0; s < NightSlots.Count; s++)
            {
                _night[_victim.Id][s] = s >= _ms
                    ? scene
                    : PickRoom(VictimEveningRooms, r => !_reserved.Contains(r));
            }

            // 6. Solo twins: a third-party glimpse if anyone was next door, otherwise the bell register.
            foreach (Character t in _soloTwins)
            {
                string room = _night[t.Id][_ms];
                Character? neighbour = _rng.Shuffled(Innocents.Where(x => x != t && _manor.AreAdjacent(_night[x.Id][_ms], room)))
                    .FirstOrDefault();
                if (neighbour is not null)
                {
                    _forcedGlimpses.Add((neighbour.Id, t.Id, _ms));
                }
                else
                {
                    _bellRegisterAlibis.Add(t);
                }
            }
        }

        /// <summary>An innocent with a secret: they lie about where they were earlier in the evening.</summary>
        private void PlanSecretLiar()
        {
            if (_s.SecretLiars <= 0)
            {
                return;
            }

            var pool = _others.Where(o => o != _loneWitness).ToList();
            if (pool.Count == 0)
            {
                return;
            }

            Character liar = _rng.Pick(pool);
            var slots = Enumerable.Range(0, NightSlots.Count).Where(s => s != _ms).ToList();
            int slot = _rng.Pick(slots);
            var spots = new[] { RoomIds.Gardens, RoomIds.Conservatory }
                .Where(r => !_reserved.Contains(r) && r != _sceneRoom.Id && r != liar.PersonalRoomId)
                .ToList();
            if (spots.Count == 0)
            {
                return;
            }

            _night[liar.Id][slot] = _rng.Pick(spots);
            _liar = liar;
            _liarSlot = slot;
        }

        // ================================================================== today

        private void BuildDaySchedule()
        {
            string scene = _sceneRoom.Id;
            string Safe(string room) => room == scene ? RoomIds.Hall : room;

            foreach (Character c in _suspects)
            {
                var hours = new string[GameRules.DayHours];
                var rooms = _defs[c.Id].DayRooms.Append(c.PersonalRoomId).Where(r => r != scene).ToList();
                string? prev = null;
                for (int h = 0; h < GameRules.DayHours; h++)
                {
                    string room;
                    bool gentry = c.Station != Station.Servant;
                    if (gentry && (h == 0 || h == 5))
                    {
                        room = Safe(RoomIds.Dining);
                    }
                    else if (!gentry && h == 5)
                    {
                        room = c.Role switch
                        {
                            RoleKind.Butler => Safe(RoomIds.Dining),
                            RoleKind.Cook => Safe(RoomIds.Kitchen),
                            _ => Safe(RoomIds.ServantsHall),
                        };
                    }
                    else if (prev is not null && _rng.Chance(0.5))
                    {
                        room = prev;
                    }
                    else
                    {
                        room = rooms.Count > 0 ? _rng.Pick(rooms) : RoomIds.Hall;
                    }

                    hours[h] = room;
                    prev = room;
                }

                _day[c.Id] = hours;
            }

            // Somebody has business in the village this afternoon.
            if (_rng.Chance(0.5))
            {
                Character away = _rng.Pick(_suspects.Where(c => c.Role != RoleKind.Spouse).ToList());
                _day[away.Id][6] = RoomIds.Away;
                _day[away.Id][7] = RoomIds.Away;
            }
        }

        // ================================================================== who knows what

        private sealed class GossipPlan
        {
            public List<(TraitCategory Category, string Value, bool Truthful)> Traits { get; } = new();
            public bool Motive { get; set; }
        }

        private GossipPlan Gossip(Character speaker, Character target)
        {
            if (!_gossip.TryGetValue((speaker.Id, target.Id), out GossipPlan? plan))
            {
                plan = new GossipPlan();
                _gossip[(speaker.Id, target.Id)] = plan;
            }

            return plan;
        }

        private void PlanKnowledge()
        {
            var innocents = Innocents.ToList();
            foreach (Character c in _suspects)
            {
                _selfTraits[c.Id] = new List<TraitCategory>();
            }

            // Sources that clear the trait-excluded innocents.
            foreach (Character o in _others)
            {
                var failing = _cats.Where(cat => TraitRules.Excludes(cat, _scene[cat], o.Traits.ValueOf(cat))).ToList();
                TraitCategory cat = _rng.Pick(failing);
                var kinds = _rng.Shuffled(new[] { 0, 1, 2 }).Take(Math.Max(1, _s.TraitSourceCount));
                foreach (int kind in kinds)
                {
                    var speakers = innocents.Where(x => x != o).ToList();
                    if (kind == 0 && speakers.Count > 0)
                    {
                        Gossip(_rng.Pick(speakers), o).Traits.Add((cat, o.Traits.ValueOf(cat), true));
                    }
                    else if (kind == 1)
                    {
                        _traitDocs.Add((o, cat));
                    }
                    else
                    {
                        AddUnique(_selfTraits[o.Id], cat);
                    }
                }
            }

            // Everyone mentions something about themselves.
            foreach (Character c in _suspects)
            {
                AddUnique(_selfTraits[c.Id], _rng.Pick(AllCategories));
            }

            // The murderer lies about one scene trait when asked about themselves...
            _murdererLieCategory = _rng.Pick(_cats);
            AddUnique(_selfTraits[_murderer.Id], _murdererLieCategory.Value);

            // ...and sometimes the truth is out there.
            if (_rng.Chance(_s.MurdererExposure))
            {
                TraitCategory exposed = _rng.Chance(0.7) ? _murdererLieCategory.Value : _rng.Pick(_cats);
                if (_rng.Chance(0.5) && innocents.Count > 0)
                {
                    Gossip(_rng.Pick(innocents), _murderer).Traits.Add((exposed, _murderer.Traits.ValueOf(exposed), true));
                }
                else
                {
                    _traitDocs.Add((_murderer, exposed));
                }
            }

            // The murderer tries to frame someone who is actually ruled out.
            if (_s.FrameLies && _others.Count > 0 && _rng.Chance(0.6))
            {
                Character target = _rng.Pick(_others);
                var failing = _cats.Where(cat => TraitRules.Excludes(cat, _scene[cat], target.Traits.ValueOf(cat))).ToList();
                TraitCategory cat = _rng.Pick(failing);
                Gossip(_murderer, target).Traits.Add((cat, FrameValue(cat), false));
            }

            // Somebody knows the murderer's motive.
            if (innocents.Count > 0)
            {
                Gossip(_rng.Pick(innocents), _murderer).Motive = true;
            }

            // General chatter: each innocent knows something about one or two others.
            foreach (Character speaker in innocents)
            {
                int count = _rng.Next(1, 3);
                foreach (Character target in _rng.Shuffled(_suspects.Where(x => x != speaker)).Take(count))
                {
                    if (target.Motive != MotiveKind.None && _rng.Chance(0.5))
                    {
                        Gossip(speaker, target).Motive = true;
                    }
                    else
                    {
                        TraitCategory cat = _rng.Pick(AllCategories);
                        Gossip(speaker, target).Traits.Add((cat, target.Traits.ValueOf(cat), true));
                    }
                }
            }

            // A partial sighting of the killer's figure.
            if (_rng.Chance(_s.GenderSightingChance))
            {
                var near = innocents.Where(x => _manor.AreAdjacent(_night[x.Id][_ms], _sceneRoom.Id)).ToList();
                _genderWitness = near.Count > 0 ? _rng.Pick(near) : _rng.PickOrDefault(innocents);
            }
        }

        /// <summary>A value for <paramref name="cat"/> that would make someone look like the killer.</summary>
        private string FrameValue(TraitCategory cat) => cat switch
        {
            TraitCategory.Allergy => nameof(Allergen.None),
            TraitCategory.Strength => TraitRules.Sound,
            _ => _scene[cat],
        };

        /// <summary>A value for <paramref name="cat"/> the murderer claims, which would clear them.</summary>
        private string LieValue(TraitCategory cat) => cat switch
        {
            TraitCategory.Smoking => nameof(SmokeKind.None),
            TraitCategory.Handedness => nameof(Handedness.Right),
            TraitCategory.ShoeSize => Enum.GetValues<ShoeSize>().First(s => s.ToString() != _scene[cat]).ToString(),
            TraitCategory.Allergy => _scene[cat],
            _ => TraitRules.Frail,
        };

        private static void AddUnique<T>(List<T> list, T item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
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

        private List<Character> PeopleAt(string roomId, int slot, Character except) =>
            Everyone.Where(p => p != except && _night[p.Id][slot] == roomId).ToList();

        // ================================================================== the scene

        private void BuildSceneClues()
        {
            string scene = _sceneRoom.Id;
            int minute = _rng.Next(4, 52);
            string watch = $"{NightSlots.Hour24(_ms) - 12}:{minute:00}";
            string pose = _rng.Pick(new[] { "face down by the hearth", "slumped across the floor", "sprawled beside an overturned chair", "crumpled against the wall" });

            Clue body = AddClue(ClueKind.Scene, "The body",
                $"{_victim.ShortName} lies {pose}. {_victim.HisCap} pocket watch, smashed in the fall, stopped at {watch}. Death came at around {NightSlots.Spoken(_ms)}.");
            body.RoomId = scene;
            body.Facts.Add(new TimeOfDeathFact(_ms));
            body.IsKey = true;

            Clue weapon = AddClue(ClueKind.Scene, $"The weapon: {_weapon.Name.ToLowerInvariant()}", _weapon.SceneText);
            weapon.RoomId = scene;
            weapon.Facts.Add(new WeaponFact(_weapon.Id));

            foreach (TraitCategory cat in _cats)
            {
                Clue c = AddClue(ClueKind.Scene, TraitRules.SceneLabel(cat, _scene[cat]), TextBank.SceneText(cat, _scene[cat], scene));
                c.RoomId = scene;
                c.Facts.Add(new SceneFact(cat, _scene[cat]));
                c.IsKey = true;
            }

            if (_rng.Chance(0.5))
            {
                Clue note = AddClue(ClueKind.Scene, "A note in the victim's pocket",
                    $"Folded in {_victim.ShortName}'s pocket: \"Tonight, {NightSlots.Spoken(_ms)}, the {Room(scene)}. We will settle this. — {_murderer.FirstName[0]}.\"");
                note.RoomId = scene;
            }

            if (_weapon.OriginRoomId != scene)
            {
                Clue missing = AddClue(ClueKind.Observation, $"Missing: {_weapon.Name.ToLowerInvariant()}", _weapon.MissingText);
                missing.RoomId = _weapon.OriginRoomId;
            }
        }

        // ================================================================== statements

        private string ClaimedRoom(Character c, int slot)
        {
            if (c == _murderer && slot == _ms)
            {
                return _claimRoom;
            }

            if (c == _liar && slot == _liarSlot)
            {
                return c.PersonalRoomId;
            }

            return _night[c.Id][slot];
        }

        private bool IsLieSlot(Character c, int slot) =>
            (c == _murderer && slot == _ms) || (c == _liar && slot == _liarSlot);

        private void BuildLastNight(Character c)
        {
            var spoken = new List<string>();
            var notes = new List<string>();
            var facts = new List<Fact>();
            var alibiFor = new List<string>();

            for (int s = 0; s < NightSlots.Count; s++)
            {
                string room = ClaimedRoom(c, s);
                bool lie = IsLieSlot(c, s);
                var companions = lie ? new List<Character>() : PeopleAt(room, s, c);
                bool exclusive = c == _loneWitness && s == _ms;
                var names = companions.Select(p => p.ShortName).ToList();

                facts.Add(new LocationFact(c.Id, s, room));
                foreach (Character p in companions)
                {
                    facts.Add(new LocationFact(p.Id, s, room));
                    if (s == _ms && !p.IsVictim)
                    {
                        alibiFor.Add(p.Id);
                    }
                }

                string with = companions.Count > 0
                    ? $" with {TextBank.JoinNames(names)}"
                    : exclusive ? ", quite alone. Not a soul came in the whole time"
                    : lie ? ", alone" : string.Empty;
                spoken.Add($"{TextBank.SlotLead(s, _rng)} I was in the {Room(room)}, {TextBank.Activity(room, _rng)}{with}.");

                string noteWith = companions.Count > 0 ? $" (with {TextBank.JoinNames(names)})" : exclusive ? " (alone; \"nobody came in\")" : string.Empty;
                notes.Add($"{NightSlots.Short(s)}: {Room(room)}{noteWith}");

                if (exclusive)
                {
                    facts.Add(new OccupancyFact(room, s, new[] { c.Id }));
                }
            }

            Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: last night", string.Join("  ·  ", notes));
            clue.Spoken = string.Join(" ", spoken);
            clue.SpeakerId = c.Id;
            clue.Topic = Topic.LastNight;
            clue.Facts.AddRange(facts);
            clue.AlibiFor.AddRange(alibiFor);
            clue.IsTruthful = c != _murderer && c != _liar;

            if (c == _murderer || c == _loneWitness)
            {
                _keyEvidence.Add(clue.Id);
            }
        }

        private void BuildSightings(Character c)
        {
            var spoken = new List<string>();
            var notes = new List<string>();
            var facts = new List<Fact>();
            var alibiFor = new List<string>();

            for (int s = 0; s < NightSlots.Count; s++)
            {
                if (IsLieSlot(c, s))
                {
                    continue;
                }

                string myRoom = _night[c.Id][s];

                // The victim's movements, if they shared a room before the murder.
                if (s < _ms && _night[_victim.Id][s] == myRoom)
                {
                    string vref = TextBank.VictimRef(c, _victim);
                    string line = $"{TextBank.SlotLead(s, _rng)} {vref} was with us in the {Room(myRoom)}.";
                    if (s == _ms - 1 && _rng.Chance(0.7))
                    {
                        line += $" {_victim.HeCap} said {_victim.He} had to see someone in the {Room(_sceneRoom.Id)} at {NightSlots.Spoken(_ms)}.";
                    }

                    spoken.Add(TextBank.Capitalise(line));
                    notes.Add($"{NightSlots.Short(s)}: saw {_victim.ShortName} in the {Room(myRoom)}");
                    facts.Add(new LocationFact(_victim.Id, s, myRoom));
                }

                // Glimpses of people in neighbouring rooms.
                foreach (Character other in _suspects.Where(x => x != c))
                {
                    string theirRoom = _night[other.Id][s];
                    if (theirRoom == myRoom || theirRoom == _sceneRoom.Id || IsLieSlot(other, s) && other == _murderer)
                    {
                        continue;
                    }

                    bool forced = _forcedGlimpses.Contains((c.Id, other.Id, s));
                    if (!forced && !(_manor.AreAdjacent(myRoom, theirRoom) && _rng.Chance(0.3)))
                    {
                        continue;
                    }

                    spoken.Add(_rng.Pick(new[]
                    {
                        $"{TextBank.SlotLead(s, _rng)} I saw {other.ShortName} through the doorway, in the {Room(theirRoom)}.",
                        $"I passed the {Room(theirRoom)} at about {NightSlots.Spoken(s)}, and {other.ShortName} was in there.",
                    }));
                    notes.Add($"{NightSlots.Short(s)}: saw {other.ShortName} in the {Room(theirRoom)}");
                    facts.Add(new LocationFact(other.Id, s, theirRoom));
                    if (s == _ms)
                    {
                        alibiFor.Add(other.Id);
                    }
                }
            }

            // The partial sighting of the killer.
            bool genderLine = c == _genderWitness;
            if (genderLine)
            {
                bool adjacent = _manor.AreAdjacent(_night[c.Id][_ms], _sceneRoom.Id);
                string figure = _murderer.IsMale ? "a gentleman, or at least a man" : "a woman; I heard her skirts";
                spoken.Add(adjacent
                    ? $"And around {NightSlots.Spoken(_ms)} someone went into the {Room(_sceneRoom.Id)}. I only caught the shape: {figure}. I'd swear to that much."
                    : $"Around {NightSlots.Spoken(_ms)} I heard someone on the way to the {Room(_sceneRoom.Id)}: {(_murderer.IsMale ? "a man's heavy tread" : "the swish of a woman's skirts")}.");
                notes.Add($"{NightSlots.Short(_ms)}: a {(_murderer.IsMale ? "man" : "woman")} heading into the {Room(_sceneRoom.Id)}");
                facts.Add(new GenderSightingFact(_murderer.Gender, _ms, _sceneRoom.Id));
            }

            if (spoken.Count == 0)
            {
                return;
            }

            Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: what they saw", string.Join("  ·  ", notes));
            clue.Spoken = string.Join(" ", spoken);
            clue.SpeakerId = c.Id;
            clue.Topic = Topic.Sightings;
            clue.Facts.AddRange(facts);
            clue.AlibiFor.AddRange(alibiFor.Distinct());
            if (genderLine)
            {
                _keyEvidence.Add(clue.Id);
            }
        }

        private void BuildSelf(Character c)
        {
            var spoken = new List<string>
            {
                c.Station == Station.Servant
                    ? $"I've been {c.RoleLabel} here these {_rng.Next(2, 25)} years."
                    : $"I'm {c.RoleLabel}, as you know.",
            };
            var notes = new List<string>();
            bool truthful = true;

            Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: on {c.Him}self", string.Empty);
            foreach (TraitCategory cat in _selfTraits[c.Id])
            {
                string value = c.Traits.ValueOf(cat);
                if (c == _murderer && cat == _murdererLieCategory)
                {
                    value = LieValue(cat);
                    truthful = false;
                }

                spoken.Add(TextBank.TraitSelf(cat, value, _rng));
                notes.Add(TraitRules.ValueLabel(cat, value));
                clue.Facts.Add(new TraitFact(c.Id, cat, value));
            }

            clue.Text = $"Says: {string.Join("; ", notes)}.";
            clue.Spoken = string.Join(" ", spoken);
            clue.SpeakerId = c.Id;
            clue.Topic = Topic.Self;
            clue.IsTruthful = truthful;
        }

        private void BuildVictimTopic(Character c)
        {
            string vref = TextBank.VictimRef(c, _victim);
            if (c != _murderer && c.Motive != MotiveKind.None && _rng.Chance(0.5))
            {
                Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: on {_victim.ShortName}", $"Admits: {TextBank.MotiveNote(c.Motive)}.");
                clue.Spoken = TextBank.MotiveAdmit(c.Motive, vref, _rng);
                clue.SpeakerId = c.Id;
                clue.Topic = Topic.Victim;
                clue.Facts.Add(new MotiveFact(c.Id, c.Motive));
            }
            else
            {
                Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: on {_victim.ShortName}", "Denies any quarrel with the victim.");
                clue.Spoken = c.Motive == MotiveKind.None && c != _murderer
                    ? TextBank.MotiveAdmit(MotiveKind.None, vref, _rng)
                    : TextBank.MotiveDeny(vref, _rng);
                clue.SpeakerId = c.Id;
                clue.Topic = Topic.Victim;

                // Anyone denying a motive they actually have is hiding something.
                clue.IsTruthful = c.Motive == MotiveKind.None;
            }
        }

        private void BuildWhereabouts(Character c)
        {
            bool wellInformed = c.Role is RoleKind.Butler or RoleKind.Housekeeper;
            int count = wellInformed ? 4 : _rng.Next(1, 3);
            var spoken = new List<string>();
            var notes = new List<string>();
            var facts = new List<Fact>();

            foreach (Character target in _rng.Shuffled(_suspects.Where(x => x != c)).Take(count))
            {
                string[] hours = _day[target.Id];
                int start = _rng.Next(0, GameRules.DayHours);
                int end = start;
                while (end + 1 < GameRules.DayHours && hours[end + 1] == hours[start])
                {
                    end++;
                }

                int from = GameRules.DayStartHour + start;
                int to = GameRules.DayStartHour + end + 1;
                string where = hours[start] == RoomIds.Away ? "away in the village" : $"in the {Room(hours[start])}";
                spoken.Add($"{target.ShortName} will be {where} from {Clock.HourWords(from)} until {Clock.HourWords(to)}.");
                notes.Add($"{target.ShortName}: {Room(hours[start])}, {Clock.Hour(from)}–{Clock.Hour(to)}");
                facts.Add(new ScheduleFact(target.Id, from, to, hours[start]));
            }

            if (facts.Count == 0)
            {
                return;
            }

            Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: today's comings and goings", string.Join("  ·  ", notes));
            clue.Spoken = string.Join(" ", spoken);
            clue.SpeakerId = c.Id;
            clue.Topic = Topic.Whereabouts;
            clue.Facts.AddRange(facts);
        }

        private void BuildGossip()
        {
            foreach (KeyValuePair<(string Speaker, string Target), GossipPlan> entry in _gossip)
            {
                (string speakerId, string targetId) = entry.Key;
                GossipPlan plan = entry.Value;
                Character speaker = _suspects.First(x => x.Id == speakerId);
                Character target = _suspects.First(x => x.Id == targetId);
                var spoken = new List<string>();
                var notes = new List<string>();
                Clue clue = AddClue(ClueKind.Statement, $"{speaker.ShortName} on {target.ShortName}", string.Empty);

                foreach ((TraitCategory cat, string value, bool truthful) in plan.Traits.DistinctBy(t => t.Category))
                {
                    spoken.Add(TextBank.TraitOther(cat, value, target, _rng));
                    notes.Add(TraitRules.ValueLabel(cat, value));
                    clue.Facts.Add(new TraitFact(target.Id, cat, value));
                    if (!truthful)
                    {
                        clue.IsTruthful = false;
                    }
                }

                if (plan.Motive && target.Motive != MotiveKind.None)
                {
                    spoken.Add(TextBank.MotiveGossip(target.Motive, target, TextBank.VictimRef(speaker, _victim)));
                    notes.Add(TextBank.MotiveNote(target.Motive));
                    clue.Facts.Add(new MotiveFact(target.Id, target.Motive));
                }

                if (spoken.Count == 0)
                {
                    _clues.Remove(clue);
                    continue;
                }

                clue.Text = $"Says {target.ShortName}: {string.Join("; ", notes)}.";
                clue.Spoken = string.Join(" ", spoken);
                clue.SpeakerId = speaker.Id;
                clue.Topic = Topic.AboutPerson;
                clue.TargetId = target.Id;
            }
        }

        // ================================================================== documents

        private void BuildDocuments()
        {
            // Trait evidence among people's belongings.
            foreach ((Character who, TraitCategory cat) in _traitDocs.Distinct())
            {
                var (title, text) = TextBank.TraitDocument(cat, who.Traits.ValueOf(cat), who);
                Clue doc = AddClue(ClueKind.Document, title, text);
                doc.RoomId = who.PersonalRoomId;
                doc.Facts.Add(new TraitFact(who.Id, cat, who.Traits.ValueOf(cat)));
                if (who == _murderer)
                {
                    _keyEvidence.Add(doc.Id);
                }
            }

            // Impartial alibis from the bell register.
            foreach (Character t in _bellRegisterAlibis)
            {
                string room = _night[t.Id][_ms];
                string time = $"{NightSlots.Hour24(_ms) - 12}.{_rng.Next(5, 50):00}";
                Clue doc = AddClue(ClueKind.Document, "The bell register",
                    $"The servants' bell register for last night: \"{time}: {Room(room)} rang. Answered. {t.ShortName} wanted the fire made up.\"");
                doc.RoomId = RoomIds.ServantsHall;
                doc.Facts.Add(new LocationFact(t.Id, _ms, room));
                doc.AlibiFor.Add(t.Id);
            }

            // Physical proof the murderer's claimed room was empty.
            if (_emptyRoomMode)
            {
                Clue doc = AddClue(ClueKind.Observation, $"The cold {Room(_claimRoom).ToLowerInvariant()}", TextBank.EmptyRoomText(Room(_claimRoom)));
                doc.RoomId = _claimRoom;
                for (int s = 0; s < NightSlots.Count; s++)
                {
                    doc.Facts.Add(new OccupancyFact(_claimRoom, s, Array.Empty<string>()));
                }

                _keyEvidence.Add(doc.Id);
            }

            // Motives on paper.
            foreach (Character c in _suspects.Where(x => x.Motive != MotiveKind.None))
            {
                if (c != _murderer && !_rng.Chance(0.5))
                {
                    continue;
                }

                var (title, text) = TextBank.MotiveDocument(c.Motive, c, _victim, _rng);
                Clue doc = AddClue(ClueKind.Document, title, text);
                doc.RoomId = c.Motive switch
                {
                    MotiveKind.Jealousy or MotiveKind.Revenge => c.PersonalRoomId,
                    MotiveKind.Dismissal => _rng.Chance(0.5) ? RoomIds.Study : RoomIds.Pantry,
                    _ => _rng.Chance(0.7) ? RoomIds.Study : RoomIds.Master,
                };
                doc.Facts.Add(new MotiveFact(c.Id, c.Motive));
                if (c == _murderer)
                {
                    _keyEvidence.Add(doc.Id);
                }
            }

            // The secret liar's note: the red herring explained.
            if (_liar is not null)
            {
                string actual = _night[_liar.Id][_liarSlot];
                Clue doc = AddClue(ClueKind.Document, "A secret note",
                    $"Tucked inside a prayer book: \"The {Room(actual)}, at {NightSlots.Spoken(_liarSlot)}. Burn this. — X.\" It belongs to {_liar.ShortName}.");
                doc.RoomId = _liar.PersonalRoomId;
                doc.Facts.Add(new LocationFact(_liar.Id, _liarSlot, actual));
            }
        }

        // ================================================================== solution bookkeeping

        private Dictionary<string, Elimination> BuildEliminations()
        {
            var result = new Dictionary<string, Elimination>();
            var sceneClueIds = _clues
                .Where(c => c.Facts.OfType<SceneFact>().Any())
                .ToDictionary(c => c.Facts.OfType<SceneFact>().First().Category, c => c.Id);

            foreach (Character i in Innocents)
            {
                var ids = new List<string>();
                var reasons = new List<string>();

                var alibiClues = _clues.Where(c => c.IsTruthful && c.AlibiFor.Contains(i.Id)).ToList();
                if (alibiClues.Count > 0)
                {
                    ids.AddRange(alibiClues.Select(c => c.Id));
                    reasons.Add($"was in the {Room(_night[i.Id][_ms])} at {NightSlots.Spoken(_ms)}, as others confirm");
                }

                foreach (TraitCategory cat in _cats)
                {
                    string value = i.Traits.ValueOf(cat);
                    if (!TraitRules.Excludes(cat, _scene[cat], value))
                    {
                        continue;
                    }

                    var sources = _clues.Where(c => c.IsTruthful && c.Facts.OfType<TraitFact>()
                        .Any(f => f.CharacterId == i.Id && f.Category == cat && f.Value == value)).ToList();
                    if (sources.Count == 0)
                    {
                        continue;
                    }

                    ids.AddRange(sources.Select(c => c.Id));
                    ids.Add(sceneClueIds[cat]);
                    reasons.Add($"is {TraitRules.ValueLabel(cat, value).ToLowerInvariant()}, but the scene shows {TraitRules.SceneLabel(cat, _scene[cat]).ToLowerInvariant()}");
                }

                string reason = reasons.Count > 0
                    ? $"{i.ShortName} {string.Join("; and ", reasons)}."
                    : $"{i.ShortName} could only be cleared by elimination of the others.";
                result[i.Id] = new Elimination(reason, ids.Distinct().ToList());
            }

            return result;
        }

        private void FlagKeyClues(Dictionary<string, Elimination> eliminations)
        {
            var key = new HashSet<string>(eliminations.Values.SelectMany(e => e.ClueIds));
            key.UnionWith(_keyEvidence);
            foreach (Clue c in _clues.Where(c => key.Contains(c.Id)))
            {
                c.IsKey = true;
            }
        }

        /// <summary>A newspaper-ready name for the case, e.g. "The Case of the Crooked Candlestick".</summary>
        private string BuildTitle()
        {
            string[] adjectives =
            {
                "Cold", "Crooked", "Silent", "Missing", "Midnight", "Broken", "Shuttered", "Weeping",
                "Velvet", "Restless", "Borrowed", "Crimson", "Unlit", "Ninth", "Hollow", "Second",
            };
            string[] objects =
            {
                "Clock", "Key", "Portrait", "Letter", "Lamp", "Staircase", "Glove", "Bell", "Mirror", "Teacup", "Candle", "Locket",
            };

            string house = _manor.Name.Split(' ')[0];
            string weaponNoun = _weapon.Name.Split(' ')[^1];
            string adjective = _rng.Pick(adjectives);
            string noun = _rng.Chance(0.4) ? TextBank.Capitalise(weaponNoun) : _rng.Pick(objects);

            return _rng.Next(7) switch
            {
                0 or 1 => $"The Case of the {adjective} {noun}",
                2 => $"The {house} Affair",
                3 => $"Murder at {_manor.Name}",
                4 => $"The {adjective} {noun} of {house}",
                5 => $"Death in the {Room(_sceneRoom.Id)}",
                _ => $"The {_victim.Surname} Mystery",
            };
        }

        private string BuildBriefing()
        {
            Character? finder = _suspects.FirstOrDefault(c => c.Role == RoleKind.Maid)
                ?? _suspects.FirstOrDefault(c => c.Station == Station.Servant);
            string finderText = finder is not null ? $"{finder.ShortName}, {finder.RoleLabel}," : "a housemaid";
            string tod = _s.RevealTimeOfDeath
                ? $" The police surgeon puts the time of death at around {NightSlots.Spoken(_ms)}."
                : string.Empty;

            return $"At a quarter past six this morning, {finderText} found {_victim.FullName} dead in the {Room(_sceneRoom.Id)} of {_manor.Name}.{tod} " +
                   $"Nobody has left the house since: the roads are flooded and the household is confined. " +
                   $"The Chief Constable expects a name by {Clock.Hour(_s.EndHour)}. Interview the household, search the rooms, " +
                   $"and find out who is lying.";
        }

        private string BuildTruth()
        {
            string disproof = _emptyRoomMode
                ? $"but the {Room(_claimRoom)} was never used that night: its grate was cold and its lamps untouched."
                : $"but {_loneWitness!.ShortName} sat alone in the {Room(_claimRoom)} at that hour, and nobody came in.";

            return $"{_murderer.FullName}, {_murderer.RoleLabel}, killed {_victim.ShortName} in the {Room(_sceneRoom.Id)} at around {NightSlots.Spoken(_ms)} " +
                   $"with the {_weapon.Name.ToLowerInvariant()}. {TextBank.MotiveDetail(_murderer.Motive, _murderer, _victim)} " +
                   $"{_murderer.ShortName} claimed to have been in the {Room(_claimRoom)} at the time, {disproof}";
        }
    }
}
