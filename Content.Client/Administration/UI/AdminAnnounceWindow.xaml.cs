using Content.Shared.Administration;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI
{
    [GenerateTypedNameReferences]
    [InjectDependencies]
    public sealed partial class AdminAnnounceWindow : DefaultWindow
    {
        [Dependency] private ILocalizationManager _localization = default!;

        public AdminAnnounceWindow()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            Announcement.Placeholder = new Rope.Leaf(_localization.GetString("admin-announce-announcement-placeholder"));
            AnnounceMethod.AddItem(_localization.GetString("admin-announce-type-station"));
            AnnounceMethod.SetItemMetadata(0, AdminAnnounceType.Station);
            AnnounceMethod.AddItem(_localization.GetString("admin-announce-type-server"));
            AnnounceMethod.SetItemMetadata(1, AdminAnnounceType.Server);
            AnnounceMethod.OnItemSelected += AnnounceMethodOnOnItemSelected;
            Announcement.OnKeyBindUp += AnnouncementOnOnTextChanged;
        }

        private void AnnouncementOnOnTextChanged(GUIBoundKeyEventArgs args)
        {
            AnnounceButton.Disabled = Rope.Collapse(Announcement.TextRope).TrimStart() == "";
        }

        private void AnnounceMethodOnOnItemSelected(OptionButton.ItemSelectedEventArgs args)
        {
            AnnounceMethod.SelectId(args.Id);
            Announcer.Editable = ((AdminAnnounceType?)args.Button.SelectedMetadata ?? AdminAnnounceType.Station) == AdminAnnounceType.Station;
        }
    }
}
