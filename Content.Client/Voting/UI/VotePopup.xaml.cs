﻿using System;
using Content.Client.Stylesheets;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Voting.UI
{
    [GenerateTypedNameReferences]
    [InjectDependencies]
    public sealed partial class VotePopup : Control
    {
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IVoteManager _voteManager = default!;

        private readonly VoteManager.ActiveVote _vote;
        private readonly Button[] _voteButtons;

        public VotePopup(VoteManager.ActiveVote vote)
        {
            _vote = vote;
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSpace;

            Modulate = Color.White.WithAlpha(0.75f);
            _voteButtons = new Button[vote.Entries.Length];
            var group = new ButtonGroup();

            for (var i = 0; i < _voteButtons.Length; i++)
            {
                var button = new Button
                {
                    ToggleMode = true,
                    Group = group
                };
                _voteButtons[i] = button;
                VoteOptionsContainer.AddChild(button);
                var i1 = i;
                button.OnPressed += _ => _voteManager.SendCastVote(vote.Id, i1);
            }
        }

        public void UpdateData()
        {
            VoteTitle.Text = _vote.Title;
            VoteCaller.Text = Loc.GetString("ui-vote-created", ("initiator", _vote.Initiator));

            for (var i = 0; i < _voteButtons.Length; i++)
            {
                var entry = _vote.Entries[i];
                _voteButtons[i].Text = Loc.GetString("ui-vote-button", ("text", entry.Text), ("votes", entry.Votes));

                if (_vote.OurVote == i)
                    _voteButtons[i].Pressed = true;
            }
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            // Logger.Debug($"{_gameTiming.ServerTime}, {_vote.StartTime}, {_vote.EndTime}");

            var curTime = _gameTiming.RealTime;
            var timeLeft = _vote.EndTime - curTime;
            if (timeLeft < TimeSpan.Zero)
                timeLeft = TimeSpan.Zero;

            // Round up a second.
            timeLeft = TimeSpan.FromSeconds(Math.Ceiling(timeLeft.TotalSeconds));

            TimeLeftBar.Value = Math.Min(1, (float) ((curTime.TotalSeconds - _vote.StartTime.TotalSeconds) /
                                                     (_vote.EndTime.TotalSeconds - _vote.StartTime.TotalSeconds)));

            TimeLeftText.Text = $"{timeLeft:m\\:ss}";
        }
    }
}
