using System.Diagnostics.CodeAnalysis;
using Content.Server.Lathe.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Server.Research.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Research;
using Content.Shared.Research.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using JetBrains.Annotations;
using System.Linq;
using Content.Server.Materials;
using Robust.Server.Player;

namespace Content.Server.Lathe
{
    [UsedImplicitly]
    public sealed class LatheSystem : SharedLatheSystem
    {
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
        [Dependency] private readonly ResearchSystem _researchSys = default!;
        [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<LatheComponent, MaterialEntityInsertedEvent>(OnMaterialEntityInserted);
            SubscribeLocalEvent<LatheComponent, GetMaterialWhitelistEvent>(OnGetWhitelist);
            SubscribeLocalEvent<LatheComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<TechnologyDatabaseComponent, LatheGetRecipesEvent>(OnGetRecipes);
            SubscribeLocalEvent<TechnologyDatabaseComponent, LatheServerSyncMessage>(OnLatheServerSyncMessage);

            SubscribeLocalEvent<LatheComponent, LatheQueueRecipeMessage>(OnLatheQueueRecipeMessage);
            SubscribeLocalEvent<LatheComponent, LatheSyncRequestMessage>(OnLatheSyncRequestMessage);
            SubscribeLocalEvent<LatheComponent, LatheServerSelectionMessage>(OnLatheServerSelectionMessage);
        }

        public override void Update(float frameTime)
        {
            foreach (var comp in EntityQuery<LatheInsertingComponent>())
            {
                comp.TimeRemaining -= frameTime;

                if (comp.TimeRemaining > 0)
                    continue;

                UpdateInsertingAppearance(comp.Owner, false);
                RemCompDeferred(comp.Owner, comp);
            }

            foreach (var comp in EntityQuery<LatheComponent>())
            {
                if (!comp.Queue.Any() || !this.IsPowered(comp.Owner, EntityManager))
                    continue;

                TryStartProducing(comp.Owner, comp);
            }

            foreach (var (comp, lathe) in EntityQuery<LatheProducingComponent, LatheComponent>())
            {
                comp.TimeRemaining -= frameTime;

                if (comp.TimeRemaining <= 0)
                    FinishProducing(comp.Owner, lathe);
            }
        }

        private void OnGetWhitelist(EntityUid uid, LatheComponent component, GetMaterialWhitelistEvent args)
        {
            var materialWhitelist = new List<string>();
            var recipes =  GetAllBaseRecipes(component);
            foreach (var id in recipes)
            {
                if (!_proto.TryIndex<LatheRecipePrototype>(id, out var proto))
                    continue;
                foreach (var (mat, _) in proto.RequiredMaterials)
                {
                    if (!materialWhitelist.Contains(mat))
                        materialWhitelist.Add(mat);
                }
            }
            args.Whitelist = args.Whitelist.Union(materialWhitelist).ToList();
        }

        [PublicAPI]
        public bool TryGetAvailableRecipes(EntityUid uid, [NotNullWhen(true)] out List<string>? recipes, LatheComponent? component = null)
        {
            recipes = null;
            if (!Resolve(uid, ref component))
                return false;
            recipes = GetAvailableRecipes(component);
            return true;
        }

        public List<string> GetAvailableRecipes(LatheComponent component)
        {
            var ev = new LatheGetRecipesEvent(component.Owner)
            {
                Recipes = component.StaticRecipes
            };
            RaiseLocalEvent(component.Owner, ev, true);
            return ev.Recipes;
        }

        public List<string> GetAllBaseRecipes(LatheComponent component)
        {
            return component.DynamicRecipes == null
                ? component.StaticRecipes
                : component.StaticRecipes.Union(component.DynamicRecipes).ToList();
        }

        public bool TryAddToQueue(EntityUid uid, LatheRecipePrototype recipe, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;

            if (!CanProduce(uid, recipe, 1, component))
                return false;

            foreach (var (mat, amount) in recipe.RequiredMaterials)
            {
                _materialStorage.TryChangeMaterialAmount(uid, mat, amount);
            }
            component.Queue.Add(recipe);

            return true;
        }

        public bool TryStartProducing(EntityUid uid, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;
            if (HasComp<LatheProducingComponent>(uid) || !component.Queue.Any())
                return false;

            var recipe = component.Queue.First();
            component.Queue.RemoveAt(0);

            var prodComp = AddComp<LatheProducingComponent>(uid);
            prodComp.Recipe = recipe;
            prodComp.TimeRemaining = (float) recipe.CompleteTime.TotalSeconds;

            // Again, this should really just be a bui state instead of two separate messages.
            _uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheProducingRecipeMessage(recipe.ID));
            //_uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheFullQueueMessage(component.Queue));
            _audio.PlayPvs(component.ProducingSound, component.Owner);
            UpdateRunningAppearance(uid, true);
            return true;
        }

