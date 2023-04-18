using Content.Server.Objectives.Interfaces;
using Content.Server.GameTicking.Rules;

namespace Content.Server.Objectives.Requirements
{
    [DataDefinition]
    public sealed class MultipleTraitorsRequirement : IObjectiveRequirement
    {
        [DataField("traitors")]
        private readonly int _requiredTraitors = 2;

        public bool CanBeAssigned(Mind.Mind mind)
        {
            //todo figure this shit out
            return true; //EntitySystem.Get<TraitorRuleSystem>().TotalTraitors >= _requiredTraitors;
        }
    }
}
