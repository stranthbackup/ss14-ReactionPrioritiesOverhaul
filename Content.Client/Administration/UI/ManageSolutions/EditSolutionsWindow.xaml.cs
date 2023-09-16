using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.AutoGenerated;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Administration.UI.ManageSolutions
{
    /// <summary>
    ///     A simple window that displays solutions and their contained reagents. Allows you to edit the reagent quantities and add new reagents.
    /// </summary>
    [GenerateTypedNameReferences]
    public sealed partial class EditSolutionsWindow : DefaultWindow
    {
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private NetEntity _target = NetEntity.Invalid;
        private string? _selectedSolution;
        private AddReagentWindow? _addReagentWindow;
        private Dictionary<string, Solution>? _solutions;

        public EditSolutionsWindow()
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            SolutionOption.OnItemSelected += SolutionSelected;
            AddButton.OnPressed += OpenAddReagentWindow;
        }

        public override void Close()
        {
            base.Close();
            _addReagentWindow?.Close();
            _addReagentWindow?.Dispose();
        }

        public void SetTargetEntity(NetEntity target)
        {
            _target = target;
            var uid = _entityManager.GetEntity(target);

            var targetName = _entityManager.EntityExists(uid)
                ? _entityManager.GetComponent<MetaDataComponent>(uid).EntityName
                : string.Empty;

            Title = Loc.GetString("admin-solutions-window-title", ("targetName", targetName));
        }

        /// <summary>
        ///     Update the capacity label and re-create the reagent list
        /// </summary>
        public void UpdateReagents()
        {
            ReagentList.DisposeAllChildren();

            if (_selectedSolution == null || _solutions == null)
                return;

            if (!_solutions.TryGetValue(_selectedSolution, out var solution))
                return;

            UpdateVolumeBox(solution);
            UpdateThermalBox(solution);

            foreach (var reagent in solution)
            {
                AddReagentEntry(reagent);
            }
        }

        /// <summary>
        ///     Updates the entry displaying the current and maximum volume of the selected solution.
        /// </summary>
        /// <param name="solution">The selected solution.</param>
        private void UpdateVolumeBox(Solution solution)
        {
            VolumeBox.DisposeAllChildren();

            var volumeLabel = new Label();
            volumeLabel.HorizontalExpand = true;
            volumeLabel.Margin = new Thickness(0, 4);
            volumeLabel.Text = Loc.GetString("admin-solutions-window-volume-label",
                ("currentVolume", solution.Volume),
                ("maxVolume", solution.MaxVolume));

            var capacityBox = new BoxContainer();
            capacityBox.Orientation = BoxContainer.LayoutOrientation.Horizontal;
            capacityBox.HorizontalExpand = true;
            capacityBox.Margin = new Thickness(0, 4);

            var capacityLabel = new Label();
            capacityLabel.HorizontalExpand = true;
            capacityLabel.Margin = new Thickness(0, 1);
            capacityLabel.Text = Loc.GetString("admin-solutions-window-capacity-label");

            var capacitySpin = new FloatSpinBox(1, 2);
            capacitySpin.HorizontalExpand = true;
            capacitySpin.Margin = new Thickness(0, 1);
            capacitySpin.Value = (float) solution.MaxVolume;
            capacitySpin.OnValueChanged += SetCapacity;

            capacityBox.AddChild(capacityLabel);
            capacityBox.AddChild(capacitySpin);

            VolumeBox.AddChild(volumeLabel);
            VolumeBox.AddChild(capacityBox);
        }

        /// <summary>
        ///     Updates the entry displaying the current specific heat, heat capacity, temperature, and thermal energy
        ///     of the selected solution.
        /// </summary>
        /// <param name="solution">The selected solution.</param>
        private void UpdateThermalBox(Solution solution)
        {
            ThermalBox.DisposeAllChildren();
            var heatCap = solution.GetHeatCapacity(null);
            var specificHeatLabel = new Label();
            specificHeatLabel.HorizontalExpand = true;
            specificHeatLabel.Margin = new Thickness(0, 1);
            specificHeatLabel.Text = Loc.GetString("admin-solutions-window-specific-heat-label", ("specificHeat", heatCap.ToString("G3")));

            var heatCapacityLabel = new Label();
            heatCapacityLabel.HorizontalExpand = true;
            heatCapacityLabel.Margin = new Thickness(0, 1);
            heatCapacityLabel.Text = Loc.GetString("admin-solutions-window-heat-capacity-label", ("heatCapacity", (heatCap/solution.Volume.Float()).ToString("G3")));

            // Temperature entry:
            var temperatureBox = new BoxContainer();
            temperatureBox.Orientation = BoxContainer.LayoutOrientation.Horizontal;
            temperatureBox.HorizontalExpand = true;
            temperatureBox.Margin = new Thickness(0, 1);

            var temperatureLabel = new Label();
            temperatureLabel.HorizontalExpand = true;
            temperatureLabel.Margin = new Thickness(0, 1);
            temperatureLabel.Text = Loc.GetString("admin-solutions-window-temperature-label");

            var temperatureSpin = new FloatSpinBox(1, 2);
            temperatureSpin.HorizontalExpand = true;
            temperatureSpin.Margin = new Thickness(0, 1);
            temperatureSpin.Value = solution.Temperature;
            temperatureSpin.OnValueChanged += SetTemperature;

            temperatureBox.AddChild(temperatureLabel);
            temperatureBox.AddChild(temperatureSpin);

            // Thermal energy entry:
            var thermalEnergyBox = new BoxContainer();
            thermalEnergyBox.Orientation = BoxContainer.LayoutOrientation.Horizontal;
            thermalEnergyBox.HorizontalExpand = true;
            thermalEnergyBox.Margin = new Thickness(0, 1);

            var thermalEnergyLabel = new Label();
            thermalEnergyLabel.HorizontalExpand = true;
            thermalEnergyLabel.Margin = new Thickness(0, 1);
            thermalEnergyLabel.Text = Loc.GetString("admin-solutions-window-thermal-energy-label");

            var thermalEnergySpin = new FloatSpinBox(1, 2);
            thermalEnergySpin.HorizontalExpand = true;
            thermalEnergySpin.Margin = new Thickness(0, 1);
            thermalEnergySpin.Value = solution.Temperature * heatCap;
            thermalEnergySpin.OnValueChanged += SetThermalEnergy;

            thermalEnergyBox.AddChild(thermalEnergyLabel);
            thermalEnergyBox.AddChild(thermalEnergySpin);

            ThermalBox.AddChild(specificHeatLabel);
            ThermalBox.AddChild(heatCapacityLabel);
            ThermalBox.AddChild(temperatureBox);
            ThermalBox.AddChild(thermalEnergyBox);
        }

        /// <summary>
        ///     Add a single reagent entry to the list
        /// </summary>
        private void AddReagentEntry(ReagentQuantity reagentQuantity)
        {
            var box = new BoxContainer();
            var spin = new FloatSpinBox(1, 2);

            spin.Value = reagentQuantity.Quantity.Float();
            spin.OnValueChanged += (args) => SetReagent(args, reagentQuantity.Reagent.Prototype);
            spin.HorizontalExpand = true;

            box.AddChild(new Label() { Text = reagentQuantity.Reagent.Prototype , HorizontalExpand = true});
            box.AddChild(spin);

            ReagentList.AddChild(box);
        }

        /// <summary>
        ///     Execute a command to modify the reagents in the solution.
        /// </summary>
        private void SetReagent(FloatSpinBox.FloatSpinBoxEventArgs args, string prototype)
        {
            if (_solutions == null || _selectedSolution == null)
                return;

            var current = _solutions[_selectedSolution].GetTotalPrototypeQuantity(prototype);
            var delta = args.Value - current.Float();

            if (MathF.Abs(delta) < 0.01)
                return;

            var command = $"addreagent {_target} {_selectedSolution} {prototype} {delta}";
            _consoleHost.ExecuteCommand(command);
        }

        private void SetCapacity(FloatSpinBox.FloatSpinBoxEventArgs args)
        {
            if (_solutions == null || _selectedSolution == null)
                return;

            var command = $"setsolutioncapacity {_target} {_selectedSolution} {args.Value}";
            _consoleHost.ExecuteCommand(command);
        }

        /// <summary>
        ///     Sets the temperature of the selected solution to a value.
        /// </summary>
        /// <param name="args">An argument struct containing the value to set the temperature to.</param>
        private void SetTemperature(FloatSpinBox.FloatSpinBoxEventArgs args)
        {
            if (_solutions == null || _selectedSolution == null)
                return;

            var command = $"setsolutiontemperature {_target} {_selectedSolution} {args.Value}";
            _consoleHost.ExecuteCommand(command);
        }

        /// <summary>
        ///     Sets the thermal energy of the selected solution to a value.
        /// </summary>
        /// <param name="args">An argument struct containing the value to set the thermal energy to.</param>
        private void SetThermalEnergy(FloatSpinBox.FloatSpinBoxEventArgs args)
        {
            if (_solutions == null || _selectedSolution == null)
                return;

            var command = $"setsolutionthermalenergy {_target} {_selectedSolution} {args.Value}";
            _consoleHost.ExecuteCommand(command);
        }

        /// <summary>
        ///     Open a new window that has options to add new reagents to the solution.
        /// </summary>
        private void OpenAddReagentWindow(BaseButton.ButtonEventArgs obj)
        {
            if (string.IsNullOrEmpty(_selectedSolution))
                return;

            _addReagentWindow?.Close();
            _addReagentWindow?.Dispose();

            _addReagentWindow = new AddReagentWindow(_target, _selectedSolution);
            _addReagentWindow.OpenCentered();
        }

        /// <summary>
        ///     When a new solution is selected, set _selectedSolution and update the reagent list.
        /// </summary>
        private void SolutionSelected(OptionButton.ItemSelectedEventArgs args)
        {
            SolutionOption.SelectId(args.Id);
            _selectedSolution = (string?) SolutionOption.SelectedMetadata;
            _addReagentWindow?.UpdateSolution(_selectedSolution);
            UpdateReagents();
        }

        /// <summary>
        ///     Update the solution options.
        /// </summary>
        public void UpdateSolutions(Dictionary<string, Solution>? solutions)
        {
            SolutionOption.Clear();
            _solutions = solutions;

            if (_solutions == null)
                return;

            int i = 0;
            foreach (var solution in _solutions.Keys)
            {
                SolutionOption.AddItem(solution, i);
                SolutionOption.SetItemMetadata(i, solution);

                if (solution == _selectedSolution)
                    SolutionOption.Select(i);

                i++;
            }

            if (SolutionOption.ItemCount == 0)
            {
                // No applicable solutions
                Close();
                Dispose();
            }

            if (_selectedSolution == null || !_solutions.ContainsKey(_selectedSolution))
            {
                // the previously selected solution is no longer valid.
                SolutionOption.Select(0);
                _selectedSolution = (string?) SolutionOption.SelectedMetadata;
            }
        }
    }
}
