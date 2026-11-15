using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Generation;

public static partial class CaseGenerator
{
    /// <summary>A secret someone keeps: what exposes it, what they admit when confronted, and any lie it caused.</summary>
    private sealed class Secret
    {
        public required Character Who { get; init; }
        public required string Label { get; init; }
        public required string DocTitle { get; init; }
        public required string DocText { get; init; }
        public required string Confession { get; init; }

        /// <summary>The night slot they lie about, if the secret made them lie.</summary>
        public int? Slot { get; init; }

        /// <summary>Where they really were at <see cref="Slot"/>.</summary>
        public string? ActualRoom { get; init; }

        public Character? Partner { get; init; }

        /// <summary>When confronted, they admit to this motive.</summary>
        public MotiveKind? Motive { get; init; }

        /// <summary>Where the document is kept (defaults to their own room).</summary>
        public string? DocRoomId { get; init; }

        /// <summary>The document matters to the solution.</summary>
        public bool IsKey { get; init; }
    }

    private sealed partial class Builder
    {
        private static readonly string[] RendezvousRooms =
        {
            RoomIds.Gardens, RoomIds.Conservatory, RoomIds.Gallery, RoomIds.WineCellar, RoomIds.Library, RoomIds.Boiler, RoomIds.Billiard,
        };

        // ================================================================== the crime

        private Character PickFrom(IEnumerable<Character> pool, IEnumerable<Character> fallback)
        {
            var list = pool.ToList();
            return list.Count > 0 ? _rng.Pick(list) : _rng.Pick(fallback.ToList());
        }

        private MotiveKind EnsureMotive(Character c)
        {
            if (c.Motive == MotiveKind.None)
            {
                c.Motive = _rng.Pick(_defs[c.Id].Motives);
            }

            return c.Motive;
        }

