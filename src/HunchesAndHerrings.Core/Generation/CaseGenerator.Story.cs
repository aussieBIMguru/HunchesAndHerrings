using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

public static partial class CaseGenerator
{
    private sealed partial class Builder
    {
        private static readonly string[] YoungsterRooms =
        {
            RoomIds.Gardens, RoomIds.Kitchen, RoomIds.Gallery, RoomIds.ServantsHall, RoomIds.Conservatory, RoomIds.Hall, RoomIds.Quarters,
        };

        private string Time(int slot) => NightSlots.Spoken(slot);

        // ================================================================== the children of the house

        private void CreateYoungsters(string family)
        {
            double roll = _rng.NextDouble();
            int count = roll < 0.45 ? 0 : roll < 0.85 ? 1 : 2;
            for (int i = 0; i < count; i++)
            {
                Gender g = _rng.Chance(0.5) ? Gender.Male : Gender.Female;
                string first = NameBank.TakeFirst(_rng, g, _usedFirst);
                bool gentry = _rng.Chance(0.5);
                bool male = g == Gender.Male;
                string label = gentry
                    ? (_victim.IsMale ? (male ? "his lordship's grandson" : "his lordship's granddaughter") : (male ? "her ladyship's grandson" : "her ladyship's granddaughter"))
                    : (male ? "the bootboy" : "the kitchen girl");
                _youngsters.Add(new Character
                {
                    Id = $"k{i}",
                    FirstName = first,
                    Surname = gentry ? family : NameBank.TakeSurname(_rng, _usedSurnames),
                    Title = gentry ? (male ? "Master" : "Miss") : string.Empty,
                    ShortName = gentry ? (male ? $"Master {first}" : $"Miss {first}") : first,
                    Gender = g,
                    Role = RoleKind.YoungChild,
                    Station = gentry ? Station.Family : Station.Servant,
                    RoleLabel = label,
                    PersonalRoomId = RoomIds.Quarters,
                    PortraitSeed = _rng.Next(),
                });
            }
        }

        private void BuildYoungsterDay()
        {
            var rooms = YoungsterRooms.Where(r => r != _sceneRoom.Id).ToList();
            foreach (Character k in _youngsters)
            {
                var hours = new string[GameRules.DayHours];
                string? prev = null;
                for (int h = 0; h < hours.Length; h++)
                {
                    hours[h] = prev is not null && _rng.Chance(0.4) ? prev : _rng.Pick(rooms);
                    prev = hours[h];
                }

                _day[k.Id] = hours;
            }
        }

        /// <summary>Children don't tell you the whole story, but what little they say is always true.</summary>
        private void BuildYoungsterClues()
        {
            for (int i = 0; i < _youngsters.Count; i++)
            {
                Character k = _youngsters[i];
                string age = $"{_rng.Next(5, 11)} and {_rng.Pick(new[] { "a half", "three-quarters", "a bit" })}";
                Clue self = AddClue(ClueKind.Statement, $"{k.ShortName}: on {k.Him}self", $"Says {k.He} is {age}.");
                self.Spoken = _rng.Pick(new[]
                {
                    $"I'm {age}. I'm not supposed to talk to policemen.",
                    $"I'm {age}, and I can count to a thousand. Do you want to hear?",
                    $"I'm {age}. Nanny says I mustn't tell stories.",
                });
                self.SpeakerId = k.Id;
                self.Topic = Topic.Self;

                Clue seen = AddClue(ClueKind.Statement, $"{k.ShortName}: what {k.He} saw", string.Empty);
                seen.SpeakerId = k.Id;
                seen.Topic = Topic.Sightings;
                string bongs = $"{NightSlots.Hour24(_cs) - 12}";

                var descriptive = _cats.Where(c => TraitCatalog.Get(c).Rule == TraitRule.Match && !HeardCategories.Contains(c)).ToList();
                var twinsAway = _twins.Where(t => t != _hand && _manor.Contains(_night[t.Id][_ms])).ToList();
                if ((i == 0 || twinsAway.Count == 0) && descriptive.Count > 0)
                {
                    TraitCategory cat = _rng.Pick(descriptive);
                    string desc = TraitCatalog.Get(cat).Value(_scene[cat]).Desc;
                    string who = _hand.IsMale ? "a man" : "a lady";
                    seen.Spoken = $"I wasn't asleep. I was on the stairs when the clock did {bongs} bongs, and I saw {who} go to the {Room(_sceneRoom.Id)}. {_hand.HeCap} {desc}. I didn't see the face. Don't tell Nanny.";
                    seen.Text = $"At {NightSlots.Short(_cs)}, saw {who} go to the {Room(_sceneRoom.Id)}: {_hand.He} {desc}.";
                    seen.Facts.Add(new GenderSightingFact(_hand.Gender, _cs, _sceneRoom.Id));
                    seen.Facts.Add(new SceneFact(cat, _scene[cat]));
                    _keyEvidence.Add(seen.Id);
                }
                else if (twinsAway.Count > 0)
                {
                    Character t = _rng.Pick(twinsAway);
                    string room = _night[t.Id][_ms];
                    seen.Spoken = $"I went looking for the cat when the clock did {NightSlots.Hour24(_ms) - 12} bongs. {t.ShortName} was in the {Room(room)}. {t.HeCap} gave me a biscuit and sent me back to bed.";
                    seen.Text = $"At {NightSlots.Short(_ms)}, saw {t.ShortName} in the {Room(room)}.";
                    seen.Facts.Add(new LocationFact(t.Id, _ms, room));
                    seen.AlibiFor.Add(t.Id);
                }
                else
                {
                    seen.Spoken = "I was asleep. I'm always asleep. Except sometimes.";
                    seen.Text = "Saw nothing, or says so.";
                }
            }
        }

