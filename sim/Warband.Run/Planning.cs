using System;
using System.Collections.Generic;
using Warband.Sim;

namespace Warband.Run
{
    public enum PlanningZone
    {
        Field,
        Bench,
    }

    public enum PlanningValidationMode
    {
        Edit,
        Commit,
    }

    public enum PlanningIssueSeverity
    {
        Warning,
        Error,
    }

    /// <summary>
    /// One decision-relevant problem in a Planning draft. Codes are stable machine-facing
    /// identifiers; Message is presentation-ready fallback copy.
    /// </summary>
    public sealed class PlanningIssue
    {
        public string Code = "";
        public string Message = "";
        public string SubjectId = "";
        public PlanningIssueSeverity Severity = PlanningIssueSeverity.Error;
    }

    public sealed class PlanningValidation
    {
        public List<PlanningIssue> Issues { get; } = new List<PlanningIssue>();

        public bool IsValid
        {
            get
            {
                foreach (var issue in Issues)
                    if (issue.Severity == PlanningIssueSeverity.Error)
                        return false;
                return true;
            }
        }

        public string FirstError
        {
            get
            {
                foreach (var issue in Issues)
                    if (issue.Severity == PlanningIssueSeverity.Error)
                        return issue.Message;
                return "";
            }
        }

        public void Error(string code, string message, string subjectId = "")
        {
            Issues.Add(new PlanningIssue
            {
                Code = code,
                Message = message,
                SubjectId = subjectId,
                Severity = PlanningIssueSeverity.Error,
            });
        }

        public void Warn(string code, string message, string subjectId = "")
        {
            Issues.Add(new PlanningIssue
            {
                Code = code,
                Message = message,
                SubjectId = subjectId,
                Severity = PlanningIssueSeverity.Warning,
            });
        }
    }

    /// <summary>
    /// One owned hero inside the reversible pre-fight draft. Id is the stable run instance id;
    /// ContentId identifies the chassis/catalog definition. Benched heroes retain Position as
    /// their preferred return anchor even though only fielded heroes occupy the board.
    /// </summary>
    public sealed class PlanningHeroState
    {
        public string Id = "";
        public string ContentId = "";
        public PlanningZone Zone;
        public int BenchSlot = -1;
        public Hex Position;
        public Dictionary<string, string> Loadout = new Dictionary<string, string>();

        public PlanningHeroState Clone()
        {
            var clone = new PlanningHeroState
            {
                Id = Id,
                ContentId = ContentId,
                Zone = Zone,
                BenchSlot = BenchSlot,
                Position = Position,
            };
            foreach (var pair in Loadout)
                clone.Loadout[pair.Key] = pair.Value;
            return clone;
        }
    }

    /// <summary>
    /// A consumable or other finite Planning resource. The generic string State bag is for
    /// content-owned metadata such as tier or charges; core Planning never interprets it.
    /// </summary>
    public sealed class PlanningResourceState
    {
        public string Id = "";
        public string ContentId = "";
        public int Quantity;
        public Dictionary<string, string> State = new Dictionary<string, string>();

        public PlanningResourceState Clone()
        {
            var clone = new PlanningResourceState
            {
                Id = Id,
                ContentId = ContentId,
                Quantity = Quantity,
            };
            foreach (var pair in State)
                clone.State[pair.Key] = pair.Value;
            return clone;
        }
    }

    /// <summary>
    /// A content-owned effect queued during Planning and consumed by the host when the draft
    /// commits. This lets consumables target heroes, hexes, encounters, or the whole warband
    /// without teaching the Planning session their mechanics.
    /// </summary>
    public sealed class PlanningIntent
    {
        public string Kind = "";
        public string SourceId = "";
        public string TargetId = "";
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public PlanningIntent Clone()
        {
            var clone = new PlanningIntent
            {
                Kind = Kind,
                SourceId = SourceId,
                TargetId = TargetId,
            };
            foreach (var pair in Parameters)
                clone.Parameters[pair.Key] = pair.Value;
            return clone;
        }
    }

    /// <summary>
    /// The complete reversible answer to an encounter. It is deliberately small and cloneable:
    /// transactional edits and snapshot undo are cheap for a warband-sized roster.
    /// </summary>
    public sealed class PlanningDraft
    {
        public int FieldCapacity;
        public int BenchCapacity;
        public int MinimumFieldCount = 1;
        public List<PlanningHeroState> Heroes = new List<PlanningHeroState>();
        public List<PlanningResourceState> Resources = new List<PlanningResourceState>();
        public List<PlanningIntent> Intents = new List<PlanningIntent>();

