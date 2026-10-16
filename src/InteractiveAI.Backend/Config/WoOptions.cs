namespace InteractiveAI.Backend.Config;

public class WoOptions
{
    public string DefaultPrefix { get; set; } = "WOT";
    public string DefaultCategory { get; set; } = "Truk";
    public int MaxPrefixLength { get; set; } = 3;

    public Dictionary<string, string> CategoryPrefixes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Truk"] = "WOT",
        ["Reefer"] = "WOR",
        ["GPS"] = "WOS",
        ["Box"] = "WOX",
        ["Ban"] = "WOB",
        ["Cabin"] = "WOC",
        ["Genset"] = "WOG"
    };

    public Dictionary<string, string> CategoryAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["truck"] = "Truk",
        ["body"] = "Truk",
        ["trailer"] = "Truk",
        ["chassis"] = "Truk",
        ["truk"] = "Truk",
        ["reefer unit"] = "Reefer",
        ["refrigerator"] = "Reefer",
        ["pendingin"] = "Reefer",
        ["ac"] = "Reefer",
        ["air conditioner"] = "Reefer",
        ["tracker"] = "GPS",
        ["boxbody"] = "Box",
        ["box body"] = "Box",
        ["generator"] = "Genset",
        ["tyre"] = "Ban",
        ["tire"] = "Ban",
        ["ban belakang"] = "Ban",
        ["ban depan"] = "Ban",
        ["kabin"] = "Cabin",
        ["cab"] = "Cabin"
    };

    public List<string> CanonicalCategories { get; set; } = new()
    {
        "Truk", "Reefer", "Cabin", "GPS", "Genset", "Box", "Ban"
    };
}