        private void FinishProducing(EntityUid uid, LatheComponent? comp = null, LatheProducingComponent? prodComp = null)
        {
            if (!Resolve(uid, ref comp, ref prodComp))
                return;

            if (prodComp.Recipe != null)
                Spawn(prodComp.Recipe.Result, Transform(uid).Coordinates);
            // TODO this should probably just be a BUI state, not a special message.
            _uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheStoppedProducingRecipeMessage());
            RemComp(prodComp.Owner, prodComp);
            UpdateRunningAppearance(uid, false);
        }


        private void OnGetRecipes(EntityUid uid, TechnologyDatabaseComponent component, LatheGetRecipesEvent args)
        {
            if (uid != args.Lathe || !TryComp<LatheComponent>(uid, out var latheComponent) || latheComponent.DynamicRecipes == null)
                return;

            //gets all of the techs that are unlocked and also in the DynamicRecipes list
            var allTechs = (from tech in component.Technologies
                from recipe in tech.UnlockedRecipes
                where latheComponent.DynamicRecipes.Contains(recipe)
                select recipe).ToList();

            args.Recipes = args.Recipes.Union(allTechs).ToList();
        }

        /// <summary>
        /// Initialize the UI and appearance.
        /// Appearance requires initialization or the layers break
        /// </summary>
        private void OnMapInit(EntityUid uid, LatheComponent component, MapInitEvent args)
        {
            _appearance.SetData(uid, LatheVisuals.IsInserting, false);
            _appearance.SetData(uid, LatheVisuals.IsRunning, false);

            _materialStorage.UpdateMaterialWhitelist(uid);
        }

        private void OnMaterialEntityInserted(EntityUid uid, LatheComponent component, MaterialEntityInsertedEvent args)
        {
            var lastMat = args.Materials.Keys.Last();
            // We need the prototype to get the color
            _proto.TryIndex(lastMat, out MaterialPrototype? matProto);
            EnsureComp<LatheInsertingComponent>(uid).TimeRemaining = component.InsertionTime;
            UpdateInsertingAppearance(uid, true, matProto?.Color);
        }

        /// <summary>
        /// Sets the machine sprite to either play the running animation
        /// or stop.
        /// </summary>
        private void UpdateRunningAppearance(EntityUid uid, bool isRunning)
        {
            _appearance.SetData(uid, LatheVisuals.IsRunning, isRunning);
        }

        /// <summary>
        /// Sets the machine sprite to play the inserting animation
        /// and sets the color of the inserted mat if applicable
        /// </summary>
        private void UpdateInsertingAppearance(EntityUid uid, bool isInserting, Color? color = null)
        {
            _appearance.SetData(uid, LatheVisuals.IsInserting, isInserting);
            if (color != null)
                _appearance.SetData(uid, LatheVisuals.InsertingColor, color);
        }

        protected override bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component)
        {
            return GetAvailableRecipes(component).Contains(recipe.ID);
        }

        #region UI Messages

        private void OnLatheQueueRecipeMessage(EntityUid uid, LatheComponent component, LatheQueueRecipeMessage args)
        {
            if (_proto.TryIndex(args.ID, out LatheRecipePrototype? recipe))
            {
                for (var i = 0; i < args.Quantity; i++)
                {
                    if (!TryAddToQueue(uid, recipe, component))
                        return;
                }

                // Again: TODO this should be handled by BUI states
                //_uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheFullQueueMessage(component.Queue));
            }

            TryStartProducing(uid, component);
        }

        private void OnLatheSyncRequestMessage(EntityUid uid, LatheComponent component, LatheSyncRequestMessage args)
        {
            // Again: TODO BUI states. Why TF was this was this ever two separate messages!?!?
            //_uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheFullQueueMessage(component.Queue.));
            if (TryComp(uid, out LatheProducingComponent? prodComp) && prodComp.Recipe != null)
                _uiSys.TrySendUiMessage(uid, LatheUiKey.Key, new LatheProducingRecipeMessage(prodComp.Recipe.ID));
        }

        private void OnLatheServerSelectionMessage(EntityUid uid, LatheComponent component, LatheServerSelectionMessage args)
        {
            // TODO W.. b.. why?
            // the client can just open the ui itself. why tf is it asking the server to open it for it.
            _uiSys.TryOpen(uid, ResearchClientUiKey.Key, (IPlayerSession) args.Session);
        }

        private void OnLatheServerSyncMessage(EntityUid uid, TechnologyDatabaseComponent component, LatheServerSyncMessage args)
        {
            _researchSys.SyncWithServer(component);

            //TODO: make the lathe ui update on sync if nothing has changed
        }

        #endregion
    }
}
