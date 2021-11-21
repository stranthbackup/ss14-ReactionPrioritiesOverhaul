using System;
using System.Collections.Generic;
using Content.Client.Items.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;
using Robust.Client.Utility;
using Robust.Client.ResourceManagement;
using Content.Client.Stylesheets;

namespace Content.Client.Storage.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class StorageWindow : SS14Window
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IResourceCache _resCache = default!;

        private readonly StyleBoxFlat _hoveredBox = new() { BackgroundColor = Color.Black.WithAlpha(0.35f) };
        private readonly StyleBoxFlat _unHoveredBox = new() { BackgroundColor = Color.Black.WithAlpha(0.0f) };

        private Action<BaseButton.ButtonEventArgs, EntityUid> _onInteract;

        public StorageWindow(Action<BaseButton.ButtonEventArgs, EntityUid> onInteract, Action<BaseButton.ButtonEventArgs> onInsert)
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
            _onInteract = onInteract;
            StorageContainerButton.OnPressed += onInsert;
            InnerContainerButton.PanelOverride = _unHoveredBox;
            Scroll.OnMouseEntered += args => InnerContainerButton.PanelOverride = _hoveredBox;
            Scroll.OnMouseExited += args => InnerContainerButton.PanelOverride = _unHoveredBox;
        }

        /// summary>
        /// Loops through stored entities creating buttons for each, updates information labels
        /// </summary>
        public void BuildEntityList(List<EntityUid> entityUids, int storageSizeUsed, int storageCapacityMax)
        {
            ButtonBox.DisposeAllChildren();
            foreach (var uid in entityUids)
            {
                _entityManager.TryGetComponent(uid, out ISpriteComponent? sprite);
                _entityManager.TryGetComponent(uid, out ItemComponent? item);
                _entityManager.TryGetComponent(uid, out MetaDataComponent? meta);

                var size = item?.Size ?? 0;
                if (storageCapacityMax == 0)
                    // infinite capacity. Don't bother displaying weights
                    size = 0;

                var button = new EntityContainerButton(meta?.EntityName ?? string.Empty, sprite, size);
                button.OnPressed += args => _onInteract(args, uid);
                ButtonBox.AddChild(button);
            }

            //Sets information about entire storage container current capacity
            if (storageCapacityMax != 0)
                Information.Text = Loc.GetString("comp-storage-window-volume", ("itemCount", entityUids.Count),
                    ("usedVolume", storageSizeUsed), ("maxVolume", storageCapacityMax));
            else
                Information.Text = Loc.GetString("comp-storage-window-volume-unlimited", ("itemCount", entityUids.Count));
        }
    }
    

    /// <summary>
    /// Button created for each entity that represents that item in the storage UI, with a texture, and name and size label
    /// </summary>
    [GenerateTypedNameReferences]
    public sealed partial class EntityContainerButton : ContainerButton
    {
        private static SpriteSpecifier _weightIcon = new SpriteSpecifier.Texture(new ResourcePath("/Textures/Interface/weight.svg.192dpi.png"));

        public EntityContainerButton(string entityName, ISpriteComponent? sprite, int size)
        {
            RobustXamlLoader.Load(this);

            EntityName.Text = entityName;
            EntityView.Sprite = sprite;

            if (size > 0)
            {
                Icon.Visible = true;
                Icon.Texture = _weightIcon.Frame0();
                if (size > 99)
                {
                    EntitySize.Text = "99+";
                    EntitySize.SetOnlyStyleClass(StyleNano.StyleClassStorageWeightSmall);
                }
                else
                {
                    EntitySize.Text = (size > 99) ? "99+" : size.ToString();
                    EntitySize.SetOnlyStyleClass(StyleNano.StyleClassStorageWeight);
                }
            }
        }
    }
}
