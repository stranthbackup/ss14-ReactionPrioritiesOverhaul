using System.Collections.Generic;
using Content.Server.Construction.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Server.Construction
{
    public partial class ConstructionSystem
    {
        private readonly Dictionary<ConstructionPrototype, ConstructionGuide> _guideCache = new();

        private void InitializeGuided()
        {
            SubscribeNetworkEvent<RequestConstructionGuide>(OnGuideRequested);
            SubscribeLocalEvent<ConstructionComponent, GetOtherVerbsEvent>(AddDeconstructVerb);
            SubscribeLocalEvent<ConstructionComponent, ExaminedEvent>(HandleConstructionExamined);
        }

        private void OnGuideRequested(RequestConstructionGuide msg, EntitySessionEventArgs args)
        {
            if (!_prototypeManager.TryIndex(msg.ConstructionId, out ConstructionPrototype? prototype))
                return;

            if(GetGuide(prototype) is {} guide)
                RaiseNetworkEvent(new ResponseConstructionGuide(msg.ConstructionId, guide), args.SenderSession.ConnectedClient);
        }

        private void AddDeconstructVerb(EntityUid uid, ConstructionComponent component, GetOtherVerbsEvent args)
        {
            if (!args.CanAccess)
                return;

            if (component.TargetNode == component.DeconstructionNode ||
                component.Node == component.DeconstructionNode)
                return;

            Verb verb = new();
            //verb.Category = VerbCategories.Construction;
            //TODO VERBS add more construction verbs? Until then, removing construction category
            verb.Text = Loc.GetString("deconstructible-verb-begin-deconstruct");
            verb.IconTexture = "/Textures/Interface/hammer_scaled.svg.192dpi.png";

            verb.Act = () =>
            {
                SetPathfindingTarget(uid, component.DeconstructionNode, component);
                if (component.TargetNode == null)
                {
                    // Maybe check, but on the flip-side a better solution might be to not make it undeconstructible in the first place, no?
                    component.Owner.PopupMessage(args.User, Loc.GetString("deconstructible-verb-activate-no-target-text"));
                }
                else
                {
                    component.Owner.PopupMessage(args.User, Loc.GetString("deconstructible-verb-activate-text"));
                }
            };

            args.Verbs.Add(verb);
        }

        private void HandleConstructionExamined(EntityUid uid, ConstructionComponent component, ExaminedEvent args)
        {
            if (GetTargetNode(uid, component) is {} target)
            {
                args.PushMarkup(Loc.GetString(
                    "construction-component-to-create-header",
                    ("targetName", target.Name)) + "\n");
            }

            if (component.EdgeIndex == null && GetTargetEdge(uid, component) is {} targetEdge)
            {
                var preventStepExamine = false;

                foreach (var condition in targetEdge.Conditions)
                {
                    preventStepExamine |= condition.DoExamine(args);
                }

                if (!preventStepExamine)
                    targetEdge.Steps[0].DoExamine(args);
                return;
            }

            if (GetCurrentEdge(uid, component) is {} edge)
            {
                var preventStepExamine = false;

                foreach (var condition in edge.Conditions)
                {
                    preventStepExamine |= condition.DoExamine(args);
                }

                if (preventStepExamine) return;
            }
        }


        private ConstructionGuide? GetGuide(ConstructionPrototype construction)
        {
            // NOTE: This method might be allocate a fair bit, but do not worry!
            // This method is specifically designed to generate guides once and cache the results,
            // therefore we don't need to worry *too much* about the performance of this.

            // If we've generated and cached this guide before, return it.
            if (_guideCache.TryGetValue(construction, out var guide))
                return guide;

            // If the graph doesn't actually exist, do nothing.
            if (!_prototypeManager.TryIndex(construction.Graph, out ConstructionGraphPrototype? graph))
                return null;

            // If either the start node or the target node are missing, do nothing.
            if (GetNodeFromGraph(graph, construction.StartNode) is not {} startNode
                || GetNodeFromGraph(graph, construction.TargetNode) is not {} targetNode)
                return null;

            // If there's no path from start to target, do nothing.
            if (graph.Path(construction.StartNode, construction.TargetNode) is not {} path
                || path.Length == 0)
                return null;

            var step = 1;

            var entries = new List<ConstructionGuideEntry>()
            {
                // Initial construction header.
                new()
                {
                    Localization = construction.Type == ConstructionType.Structure
                        ? "construction-presenter-to-build" : "construction-presenter-to-craft",
                    EntryNumber = step,
                }
            };

            foreach (var condition in construction.Conditions)
            {
                if(condition.GenerateGuideEntry() is {} conditionEntry)
                    entries.Add(conditionEntry);
            }

            var conditions = new HashSet<string>();

            // Iterate until the penultimate node.
            var node = startNode;
            var index = 0;
            while(node != targetNode)
            {
                // Can't find path, therefore can't generate guide...
                if (!node.TryGetEdge(path[index].Name, out var edge))
                    return null;

                // First steps are handled specially.
                if (step == 1)
                {
                    foreach (var graphStep in edge.Steps)
                    {
                        // This graph is invalid, we only allow insert steps as the initial construction steps.
                        if (graphStep is not EntityInsertConstructionGraphStep insertStep)
                            return null;

                        entries.Add(insertStep.GenerateGuideEntry());
                    }

                    step++;
                    node = path[index++];
                    continue;
                }

                var old = conditions;
                conditions = new HashSet<string>();

                var addedAnythingYet = false;

                foreach (var condition in edge.Conditions)
                {
                    foreach (var conditionEntry in condition.GenerateGuideEntry())
                    {
                        conditions.Add(conditionEntry.Localization);

                        // To prevent spamming the same stuff over and over again. This is a bit naive, but..ye
                        if (old.Contains(conditionEntry.Localization))
                            continue;

                        if (!addedAnythingYet)
                        {
                            addedAnythingYet = true;
                            // add padding since we're a superset of the old conditions.
                            entries.Add(new ConstructionGuideEntry());
                        }

                        conditionEntry.Padding += 4;
                        entries.Add(conditionEntry);
                    }
                }

                foreach (var graphStep in edge.Steps)
                {
                    var entry = graphStep.GenerateGuideEntry();
                    entry.EntryNumber = step++;
                    entries.Add(entry);
                }

                node = path[index++];
            }

            guide = new ConstructionGuide(entries.ToArray());
            _guideCache[construction] = guide;
            return guide;
        }
    }
}
