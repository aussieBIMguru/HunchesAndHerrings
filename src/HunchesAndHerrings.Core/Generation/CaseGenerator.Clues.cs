using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

public static partial class CaseGenerator
{
    private sealed partial class Builder
    {
        /// <summary>Indicators that come from what a servant heard or glimpsed, rather than marks in the room.</summary>
        private static readonly TraitCategory[] HeardCategories =
        {
            TraitCategory.Accent, TraitCategory.GoldTooth, TraitCategory.Tattoo, TraitCategory.Stammer, TraitCategory.Pianist,
        };

        /// <summary>Motives that only come out through secrets, documents or a confrontation, never idle gossip.</summary>
        private static readonly MotiveKind[] HiddenMotives =
        {
            MotiveKind.Silence, MotiveKind.Recognition, MotiveKind.Negligence, MotiveKind.Imposture, MotiveKind.Accident,
        };

        private string? _handLieValue;

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
            var allCategories = TraitCatalog.All.Select(d => d.Category).ToList();
            foreach (Character c in _suspects)
            {
                _selfTraits[c.Id] = new List<TraitCategory>();
            }

            // Sources that clear the trait-excluded innocents.
            foreach (Character o in _others)
            {
                var failing = _cats.Where(cat => TraitRules.Excludes(cat, _scene[cat], o.Traits.ValueOf(cat))).ToList();
                TraitCategory cat = _rng.Pick(failing);
                foreach (int kind in _rng.Shuffled(new[] { 0, 1, 2 }).Take(Math.Max(1, _s.TraitSourceCount)))
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

            // Everyone mentions a thing or two about themselves, most of it beside the point.
            foreach (Character c in _suspects)
            {
                int count = _rng.Next(1, 3);
                for (int i = 0; i < count; i++)
                {
                    AddUnique(_selfTraits[c.Id], _rng.Chance(0.35) ? _rng.Pick(_cats) : _rng.Pick(allCategories));
                }
            }

            // The hand lies about one scene trait when asked about themselves...
            var lieable = _cats.Where(cat => !TraitCatalog.Get(cat).Visible).ToList();
            if (lieable.Count > 0)
            {
                TraitCategory lie = _rng.Pick(lieable);
                _handLieCategory = lie;
                _handLieValue = TraitCatalog.Get(lie).ClearingValue(_scene[lie], _rng);
                AddUnique(_selfTraits[_hand.Id], lie);

                // ...and sometimes the truth is out there.
                if (_rng.Chance(_s.MurdererExposure))
                {
                    TraitCategory exposed = _rng.Chance(0.7) ? lie : _rng.Pick(_cats);
                    var gossips = innocents.Where(x => x != _hand).ToList();
                    if (_rng.Chance(0.5) && gossips.Count > 0)
                    {
                        Gossip(_rng.Pick(gossips), _hand).Traits.Add((exposed, _hand.Traits.ValueOf(exposed), true));
                    }
                    else
                    {
                        _traitDocs.Add((_hand, exposed));
                    }
                }
            }

            // A culprit tries to frame someone who is actually ruled out.
            if (_s.FrameLies && _culprits.Count > 0 && _others.Count > 0 && _rng.Chance(0.6))
            {
                Character target = _rng.Pick(_others);
                var failing = _cats.Where(cat => TraitRules.Excludes(cat, _scene[cat], target.Traits.ValueOf(cat))).ToList();
                TraitCategory cat = _rng.Pick(failing);
                Gossip(_rng.Pick(_culprits), target).Traits.Add((cat, TraitCatalog.Get(cat).FittingValue(_scene[cat]), false));
            }

            // Somebody knows each culprit's motive, unless it's the kind of thing only a secret reveals.
            if (!HiddenMotives.Contains(_motive))
            {
                foreach (Character culprit in _culprits)
                {
                    var speakers = innocents.Where(x => x != culprit).ToList();
                    if (speakers.Count > 0)
                    {
                        Gossip(_rng.Pick(speakers), culprit).Motive = true;
                    }
                }
            }

            // General chatter: each innocent knows something about one or two others.
            foreach (Character speaker in innocents)
            {
                int count = _rng.Next(1, 3);
                foreach (Character target in _rng.Shuffled(_suspects.Where(x => x != speaker)).Take(count))
                {
                    if (target.Motive != MotiveKind.None && !HiddenMotives.Contains(target.Motive) && _rng.Chance(0.4))
                    {
                        Gossip(speaker, target).Motive = true;
                    }
                    else
                    {
                        TraitCategory cat = _rng.Chance(0.5) ? _rng.Pick(_cats) : _rng.Pick(allCategories);
                        Gossip(speaker, target).Traits.Add((cat, target.Traits.ValueOf(cat), true));
                    }
                }
            }

            // A partial sighting of the hand's figure.
            if (_rng.Chance(_s.GenderSightingChance))
            {
                var near = innocents.Where(x => x != _hand && _manor.Contains(_night[x.Id][_cs]) && _manor.AreAdjacent(_night[x.Id][_cs], _sceneRoom.Id)).ToList();
                _genderWitness = near.Count > 0 ? _rng.Pick(near) : _rng.PickOrDefault(innocents.Where(x => x != _hand && !x.IsVisitor).ToList());
            }
        }

