﻿using System.Collections.Generic;
using System.Linq;
using PawnRules.Data;
using PawnRules.Patch;
using UnityEngine;
using Verse;

namespace PawnRules.Interface
{
    internal class Dialog_Rules : WindowPlus
    {
        private const float OptionButtonSize = 80f;

        private readonly Pawn _pawn;

        private readonly Listing_Preset<Rules> _preset;
        private readonly Listing_StandardPlus _addons = new Listing_StandardPlus();

        private readonly List<FloatMenuOption> _floatMenuViews = new List<FloatMenuOption>();
        private List<FloatMenuOption> _floatMenuAssign;

        private PawnType _type;
        private Rules _template;
        private Rules _personalized;

        private Dialog_Rules(Pawn pawn, Rules rules) : base(new Vector2(700f, 600f))
        {
            _pawn = pawn;

            _personalized = rules.CloneRulesFor(_pawn);
            _template = rules.IsPreset ? rules.ClonePreset() : _personalized;

            _preset = new Listing_Preset<Rules>(rules.Type, rules.IsPreset ? rules : _personalized, new[] { _personalized }, UpdateSelected, CommitTemplate, RevertTemplate);

            if (_pawn != null) { _floatMenuViews.Add(new FloatMenuOption(Lang.Get("PawnType.Individual"), () => ChangeType(null))); }
            foreach (var type in PawnType.List) { _floatMenuViews.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.DefaultType", type.Label), () => ChangeType(type))); }

            _floatMenuAssign = GetAssignmentOptions();
        }

        public static void OpenFromPawn(Pawn pawn) => Find.WindowStack.Add(new Dialog_Rules(pawn, Registry.GetOrCreateRules(pawn)));

        private void ChangeType(PawnType type)
        {
            _preset.Type = type ?? _pawn.GetTargetType();
            _type = type;
            _preset.FixedPresets = _type == null ? new[] { _personalized } : new[] { Registry.GetVoidPreset<Rules>(_template.Type) };
            if (type == null)
            {
                var rules = Registry.GetOrCreateRules(_pawn);
                _preset.Selected = rules.IsPreset ? rules : _personalized;
            }
            else { _preset.Selected = Registry.GetDefaultRules(type); }

            UpdateTemplate();
        }

        private void ChangeRestriction(RestrictionType type)
        {
            var list = new List<FloatMenuOption>();

            var presets = Registry.GetPresets<Restriction>(type).Where(preset => preset != _template.GetRestriction(type));

            if (!presets.Any() && _template.GetRestriction(type).IsVoid)
            {
                Find.WindowStack.Add(new Dialog_Restrictions(type, _template));
                return;
            }

            list.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.EditRestriction"), () => Find.WindowStack.Add(new Dialog_Restrictions(type, _template))));

            var voidPreset = Registry.GetVoidPreset<Restriction>(type);
            if (!_template.GetRestriction(type).IsVoid) { list.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.ClearRestriction", voidPreset.Type.Categorization.ToLower()), () => _template.SetRestriction(type, voidPreset))); }
            list.AddRange(presets.Select(restriction => new FloatMenuOption(Lang.Get("Dialog_Rules.ChangeRestriction", restriction.Name.Bold()), () => _template.SetRestriction(type, restriction))));

            Find.WindowStack.Add(new FloatMenu(list));
        }

        private void CommitTemplate()
        {
            if (_preset.Selected == _personalized)
            {
                void OnCommit(Rules rules)
                {
                    rules.CopyRules(_personalized);
                    _preset.Selected = rules;
                    UpdateSelected();
                }

                Find.WindowStack.Add(new Dialog_PresetName<Rules>(_preset.Type, OnCommit));
                return;
            }

            _preset.Selected.CopyRules(_template);
            UpdateTemplate();
        }

        private void UpdateTemplate()
        {
            if (_preset.Selected.IsVoid && (_type == null))
            {
                _personalized = _preset.Selected.CloneRulesFor(_pawn);
                _preset.FixedPresets = new[] { _personalized };
                _preset.Selected = _personalized;
                Registry.ReplaceRules(_pawn, _preset.Selected);
            }

            _template = _preset.Selected.IsPreset ? _preset.Selected.ClonePreset() : _personalized;
            _floatMenuAssign = GetAssignmentOptions();
        }

        private void RevertTemplate()
        {
            if (_preset.Selected == _personalized) { _personalized.SetToVanilla(); }
            else { UpdateTemplate(); }
        }

        private void UpdateSelected()
        {
            if (_type == null) { Registry.ReplaceRules(_pawn, _preset.Selected); }
            else { Registry.ReplaceDefaultRules(_type, _preset.Selected); }

            UpdateTemplate();
        }

        private IEnumerable<Pawn> GetOtherPawnsOfType(bool byKind) => _type == null ? Find.CurrentMap.mapPawns.AllPawns.Where(pawn => (pawn != _pawn) && (pawn.GetTargetType() == _preset.Type) && (!byKind || (pawn.kindDef == _pawn.kindDef))) : Find.CurrentMap.mapPawns.AllPawns.Where(pawn => pawn.GetTargetType() == _type);

        private string GetPresetNameDefinite()
        {
            if (_preset.Selected == _personalized) { return Lang.Get("Dialog_Rules.AssignPersonalizedName"); }
            return _preset.Selected.IsVoid ? Lang.Get("Dialog_Rules.AssignVoidName") : Lang.Get("Dialog_Rules.AssignSpecificName", _preset.Selected.Name.Bold());
        }

        private List<FloatMenuOption> GetAssignmentOptions()
        {
            var options = new List<FloatMenuOption>();

            var otherPawnsOfType = GetOtherPawnsOfType(false);
            if (GetOtherPawnsOfType(false).Any()) { options.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.AssignAll", _preset.Type.LabelPlural.ToLower()), () => AssignAll(false))); }
            if ((_type == null) && _pawn.RaceProps.Animal && otherPawnsOfType.Any(kind => kind.kindDef == _pawn.kindDef)) { options.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.AssignAll", _pawn.kindDef.GetLabelPlural().ToLower()), () => AssignAll(true))); }
            options.AddRange(Find.CurrentMap.mapPawns.AllPawns.Where(pawn => ((_type != null) || (pawn != _pawn)) && (pawn.GetTargetType() == _preset.Type)).Select(pawn => new FloatMenuOption(Lang.Get("Dialog_Rules.AssignSpecific", pawn.Name.ToString().Italic()), () => AssignSpecific(pawn))));
            if ((_type == null) && _preset.Selected.IsPreset) { options.Add(new FloatMenuOption(Lang.Get("Dialog_Rules.AssignDefault", _preset.Type.LabelPlural.ToLower()), () => Find.WindowStack.Add(new Dialog_Alert(Lang.Get("Dialog_Rules.AssignDefaultConfirm", _preset.Type.LabelPlural.ToLower(), _preset.Selected.Name.Bold()), Dialog_Alert.Buttons.YesNo, () => Registry.SetDefaultRules(_preset.Selected))))); }

            return options;
        }

        private void AssignAll(bool byKind)
        {
            var pawns = GetOtherPawnsOfType(byKind);

            void OnAccept()
            {
                foreach (var pawn in pawns) { Registry.ReplaceRules(pawn, _preset.Selected.IsPreset ? _preset.Selected : _template.ClonePreset()); }
            }

            var count = pawns.Count();
            Find.WindowStack.Add(new Dialog_Alert(Lang.Get("Dialog_Rules.AssignAllConfirm", GetPresetNameDefinite(), count.ToString().Bold(), byKind ? _pawn.kindDef.GetLabelPlural(count) : count > 1 ? _preset.Selected.Type.LabelPlural : _preset.Selected.Type.Label), Dialog_Alert.Buttons.YesNo, OnAccept));
        }

        private void AssignSpecific(Pawn pawn)
        {
            void OnAccept() => Registry.ReplaceRules(pawn, _preset.Selected.IsPreset ? _preset.Selected : _template.ClonePreset());

            Find.WindowStack.Add(new Dialog_Alert(Lang.Get("Dialog_Rules.AssignSpecificConfirm", GetPresetNameDefinite(), pawn.Name.ToString().Italic()), Dialog_Alert.Buttons.YesNo, OnAccept));
        }

        private string GetRestrictionDisplayName(Presetable restriction) => restriction.IsPreset && !restriction.IsVoid ? restriction.Name.Bold() : restriction.Name;

        public override void Close(bool doCloseSound = true)
        {
            if (_preset.EditMode)
            {
                void OnAccept()
                {
                    CommitTemplate();
                    base.Close(doCloseSound);
                }

                void OnCancel()
                {
                    _preset.Revert();
                    base.Close(doCloseSound);
                }

                Find.WindowStack.Add(new Dialog_Alert(Lang.Get("Button.PresetSaveConfirm"), Dialog_Alert.Buttons.YesNo, OnAccept, OnCancel));
                return;
            }

            if (_preset.Selected == _personalized) { Registry.ReplaceRules(_pawn, _personalized); }
            base.Close(doCloseSound);
        }

        public override void DoContent(Rect rect)
        {
            if (!Registry.IsActive)
            {
                Close();
                return;
            }

            Title = _type == null ? Lang.Get("Dialog_Rules.Title", _pawn.Name.ToStringFull.Bold(), _preset.Type.Label) : Lang.Get("Dialog_Rules.TitleDefault", _type.LabelPlural.Bold());

            var listing = new Listing_StandardPlus();
            var hGrid = rect.GetHGrid(8f, 200f, 0f);
            var lGrid = hGrid[0].GetVGrid(4f, 42f, 0f);

            listing.Begin(lGrid[0]);
            listing.Label(Lang.Get("Preset.Header").Italic().Bold());
            listing.GapLine();
            listing.End();
            _preset.DoContent(lGrid[1]);

            var vGrid = hGrid[1].GetVGrid(4f, 42f, 0f, 62f);
            listing.Begin(vGrid[0]);
            listing.Label(Lang.Get("Dialog_Rules.Configuration").Italic().Bold());
            listing.GapLine();
            listing.End();

            var editMode = _preset.EditMode || (_template == _personalized);

            var color = GUI.color;
            if (!editMode) { GUI.color = GuiPlus.ReadOnlyColor; }

            listing.Begin(vGrid[1]);
            if (listing.ButtonText(Lang.Get("Rules.FoodRestrictions", GetRestrictionDisplayName(_template.GetRestriction(RestrictionType.Food))), Lang.Get("Rules.FoodRestrictionsDesc")) && editMode) { ChangeRestriction(RestrictionType.Food); }
            if (_template.Type == PawnType.Colonist)
            {
                if (listing.ButtonText(Lang.Get("Rules.BondingRestrictions", GetRestrictionDisplayName(_template.GetRestriction(RestrictionType.Bonding))), Lang.Get("Rules.BondingRestrictionsDesc")) && editMode) { ChangeRestriction(RestrictionType.Bonding); }
                listing.GapLine();
                listing.CheckboxLabeled(Lang.Get("Rules.AllowCourting"), ref _template.AllowCourting, Lang.Get("Rules.AllowCourtingDesc"), editMode);
                listing.CheckboxLabeled(Lang.Get("Rules.AllowArtisan"), ref _template.AllowArtisan, Lang.Get("Rules.AllowArtisanDesc"), editMode);
            }
            listing.GapLine();
            listing.End();

            if (_template.HasAddons)
            {
                var addonsRect = vGrid[1].GetVGrid(4f, listing.CurHeight, 0f)[1];
                _addons.Begin(addonsRect, addonsRect.height <= _template.AddonsRectHeight);
                GuiPlus.DoAddonsListing(_addons, _template, editMode);
                _addons.End();
            }

            GUI.color = color;

            var optionGrid = vGrid[2].GetVGrid(2f, 0f, 0f);
            listing.Begin(optionGrid[0]);
            if (listing.ButtonText(Lang.Get("Button.AssignTo"), Lang.Get("Button.AssignToDesc"), (_floatMenuAssign.Count > 0) && (!editMode || (_template == _personalized)))) { Find.WindowStack.Add(new FloatMenu(_floatMenuAssign)); }
            listing.End();

            listing.Begin(optionGrid[1]);
            if (listing.ButtonText(_type == null ? Lang.Get("Button.ViewType", Lang.Get("PawnType.Individual")) : Lang.Get("Button.ViewTypeDefault", _type.LabelPlural), Lang.Get("Button.ViewTypeDesc"), !editMode || (_template == _personalized))) { Find.WindowStack.Add(new FloatMenu(_floatMenuViews)); }
            listing.End();

            GUI.EndGroup();

            if (GuiPlus.ButtonText(new Rect(rect.xMax - (80f - Margin), rect.yMax + (Margin * 2), OptionButtonSize, CloseButSize.y), Lang.Get("Button.GlobalOptions"), Lang.Get("Button.GlobalOptionsDesc"))) { Find.WindowStack.Add(new Dialog_Global()); }
            GUI.BeginGroup(windowRect);
        }
    }
}