        // ================================================================== clues that belong to the story

        private void BuildStoryClues()
        {
            switch (_model)
            {
                case CaseModel.Conspiracy:
                {
                    Character accomplice = _culprits.First(c => c != _hand);
                    Clue note = AddClue(ClueKind.Document, "A scrap of a note",
                        $"A scrap of paper, half burned in {accomplice.ShortName}'s grate: \"…{Time(_cs)}. You see to it; I'll swear we were both in the {Room(_claimRoom)} all evening. Burn this. — {_hand.FirstName[0]}.\"");
                    note.RoomId = accomplice.PersonalRoomId;
                    _keyEvidence.Add(note.Id);
                    break;
                }

                case CaseModel.FamilyPact:
                {
                    string initials = string.Join(", ", _culprits.Select(c => c.Initials));
                    Character eldest = _culprits[0];
                    Clue compact = AddClue(ClueKind.Document, "A pact between the children",
                        $"Locked in a writing box in {eldest.ShortName}'s room: \"We are agreed. All of us, or none of us. Whatever happens on Friday night, we were all together in the {Room(_claimRoom)}. — {initials}\"");
                    compact.RoomId = eldest.PersonalRoomId;
                    _keyEvidence.Add(compact.Id);

                    if (_variant == 1 && _newcomer is not null)
                    {
                        Clue will = AddClue(ClueKind.Scene, "A charred fragment of a will",
                            $"Caught at the back of the grate, a fragment of a will in {_victim.ShortName}'s hand, signed and witnessed: \"…the whole of my estate, real and personal, to {_newcomer.FullName}, and to my children…\" The rest is ash.");
                        will.RoomId = _sceneRoom.Id;
                        will.Facts.Add(new MotiveFact(eldest.Id, MotiveKind.Disinheritance));
                        _keyEvidence.Add(will.Id);
                    }
                    else
                    {
                        Clue bonds = AddClue(ClueKind.Document, "Post-obit bonds",
                            $"A moneylender's file: loans to {TextBank.JoinNames(_culprits.Select(c => c.ShortName).ToList())}, each secured against their expectations, and all of them \"payable upon the death of {_victim.FullName}\".");
                        bonds.RoomId = RoomIds.Study;
                        foreach (Character c in _culprits)
                        {
                            bonds.Facts.Add(new MotiveFact(c.Id, MotiveKind.Inheritance));
                        }

                        _keyEvidence.Add(bonds.Id);
                    }

                    break;
                }

                case CaseModel.HiddenSecrets when _keyword is not null:
                {
                    Clue diary = AddClue(ClueKind.Document, "The victim's diary",
                        $"{_victim.ShortName}'s diary, the last entry written yesterday: \"I know the truth about {_keyword} now. I have given them until Monday to own it, or I go to the police. Everyone in this house is hiding something, but that is the worst of it.\"");
                    diary.RoomId = _rng.Chance(0.5) ? RoomIds.Study : RoomIds.Master;
                    _keyEvidence.Add(diary.Id);
                    break;
                }

                case CaseModel.SecretHeir when _secretHeir is not null:
                {
                    string child = _secretHeir.IsMale ? "son" : "daughter";
                    string text = _hand == _secretHeir
                        ? $"A draft letter in {_victim.ShortName}'s hand to the family solicitor: \"I will NOT acknowledge {_secretHeir.FirstName} as my {child}, whatever {_secretHeir.He} threatens. Pay {_secretHeir.Him} a quarter's wages and see {_secretHeir.Him} off the premises.\""
                        : $"A letter in {_victim.ShortName}'s hand to the family solicitor: \"I mean to acknowledge {_secretHeir.FirstName}, my {child}, and to leave {_secretHeir.Him} the bulk of the estate. Draw up the new will for Monday. Not a word to {_hand.ShortName}.\"";
                    Clue letter = AddClue(ClueKind.Document, "A letter to the solicitor", text);
                    letter.RoomId = RoomIds.Study;
                    letter.Facts.Add(new MotiveFact(_hand.Id, _motive));
                    _keyEvidence.Add(letter.Id);

                    var witnesses = Innocents.Where(c => c != _secretHeir && !c.IsVisitor).ToList();
                    if (witnesses.Count > 0)
                    {
                        Character w = _rng.Pick(witnesses);
                        Clue gossip = AddClue(ClueKind.Statement, $"{w.ShortName} on {_secretHeir.ShortName}", $"Says {_secretHeir.ShortName} has the family likeness.");
                        gossip.Spoken = $"Have you looked at {_secretHeir.ShortName}, {{SIR}}? Really looked? The eyes, the chin. It's {TextBank.VictimRef(w, _victim)} to the life. Nobody says it aloud.";
                        gossip.SpeakerId = w.Id;
                        gossip.Topic = Topic.AboutPerson;
                        gossip.TargetId = _secretHeir.Id;
                    }

                    break;
                }

                case CaseModel.Accident when _accident is not null:
                {
                    Clue cause = AddClue(ClueKind.Scene, "What really happened here?", _accident.Cause);
                    cause.RoomId = _sceneRoom.Id;
                    _keyEvidence.Add(cause.Id);

                    Character? warner = _suspects.FirstOrDefault(c => c.Role is RoleKind.Butler or RoleKind.Housekeeper or RoleKind.Gardener && c != _hand)
                        ?? _suspects.FirstOrDefault(c => c.Station == Station.Servant && c != _hand && !c.IsVisitor);
                    if (warner is not null)
                    {
                        Clue warning = AddClue(ClueKind.Statement, $"{warner.ShortName}: a warning given", $"Says {warner.He} warned {_victim.ShortName} about the danger.");
                        warning.Spoken = _accident.Warning;
                        warning.SpeakerId = warner.Id;
                        warning.Topic = Topic.Victim;
                        _keyEvidence.Add(warning.Id);
                    }

                    break;
                }

                case CaseModel.DoctorPoison:
                {
                    Clue book = AddClue(ClueKind.Document, "The poison book",
                        $"The household poison register, kept in the butler's pantry. The latest entry, in a hand that isn't the butler's: \"{_weapon.Name}, one ounce, for the surgery. — {_hand.ShortName}.\"");
                    book.RoomId = RoomIds.Pantry;
                    _keyEvidence.Add(book.Id);
                    break;
                }

                case CaseModel.DoctorNegligence:
                {
                    Clue label = AddClue(ClueKind.Document, "A chemist's note",
                        $"A note from the village chemist, pinned to the empty draught bottle: \"Sir: I queried this dose with {_hand.ShortName}, who insisted it be made up as written. Thirty drops. I beg you take care.\"");
                    label.RoomId = _sceneRoom.Id;
                    _keyEvidence.Add(label.Id);

                    Clue book = AddClue(ClueKind.Document, "A doctor's casebook",
                        $"{_hand.ShortName}'s casebook. The page for {_victim.ShortName}'s visit yesterday has been cut out with a razor; the stub shows the start of the word \"thir—\".");
                    book.RoomId = _hand.PersonalRoomId;
                    _keyEvidence.Add(book.Id);
                    break;
                }

                case CaseModel.Affair when _variant == 0 && _lover is not null:
                {
                    Clue torn = AddClue(ClueKind.Document, "A torn note",
                        $"A note in {_hand.ShortName}'s hand, torn across and thrown in the wastepaper basket: \"I know about you and {_lover.ShortName}. I will not be made a fool of in my own house.\"");
                    torn.RoomId = _hand.PersonalRoomId;
                    torn.Facts.Add(new MotiveFact(_hand.Id, MotiveKind.Affair));
                    _keyEvidence.Add(torn.Id);
                    break;
                }

                case CaseModel.MistakenIdentity when _intended is not null:
                {
                    Clue coat = AddClue(ClueKind.Scene, "The victim's coat",
                        $"{_victim.ShortName} is wearing a dark ulster far too big for {_victim.Him}. It isn't {_victim.His}: the name tape inside reads \"{_intended.FullName}\". In the dark, from behind, anyone would have taken the wearer for {_intended.ShortName}.");
                    coat.RoomId = _sceneRoom.Id;
                    _keyEvidence.Add(coat.Id);

                    Clue note = AddClue(ClueKind.Scene, "A note in the coat pocket",
                        $"In the ulster's pocket, a note addressed to {_intended.ShortName}, not to the victim: \"{Time(_ms)}, the {Room(_sceneRoom.Id)}. Come alone. — {_hand.FirstName[0]}.\"");
                    note.RoomId = _sceneRoom.Id;
                    _keyEvidence.Add(note.Id);

                    Clue lent = AddClue(ClueKind.Statement, $"{_intended.ShortName}: the borrowed coat", $"Says {_victim.ShortName} borrowed {_intended.His} ulster last night.");
                    lent.Spoken = $"{TextBank.Capitalise(TextBank.VictimRef(_intended, _victim))} borrowed my ulster last night; {_victim.He} was cold and mine was on the peg. Dear God. It should have been me in that coat. It should have been me.";
                    lent.SpeakerId = _intended.Id;
                    lent.Topic = Topic.Victim;
                    _keyEvidence.Add(lent.Id);

                    var gossips = Innocents.Where(c => c != _intended && !c.IsVisitor).ToList();
                    if (gossips.Count > 0)
                    {
                        Character w = _rng.Pick(gossips);
                        Clue grudge = AddClue(ClueKind.Statement, $"{w.ShortName} on {_hand.ShortName}", $"Says {_hand.ShortName} had a grudge against {_intended.ShortName}.");
                        grudge.Spoken = $"If you ask me, {_hand.ShortName} hated {_intended.ShortName}, not {TextBank.VictimRef(w, _victim)}. {TextBank.MotiveGossip(_motive, _hand, _intended.ShortName)}";
                        grudge.SpeakerId = w.Id;
                        grudge.Topic = Topic.AboutPerson;
                        grudge.TargetId = _hand.Id;
                        grudge.Facts.Add(new MotiveFact(_hand.Id, _motive));
                    }

                    break;
                }
            }
        }