        private void PlanCrime()
        {
            var residents = _suspects.Where(c => !c.IsVisitor).ToList();
            var family = residents.Where(c => c.Station == Station.Family).ToList();
            var servants = residents.Where(c => c.Station == Station.Servant).ToList();
            _ms = _rng.Next(1, 3);
            _cs = _ms;
            string? sceneId = null;
            bool poison = false;

            switch (_model)
            {
                case CaseModel.Conspiracy:
                {
                    _hand = _rng.Pick(residents);
                    Character accomplice = PickFrom(residents.Where(c => c != _hand), residents);
                    _culprits.Add(_hand);
                    _culprits.Add(accomplice);
                    _motive = _rng.Pick(new[] { MotiveKind.Inheritance, MotiveKind.Revenge, MotiveKind.Blackmail, MotiveKind.Debt, MotiveKind.Insurance });
                    break;
                }

                case CaseModel.FamilyPact:
                {
                    var children = residents.Where(c => c.Role == RoleKind.Child).ToList();
                    _culprits.AddRange(children);
                    _hand = _rng.Pick(children);
                    _motive = _variant == 1 ? MotiveKind.Disinheritance : MotiveKind.Inheritance;
                    if (_variant == 1)
                    {
                        _newcomer = residents.FirstOrDefault(c => c.Role is RoleKind.Ward or RoleKind.Companion)
                            ?? residents.FirstOrDefault(c => c.Role == RoleKind.Spouse)
                            ?? PickFrom(residents.Where(c => !_culprits.Contains(c)), residents);
                    }

                    break;
                }

                case CaseModel.HiddenSecrets:
                    _hand = PickFrom(residents.Where(c => c.Station != Station.Servant), residents);
                    _culprits.Add(_hand);
                    _motive = MotiveKind.Silence;
                    break;

                case CaseModel.SecretHeir:
                {
                    _secretHeir = PickFrom(servants, residents);
                    if (_rng.Chance(0.5))
                    {
                        _hand = _secretHeir;
                        _motive = MotiveKind.Recognition;
                    }
                    else
                    {
                        _hand = PickFrom(family.Where(c => c.Role is RoleKind.Spouse or RoleKind.Heir or RoleKind.Child), residents.Where(c => c != _secretHeir));
                        _motive = MotiveKind.Disinheritance;
                    }

                    _culprits.Add(_hand);
                    break;
                }

                case CaseModel.Accident:
                    _accident = _rng.Pick(StoryBank.Accidents);
                    _hand = _rng.Pick(residents);
                    _motive = MotiveKind.Accident;
                    _ms = 1;
                    _cs = 2;
                    sceneId = _accident.RoomId;
                    break;

                case CaseModel.DoctorPoison:
                    _hand = _suspects.First(c => c.Role == RoleKind.Doctor);
                    _culprits.Add(_hand);
                    _motive = _rng.Pick(new[] { MotiveKind.Debt, MotiveKind.Insurance, MotiveKind.Blackmail, MotiveKind.Silence });
                    poison = true;
                    break;

                case CaseModel.DoctorNegligence:
                    _hand = _suspects.First(c => c.Role == RoleKind.Doctor);
                    _culprits.Add(_hand);
                    _motive = MotiveKind.Negligence;
                    poison = true;
                    sceneId = RoomIds.Master;
                    break;

                case CaseModel.Affair:
                {
                    Character spouse = residents.First(c => c.Role == RoleKind.Spouse);
                    var lovers = residents.Where(c => c != spouse && c.Role != RoleKind.Child && c.Role != RoleKind.Heir).ToList();
                    if (_variant == 0)
                    {
                        // The victim was the one straying; the spouse found out.
                        _lover = PickFrom(lovers.Where(c => c.Gender != _victim.Gender), lovers.Count > 0 ? lovers : residents.Where(c => c != spouse));
                        _hand = spouse;
                        _culprits.Add(spouse);
                    }
                    else
                    {
                        // The spouse was the one straying; the lover struck, alone or with the spouse.
                        _lover = PickFrom(lovers.Where(c => c.Gender != spouse.Gender), lovers.Count > 0 ? lovers : residents.Where(c => c != spouse));
                        _hand = _lover;
                        _culprits.Add(_lover);
                        if (_variant == 2)
                        {
                            _culprits.Add(spouse);
                            if (_rng.Chance(0.5))
                            {
                                _hand = spouse;
                            }
                        }
                    }

                    _motive = MotiveKind.Affair;
                    break;
                }

                case CaseModel.MistakenIdentity:
                    _hand = _rng.Pick(residents);
                    _intended = PickFrom(residents.Where(c => c != _hand && c.Gender == _victim.Gender), residents.Where(c => c != _hand));
                    _culprits.Add(_hand);
                    _motive = _rng.Pick(new[] { MotiveKind.Jealousy, MotiveKind.Revenge, MotiveKind.Debt, MotiveKind.Blackmail });
                    break;

                case CaseModel.Impostor:
                    _hand = PickFrom(residents.Where(c => c.Station == Station.Guest), residents);
                    _culprits.Add(_hand);
                    _motive = MotiveKind.Imposture;
                    break;

                default:
                    _hand = _rng.Pick(residents);
                    _culprits.Add(_hand);
                    _motive = EnsureMotive(_hand);
                    break;
            }

            foreach (Character c in _culprits)
            {
                c.Motive = _motive;
            }

            if (_model == CaseModel.LoneKiller || _model == CaseModel.Impostor || _model == CaseModel.HiddenSecrets || _model == CaseModel.Affair)
            {
                poison |= _rng.Chance(0.2);
            }

            if (sceneId is null)
            {
                var candidates = _manor.Rooms.Where(r => !r.IsPassage && r.Id != RoomIds.Hall).ToList();
                if (_intended is not null)
                {
                    candidates = candidates.Where(r => r.Id != _intended.PersonalRoomId).ToList();
                }

                sceneId = _rng.Pick(candidates).Id;
            }

            _sceneRoom = _manor[sceneId];

            if (poison)
            {
                Weapon w = _model == CaseModel.DoctorNegligence
                    ? WeaponCatalog.Poisons.First(p => p.Id == "laudanum")
                    : _rng.Pick(WeaponCatalog.Poisons);
                if (_hand.Role == RoleKind.Doctor)
                {
                    w = w with
                    {
                        Name = _model == CaseModel.DoctorNegligence ? "Sleeping draught" : w.Name,
                        OriginRoomId = _hand.PersonalRoomId,
                        MissingText = $"{_hand.ShortName}'s medical bag: the phial of {w.Name.ToLowerInvariant()} is half empty, and the label has been scraped clean.",
                    };
                }

                _weapon = w;
            }
            else
            {
                _weapon = _rng.Pick(WeaponCatalog.All);
            }

            _finder = _suspects.FirstOrDefault(c => c.Role == RoleKind.Maid)
                ?? _suspects.FirstOrDefault(c => c.Station == Station.Servant && !c.IsVisitor);
        }

