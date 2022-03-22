using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CharacterAppearance;
using Content.Client.Stylesheets;
using Content.Shared.CharacterAppearance;
using Content.Shared.Markings;
using Content.Shared.Species;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Markings
{
    [GenerateTypedNameReferences]
    public sealed partial class MarkingPicker : Control
    {
        [Dependency] private readonly MarkingManager _markingManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        
        public Action<List<Marking>>? OnMarkingAdded;
        public Action<List<Marking>>? OnMarkingRemoved;
        public Action<List<Marking>>? OnMarkingColorChange;
        public Action<List<Marking>>? OnMarkingRankChange;
        
        private List<Color> _currentMarkingColors = new();

        private Dictionary<MarkingCategories, MarkingPoints> PointLimits = new();
        private Dictionary<MarkingCategories, MarkingPoints> PointsUsed = new();

        private ItemList.Item? _selectedMarking;
        private ItemList.Item? _selectedUnusedMarking;
        private MarkingCategories _selectedMarkingCategory = MarkingCategories.Chest;
        private List<Marking> _usedMarkingList = new();

        private List<MarkingCategories> _markingCategories = Enum.GetValues<MarkingCategories>().ToList();

        private string _currentSpecies = SpeciesManager.DefaultSpecies; // mmmmm

        public void SetData(List<Marking> newMarkings, string species)
        {
            _usedMarkingList = newMarkings;
            _currentSpecies = species;

            SpeciesPrototype speciesPrototype = _prototypeManager[species];
            EntityPrototype body = _prototypeManager[speciesPrototype.Prototype];

            body.TryGetComponent("Markings", out MarkingsComponent? markingsComponent);

            PointLimits = markingsComponent!.LayerPoints;

            foreach (var (category, points) in PointLimits)
            {
                PointsUsed[category] = new MarkingPoints()
                {
                    Points = points.Points,
                    Required = points.Required,
                    DefaultMarkings = points.DefaultMarkings 
                };
            }

            Populate();
            List<Marking> toRemove = PopulateUsed();

            if (toRemove.Count != 0)
            {
                foreach (var marking in toRemove)
                {
                    _usedMarkingList.Remove(marking);
                }

                OnMarkingRemoved?.Invoke(_usedMarkingList);
            }
        }

        public MarkingPicker()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            for (int i = 0; i < _markingCategories.Count; i++)
            {
                CMarkingCategoryButton.AddItem(Loc.GetString($"markings-category-{_markingCategories[i].ToString()}"), i);
            }
            CMarkingCategoryButton.SelectId(_markingCategories.IndexOf(MarkingCategories.Chest));
            CMarkingCategoryButton.OnItemSelected +=  OnCategoryChange;
            CMarkingsUnused.OnItemSelected += item =>
               _selectedUnusedMarking = CMarkingsUnused[item.ItemIndex];

            CMarkingAdd.OnPressed += args =>
                MarkingAdd();

            CMarkingsUsed.OnItemSelected += OnUsedMarkingSelected;

            CMarkingRemove.OnPressed += args =>
                MarkingRemove();

            CMarkingRankUp.OnPressed += _ => SwapMarkingUp();
            CMarkingRankDown.OnPressed += _ => SwapMarkingDown();
        }

        private string GetMarkingName(MarkingPrototype marking) => Loc.GetString($"marking-{marking.ID}");
        private List<string> GetMarkingStateNames(MarkingPrototype marking)
        {
            List<string> result = new(); 
            foreach (var markingState in marking.Sprites)
            {
                switch (markingState)
                {
                    case SpriteSpecifier.Rsi rsi:
                        result.Add(Loc.GetString($"marking-{marking.ID}-{rsi.RsiState}"));
                        break;
                    case SpriteSpecifier.Texture texture:
                        result.Add(Loc.GetString($"marking-{marking.ID}-{texture.TexturePath.Filename}"));
                        break;
                }
            }

            return result;
        }

        public void Populate()
        {
            CMarkingsUnused.Clear();
            _selectedUnusedMarking = null;

            var markings = _markingManager.CategorizedMarkings();
            foreach (var marking in markings[_selectedMarkingCategory])
            {
                if (_usedMarkingList.Contains(marking.AsMarking())) continue;
                if (!marking.SpeciesRestrictions.Contains(_currentSpecies) && !marking.Unrestricted) continue;
                var item = CMarkingsUnused.AddItem($"{GetMarkingName(marking)}", marking.Sprites[0].Frame0());
                item.Metadata = marking;
            }

            if (PointsUsed.ContainsKey(_selectedMarkingCategory))
            {
                CMarkingPoints.Visible = true;
            }
            else
            {
                CMarkingPoints.Visible = false;
            }
        }

        // Populate the used marking list. Returns a list of markings that weren't
        // valid to add to the marking list.
        public List<Marking> PopulateUsed()
        {
            CMarkingsUsed.Clear();
            CMarkingColors.Visible = false;
            _selectedMarking = null;

            List<Marking> toRemove = new();
            for (var i = 0; i < _usedMarkingList.Count; i++)
            {
                var marking = _usedMarkingList[i];
                if (_markingManager.IsValidMarking(marking, out MarkingPrototype? newMarking))
                {
                    if (!newMarking.SpeciesRestrictions.Contains(_currentSpecies) && !newMarking.Unrestricted)
                    {
                        toRemove.Add(marking);
                        continue;
                    }

                    if (PointsUsed.TryGetValue(newMarking.MarkingCategory, out var points))
                    {
                        if (points.Points == 0)
                        {
                            continue;
                        }

                        points.Points--;
                    }

                    var _item = new ItemList.Item(CMarkingsUsed)
                    {
                        Text = Loc.GetString("marking-used", ("marking-name", $"{GetMarkingName(newMarking)}"), ("marking-category", Loc.GetString($"markings-category-{newMarking.MarkingCategory}"))), 
                        Icon = newMarking.Sprites[0].Frame0(),
                        Selectable = true,
                        Metadata = newMarking,
                        IconModulate = marking.MarkingColors[0]
                    };
                    CMarkingsUsed.Insert(0, _item);

                    if (marking.MarkingColors.Count != _usedMarkingList[i].MarkingColors.Count)
                    {
                        _usedMarkingList[i] = new Marking(marking.MarkingId, marking.MarkingColors);
                    }

                    foreach (var unusedMarking in CMarkingsUnused)
                    {
                        if (unusedMarking.Metadata == newMarking)
                        {
                            CMarkingsUnused.Remove(unusedMarking);
                            break;
                        }
                    }

                }
                else
                {
                    toRemove.Add(marking);
                }
            }

            UpdatePoints();
            
            return toRemove;
        }

        private void SwapMarkingUp()
        {
            if (_selectedMarking == null)
            {
                return;
            }

            var i = CMarkingsUsed.IndexOf(_selectedMarking);
            if (ShiftMarkingRank(i, -1))
            {
                OnMarkingRankChange?.Invoke(_usedMarkingList);
            }
        }

        private void SwapMarkingDown()
        {
            if (_selectedMarking == null)
            {
                return;
            }

            var i = CMarkingsUsed.IndexOf(_selectedMarking);
            if (ShiftMarkingRank(i, 1))
            {
                OnMarkingRankChange?.Invoke(_usedMarkingList);
            }
        }

        private bool ShiftMarkingRank(int src, int places)
        {
            if (src + places >= CMarkingsUsed.Count || src + places < 0)
            {
                return false;
            }

            var visualDest = src + places; // what it would visually look like
            var visualTemp = CMarkingsUsed[visualDest];
            CMarkingsUsed[visualDest] = CMarkingsUsed[src];
            CMarkingsUsed[src] = visualTemp;

            var backingSrc = _usedMarkingList.Count - 1 - src; // what it actually needs to be
            var backingDest = backingSrc - places;             
            var backingTemp = _usedMarkingList[backingDest];
            _usedMarkingList[backingDest] = _usedMarkingList[backingSrc];
            _usedMarkingList[backingSrc] = backingTemp;
            
            return true;
        }

        // repopulate in case markings are restricted,
        // and also filter out any markings that are now invalid
        // attempt to preserve any existing markings as well:
        // it would be frustrating to otherwise have all markings
        // cleared, imo
        public void SetSpecies(string species)
        {
            _currentSpecies = species;
            var markingCount = _usedMarkingList.Count;

            SpeciesPrototype speciesPrototype = _prototypeManager[species];
            EntityPrototype body = _prototypeManager[speciesPrototype.Prototype];
            
            body.TryGetComponent("Markings", out MarkingsComponent? markingsComponent);

            PointLimits = markingsComponent!.LayerPoints;

            foreach (var (category, points) in PointLimits)
            {
                PointsUsed[category] = new MarkingPoints()
                {
                    Points = points.Points,
                    Required = points.Required,
                    DefaultMarkings = points.DefaultMarkings 
                };
            }
            
            Populate();
            List<Marking> toRemove = PopulateUsed();
            
            if (toRemove.Count != 0)
            {
                foreach (var i in toRemove)
                {
                    _usedMarkingList.Remove(i);
                }
                
                OnMarkingRemoved?.Invoke(_usedMarkingList);
            }
        }

        private void UpdatePoints()
        {
            if (PointsUsed.TryGetValue(_selectedMarkingCategory, out var pointsRemaining))
            {
                CMarkingPoints.Text = Loc.GetString("marking-points-remaining", ("points", pointsRemaining.Points));
            }
        }

        private void OnCategoryChange(OptionButton.ItemSelectedEventArgs category)
        {
            CMarkingCategoryButton.SelectId(category.Id);
            _selectedMarkingCategory = _markingCategories[category.Id];
            Populate();
            UpdatePoints();
        }

        private void OnUsedMarkingSelected(ItemList.ItemListSelectedEventArgs item)
        {
            _selectedMarking = CMarkingsUsed[item.ItemIndex];
            var prototype = (MarkingPrototype) _selectedMarking.Metadata!;

            if (prototype.FollowSkinColor)
            {
                CMarkingColors.Visible = false;

                return;
            }

            var stateNames = GetMarkingStateNames(prototype);
            _currentMarkingColors.Clear();
            CMarkingColors.DisposeAllChildren();
            List<List<ColorSlider>> colorSliders = new();
            for (int i = 0; i < prototype.Sprites.Count; i++)
            {
                var colorContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                };

                CMarkingColors.AddChild(colorContainer);

                List<ColorSlider> sliders = new();
                ColorSlider colorSliderR = new ColorSlider(StyleNano.StyleClassSliderRed);
                ColorSlider colorSliderG = new ColorSlider(StyleNano.StyleClassSliderGreen);
                ColorSlider colorSliderB = new ColorSlider(StyleNano.StyleClassSliderBlue);

                colorContainer.AddChild(new Label { Text = $"{stateNames[i]} color:" });
                colorContainer.AddChild(colorSliderR);
                colorContainer.AddChild(colorSliderG);
                colorContainer.AddChild(colorSliderB);

                var currentColor = new Color(
                    _usedMarkingList[_usedMarkingList.Count - 1 - item.ItemIndex].MarkingColors[i].RByte,
                    _usedMarkingList[_usedMarkingList.Count - 1 - item.ItemIndex].MarkingColors[i].GByte,
                    _usedMarkingList[_usedMarkingList.Count - 1 - item.ItemIndex].MarkingColors[i].BByte
                );
                _currentMarkingColors.Add(currentColor);
                int colorIndex = _currentMarkingColors.IndexOf(currentColor);

                colorSliderR.ColorValue = currentColor.RByte;
                colorSliderG.ColorValue = currentColor.GByte;
                colorSliderB.ColorValue = currentColor.BByte;

                Action colorChanged = delegate()
                {
                    _currentMarkingColors[colorIndex] = new Color(
                        colorSliderR.ColorValue,
                        colorSliderG.ColorValue,
                        colorSliderB.ColorValue
                    );

                    ColorChanged(colorIndex);
                };
                colorSliderR.OnValueChanged += colorChanged;
                colorSliderG.OnValueChanged += colorChanged;
                colorSliderB.OnValueChanged += colorChanged;
            }

            CMarkingColors.Visible = true;
        }

        private void ColorChanged(int colorIndex)
        {
            if (_selectedMarking is null) return;
            var markingPrototype = (MarkingPrototype) _selectedMarking.Metadata!;
            int markingIndex = _usedMarkingList.FindIndex(m => m.MarkingId == markingPrototype.ID);

            if (markingIndex < 0) return;

            _selectedMarking.IconModulate = _currentMarkingColors[colorIndex];
            _usedMarkingList[markingIndex].SetColor(colorIndex, _currentMarkingColors[colorIndex]);
            OnMarkingColorChange?.Invoke(_usedMarkingList);
        }

        private void MarkingAdd()
        {
            if (_usedMarkingList is null || _selectedUnusedMarking is null) return;

            MarkingPrototype marking = (MarkingPrototype) _selectedUnusedMarking.Metadata!;

            if (PointsUsed.TryGetValue(marking.MarkingCategory, out var points))
            {
                if (points.Points == 0)
                {
                    return;
                }

                points.Points--;
            }

            UpdatePoints();

            _usedMarkingList.Add(marking.AsMarking());

            CMarkingsUnused.Remove(_selectedUnusedMarking);
            var item = new ItemList.Item(CMarkingsUsed)
            {
                Text = Loc.GetString("marking-used", ("marking-name", $"{GetMarkingName(marking)}"), ("marking-category", Loc.GetString($"markings-category-{marking.MarkingCategory}"))), 
                Icon = marking.Sprites[0].Frame0(),
                Selectable = true,
                Metadata = marking,
            };
            CMarkingsUsed.Insert(0, item);
            
            _selectedUnusedMarking = null;
            OnMarkingAdded?.Invoke(_usedMarkingList);
        }

        private void MarkingRemove()
        {
            if (_usedMarkingList is null || _selectedMarking is null) return;

            MarkingPrototype marking = (MarkingPrototype) _selectedMarking.Metadata!;

            if (PointsUsed.TryGetValue(marking.MarkingCategory, out var points))
            {
                points.Points++;
            }

            UpdatePoints();

            _usedMarkingList.Remove(marking.AsMarking());
            CMarkingsUsed.Remove(_selectedMarking);

            if (marking.MarkingCategory == _selectedMarkingCategory)
            {
                var item = CMarkingsUnused.AddItem($"{GetMarkingName(marking)}", marking.Sprites[0].Frame0());
                item.Metadata = marking;
            }
            _selectedMarking = null;
            CMarkingColors.Visible = false;
            OnMarkingRemoved?.Invoke(_usedMarkingList);
        }
    }
}
