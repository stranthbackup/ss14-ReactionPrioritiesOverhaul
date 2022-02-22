using System.Collections.Generic;
using System.Text;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using static Content.Shared.MedicalScanner.SharedMedicalScannerComponent;

namespace Content.Client.MedicalScanner.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class MedicalScannerWindow : DefaultWindow
    {
        public MedicalScannerWindow()
        {
            RobustXamlLoader.Load(this);
        }

        public void Populate(MedicalScannerBoundUserInterfaceState state)
        {
            var text = new StringBuilder();

            var entities = IoCManager.Resolve<IEntityManager>();
            if (!state.Entity.HasValue ||
                !state.HasDamage() ||
                !entities.EntityExists(state.Entity.Value))
            {
                Diagnostics.Text = Loc.GetString("medical-scanner-window-no-patient-data-text");
                ScanButton.Disabled = true;
                SetSize = (250, 100);
            }
            else
            {
                text.Append($"{Loc.GetString("medical-scanner-window-entity-health-text", ("entityName", entities.GetComponent<MetaDataComponent>(state.Entity.Value).EntityName))}\n");

                var totalDamage = state.DamagePerType.Values.Sum();

                text.Append($"{Loc.GetString("medical-scanner-window-entity-damage-total-text", ("amount", totalDamage))}\n");

                HashSet<string> shownTypes = new();

                // Show the total damage and type breakdown for each damage group.
                foreach (var (damageGroupId, damageAmount) in state.DamagePerGroup)
                {
                    text.Append($"\n{Loc.GetString("medical-scanner-window-damage-group-text", ("damageGroup", damageGroupId), ("amount", damageAmount))}");

                    // Show the damage for each type in that group.
                    var group = IoCManager.Resolve<IPrototypeManager>().Index<DamageGroupPrototype>(damageGroupId);
                    foreach (var type in group.DamageTypes)
                    {
                        if (state.DamagePerType.TryGetValue(type, out var typeAmount))
                        {
                            // If damage types are allowed to belong to more than one damage group, they may appear twice here. Mark them as duplicate.
                            if (!shownTypes.Contains(type))
                            {
                                shownTypes.Add(type);
                                text.Append($"\n- {Loc.GetString("medical-scanner-window-damage-type-text", ("damageType", type), ("amount", typeAmount))}");
                            }
                            else {
                                text.Append($"\n- {Loc.GetString("medical-scanner-window-damage-type-duplicate-text", ("damageType", type), ("amount", typeAmount))}");
                            }
                        }
                    }
                    text.Append('\n');
                }

                Diagnostics.Text = text.ToString();
                ScanButton.Disabled = state.IsScanned;

                // TODO MEDICALSCANNER resize window based on the length of text / number of damage types?
                // Also, maybe add color schemes for specific damage groups?
                SetSize = (250, 600);
            }
        }
    }
}