        public PlanningHeroState? FindHero(string id)
        {
            foreach (var hero in Heroes)
                if (hero.Id == id)
                    return hero;
            return null;
        }

        public PlanningResourceState? FindResource(string id)
        {
            foreach (var resource in Resources)
                if (resource.Id == id)
                    return resource;
            return null;
        }

        public int FieldCount
        {
            get
            {
                int count = 0;
                foreach (var hero in Heroes)
                    if (hero.Zone == PlanningZone.Field)
                        count++;
                return count;
            }
        }

        public int BenchCount => Heroes.Count - FieldCount;

        public PlanningDraft Clone()
        {
            var clone = new PlanningDraft
            {
                FieldCapacity = FieldCapacity,
                BenchCapacity = BenchCapacity,
                MinimumFieldCount = MinimumFieldCount,
            };
            foreach (var hero in Heroes)
                clone.Heroes.Add(hero.Clone());
            foreach (var resource in Resources)
                clone.Resources.Add(resource.Clone());
            foreach (var intent in Intents)
                clone.Intents.Add(intent.Clone());
            return clone;
        }
    }

    /// <summary>
    /// Content adapter for the generic Planning machinery. Combat/loadout catalogs and future
    /// consumable definitions stay outside Warband.Run; the host answers only legality.
    /// </summary>
    public abstract class PlanningRules
    {
        public abstract bool IsLegalPosition(Hex position);

        public virtual bool CanSetLoadoutOption(
            PlanningDraft draft,
            PlanningHeroState hero,
            string slotId,
            string optionId,
            out string reason)
        {
            reason = "";
            return true;
        }

        public virtual bool CanUseResource(
            PlanningDraft draft,
            PlanningResourceState resource,
            string intentKind,
            string targetId,
            out string reason)
        {
            reason = "That resource has no Planning use.";
            return false;
        }

        public virtual bool CanUseResource(
            PlanningDraft draft,
            PlanningResourceState resource,
            string intentKind,
            string targetId,
            IReadOnlyDictionary<string, string> parameters,
            out string reason) =>
            CanUseResource(draft, resource, intentKind, targetId, out reason);

        public virtual void ValidateContent(
            PlanningDraft draft,
            PlanningValidationMode mode,
            PlanningValidation validation)
        {
        }
    }

    public static class PlanningValidator
    {
        public static PlanningValidation Validate(
            PlanningDraft draft,
            PlanningRules rules,
            PlanningValidationMode mode)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var result = new PlanningValidation();
            if (draft.FieldCapacity < 1)
                result.Error("field-capacity", "Field capacity must be at least one.");
            if (draft.BenchCapacity < 0)
                result.Error("bench-capacity", "Bench capacity cannot be negative.");
            if (draft.MinimumFieldCount < 1 || draft.MinimumFieldCount > draft.FieldCapacity)
                result.Error("minimum-field", "Minimum field count must fit the field capacity.");

            var heroIds = new HashSet<string>();
            var benchSlots = new HashSet<int>();
            var fieldPositions = new HashSet<Hex>();
            int fieldCount = 0;
            int benchCount = 0;

            foreach (var hero in draft.Heroes)
            {
                if (string.IsNullOrWhiteSpace(hero.Id) || !heroIds.Add(hero.Id))
                    result.Error("hero-id", "Every Planning hero needs a unique stable id.", hero.Id);

                if (hero.Zone == PlanningZone.Field)
                {
                    fieldCount++;
                    if (hero.BenchSlot != -1)
                        result.Error("field-bench-slot", "A fielded hero cannot occupy a bench slot.", hero.Id);
                    if (!rules.IsLegalPosition(hero.Position))
                        result.Error("illegal-position", "A fielded hero is outside the legal deployment area.", hero.Id);
                    else if (!fieldPositions.Add(hero.Position))
                        result.Error("duplicate-position", "Two fielded heroes cannot share a hex.", hero.Id);
                }
                else
                {
                    benchCount++;
                    if (hero.BenchSlot < 0 || hero.BenchSlot >= draft.BenchCapacity)
                        result.Error("bench-slot", "A benched hero occupies an invalid reserve slot.", hero.Id);
                    else if (!benchSlots.Add(hero.BenchSlot))
                        result.Error("duplicate-bench-slot", "Two heroes cannot share a reserve slot.", hero.Id);
                }
            }

