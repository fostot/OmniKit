using OmniKit.Search;

var entries = new[]
{
    new SearchEntry("fc", "Free Company", "", "guild community clan", "FFXIV", () => { }),
    new SearchEntry("ls", "Linkshells", "", "guild community chat", "FFXIV", () => { }),
    new SearchEntry("fc-finder", "Find a Free Company", "", "guild community recruitment", "Lodestone", () => { }),
    new SearchEntry("pld", "Paladin set", "", "job paladin pld equip gear", "Gearsets", () => { }),
    new SearchEntry("inv", "Inventory", "", "bags items", "FFXIV", () => { }),
};
var count = 0;
void Check(bool condition, string name) { count++; if (!condition) throw new Exception(name); }
foreach (var synonym in new[] { "community", "guild", "where can I find a new community?" })
    Check(SearchEngine.Find(entries, synonym).Count == 3, "social synonyms: " + synonym);
Check(SearchEngine.Find(entries, "free company").Count == 2, "multiword term");
Check(SearchEngine.Find(entries, "job paladin").Single().Id == "pld", "spoken job");
Check(SearchEngine.Find(entries, "job pld").Single().Id == "pld", "job abbreviation");
Check(SearchEngine.Find(entries, "please open inventory").Single().Id == "inv", "natural command");
Check(SearchEngine.Find(entries, "inventroy").Count == 0, "no unrelated typo match");
Check(SearchEngine.Find(entries, "inventry").Single().Id == "inv", "one-edit typo");
Check(SearchEngine.Find(entries, "give me apple pie recipe").Count == 0, "offtopic no action");
Check(SearchEngine.Find(entries, "inventory community").Count == 0, "all meaningful terms required");
Check(SearchEngine.Find(entries, "", 2).Count == 2, "result bound");
Check(SearchEngine.Normalize("  JOB---Paladin! ") == "job paladin", "normalization");
Console.WriteLine($"PASS: {count} search checks.");