        // ================================================================== the scene evidence

        private int SceneTraitCount => _s.Difficulty switch
        {
            Difficulty.Novice => 2,
            _ => 3,
        };

        private bool HandFits(TraitDef d)
        {
            string value = _hand.Traits.ValueOf(d.Category);
            return d.Rule == TraitRule.Match ? d.Value(value).CanBeScene : true;
        }

        /// <summary>
        /// Chooses the scene indicators, makes the hand match them, splits the innocents into alibi-dependent "twins"
        /// and trait-excluded "others", and adjusts traits so both groups behave as intended.
        /// </summary>
        private void ConformTraits()
        {
            bool poison = _weapon.Poison;
            var pool = TraitCatalog.All
                .Where(d => d.SceneValues.Count > 0 && !d.Visible)
                .Where(d => d.Fit == TraitFit.Any || (d.Fit == TraitFit.Poison) == poison)
                .Where(d => d.Category != TraitCategory.Strength || _weapon.Heavy)
                .Where(d => _model != CaseModel.Accident || d.Fit == TraitFit.Any)
                .ToList();

            var chosen = pool
                .Select(d => (Def: d, Score: (HandFits(d) ? 0.0 : 1.0) + _rng.NextDouble() * 1.6))
                .OrderBy(x => x.Score)
                .Take(SceneTraitCount)
                .Select(x => x.Def)
                .ToList();

            foreach (TraitDef d in chosen)
            {
                string handValue = _hand.Traits.ValueOf(d.Category);
                string sceneValue;
                if (d.Rule == TraitRule.Avoid)
                {
                    var options = d.SceneValues.Where(v => v.Id != handValue).Select(v => v.Id).ToList();
                    if (options.Count == 0)
                    {
                        // The hand has the only value this scene could show (e.g. vertigo): cure them of it.
                        options = d.SceneValues.Select(v => v.Id).ToList();
                        _hand.Traits.Set(d.Category, d.FittingValue(options[0]));
                    }

                    sceneValue = _rng.Pick(options);
                }
                else
                {
                    sceneValue = d.Value(handValue).CanBeScene ? handValue : _rng.Pick(d.SceneValues).Id;
                    _hand.Traits.Set(d.Category, sceneValue);
                }

                _cats.Add(d.Category);
                _scene[d.Category] = sceneValue;
            }

            // Residents who aren't culprits are split; innocent visitors are cleared by the visitors' book instead.
            var innocents = _rng.Shuffled(Innocents.Where(c => !c.IsVisitor && c != _hand));
            int twinCount = Math.Clamp(_s.AlibiTwins, 1, Math.Max(1, innocents.Count - 1));
            if (_model == CaseModel.Accident)
            {
                // The meddler needs a sound alibi for the time of death.
                _twins.Add(_hand);
            }

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
            foreach (TraitCategory cat in _cats)
            {
                TraitDef d = TraitCatalog.Get(cat);
                if (TraitRules.Excludes(cat, _scene[cat], c.Traits.ValueOf(cat)))
                {
                    c.Traits.Set(cat, d.FittingValue(_scene[cat]));
                }
            }
        }