            if (fieldCount < draft.MinimumFieldCount)
                result.Error("field-minimum", $"At least {draft.MinimumFieldCount} hero must be fielded.");
            if (fieldCount > draft.FieldCapacity)
                result.Error("field-full", "The active lineup exceeds its field capacity.");
            if (benchCount > draft.BenchCapacity)
                result.Error("bench-full", "The reserve exceeds its bench capacity.");

            var resourceIds = new HashSet<string>();
            foreach (var resource in draft.Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.Id) || !resourceIds.Add(resource.Id))
                    result.Error("resource-id", "Every Planning resource needs a unique stable id.", resource.Id);
                if (resource.Quantity < 0)
                    result.Error("resource-quantity", "A Planning resource cannot have negative quantity.", resource.Id);
            }

            rules.ValidateContent(draft, mode, result);
            return result;
        }
    }

    public sealed class PlanningActionResult
    {
        public bool Succeeded;
        public string Message = "";
        public PlanningValidation? Validation;

        public static PlanningActionResult Success(string message = "") =>
            new PlanningActionResult { Succeeded = true, Message = message };

        public static PlanningActionResult Failure(string message) =>
            new PlanningActionResult { Succeeded = false, Message = message };
    }

    /// <summary>
    /// Result of the one Planning commitment boundary. A successful Draft is an isolated,
    /// validated snapshot suitable for battle composition and authoritative run reconciliation.
    /// </summary>
    public sealed class PlanningCommitResult
    {
        public bool Succeeded;
        public string Message = "";
        public PlanningDraft? Draft;
        public PlanningValidation Validation = new PlanningValidation();
    }

    /// <summary>
    /// Open Planning command contract. New free actions automatically gain atomic validation,
    /// history, undo, and redo. Set IsReversible false only for an action that mutates external
    /// economic state immediately; such an action clears the local draft history.
    /// </summary>
    public interface IPlanningAction
    {
        string Kind { get; }
        bool IsReversible { get; }
        PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules);
    }

    /// <summary>
    /// Transactional owner of a Planning draft. Actions mutate a clone; invalid candidates never
    /// touch Current. Snapshot history is intentionally preferred over hand-authored inverse
    /// commands because the roster is small and new action types remain safe by default.
    /// </summary>
    public sealed class PlanningSession
    {
        private PlanningDraft _current;
        private readonly PlanningRules _rules;
        private readonly Stack<PlanningDraft> _undo = new Stack<PlanningDraft>();
        private readonly Stack<PlanningDraft> _redo = new Stack<PlanningDraft>();

        /// <summary>
        /// Isolated read snapshot. Callers cannot bypass actions by mutating session-owned state.
        /// </summary>
        public PlanningDraft Current => _current.Clone();
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public event Action? Changed;

        public PlanningSession(PlanningDraft initial, PlanningRules rules)
        {
            _current = initial?.Clone() ?? throw new ArgumentNullException(nameof(initial));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            var validation = PlanningValidator.Validate(
                _current,
                _rules,
                PlanningValidationMode.Edit);
            if (!validation.IsValid)
                throw new ArgumentException(validation.FirstError, nameof(initial));
        }

        public PlanningActionResult Execute(IPlanningAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var before = _current.Clone();
            var candidate = _current.Clone();
            var applied = action.Apply(candidate, _rules);
            if (!applied.Succeeded)
                return applied;

            var validation = PlanningValidator.Validate(
                candidate,
                _rules,
                PlanningValidationMode.Edit);
            if (!validation.IsValid)
                return new PlanningActionResult
                {
                    Succeeded = false,
                    Message = validation.FirstError,
                    Validation = validation,
                };

            _current = candidate;
            if (action.IsReversible)
            {
                _undo.Push(before);
                _redo.Clear();
            }
            else
            {
                _undo.Clear();
                _redo.Clear();
            }
            Changed?.Invoke();
            return applied;
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            _redo.Push(_current.Clone());
            _current = _undo.Pop();
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            _undo.Push(_current.Clone());
            _current = _redo.Pop();
            Changed?.Invoke();
            return true;
        }

        public PlanningValidation ValidateForCommit() =>
            PlanningValidator.Validate(_current, _rules, PlanningValidationMode.Commit);

        public PlanningCommitResult Commit()
        {
            var validation = ValidateForCommit();
            if (!validation.IsValid)
                return new PlanningCommitResult
                {
                    Succeeded = false,
                    Message = validation.FirstError,
                    Validation = validation,
                };

            _undo.Clear();
            _redo.Clear();
            return new PlanningCommitResult
            {
                Succeeded = true,
                Message = "Planning committed.",
                Draft = _current.Clone(),
                Validation = validation,
            };
        }
    }

    public sealed class MovePlanningHeroAction : IPlanningAction
    {
        public string HeroId { get; }
        public Hex Destination { get; }
        public string Kind => "move-hero";
        public bool IsReversible => true;

        public MovePlanningHeroAction(string heroId, Hex destination)
        {
            HeroId = heroId;
            Destination = destination;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var hero = draft.FindHero(HeroId);
            if (hero == null) return PlanningActionResult.Failure("That hero is no longer owned.");
            if (hero.Zone != PlanningZone.Field)
                return PlanningActionResult.Failure("Only a fielded hero can move on the board.");
            if (!rules.IsLegalPosition(Destination))
                return PlanningActionResult.Failure("Choose a legal blue deployment hex.");

            PlanningHeroState? occupant = null;
            foreach (var other in draft.Heroes)
                if (other.Id != hero.Id &&
                    other.Zone == PlanningZone.Field &&
                    other.Position == Destination)
                {
                    occupant = other;
                    break;
                }

            var origin = hero.Position;
            hero.Position = Destination;
            if (occupant != null)
                occupant.Position = origin;
            return PlanningActionResult.Success(
                occupant == null ? "Formation updated." : "Fielded heroes swapped positions.");
        }
    }

    public sealed class SwapFieldBenchPlanningAction : IPlanningAction
    {
        public string FieldHeroId { get; }
        public string BenchHeroId { get; }
        public string Kind => "swap-field-bench";
        public bool IsReversible => true;

        public SwapFieldBenchPlanningAction(string fieldHeroId, string benchHeroId)
        {
            FieldHeroId = fieldHeroId;
            BenchHeroId = benchHeroId;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var field = draft.FindHero(FieldHeroId);
            var bench = draft.FindHero(BenchHeroId);
            if (field == null || bench == null)
                return PlanningActionResult.Failure("That roster target is no longer available.");
            if (field.Zone != PlanningZone.Field || bench.Zone != PlanningZone.Bench)
                return PlanningActionResult.Failure("Swap one active hero with one reserve.");

            int slot = bench.BenchSlot;
            Hex inherited = field.Position;
            field.Zone = PlanningZone.Bench;
            field.BenchSlot = slot;
            bench.Zone = PlanningZone.Field;
            bench.BenchSlot = -1;
            bench.Position = inherited;
            return PlanningActionResult.Success("Active hero and reserve swapped.");
        }
    }

    public sealed class MovePlanningHeroToBenchAction : IPlanningAction
    {
        public string HeroId { get; }
        public int BenchSlot { get; }
        public string Kind => "move-hero-to-bench";
        public bool IsReversible => true;

        public MovePlanningHeroToBenchAction(string heroId, int benchSlot)
        {
            HeroId = heroId;
            BenchSlot = benchSlot;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var hero = draft.FindHero(HeroId);
            if (hero == null) return PlanningActionResult.Failure("That hero is no longer owned.");
            if (hero.Zone != PlanningZone.Field)
                return PlanningActionResult.Failure("That hero is already in reserve.");
            if (BenchSlot < 0 || BenchSlot >= draft.BenchCapacity)
                return PlanningActionResult.Failure("That reserve slot does not exist.");
            foreach (var other in draft.Heroes)
                if (other.Zone == PlanningZone.Bench && other.BenchSlot == BenchSlot)
                    return PlanningActionResult.Failure("That reserve slot is occupied.");

            hero.Zone = PlanningZone.Bench;
            hero.BenchSlot = BenchSlot;
            return PlanningActionResult.Success("Hero moved to reserve.");
        }
    }

    public sealed class MovePlanningHeroToFieldAction : IPlanningAction
    {
        public string HeroId { get; }
        public Hex Destination { get; }
        public string Kind => "move-hero-to-field";
        public bool IsReversible => true;

        public MovePlanningHeroToFieldAction(string heroId, Hex destination)
        {
            HeroId = heroId;
            Destination = destination;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var hero = draft.FindHero(HeroId);
            if (hero == null) return PlanningActionResult.Failure("That hero is no longer owned.");
            if (hero.Zone != PlanningZone.Bench)
                return PlanningActionResult.Failure("That hero is already fielded.");
            if (draft.FieldCount >= draft.FieldCapacity)
                return PlanningActionResult.Failure("The active lineup is full; replace a fielded hero.");
            if (!rules.IsLegalPosition(Destination))
                return PlanningActionResult.Failure("Choose a legal blue deployment hex.");
            foreach (var other in draft.Heroes)
                if (other.Zone == PlanningZone.Field && other.Position == Destination)
                    return PlanningActionResult.Failure("That hex is already occupied.");

            hero.Zone = PlanningZone.Field;
            hero.BenchSlot = -1;
            hero.Position = Destination;
            return PlanningActionResult.Success("Reserve joined the active lineup.");
        }
    }

    public sealed class MovePlanningBenchHeroAction : IPlanningAction
    {
        public string HeroId { get; }
        public int BenchSlot { get; }
        public string Kind => "move-bench-hero";
        public bool IsReversible => true;

        public MovePlanningBenchHeroAction(string heroId, int benchSlot)
        {
            HeroId = heroId;
            BenchSlot = benchSlot;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var hero = draft.FindHero(HeroId);
            if (hero == null) return PlanningActionResult.Failure("That hero is no longer owned.");
            if (hero.Zone != PlanningZone.Bench)
                return PlanningActionResult.Failure("Only a reserve can move between bench slots.");
            if (BenchSlot < 0 || BenchSlot >= draft.BenchCapacity)
                return PlanningActionResult.Failure("That reserve slot does not exist.");
            if (hero.BenchSlot == BenchSlot)
                return PlanningActionResult.Success();

            PlanningHeroState? occupant = null;
            foreach (var other in draft.Heroes)
                if (other.Id != hero.Id &&
                    other.Zone == PlanningZone.Bench &&
                    other.BenchSlot == BenchSlot)
                {
                    occupant = other;
                    break;
                }

            int origin = hero.BenchSlot;
            hero.BenchSlot = BenchSlot;
            if (occupant != null)
                occupant.BenchSlot = origin;
            return PlanningActionResult.Success(
                occupant == null ? "Reserve reordered." : "Reserves swapped slots.");
        }
    }

    public sealed class SetPlanningLoadoutOptionAction : IPlanningAction
    {
        public string HeroId { get; }
        public string SlotId { get; }
        public string OptionId { get; }
        public string Kind => "set-loadout-option";
        public bool IsReversible => true;

        public SetPlanningLoadoutOptionAction(string heroId, string slotId, string optionId)
        {
            HeroId = heroId;
            SlotId = slotId;
            OptionId = optionId;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var hero = draft.FindHero(HeroId);
            if (hero == null) return PlanningActionResult.Failure("That hero is no longer owned.");
            if (!rules.CanSetLoadoutOption(draft, hero, SlotId, OptionId, out string reason))
                return PlanningActionResult.Failure(reason);
            hero.Loadout[SlotId] = OptionId;
            return PlanningActionResult.Success("Loadout updated.");
        }
    }

    /// <summary>
    /// Generic future-facing consumable seam. Content rules decide whether the resource supports
    /// the intent and target. The resource decrement and queued intent live in the reversible
    /// draft, so backing out before Fight restores the charge automatically.
    /// </summary>
    public sealed class UsePlanningResourceAction : IPlanningAction
    {
        public string ResourceId { get; }
        public string IntentKind { get; }
        public string TargetId { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
        public string Kind => "use-resource";
        public bool IsReversible => true;

        public UsePlanningResourceAction(
            string resourceId,
            string intentKind,
            string targetId,
            IReadOnlyDictionary<string, string>? parameters = null)
        {
            ResourceId = resourceId;
            IntentKind = intentKind;
            TargetId = targetId;
            var owned = new Dictionary<string, string>();
            if (parameters != null)
                foreach (var pair in parameters)
                    owned[pair.Key] = pair.Value;
            Parameters = owned;
        }

        public PlanningActionResult Apply(PlanningDraft draft, PlanningRules rules)
        {
            var resource = draft.FindResource(ResourceId);
            if (resource == null)
                return PlanningActionResult.Failure("That resource is no longer available.");
            if (resource.Quantity <= 0)
                return PlanningActionResult.Failure("That resource has no uses remaining.");
            if (!rules.CanUseResource(
                    draft,
                    resource,
                    IntentKind,
                    TargetId,
                    Parameters,
                    out string reason))
                return PlanningActionResult.Failure(reason);

            resource.Quantity--;
            var intent = new PlanningIntent
            {
                Kind = IntentKind,
                SourceId = resource.Id,
                TargetId = TargetId,
            };
            foreach (var pair in Parameters)
                intent.Parameters[pair.Key] = pair.Value;
            draft.Intents.Add(intent);
            return PlanningActionResult.Success("Use queued for this fight.");
        }
    }
}
