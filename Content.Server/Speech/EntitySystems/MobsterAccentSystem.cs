using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed class MobsterAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

    private static readonly Dictionary<string, string> DirectReplacements = new()
    {
        { "let me", "lemme" },
        { "should", "oughta" },
        { "the", "da" },
        { "them", "dem" },
        { "attack", "whack" },
        { "kill", "whack" },
        { "murder", "whack" },
        { "dead", "sleepin' with da fishies"},
        { "hey", "ey'o" },
        { "hi", "ey'o"},
        { "hello", "ey'o"},
        { "rules", "roolz" },
        { "you", "yous" },
        { "have to", "gotta" },
        { "going to", "boutta" },
        { "about to", "boutta" },
        { "here", "'ere" }
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobsterAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message, MobsterAccentComponent component)
    {
        // Order:
        // Do text manipulations first
        // Then prefix/suffix funnyies

        var msg = message;

        // direct word replacements
        msg = _replacement.ApplyReplacements(message, "mobster");

        // thinking -> thinkin'
        // king -> king
        msg = Regex.Replace(msg, @"(?<=\w\w)ing(?!\w)", "in'", RegexOptions.IgnoreCase);

        // or -> uh and ar -> ah in the middle of words (fuhget, tahget)
        msg = Regex.Replace(msg, @"(?<=\w)or(?=\w)", "uh", RegexOptions.IgnoreCase);
        msg = Regex.Replace(msg, @"(?<=\w)ar(?=\w)", "ah", RegexOptions.IgnoreCase);

        // Prefix
        if (_random.Prob(0.15f))
        {
            bool firstWordAllCaps = !Regex.Match(msg, @"^([\w\-]+)").Value.Any(char.IsLower);
            var pick = _random.Next(1, 2);

            // Reverse sanitize capital
            var prefix = Loc.GetString($"accent-mobster-prefix-{pick}");
            if (!firstWordAllCaps)
                msg = msg[0].ToString().ToLower() + msg.Remove(0, 1);
            else
                prefix = prefix.ToUpper();
            msg = prefix + " " + msg;
        }

        // Sanitize capital again, in case we substituted a word that should be capitalized
        msg = msg[0].ToString().ToUpper() + msg.Remove(0, 1);

        // Suffixes
        if (_random.Prob(0.4f))
        {
            bool lastWordAllCaps = !Regex.Match(msg, @"(\S+)$").Value.Any(char.IsLower);
            var suffix = "";
            if (component.IsBoss)
            {
                var pick = _random.Next(1, 4);
                suffix = Loc.GetString($"accent-mobster-suffix-boss-{pick}");
            }
            else
            {
                var pick = _random.Next(1, 3);
                suffix = Loc.GetString($"accent-mobster-suffix-minion-{pick}");                
            }
            if (lastWordAllCaps)
                suffix = suffix.ToUpper();
            msg += suffix;
        }

        return msg;
    }

    private void OnAccentGet(EntityUid uid, MobsterAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