        // ================================================================== the rival

        private void BuildRival()
        {
            if (!_options.Rival)
            {
                return;
            }

            var style = (RivalStyle)_rng.Next(3);
            Gender g = _rng.Chance(0.5) ? Gender.Male : Gender.Female;
            var (first, surname) = StoryBank.RivalName(_rng, g);
            string title = g == Gender.Male ? "Mr" : "Miss";
            var rival = new Character
            {
                Id = "rival",
                FirstName = first,
                Surname = surname,
                Title = title,
                ShortName = $"{title} {surname}",
                Gender = g,
                Role = RoleKind.RivalDetective,
                Station = Station.Guest,
                RoleLabel = StoryBank.RivalLabel(style),
                PersonalRoomId = RoomIds.Hall,
                PortraitSeed = _rng.Next(),
            };
            PortraitCatalog.Assign(rival, _rng, _usedPortraits);

            (double closetChance, int pockets, int tips, int falseTips, double insight) = _s.Difficulty switch
            {
                Difficulty.Novice => (0.0, 0, 4, 0, 0.85),
                Difficulty.Inspector => (0.3, 1, 3, 1, 0.55),
                _ => (0.5, 2, 3, 2, 0.35),
            };

            // Where they go today, and whom they keep to themselves.
            var hours = new string[GameRules.DayHours];
            var closeted = new Dictionary<int, string>();
            var roamRooms = _manor.Rooms.Where(r => !r.IsPassage && r.Id != _sceneRoom.Id).Select(r => r.Id).ToList();
            for (int h = 0; h < hours.Length; h++)
            {
                if (h == 0)
                {
                    hours[h] = _sceneRoom.Id;
                    continue;
                }

                var available = _suspects.Where(s => _manor.Contains(_day[s.Id][h]) && _day[s.Id][h] != _sceneRoom.Id).ToList();
                if (available.Count > 0 && _rng.Chance(closetChance))
                {
                    Character mark = _rng.Pick(available);
                    hours[h] = _day[mark.Id][h];
                    closeted[h] = mark.Id;
                }
                else
                {
                    hours[h] = _rng.Pick(roamRooms);
                }
            }

            // What they pocket before you get to it.
            var pocketed = new List<RivalPocket>();
            var pocketable = _clues
                .Where(c => c.RoomId is not null && c.RoomId != _sceneRoom.Id && c.Kind is ClueKind.Document or ClueKind.Observation)
                .OrderByDescending(c => _s.Difficulty == Difficulty.Master && c.IsKey ? 1 : 0)
                .ThenBy(_ => _rng.Next())
                .Take(pockets)
                .ToList();
            foreach (Clue c in pocketable)
            {
                int h = _rng.Next(1, 6);
                hours[h] = c.RoomId!;
                closeted.Remove(h);
                pocketed.Add(new RivalPocket(c.Id, h));
            }

            _day[rival.Id] = hours;

            // What they'll tell you, true or otherwise.
            var tipIds = new List<string>();
            Clue Tip(string spoken, string text)
            {
                Clue tip = AddClue(ClueKind.Report, $"{rival.ShortName}'s tip", text);
                tip.Spoken = $"{StoryBank.RivalTipIntro(style, _rng)} {spoken}";
                tip.SpeakerId = rival.Id;
                tip.Topic = Topic.CompareNotes;
                tipIds.Add(tip.Id);
                return tip;
            }

            var trueTips = new List<Action>();
            foreach (Character o in _rng.Shuffled(_others).Take(2))
            {
                var failing = _cats.Where(cat => TraitRules.Excludes(cat, _scene[cat], o.Traits.ValueOf(cat))).ToList();
                if (failing.Count == 0)
                {
                    continue;
                }

                TraitCategory cat = _rng.Pick(failing);
                string value = o.Traits.ValueOf(cat);
                trueTips.Add(() =>
                {
                    Clue t = Tip(TextBank.TraitOther(cat, value, o), $"{o.ShortName}: {TraitRules.ValueLabel(cat, value).ToLowerInvariant()}.");
                    t.Facts.Add(new TraitFact(o.Id, cat, value));
                });
            }

            foreach (Character t in _rng.Shuffled(_twins.Where(x => x != _hand && _manor.Contains(_night[x.Id][_ms]))).Take(1))
            {
                string room = _night[t.Id][_ms];
                trueTips.Add(() =>
                {
                    Clue tip = Tip($"{t.ShortName} was in the {Room(room)} at {Time(_ms)}. I have it on unimpeachable authority.", $"{t.ShortName} in the {Room(room)} at {NightSlots.Short(_ms)}.");
                    tip.Facts.Add(new LocationFact(t.Id, _ms, room));
                    tip.AlibiFor.Add(t.Id);
                });
            }

            Clue? hidden = _clues.FirstOrDefault(c => c.IsKey && c.RoomId is not null && c.RoomId != _sceneRoom.Id && pocketed.All(p => p.ClueId != c.Id));
            if (hidden is not null)
            {
                trueTips.Add(() => Tip($"If I were you I'd take a closer look at the {Room(hidden.RoomId!)}.", $"Suggests searching the {Room(hidden.RoomId!)}."));
            }

            var falseTipsList = new List<Action>();
            if (_model != CaseModel.Accident)
            {
                falseTipsList.Add(() =>
                {
                    string room = _rng.Pick(ManorLayout.SharedRooms.Where(r => r != _sceneRoom.Id).ToList());
                    Clue tip = Tip($"{_hand.ShortName} was in the {Room(room)} at {Time(_cs)}. Quite cleared. Look elsewhere.", $"{_hand.ShortName} in the {Room(room)} at {NightSlots.Short(_cs)}.");
                    tip.Facts.Add(new LocationFact(_hand.Id, _cs, room));
                    tip.IsTruthful = false;
                });
            }

            foreach (Character o in _rng.Shuffled(_others).Take(1))
            {
                TraitCategory cat = _rng.Pick(_cats);
                string value = TraitCatalog.Get(cat).FittingValue(_scene[cat]);
                if (value == o.Traits.ValueOf(cat))
                {
                    continue;
                }

                falseTipsList.Add(() =>
                {
                    Clue tip = Tip(TextBank.TraitOther(cat, value, o), $"{o.ShortName}: {TraitRules.ValueLabel(cat, value).ToLowerInvariant()}.");
                    tip.Facts.Add(new TraitFact(o.Id, cat, value));
                    tip.IsTruthful = false;
                });
            }

            var chosen = _rng.Shuffled(trueTips).Take(tips - Math.Min(falseTips, falseTipsList.Count))
                .Concat(_rng.Shuffled(falseTipsList).Take(falseTips))
                .ToList();
            foreach (Action make in _rng.Shuffled(chosen))
            {
                make();
            }

            // Whom they'll name when the Chief Constable arrives.
            bool right = _rng.Chance(insight);
            var accuses = new List<string>();
            bool accusesAccident = false;
            if (right)
            {
                accusesAccident = _model == CaseModel.Accident;
                accuses.AddRange(_culprits.Select(c => c.Id));
            }
            else
            {
                Character wrong = _model == CaseModel.Accident ? _hand : _rng.Pick(_suspects.Where(s => !IsCulprit(s)).ToList());
                accuses.Add(wrong.Id);
            }

            _rival = new RivalPlan
            {
                Detective = rival,
                Style = style,
                EngagedBy = StoryBank.RivalEngagedBy(_rng),
                Closeted = closeted,
                Pockets = pocketed,
                TipIds = tipIds,
                AccusesIds = accuses,
                AccusesAccident = accusesAccident,
                Insight = insight,
                Generous = _s.Difficulty != Difficulty.Master,
            };
        }