        // ================================================================== the scene

        private void BuildSceneClues()
        {
            string scene = _sceneRoom.Id;
            bool poison = _weapon.Poison;
            int minute = _rng.Next(4, 52);
            string watch = $"{NightSlots.Hour24(_ms) - 12}:{minute:00}";

            string bodyText;
            if (_model == CaseModel.Accident || poison)
            {
                string pose = _sceneRoom.Id == RoomIds.Master
                    ? "in bed, the covers thrown back"
                    : _rng.Pick(new[] { "slumped in a chair", "sprawled on the floor", "crumpled at the foot of the steps", "face down on the rug" });
                bodyText = $"{_victim.ShortName} lies {pose}. The body is cold, and the police surgeon puts death at around {NightSlots.Spoken(_ms)}.";
            }
            else
            {
                string pose = _rng.Pick(new[] { "face down by the hearth", "slumped across the floor", "sprawled beside an overturned chair", "crumpled against the wall" });
                bodyText = $"{_victim.ShortName} lies {pose}. {_victim.HisCap} pocket watch, smashed in the fall, stopped at {watch}. Death came at around {NightSlots.Spoken(_ms)}.";
            }

            Clue body = AddClue(ClueKind.Scene, "The body", bodyText);
            body.RoomId = scene;
            body.Facts.Add(new TimeOfDeathFact(_ms));
            body.IsKey = true;

            if (_model == CaseModel.Accident)
            {
                Clue staged = AddClue(ClueKind.Scene, $"A {_weapon.Name.ToLowerInvariant()} by the body",
                    $"A {_weapon.Name.ToLowerInvariant()} has been laid by the body, as if it were the weapon. Yet there's no blood or hair on it at all, and it lies on top of the spilled things, not under them: it was put there afterwards.");
                staged.RoomId = scene;
            }
            else
            {
                Clue weapon = AddClue(ClueKind.Scene, $"The weapon: {_weapon.Name.ToLowerInvariant()}", _weapon.SceneText);
                weapon.RoomId = scene;
                weapon.Facts.Add(new WeaponFact(_weapon.Id));
            }

            // The indicators, at the scene, on the way to or from it, or in what the servants heard.
            var nearby = _manor.Neighbours(scene)
                .Select(n => n.Room)
                .Where(r => !r.IsPassage && r.Id != _claimRoom && !_reserved.Contains(r.Id))
                .ToList();
            foreach (TraitCategory cat in _cats)
            {
                string label = TraitRules.SceneLabel(cat, _scene[cat]);
                string text = TextBank.SceneText(cat, _scene[cat], scene);
                Clue c;
                if (HeardCategories.Contains(cat))
                {
                    c = AddClue(ClueKind.Observation, $"Below stairs: {label.ToLowerInvariant()}", text);
                    c.RoomId = RoomIds.ServantsHall;
                }
                else if (nearby.Count > 0 && _rng.Chance(0.3))
                {
                    Room near = _rng.Pick(nearby);
                    c = AddClue(ClueKind.Observation, $"{label}, in the {near.Name}", $"Between here and the {Room(scene)}: {text}");
                    c.RoomId = near.Id;
                }
                else
                {
                    c = AddClue(ClueKind.Scene, label, text);
                    c.RoomId = scene;
                }

                c.Facts.Add(new SceneFact(cat, _scene[cat]));
                c.IsKey = true;
            }

            bool lure = _model is CaseModel.LoneKiller or CaseModel.Conspiracy or CaseModel.Affair or CaseModel.HiddenSecrets or CaseModel.Impostor or CaseModel.SecretHeir;
            if (lure && !_cats.Contains(TraitCategory.Literacy) && _rng.Chance(0.5))
            {
                Clue note = AddClue(ClueKind.Scene, "A note in the victim's pocket",
                    $"Folded in {_victim.ShortName}'s pocket: \"Tonight, {NightSlots.Spoken(_ms)}, the {Room(scene)}. We will settle this. — {_hand.FirstName[0]}.\"");
                note.RoomId = scene;
            }

            if (_weapon.OriginRoomId != scene && _manor.Contains(_weapon.OriginRoomId))
            {
                Clue missing = AddClue(ClueKind.Observation, $"Missing: {_weapon.Name.ToLowerInvariant()}", _weapon.MissingText);
                missing.RoomId = _weapon.OriginRoomId;
                if (_weapon.OriginRoomId == _hand.PersonalRoomId)
                {
                    _keyEvidence.Add(missing.Id);
                }
            }
        }