        private void MakeExcluded(Character c, TraitCategory cat) =>
            c.Traits.Set(cat, TraitCatalog.Get(cat).ClearingValue(_scene[cat], _rng));

        // ================================================================== last night

        private List<string> NightRooms(Character c)
        {
            var rooms = _defs[c.Id].NightRooms.ToList();
            if (!c.IsVisitor && !rooms.Contains(c.PersonalRoomId))
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
        /// Decides everyone's true whereabouts for the three night slots, building in the alibi structures that
        /// make the case solvable.
        /// </summary>
        private void PlaceNight()
        {
            foreach (Character c in Everyone)
            {
                _night[c.Id] = new string[NightSlots.Count];
            }

            string scene = _sceneRoom.Id;
            _night[_hand.Id][_cs] = scene;

            // 1. Where the liars will claim to have been, and what disproves it.
            if (_hand.IsVisitor)
            {
                _disproof = Disproof.VisitorsBook;
                _claimRoom = RoomIds.Away;
            }
            else
            {
                _disproof = Disproof.EmptyRoom;
                var witnesses = (_cs == _ms ? _others : Innocents.Where(c => !c.IsVisitor && c != _hand))
                    .Where(c => !IsCulprit(c))
                    .ToList();
                if (witnesses.Count > 0 && _rng.Chance(0.6))
                {
                    Character witness = _rng.Pick(witnesses);
                    var rooms = NightRooms(witness).Where(r => ManorLayout.SharedRooms.Contains(r) && r != scene).ToList();
                    if (rooms.Count > 0)
                    {
                        _loneWitness = witness;
                        _claimRoom = _rng.Pick(rooms);
                        _night[witness.Id][_cs] = _claimRoom;
                        _disproof = Disproof.LoneWitness;
                    }
                }

                if (_disproof == Disproof.EmptyRoom)
                {
                    var rooms = NightRooms(_hand).Where(r => ManorLayout.ProvablyEmptyRooms.Contains(r) && r != scene).ToList();
                    if (rooms.Count == 0)
                    {
                        rooms = ManorLayout.ProvablyEmptyRooms.Where(r => r != scene).ToList();
                    }

                    _claimRoom = _rng.Pick(rooms);
                    _reserved.Add(_claimRoom);
                }
            }

            var forbiddenAtCrime = new HashSet<string>(_reserved) { scene, _claimRoom };

            // 2. Accomplices keep watch, alone, close to the scene.
            foreach (Character a in _culprits.Where(c => c != _hand))
            {
                var lookouts = _manor.Neighbours(scene).Select(n => n.Room.Id)
                    .Where(r => !forbiddenAtCrime.Contains(r) && _culprits.All(x => _night[x.Id][_cs] != r))
                    .ToList();
                _night[a.Id][_cs] = lookouts.Count > 0 ? _rng.Pick(lookouts) : PickRoom(new[] { a.PersonalRoomId }, r => !forbiddenAtCrime.Contains(r));
            }

            // 3. Visitors: away at the time of death, unless the visitor is the killer.
            foreach (Character v in _suspects.Where(c => c.IsVisitor))
            {
                int arrive;
                int leave;
                if (v == _hand)
                {
                    arrive = 0;
                    leave = _cs;
                }
                else
                {
                    var slots = Enumerable.Range(0, NightSlots.Count).Where(s => s != _ms && s != _cs).ToList();
                    arrive = leave = _rng.Pick(slots);
                }

                for (int s = 0; s < NightSlots.Count; s++)
                {
                    if (_night[v.Id][s] is not null)
                    {
                        continue;
                    }

                    _night[v.Id][s] = s >= arrive && s <= leave
                        ? PickRoom(NightRooms(v), r => !_reserved.Contains(r) && r != scene && !(s == _cs && r == _claimRoom))
                        : RoomIds.Away;
                }
            }

            // 4. Twins get paired up, or a solo alibi, at the time of death.
            var forbiddenAtDeath = _ms == _cs ? forbiddenAtCrime : new HashSet<string>(_reserved) { scene };
            bool IsFree(Character c) => _night[c.Id][_ms] is null;
            var pairable = Innocents.Where(c => !c.IsVisitor).ToList();

            foreach (Character t in _twins)
            {
                if (!IsFree(t))
                {
                    continue;
                }

                Character? partner = _rng.Shuffled(pairable.Where(p => p != t && IsFree(p)))
                    .OrderBy(p => _twins.Contains(p) ? 1 : 0)
                    .FirstOrDefault();

                if (partner is not null)
                {
                    var common = NightRooms(t).Intersect(NightRooms(partner)).Where(ManorLayout.SharedRooms.Contains).ToList();
                    var either = NightRooms(t).Union(NightRooms(partner)).Where(ManorLayout.SharedRooms.Contains).ToList();
                    string room = PickRoom(common.Count > 0 ? common : either, r => !forbiddenAtDeath.Contains(r));
                    _night[t.Id][_ms] = room;
                    _night[partner.Id][_ms] = room;
                    _pairs.Add((t, partner, room));
                }
                else
                {
                    _night[t.Id][_ms] = PickRoom(NightRooms(t), r => !forbiddenAtDeath.Contains(r));
                    _soloTwins.Add(t);
                }
            }

            // 5. Everyone else at the time of death, then (in an accident) at the crime slot.
            foreach (Character c in _suspects.Where(IsFree))
            {
                _night[c.Id][_ms] = PickRoom(NightRooms(c), r => !forbiddenAtDeath.Contains(r));
            }

            foreach (Character c in _suspects.Where(c => _night[c.Id][_cs] is null))
            {
                _night[c.Id][_cs] = PickRoom(NightRooms(c), r => !forbiddenAtCrime.Contains(r));
            }

            // 6. The other slots. Nobody but the hand goes into the scene; nobody enters a provably empty room.
            foreach (Character c in _suspects)
            {
                for (int s = 0; s < NightSlots.Count; s++)
                {
                    _night[c.Id][s] ??= PickRoom(NightRooms(c), r => !_reserved.Contains(r) && r != scene);
                }
            }

            // 7. The victim: about the house early, then in the scene from the time of death onwards.
            for (int s = 0; s < NightSlots.Count; s++)
            {
                _night[_victim.Id][s] = s >= _ms
                    ? scene
                    : PickRoom(VictimEveningRooms, r => !_reserved.Contains(r) && r != _claimRoom);
            }

            // 8. Solo twins: a third-party glimpse if anyone was next door, otherwise the bell register.
            foreach (Character t in _soloTwins)
            {
                string room = _night[t.Id][_ms];
                Character? neighbour = _rng.Shuffled(pairable.Where(x => x != t && _manor.Contains(_night[x.Id][_ms]) && _manor.AreAdjacent(_night[x.Id][_ms], room)))
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

        // ================================================================== secrets

        /// <summary>A free slot and a quiet room where someone could have been doing something they'd rather hide.</summary>
        private (int Slot, string Room)? Rendezvous(params Character[] people)
        {
            var slots = _rng.Shuffled(Enumerable.Range(0, NightSlots.Count).Where(s => s != _ms && s != _cs));
            foreach (int slot in slots)
            {
                var rooms = RendezvousRooms
                    .Where(r => r != _sceneRoom.Id && r != _claimRoom && !_reserved.Contains(r))
                    .Where(r => people.All(p => r != p.PersonalRoomId))
                    .Where(r => Everyone.All(p => people.Contains(p) || _night[p.Id][slot] != r))
                    .ToList();
                if (rooms.Count > 0)
                {
                    return (slot, _rng.Pick(rooms));
                }
            }

            return null;
        }

        private Secret? MakeSecretLiar(Character who, SecretKind kind)
        {
            if (Rendezvous(who) is not { } spot)
            {
                return null;
            }

            _night[who.Id][spot.Slot] = spot.Room;
            string kw = _rng.Pick(StoryBank.Keywords);
            string Fill(string template) => TraitCatalog.Fill(template, who)
                .Replace("{kw}", kw, StringComparison.Ordinal)
                .Replace("{room}", Room(spot.Room), StringComparison.Ordinal)
                .Replace("{time}", $"{StoryBank.SecretTime(spot.Slot)} o'clock", StringComparison.Ordinal);

            return new Secret
            {
                Who = who,
                Label = kind.Label,
                DocTitle = kind.DocTitle,
                DocText = Fill(kind.DocText),
                Confession = Fill(kind.Confession),
                Slot = spot.Slot,
                ActualRoom = spot.Room,
            };
        }

        private void PlanSecrets()
        {
            var eligible = _rng.Shuffled(Innocents.Where(c => !c.IsVisitor && c != _loneWitness && c != _hand && c != _secretHeir && c != _lover));
            var kinds = new Queue<SecretKind>(_rng.Shuffled(StoryBank.Secrets));
            int liars = _model == CaseModel.HiddenSecrets ? 3 : _s.SecretLiars;

            foreach (Character who in eligible.Take(liars))
            {
                if (kinds.Count > 0 && MakeSecretLiar(who, kinds.Dequeue()) is { } secret)
                {
                    _secrets.Add(secret);
                }
            }

            string victimRef(Character c) => TextBank.VictimRef(c, _victim);

            switch (_model)
            {
                case CaseModel.HiddenSecrets:
                {
                    string kw = _rng.Pick(StoryBank.Keywords.Where(k => _secrets.All(s => !s.DocText.Contains(k))).ToList());
                    _secrets.Add(new Secret
                    {
                        Who = _hand,
                        Label = $"the {kw} business",
                        DocTitle = "A letter, hidden",
                        DocText = $"Folded into the lining of {_hand.ShortName}'s writing case, a letter from a stranger: \"Nobody need ever know what happened at {kw}, so long as you keep your nerve.\"",
                        Confession = $"So you've found that. Yes, {victimRef(_hand)} knew about {kw}. Knew everything. But I didn't kill for it, I swear.",
                        Motive = MotiveKind.Silence,
                        IsKey = true,
                    });
                    _keyword = kw;
                    break;
                }

                case CaseModel.SecretHeir when _secretHeir is not null:
                    _secrets.Add(new Secret
                    {
                        Who = _secretHeir,
                        Label = "a secret parentage",
                        DocTitle = "A locket and a certificate",
                        DocText = $"Wrapped in a handkerchief among {_secretHeir.ShortName}'s things: a locket holding a miniature of {_victim.ShortName} as a young {(_victim.IsMale ? "man" : "woman")}, and a baptismal certificate for {_secretHeir.FirstName}, \"father unnamed\". A pencilled note: \"Mine. God forgive me.\"",
                        Confession = $"You know, then. {_victim.ShortName} was my {(_victim.IsMale ? "father" : "mother")}. My mother was in service here. I came to be near {_victim.Him}. Nobody was ever to know.",
                        Motive = _hand == _secretHeir ? MotiveKind.Recognition : null,
                        IsKey = _hand == _secretHeir,
                    });
                    break;

                case CaseModel.Affair when _lover is not null:
                {
                    Character spouse = _suspects.First(c => c.Role == RoleKind.Spouse);
                    if (_variant == 0)
                    {
                        // The lover met the victim earlier in the evening, and lies about it.
                        if (Rendezvous(_lover, _victim) is { } spot && spot.Slot < _ms)
                        {
                            _night[_lover.Id][spot.Slot] = spot.Room;
                            _night[_victim.Id][spot.Slot] = spot.Room;
                            _secrets.Add(new Secret
                            {
                                Who = _lover,
                                Label = $"an affair with {_victim.ShortName}",
                                DocTitle = "Love letters",
                                DocText = $"Hidden among {_lover.ShortName}'s things, a bundle of love letters in {_victim.ShortName}'s hand. The last: \"The {Room(spot.Room)}, {StoryBank.SecretTime(spot.Slot)} o'clock. {spouse.ShortName} suspects. We must be careful.\"",
                                Confession = $"Yes. {_victim.ShortName} and I were lovers. At {StoryBank.SecretTime(spot.Slot)} o'clock we met in the {Room(spot.Room)}, as we often did. {spouse.ShortName} had begun to suspect.",
                                Slot = spot.Slot,
                                ActualRoom = spot.Room,
                                IsKey = true,
                            });
                        }
                        else
                        {
                            _secrets.Add(new Secret
                            {
                                Who = _lover,
                                Label = $"an affair with {_victim.ShortName}",
                                DocTitle = "Love letters",
                                DocText = $"Hidden among {_lover.ShortName}'s things, a bundle of love letters in {_victim.ShortName}'s hand: \"{spouse.ShortName} suspects. We must be careful.\"",
                                Confession = $"Yes. {_victim.ShortName} and I were lovers. {spouse.ShortName} had begun to suspect.",
                                IsKey = true,
                            });
                        }
                    }
                    else if (_variant == 1 && Rendezvous(_lover, spouse) is { } tryst)
                    {
                        _night[_lover.Id][tryst.Slot] = tryst.Room;
                        _night[spouse.Id][tryst.Slot] = tryst.Room;
                        string letters = $"Love letters between {spouse.ShortName} and {_lover.ShortName}. The last: \"The {Room(tryst.Room)} at {StoryBank.SecretTime(tryst.Slot)}. Soon we shan't need to hide.\"";
                        foreach (Character who in new[] { _lover, spouse })
                        {
                            Character other = who == spouse ? _lover : spouse;
                            _secrets.Add(new Secret
                            {
                                Who = who,
                                Partner = other,
                                Label = $"an affair with {other.ShortName}",
                                DocTitle = "Love letters",
                                DocText = letters,
                                Confession = $"Very well. {other.ShortName} and I are in love. At {StoryBank.SecretTime(tryst.Slot)} o'clock we were together in the {Room(tryst.Room)}. That's all it was.",
                                Slot = tryst.Slot,
                                ActualRoom = tryst.Room,
                                IsKey = true,
                            });
                        }
                    }
                    else
                    {
                        foreach (Character who in new[] { _lover, spouse })
                        {
                            Character other = who == spouse ? _lover : spouse;
                            _secrets.Add(new Secret
                            {
                                Who = who,
                                Partner = other,
                                Label = $"an affair with {other.ShortName}",
                                DocTitle = "Love letters",
                                DocText = $"Love letters between {spouse.ShortName} and {_lover.ShortName}: \"When {_victim.ShortName} is out of the way, nothing will part us.\"",
                                Confession = $"We love each other, {other.ShortName} and I. That isn't a crime.",
                                IsKey = true,
                            });
                        }
                    }

                    break;
                }

                case CaseModel.Impostor:
                {
                    string realName = $"{NameBank.TakeFirst(_rng, _hand.Gender, _usedFirst)} {NameBank.TakeSurname(_rng, _usedSurnames)}";
                    _secrets.Add(new Secret
                    {
                        Who = _hand,
                        Label = "a false identity",
                        DocTitle = "Papers in another name",
                        DocText = $"Sewn into the lining of {_hand.ShortName}'s portmanteau: a pawn ticket and a prison discharge paper, both in the name of {realName}.",
                        Confession = $"My name is {realName}. The real {_hand.FullName} died of fever years ago, and I took his place. {_victim.ShortName} found me out. But that's all I'll admit to.",
                        Motive = MotiveKind.Imposture,
                        IsKey = true,
                    });
                    break;
                }

                case CaseModel.Accident:
                {
                    (string Mine, string Theirs) taken = _rng.Pick(new[]
                    {
                        ("a letter that would have ruined me", $"a letter that would have ruined {_hand.Him}"),
                        ("some money from the desk", "a roll of banknotes"),
                        ("the pocket book with my IOU in it", $"a pocket book holding {_hand.His} own IOU"),
                        ("a brooch I'd pawned and put back", "a brooch, with a pawnbroker's tag still on it"),
                    });
                    _secrets.Add(new Secret
                    {
                        Who = _hand,
                        Label = $"was in the {Room(_sceneRoom.Id)} at {StoryBank.SecretTime(_cs)}",
                        DocTitle = "Something taken from the scene",
                        DocText = $"Hidden in {_hand.ShortName}'s room: {taken.Theirs}, plainly taken from {_victim.ShortName}'s person or desk. It is spotted with dried blood.",
                        Confession = $"All right! I went into the {Room(_sceneRoom.Id)} at {StoryBank.SecretTime(_cs)} o'clock and found {_victim.ShortName} already dead and cold on the floor. I took {taken.Mine} and I moved things to make it look like a thief. I didn't kill anyone. {_victim.HeCap} was dead when I got there.",
                        IsKey = true,
                    });
                    break;
                }
            }
        }

        /// <summary>The secret keyword linking the victim's diary to the killer's secret (Hidden Secrets cases).</summary>
        private string? _keyword;

        private Secret? SecretLie(Character c, int slot) => _secrets.FirstOrDefault(x => x.Who == c && x.Slot == slot);

        // ================================================================== today

        private void BuildDaySchedule()
        {
            string scene = _sceneRoom.Id;
            string Safe(string room) => room == scene ? RoomIds.Hall : room;

            foreach (Character c in _suspects)
            {
                var hours = new string[GameRules.DayHours];
                if (c.IsVisitor)
                {
                    for (int h = 0; h < hours.Length; h++)
                    {
                        hours[h] = RoomIds.Away;
                    }

                    int visits = c.Role == RoleKind.Constable ? 2 : 1;
                    int earliest = _lockdown.Id == "causeway" ? 3 : 1;
                    int latest = _lockdown.Id == "causeway" ? 6 : 8;
                    for (int v = 0; v < visits; v++)
                    {
                        int start = _rng.Next(earliest, latest);
                        int length = c.Role is RoleKind.Doctor or RoleKind.Solicitor ? 2 : 1;
                        var rooms = _defs[c.Id].DayRooms.Where(r => r != scene).ToList();
                        string room = rooms.Count > 0 ? _rng.Pick(rooms) : RoomIds.Hall;
                        for (int h = start; h < Math.Min(hours.Length, start + length); h++)
                        {
                            hours[h] = room;
                        }
                    }

                    _day[c.Id] = hours;
                    continue;
                }

                var dayRooms = _defs[c.Id].DayRooms.Append(c.PersonalRoomId).Where(r => r != scene).ToList();
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
                        room = dayRooms.Count > 0 ? _rng.Pick(dayRooms) : RoomIds.Hall;
                    }

                    hours[h] = room;
                    prev = room;
                }

                _day[c.Id] = hours;
            }

            // Somebody has business in the village this afternoon, if anyone may leave.
            if (_lockdown.AllowsErrands && _rng.Chance(0.5))
            {
                Character away = _rng.Pick(_suspects.Where(c => c.Role != RoleKind.Spouse && !c.IsVisitor).ToList());
                _day[away.Id][6] = RoomIds.Away;
                _day[away.Id][7] = RoomIds.Away;
            }
        }
    }
}
