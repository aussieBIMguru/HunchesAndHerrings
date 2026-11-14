using HunchesAndHerrings.Core.Model;
using HunchesAndHerrings.Core.Util;

namespace HunchesAndHerrings.Core.Game;

/// <summary>The sergeant's banter, flavoured by personality.</summary>
public static class SergeantVoice
{
    public static string Assigned(SergeantStyle style, string task, Random rng) => style switch
    {
        SergeantStyle.Eager => rng.Pick(new[] { $"Right away! I'll {task}.", $"Leave it to me. I'll {task} directly." }),
        SergeantStyle.Gloomy => rng.Pick(new[] { $"If I must. I'll {task}. Not that it'll do much good.", $"I'll {task}. Probably find nothing." }),
        _ => rng.Pick(new[] { $"Very good. I'll {task}.", $"Righto. I'll {task}, then." }),
    };

    public static string Found(SergeantStyle style, int count, Random rng)
    {
        if (count == 0)
        {
            return style switch
            {
                SergeantStyle.Eager => "Nothing, I'm afraid! But I looked very hard.",
                SergeantStyle.Gloomy => "Nothing. As I expected.",
                _ => "Nothing worth writing down, I'm afraid.",
            };
        }

        return style switch
        {
            SergeantStyle.Eager => $"I've got {count} thing{(count == 1 ? "" : "s")} for the notebook!",
            SergeantStyle.Gloomy => $"Found {count} thing{(count == 1 ? "" : "s")}. Doubt {(count == 1 ? "it helps" : "they help")}.",
            _ => rng.Pick(new[] { $"Turned up {count} item{(count == 1 ? "" : "s")} for you.", $"{count} for the notebook." }),
        };
    }

    public static string Theory(SergeantStyle style, string name, Random rng) => style switch
    {
        SergeantStyle.Eager => rng.Pick(new[] { $"If you ask me, it's {name}. Shifty eyes!", $"I've a feeling about {name}. A strong feeling." }),
        SergeantStyle.Gloomy => rng.Pick(new[] { $"It'll turn out to be {name}, you'll see. It always is the one you least want.", $"My money's on {name}. Not that I've any money." }),
        _ => rng.Pick(new[] { $"Can't say I trust {name}, myself.", $"{name} was very quick with an answer. Too quick, maybe." }),
    };

    public static string Rescue(SergeantStyle style, Random rng) => style switch
    {
        SergeantStyle.Eager => "While you were busy I found this! Thought you'd want it straight away.",
        SergeantStyle.Gloomy => "I wasn't going to mention it, but I found this. Seems we need all the help we can get.",
        _ => rng.Pick(new[] { "Begging your pardon, but I came across this while you were occupied.", "Thought you ought to see this. Turned it up just now." }),
    };

    public static string Greeting(SergeantStyle style) => style switch
    {
        SergeantStyle.Eager => "Reporting for duty, {DET}! Tell me to search a room or question someone, ask me where folk are, or what I make of it all.",
        SergeantStyle.Gloomy => "Here again, {DET}. I can search rooms, question people and tell you where folk are. For what it's worth.",
        _ => "Ready when you are, {DET}. I can search a room, question someone, tell you where folk are, or give you my thoughts.",
    };

    public static string Puzzled(SergeantStyle style) => style switch
    {
        SergeantStyle.Eager => "Sorry, {SIR}, didn't quite catch that! Try \"search the library\", \"question the cook\", \"where is the colonel?\" or \"what next?\"",
        SergeantStyle.Gloomy => "Lost me there. Try \"search the library\", \"question the cook\", \"where is the colonel?\" or \"what next?\"",
        _ => "Beg pardon? Try \"search the library\", \"question the cook\", \"where is the colonel?\", \"any thoughts?\" or \"what next?\"",
    };

    public static string Busy(SergeantStyle style) => style switch
    {
        SergeantStyle.Eager => "I'm still on the last job, but I'll be quick!",
        SergeantStyle.Gloomy => "I've only got the one pair of hands.",
        _ => "I'm still about the last errand.",
    };
}