        // ================================================================== statements

        private string ClaimedRoom(Character c, int slot)
        {
            if (slot == _cs && SlotLiars.Contains(c))
            {
                return _claimRoom;
            }

            return SecretLie(c, slot) is not null ? c.PersonalRoomId : _night[c.Id][slot];
        }

        private bool IsLieSlot(Character c, int slot) =>
            (slot == _cs && SlotLiars.Contains(c)) || SecretLie(c, slot) is not null;

        private void BuildLastNight(Character c)
        {
            var spoken = new List<string>();
            var notes = new List<string>();
            var facts = new List<Fact>();
            var alibiFor = new List<string>();
            bool lied = false;

            for (int s = 0; s < NightSlots.Count; s++)
            {
                string room = ClaimedRoom(c, s);
                bool lie = IsLieSlot(c, s);
                lied |= lie;

                // Liars at the crime slot vouch for each other; everyone else names who was truly there.
                var companions = lie
                    ? (s == _cs && _culprits.Contains(c) ? _culprits.Where(x => x != c).ToList() : new List<Character>())
                    : PeopleAt(room, s, c);
                bool exclusive = c == _loneWitness && s == _cs;
                var names = companions.Select(p => p.ShortName).ToList();

                facts.Add(new LocationFact(c.Id, s, room));
                foreach (Character p in companions)
                {
                    facts.Add(new LocationFact(p.Id, s, room));
                    if (s == _ms && !lie && !p.IsVictim)
                    {
                        alibiFor.Add(p.Id);
                    }
                }

                if (room == RoomIds.Away)
                {
                    spoken.Add(s == 0
                        ? $"{TextBank.SlotLead(s, _rng)} I wasn't at the house at all; I came up later."
                        : $"{TextBank.SlotLead(s, _rng)} I'd left the house and was {TextBank.Activity(RoomIds.Away, _rng)}.");
                    notes.Add($"{NightSlots.Short(s)}: away from the house");
                    continue;
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
            clue.IsTruthful = !lied;

            if (SlotLiars.Contains(c) || c == _loneWitness)
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
            bool appointment = _model is not CaseModel.Accident and not CaseModel.MistakenIdentity and not CaseModel.DoctorNegligence;

            for (int s = 0; s < NightSlots.Count; s++)
            {
                string myRoom = _night[c.Id][s];
                if (IsLieSlot(c, s) || myRoom == RoomIds.Away)
                {
                    continue;
                }

                // The victim's movements, if they shared a room before the death.
                if (s < _ms && _night[_victim.Id][s] == myRoom)
                {
                    string vref = TextBank.VictimRef(c, _victim);
                    string line = $"{TextBank.SlotLead(s, _rng)} {vref} was with us in the {Room(myRoom)}.";
                    if (appointment && s == _ms - 1 && _rng.Chance(0.7))
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
                    if (theirRoom == myRoom || theirRoom == _sceneRoom.Id || theirRoom == RoomIds.Away)
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

            // The partial sighting of whoever went into the scene.
            bool genderLine = c == _genderWitness;
            if (genderLine)
            {
                bool adjacent = _manor.Contains(_night[c.Id][_cs]) && _manor.AreAdjacent(_night[c.Id][_cs], _sceneRoom.Id);
                string figure = _hand.IsMale ? "a gentleman, or at least a man" : "a woman; I heard her skirts";
                spoken.Add(adjacent
                    ? $"And around {NightSlots.Spoken(_cs)} someone went into the {Room(_sceneRoom.Id)}. I only caught the shape: {figure}. I'd swear to that much."
                    : $"Around {NightSlots.Spoken(_cs)} I heard someone on the way to the {Room(_sceneRoom.Id)}: {(_hand.IsMale ? "a man's heavy tread" : "the swish of a woman's skirts")}.");
                notes.Add($"{NightSlots.Short(_cs)}: a {(_hand.IsMale ? "man" : "woman")} heading into the {Room(_sceneRoom.Id)}");
                facts.Add(new GenderSightingFact(_hand.Gender, _cs, _sceneRoom.Id));
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
                c.IsVisitor
                    ? $"I'm {c.RoleLabel}. I don't live here, you understand."
                    : c.Station == Station.Servant
                        ? $"I've been {c.RoleLabel} here these {_rng.Next(2, 25)} years."
                        : $"I'm {c.RoleLabel}, as you know.",
            };
            var notes = new List<string>();
            bool truthful = true;

            Clue clue = AddClue(ClueKind.Statement, $"{c.ShortName}: on {c.Him}self", string.Empty);
            foreach (TraitCategory cat in _selfTraits[c.Id])
            {
                string value = c.Traits.ValueOf(cat);
                if (c == _hand && cat == _handLieCategory && _handLieValue is not null)
                {
                    value = _handLieValue;
                    truthful = false;
                }

                spoken.Add(TextBank.TraitSelf(cat, value));
                notes.Add(TraitRules.ValueLabel(cat, value));
                clue.Facts.Add(new TraitFact(c.Id, cat, value));
            }

            clue.Text = notes.Count > 0 ? $"Says: {string.Join("; ", notes)}." : "Says little about themselves.";
            clue.Spoken = string.Join(" ", spoken);
            clue.SpeakerId = c.Id;
            clue.Topic = Topic.Self;
            clue.IsTruthful = truthful;
        }

        private void BuildVictimTopic(Character c)
        {
            string vref = TextBank.VictimRef(c, _victim);
            bool hidden = HiddenMotives.Contains(c.Motive);
            if (!IsCulprit(c) && c.Motive != MotiveKind.None && !hidden && _rng.Chance(0.5))
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
                clue.Spoken = c.Motive == MotiveKind.None && !IsCulprit(c)
                    ? TextBank.MotiveAdmit(MotiveKind.None, vref, _rng)
                    : TextBank.MotiveDeny(vref, _rng);
                clue.SpeakerId = c.Id;
                clue.Topic = Topic.Victim;

                // Anyone denying a motive they actually have is hiding something.
                clue.IsTruthful = c.Motive == MotiveKind.None && !IsCulprit(c);
            }
        }

        private void BuildWhereabouts(Character c)
        {
            bool wellInformed = c.Role is RoleKind.Butler or RoleKind.Housekeeper or RoleKind.Constable;
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
                string where = hours[start] == RoomIds.Away
                    ? (target.IsVisitor ? "away from the house" : "away in the village")
                    : $"in the {Room(hours[start])}";
                spoken.Add($"{target.ShortName} will be {where} from {Clock.HourWords(from)} until {Clock.HourWords(to)}.");
                notes.Add($"{target.ShortName}: {(hours[start] == RoomIds.Away ? "away" : Room(hours[start]))}, {Clock.Hour(from)}–{Clock.Hour(to)}");
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
                    spoken.Add(TextBank.TraitOther(cat, value, target));
                    notes.Add(TraitRules.ValueLabel(cat, value));
                    clue.Facts.Add(new TraitFact(target.Id, cat, value));
                    if (!truthful)
                    {
                        clue.IsTruthful = false;
                    }
                }

                if (plan.Motive && target.Motive != MotiveKind.None)
                {
                    string aboutWhom = target == _hand && _intended is not null ? _intended.ShortName : TextBank.VictimRef(speaker, _victim);
                    spoken.Add(TextBank.MotiveGossip(target.Motive, target, aboutWhom));
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
                if (who == _hand)
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

            // Physical proof the claimed room was empty.
            if (_disproof == Disproof.EmptyRoom)
            {
                Clue doc = AddClue(ClueKind.Observation, $"The cold {Room(_claimRoom).ToLowerInvariant()}", TextBank.EmptyRoomText(Room(_claimRoom)));
                doc.RoomId = _claimRoom;
                for (int s = 0; s < NightSlots.Count; s++)
                {
                    doc.Facts.Add(new OccupancyFact(_claimRoom, s, Array.Empty<string>()));
                }

                _keyEvidence.Add(doc.Id);
            }

            // Comings and goings of callers last night.
            var visitors = _suspects.Where(c => c.IsVisitor).ToList();
            if (visitors.Count > 0)
            {
                var lines = new List<string>();
                Clue book = AddClue(ClueKind.Document, "The visitors' book", string.Empty);
                book.RoomId = RoomIds.Hall;
                foreach (Character v in visitors)
                {
                    var present = Enumerable.Range(0, NightSlots.Count).Where(s => _night[v.Id][s] != RoomIds.Away).ToList();
                    if (present.Count == 0)
                    {
                        lines.Add($"{v.ShortName}: did not call.");
                    }
                    else
                    {
                        string arrive = $"{NightSlots.Hour24(present[0]) - 13}.{_rng.Next(35, 55)}";
                        string leave = $"{NightSlots.Hour24(present[^1]) - 12}.{_rng.Next(20, 50)}";
                        lines.Add($"{v.ShortName}: in at {arrive}, out at {leave}.");
                    }

                    for (int s = 0; s < NightSlots.Count; s++)
                    {
                        book.Facts.Add(new PresenceFact(v.Id, s, _night[v.Id][s] != RoomIds.Away));
                    }

                    if (_night[v.Id][_ms] == RoomIds.Away)
                    {
                        book.AlibiFor.Add(v.Id);
                    }
                }

                book.Text = $"The visitors' book on the hall table, kept in the butler's hand. Last night: {string.Join(" ", lines)}";
                if (_disproof == Disproof.VisitorsBook)
                {
                    _keyEvidence.Add(book.Id);
                }
            }

            // Motives on paper.
            bool storyCovers = _model is CaseModel.Affair or CaseModel.HiddenSecrets or CaseModel.SecretHeir or CaseModel.FamilyPact or CaseModel.Accident;
            foreach (Character c in _suspects.Where(x => x.Motive != MotiveKind.None))
            {
                bool culprit = IsCulprit(c);
                if (culprit ? storyCovers : (HiddenMotives.Contains(c.Motive) || !_rng.Chance(0.5)))
                {
                    continue;
                }

                Character about = c == _hand && _intended is not null ? _intended : _victim;
                var (title, text) = TextBank.MotiveDocument(c.Motive, c, about, _rng);
                Clue doc = AddClue(ClueKind.Document, title, text);
                doc.RoomId = c.Motive switch
                {
                    MotiveKind.Jealousy or MotiveKind.Revenge or MotiveKind.Affair => c.PersonalRoomId,
                    MotiveKind.Dismissal or MotiveKind.Theft => _rng.Chance(0.5) ? RoomIds.Study : RoomIds.Pantry,
                    MotiveKind.Negligence => _sceneRoom.Id,
                    _ => _rng.Chance(0.7) ? RoomIds.Study : RoomIds.Master,
                };
                doc.Facts.Add(new MotiveFact(c.Id, c.Motive));
                if (culprit)
                {
                    _keyEvidence.Add(doc.Id);
                }
            }
        }

        // ================================================================== secrets

        private void BuildSecretClues()
        {
            foreach (Secret secret in _secrets)
            {
                Character who = secret.Who;
                Clue doc = AddClue(ClueKind.Document, secret.DocTitle, secret.DocText);
                doc.RoomId = secret.DocRoomId ?? who.PersonalRoomId;
                doc.SecretOf = who.Id;
                if (secret.Slot is { } slot && secret.ActualRoom is { } room)
                {
                    doc.Facts.Add(new LocationFact(who.Id, slot, room));
                    if (secret.Partner is not null)
                    {
                        doc.Facts.Add(new LocationFact(secret.Partner.Id, slot, room));
                    }
                }

                if (secret.Motive is { } m)
                {
                    doc.Facts.Add(new MotiveFact(who.Id, m));
                }

                if (secret.IsKey)
                {
                    _keyEvidence.Add(doc.Id);
                }

                // Confronted with it, they come clean about the secret, if not the crime.
                Clue confession = AddClue(ClueKind.Statement, $"{who.ShortName}: confronted", $"Admits: {secret.Label}.");
                confession.Spoken = secret.Confession;
                confession.SpeakerId = who.Id;
                confession.Topic = Topic.Confront;
                if (secret.Slot is { } s2 && secret.ActualRoom is { } r2)
                {
                    confession.Facts.Add(new LocationFact(who.Id, s2, r2));
                }

                if (_model == CaseModel.Accident && who == _hand)
                {
                    confession.Facts.Add(new LocationFact(who.Id, _cs, _sceneRoom.Id));
                    _keyEvidence.Add(confession.Id);
                }

                if (secret.Motive is { } m2)
                {
                    confession.Facts.Add(new MotiveFact(who.Id, m2));
                    _keyEvidence.Add(confession.Id);
                }
            }
        }

        // ================================================================== red herring at the scene

        /// <summary>An extra mark at the scene that has nothing to do with the crime, and somebody who can explain it.</summary>
        private void BuildHerring()
        {
            double chance = _s.Difficulty switch { Difficulty.Novice => 0, Difficulty.Inspector => 0.5, _ => 0.85 };
            if (!_s.FrameLies || !_rng.Chance(chance))
            {
                return;
            }

            bool poison = _weapon.Poison;
            var pool = TraitCatalog.All
                .Where(d => !_cats.Contains(d.Category) && d.Rule == TraitRule.Match && !d.Visible && d.SceneValues.Count > 0)
                .Where(d => d.Fit == TraitFit.Any || (d.Fit == TraitFit.Poison) == poison)
                .Where(d => !HeardCategories.Contains(d.Category) && d.Category is not TraitCategory.Dog and not TraitCategory.Literacy and not TraitCategory.Keys)
                .ToList();
            if (pool.Count == 0)
            {
                return;
            }

            TraitDef def = _rng.Pick(pool);
            bool byFinder = _finder is not null && !IsCulprit(_finder) && _finder != _hand && _rng.Chance(0.6);
            Character owner = byFinder ? _finder! : _victim;
            string value = def.Value(owner.Traits.ValueOf(def.Category)).CanBeScene ? owner.Traits.ValueOf(def.Category) : _rng.Pick(def.SceneValues).Id;
            owner.Traits.Set(def.Category, value);
            string tag = def.Value(value).SceneTag ?? value;

            Clue mark = AddClue(ClueKind.Scene, TraitRules.SceneLabel(def.Category, value), TextBank.SceneText(def.Category, value, _sceneRoom.Id));
            mark.RoomId = _sceneRoom.Id;
            mark.Facts.Add(new SceneFact(def.Category, value));

            Character? explainer = byFinder
                ? _finder
                : _suspects.FirstOrDefault(c => c.Role is RoleKind.Spouse or RoleKind.Valet or RoleKind.Butler or RoleKind.Housekeeper && !IsCulprit(c))
                  ?? _suspects.FirstOrDefault(c => !IsCulprit(c) && !c.IsVisitor);
            if (explainer is null)
            {
                return;
            }

            string vref = TextBank.VictimRef(explainer, _victim);
            Clue why = AddClue(ClueKind.Statement, $"{explainer.ShortName}: about the {tag}",
                byFinder ? $"Says the {tag} was left by {explainer.ShortName} on finding the body." : $"Says the {tag} was {_victim.ShortName}'s own.");
            why.Spoken = byFinder
                ? $"When I found {vref} this morning I came over that faint, I didn't know what I was doing. The {tag} in there? That was me, {{SIR}}. I'm ashamed to say it."
                : $"The {tag}? Oh, that's nothing. That was {vref}'s own; {_victim.He} {def.Value(value).Desc}. It'll have been there before anything happened.";
            why.SpeakerId = explainer.Id;
            why.Topic = byFinder ? Topic.Sightings : Topic.Victim;
            why.Facts.Add(new ExplainedFact(def.Category, byFinder ? $"left by {explainer.ShortName} this morning" : $"{_victim.ShortName}'s own"));
            if (byFinder)
            {
                why.Facts.Add(new TraitFact(explainer.Id, def.Category, value));
            }

            _herring = (def.Category, value, owner);
        }

        // ================================================================== solution bookkeeping

        private Dictionary<string, Elimination> BuildEliminations()
        {
            var result = new Dictionary<string, Elimination>();
            var sceneClueIds = _clues
                .Where(c => c.Facts.OfType<SceneFact>().Any(f => _cats.Contains(f.Category)))
                .GroupBy(c => c.Facts.OfType<SceneFact>().First().Category)
                .ToDictionary(g => g.Key, g => g.First().Id);

            foreach (Character i in Innocents)
            {
                var ids = new List<string>();
                var reasons = new List<string>();

                var alibiClues = _clues.Where(c => c.IsTruthful && c.AlibiFor.Contains(i.Id)).ToList();
                if (alibiClues.Count > 0)
                {
                    ids.AddRange(alibiClues.Select(c => c.Id));
                    reasons.Add(_night[i.Id][_ms] == RoomIds.Away
                        ? $"had left the house before {NightSlots.Spoken(_ms)}, as the visitors' book shows"
                        : $"was in the {Room(_night[i.Id][_ms])} at {NightSlots.Spoken(_ms)}, as others confirm");
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
                    if (sceneClueIds.TryGetValue(cat, out string? sceneId))
                    {
                        ids.Add(sceneId);
                    }

                    reasons.Add($"{TraitRules.ValueLabel(cat, value).ToLowerInvariant()}, but the evidence shows {TraitRules.SceneLabel(cat, _scene[cat]).ToLowerInvariant()}");
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
    }
}