        // ================================================================== the telling

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
            string weaponNoun = _weapon.Name.Split(' ')[^1].Trim('(', ')');
            string adjective = _rng.Pick(adjectives);
            string noun = _rng.Chance(0.4) ? TextBank.Capitalise(weaponNoun) : _rng.Pick(objects);

            return _rng.Next(8) switch
            {
                0 or 1 => $"The Case of the {adjective} {noun}",
                2 => $"The {house} Affair",
                3 => $"Death at {_manor.Name}",
                4 => $"The {adjective} {noun} of {house}",
                5 => $"Death in the {Room(_sceneRoom.Id)}",
                6 => $"The {house} Inheritance",
                _ => $"The {_victim.Surname} Mystery",
            };
        }

        private string BuildBriefing()
        {
            string finderText = _finder is not null ? $"{_finder.ShortName}, {_finder.RoleLabel}," : "a housemaid";
            string tod = _s.RevealTimeOfDeath
                ? $" The police surgeon puts the time of death at around {Time(_ms)}."
                : string.Empty;

            var visitors = _suspects.Where(c => c.IsVisitor).ToList();
            string callers = visitors.Count > 0
                ? $" {TextBank.JoinNames(visitors.Select(v => $"{v.ShortName}, {v.RoleLabel.Split(',')[0]},").ToList())} called at the house yesterday and may be sent for today."
                : string.Empty;
            string young = _youngsters.Count > 0
                ? $" There {(_youngsters.Count == 1 ? "is a child" : "are children")} in the house too: {TextBank.JoinNames(_youngsters.Select(k => k.ShortName).ToList())}."
                : string.Empty;
            string rival = _rival is null
                ? string.Empty
                : $" To your annoyance, {_rival.Detective.FullName}, {_rival.Detective.RoleLabel}, has been engaged by {_rival.EngagedBy} and is already poking about.";

            return $"At a quarter past six this morning, {finderText} found {_victim.FullName} dead in the {Room(_sceneRoom.Id)} of {_manor.Name}.{tod} " +
                   $"{_lockdown.Text}{callers}{young}{rival} " +
                   $"The Chief Constable expects a name by {Clock.Hour(_s.EndHour)}. Interview the household, search the rooms, " +
                   "and find out who is lying.";
        }

        private string DisproofSentence(string who, bool plural) => _disproof switch
        {
            Disproof.EmptyRoom => $"but the {Room(_claimRoom)} was never used that night: its grate was cold and its lamps untouched.",
            Disproof.VisitorsBook => $"but the visitors' book shows {who} still in the house until after {Time(_cs)}.",
            _ => $"but {_loneWitness!.ShortName} sat alone in the {Room(_claimRoom)} at that hour, and nobody came in.",
        };

        private string MotiveSentence() => _model switch
        {
            CaseModel.Accident => "There was no motive, because there was no murder.",
            CaseModel.FamilyPact when _variant == 1 && _newcomer is not null =>
                $"{_victim.ShortName} had signed a new will leaving everything to {_newcomer.ShortName}, and the children meant to see it burned and their father's old will stand.",
            CaseModel.FamilyPact => $"The children were drowning in debts raised against their inheritance, and could not wait for {_victim.ShortName} to die.",
            _ => TextBank.MotiveDetail(_motive, _hand, _intended ?? _victim),
        };

        private string BuildTruth()
        {
            string scene = Room(_sceneRoom.Id);
            string how = _weapon.Poison ? $"with {_weapon.Name.ToLowerInvariant()}" : $"with the {_weapon.Name.ToLowerInvariant()}";
            string claimed = _claimRoom == RoomIds.Away ? "to have left the house" : $"to have been in the {Room(_claimRoom)}";

            switch (_model)
            {
                case CaseModel.Accident when _accident is not null:
                    return $"Nobody killed {_victim.ShortName}. At around {Time(_ms)}, {_victim.ShortName} " +
                           $"{TraitCatalog.Fill(_accident.Summary, _victim)}. At {Time(_cs)}, {_hand.FullName}, {_hand.RoleLabel}, came into the {scene}, " +
                           $"found the body, took something {_hand.He} wanted and laid the {_weapon.Name.ToLowerInvariant()} by the body to make it look like murder. " +
                           $"{_hand.ShortName} then claimed {claimed}, {DisproofSentence(_hand.ShortName, false)} Everyone, {_hand.ShortName} included, can be placed elsewhere at the time of death.";

                case CaseModel.DoctorNegligence:
                    return $"{_hand.FullName} gave {_victim.ShortName} {_victim.His} sleeping draught in the {scene} at around {Time(_ms)}, " +
                           $"at ten times the proper dose: a slip of the pen on the prescription. Rather than own the blunder, {_hand.ShortName} claimed {claimed}, " +
                           $"{DisproofSentence(_hand.ShortName, false)}";
            }

            string names = TextBank.JoinNames(_culprits.Select(c => c.FullName).ToList());
            string lead = _culprits.Count > 1
                ? $"{names} acted together. {_hand.ShortName} killed {_victim.ShortName} in the {scene} at around {Time(_ms)} {how}, while {TextBank.JoinNames(_culprits.Where(c => c != _hand).Select(c => c.ShortName).ToList())} kept watch."
                : $"{_hand.FullName}, {_hand.RoleLabel}, killed {_victim.ShortName} in the {scene} at around {Time(_ms)} {how}.";

            string twist = _model switch
            {
                CaseModel.MistakenIdentity when _intended is not null =>
                    $" But {_victim.ShortName} was never the target: {_victim.He} was wearing {_intended.ShortName}'s coat, and in the dark {_hand.ShortName} took {_victim.Him} for {_intended.ShortName}.",
                CaseModel.SecretHeir when _secretHeir is not null =>
                    _hand == _secretHeir
                        ? $" {_secretHeir.ShortName} was {_victim.ShortName}'s own unacknowledged child."
                        : $" {_secretHeir.ShortName}, {_secretHeir.RoleLabel}, was {_victim.ShortName}'s secret child, about to be acknowledged as heir.",
                CaseModel.Impostor => $" {_hand.ShortName} was not who {_hand.He} claimed to be.",
                CaseModel.HiddenSecrets => " Nearly everyone in the house had a secret to keep; only one of them was worth killing for.",
                CaseModel.DoctorPoison => $" As the family doctor, {_hand.ShortName} had the poison and the knowledge to use it.",
                _ => string.Empty,
            };

            string lie = _culprits.Count > 1
                ? $" They all swore they were together in the {Room(_claimRoom)}, {DisproofSentence(names, true)}"
                : $" {_hand.ShortName} claimed {claimed} at the time, {DisproofSentence(_hand.ShortName, false)}";

            return $"{lead}{twist} {MotiveSentence()}{lie}";
        }
    }
}
