// Usage: dotnet run purchase-notes.cs new --vault <vault-path> [--notes-dir <dir>]
// Generates Purchase Order, Purchase Line, Spool and Master Spool notes for one
// real-world purchase (ADR 0008). Interactive; refuses to overwrite existing notes.

using System.Globalization;
using System.Text;

if (args.Length == 0 || args[0] != "new")
{
    Console.WriteLine("Usage: dotnet run purchase-notes.cs new --vault <vault-path> [--notes-dir <dir>]");
    return 1;
}

var (vault, notesDirOverride) = ParseFlags(args);
if (vault is null)
{
    Console.WriteLine("error: --vault <path> is required");
    return 1;
}
if (!Directory.Exists(vault))
{
    Console.WriteLine($"error: vault path does not exist: {vault}");
    return 1;
}
var notesDir = notesDirOverride ?? Path.Combine(vault, "para.resources", "3d", "printer");
if (!Directory.Exists(notesDir))
{
    Console.WriteLine($"error: notes directory does not exist: {notesDir}");
    return 1;
}

try
{
    var order = PromptOrder();
    var lines = new List<Line>();
    do
    {
        lines.Add(PromptLine());
    } while (PromptYes("Add another line?"));

    Console.WriteLine("\nScanning the vault for existing ids...");
    var scan = VaultScan.Run(notesDir);

    Console.WriteLine("\nWriting notes...");
    var writers = new List<(string Path, string Content)>();
    var orderName = $"purchase-order_{order.Date:yyyy-MM-dd}_{order.Slug}";
    writers.Add((Path.Combine(notesDir, orderName + ".md"), Note.Order(order)));

    var ordinals = new Dictionary<string, int>();
    var nextLetter = new Dictionary<string, int>();
    var masterLabel = scan.NextMasterLabel();

    var lineNo = 0;
    foreach (var line in lines)
    {
        lineNo++;
        var lineName = $"purchase-line_{order.Date:yyyy-MM-dd}_{order.Slug}_{lineNo}";

        if (line.Filament is not null)
        {
            var sequence = ordinals.TryGetValue(line.Filament, out var s)
                ? s
                : ordinals[line.Filament] = scan.NextOrdinalFor(line.Filament);
            foreach (var spoolType in SpoolTypes(line))
            {
                var spoolIndex = nextLetter.GetValueOrDefault(line.Filament);
                if (spoolIndex >= 26)
                {
                    Console.WriteLine("error: more than 26 spools of one filament in one purchase; extend the sequence scheme first");
                    return 1;
                }
                var letter = (char)('a' + spoolIndex);
                var spoolId = $"{sequence}{letter}";
                var spoolName = $"spool_{spoolId}_{line.Filament["filament_".Length..]}";
                writers.Add((Path.Combine(notesDir, spoolName + ".md"),
                    Note.Spool(line, lineName, order, spoolId, sequence, letter.ToString(), spoolType)));
                nextLetter[line.Filament] = spoolIndex + 1;
            }
        }

        for (var i = 0; i < line.MasterSpoolCount; i++)
        {
            var msName = $"master-spool_{masterLabel}";
            masterLabel = scan.NextMasterLabel();
            writers.Add((Path.Combine(notesDir, msName + ".md"), Note.MasterSpool(line, lineName)));
        }

        writers.Add((Path.Combine(notesDir, lineName + ".md"), Note.Line(order, line, orderName)));
    }

    var existing = writers.Where(w => File.Exists(w.Path)).ToList();
    if (existing.Count > 0)
    {
        Console.WriteLine("error: refusing to overwrite existing notes:");
        foreach (var (path, _) in existing) Console.WriteLine("  " + path);
        return 1;
    }

    foreach (var (path, content) in writers) File.WriteAllText(path, content);

    Console.WriteLine("\nWritten:");
    foreach (var (path, _) in writers) Console.WriteLine("  " + Path.GetFileName(path));

    Console.WriteLine("\nAllocation (estimated here for review; stored nowhere, computed by the note queries):");
    lineNo = 0;
    foreach (var line in lines)
    {
        lineNo++;
        var items = line.RefillCount + line.SpoolCount;
        var premium = line.ReferencePrice is null ? 0 : line.Price - items * line.ReferencePrice.Value;
        var masterValue = line.MasterSpoolCount > 0 ? Math.Max(0, premium) / line.MasterSpoolCount : 0;
        var spoolCost = items > 0 ? (line.Price - line.MasterSpoolCount * masterValue) / items : 0;
        Console.WriteLine($"  line {lineNo} ({line.Name}): price {Note.Fmt(line.Price)}, " +
            $"premium {Note.Fmt(premium)}, master value {Note.Fmt(masterValue)} each, spool cost {Note.Fmt(spoolCost)} each");
    }
    return 0;
}
catch (InvalidOperationException e)
{
    Console.WriteLine("error: " + e.Message);
    return 1;
}

static IEnumerable<string> SpoolTypes(Line line)
{
    for (var i = 0; i < line.RefillCount; i++) yield return "refill";
    for (var i = 0; i < line.SpoolCount; i++) yield return "spool";
}

static (string? Vault, string? NotesDir) ParseFlags(string[] args)
{
    string? vault = null, notesDir = null;
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--vault" && i + 1 < args.Length) vault = args[++i];
        else if (args[i] == "--notes-dir" && i + 1 < args.Length) notesDir = args[++i];
    }
    return (vault, notesDir);
}

static Order PromptOrder()
{
    Console.WriteLine("New purchase order");
    var date = Prompt("Purchase date (YYYY-MM-DD, empty = today)", ParseDate, "a valid date");
    var retailer = Prompt("Retailer", s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(), "a retailer name");
    var slug = Prompt("Order slug (short descriptor, e.g. sunlu-haul)",
        s => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Replace(' ', '-'), "a slug");
    var shipping = Prompt("Shipping DKK (empty = 0)", s => ParseNumber(s, 0m), "a number");
    return new Order(date, retailer, slug, shipping);
}

static Line PromptLine()
{
    Console.WriteLine("\nInvoice line");
    var name = Prompt("Line name (what the invoice calls it, e.g. 'PLA refill 10-pack')",
        s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(), "a name");
    var price = Prompt("Line price DKK", s => ParseNumber(s, null), "a number");

    string? filament = null;
    while (true)
    {
        Console.Write("Filament note name (e.g. filament_white_pla_snapmaker_snapspeed, empty = not a filament line): ");
        var input = (Console.ReadLine() ?? "").Trim();
        if (input.Length == 0) break;
        if (input.StartsWith("filament_", StringComparison.Ordinal)) { filament = input; break; }
        Console.WriteLine("  expected a note name starting with 'filament_'");
    }

    var refillCount = Prompt("Refill count", s => ParseCount(s, 0), "a count");
    var spoolCount = Prompt("Spool count (own hardware)", s => ParseCount(s, 0), "a count");
    var masterCount = Prompt("Master spool count", s => ParseCount(s, 0), "a count");
    if (refillCount + spoolCount + masterCount == 0)
        throw new InvalidOperationException("a line must contain at least one item");
    if (refillCount + spoolCount > 0 && filament is null)
        throw new InvalidOperationException("a line with spools needs its Filament note name");

    decimal? reference = null;
    if (masterCount > 0)
    {
        var answer = Prompt("Plain-refill reference price DKK ('unknown' keeps it unrecorded)", s =>
        {
            s = s.Trim();
            if (s == "unknown") return "unknown";
            return ParseNumber(s, null) is decimal v ? v.ToString(CultureInfo.InvariantCulture) : null;
        }, "a number or 'unknown'");
        if (answer != "unknown") reference = decimal.Parse(answer, CultureInfo.InvariantCulture);
    }

    var msBrand = "";
    var msWeight = 0m;
    if (masterCount > 0)
    {
        msBrand = Prompt("Master spool brand", s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(), "a brand");
        msWeight = Prompt("Master spool weight-grams (the disk pair)", s => ParseNumber(s, null), "a number");
    }

    return new Line(name, price, filament, refillCount, spoolCount, masterCount, reference, msBrand, msWeight);
}

static T Prompt<T>(string label, Func<string, T?> parse, string expectation) where T : notnull
{
    while (true)
    {
        Console.Write(label + ": ");
        var parsed = parse(Console.ReadLine() ?? "");
        if (parsed is not null) return parsed;
        Console.WriteLine($"  expected {expectation}");
    }
}

static bool PromptYes(string label)
{
    Console.Write(label + " [y/N]: ");
    return (Console.ReadLine() ?? "").Trim().ToLowerInvariant() == "y";
}

static DateOnly? ParseDate(string s)
{
    if (string.IsNullOrWhiteSpace(s)) return DateOnly.FromDateTime(DateTime.Now);
    return DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d) ? d : null;
}

static decimal? ParseNumber(string s, decimal? fallback)
{
    s = s.Trim();
    if (s.Length == 0) return fallback;
    return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
}

static int? ParseCount(string s, int fallback)
{
    s = s.Trim();
    if (s.Length == 0) return fallback;
    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
}

record Order(DateOnly Date, string Retailer, string Slug, decimal Shipping);

record Line(
    string Name, decimal Price, string? Filament,
    int RefillCount, int SpoolCount, int MasterSpoolCount,
    decimal? ReferencePrice, string MasterSpoolBrand, decimal MasterSpoolWeight);

static class Note
{
    static string Quoted(string s) => $"\"{s}\"";
    static string Link(string noteName) => Quoted($"[[{noteName}]]");
    static string Tags(string tag) => $"tags:\n  - {tag}\n";

    public static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    const string SpoolQuery = """
        ```dataviewjs
        const line = dv.page(dv.current()["purchase-line"]);
        const items = (line["refill-count"] ?? 0) + (line["spool-count"] ?? 0);
        const masters = line["master-spool-count"] ?? 0;
        const ref = line["refill-reference-price-dkk"];
        const premium = ref != null ? line["price-dkk"] - items * ref : 0;
        const masterValue = masters > 0 ? Math.max(0, premium) / masters : 0;
        dv.paragraph("Estimated cost: " + ((line["price-dkk"] - masters * masterValue) / items).toFixed(2) + " DKK");
        ```
        """;

    const string MasterQuery = """
        ```dataviewjs
        const line = dv.page(dv.current()["purchase-line"]);
        const items = (line["refill-count"] ?? 0) + (line["spool-count"] ?? 0);
        const masters = line["master-spool-count"] ?? 0;
        const ref = line["refill-reference-price-dkk"];
        const premium = ref != null ? line["price-dkk"] - items * ref : 0;
        dv.paragraph("Estimated value: " + (masters > 0 ? Math.max(0, premium) / masters : 0).toFixed(2) + " DKK");
        ```
        """;

    public static string Order(Order o) =>
        "---\n" + Tags("3dprint/purchase-order") +
        $"purchase-date: {o.Date:yyyy-MM-dd}\n" +
        $"retailer: {Quoted(o.Retailer)}\n" +
        $"shipping-dkk: {Fmt(o.Shipping)}\n" +
        "---\n";

    public static string Line(Order o, Line l, string orderName)
    {
        var sb = new StringBuilder();
        sb.Append("---\n").Append(Tags("3dprint/purchase-line"));
        sb.AppendLine($"name: {Quoted(l.Name)}");
        sb.AppendLine($"purchase-order: {Link(orderName)}");
        sb.AppendLine($"purchase-date: {o.Date:yyyy-MM-dd}");
        sb.AppendLine($"retailer: {Quoted(o.Retailer)}");
        sb.AppendLine($"price-dkk: {Fmt(l.Price)}");
        if (l.Filament is not null) sb.AppendLine($"filament: {Link(l.Filament)}");
        if (l.MasterSpoolCount > 0)
        {
            if (l.ReferencePrice is not null) sb.AppendLine($"refill-reference-price-dkk: {Fmt(l.ReferencePrice.Value)}");
            else sb.AppendLine("refill-reference-price-dkk:");
        }
        if (l.RefillCount > 0) sb.AppendLine($"refill-count: {l.RefillCount}");
        if (l.SpoolCount > 0) sb.AppendLine($"spool-count: {l.SpoolCount}");
        if (l.MasterSpoolCount > 0) sb.AppendLine($"master-spool-count: {l.MasterSpoolCount}");
        sb.AppendLine("---");
        return sb.ToString();
    }

    public static string Spool(Line l, string lineName, Order o, string spoolId,
        int ordinal, string sequence, string spoolType) =>
        "---\n" + Tags("3dprint/spool") +
        $"filament: {Link(l.Filament!)}\n" +
        $"spool-id: {Quoted(spoolId)}\n" +
        $"purchase-order-code: {ordinal}\n" +
        $"spool-sequence: {Quoted(sequence)}\n" +
        $"status: \"unopened\"\n" +
        $"card-uid:\n" +
        $"master-spool:\n" +
        $"spool-type: {Quoted(spoolType)}\n" +
        $"gross-weight:\n" +
        $"purchase-date: {o.Date:yyyy-MM-dd}\n" +
        $"purchase-line: {Link(lineName)}\n" +
        $"retailer: {Quoted(o.Retailer)}\n" +
        $"lot-code:\n" +
        "---\n" + SpoolQuery + "\n";

    public static string MasterSpool(Line l, string lineName) =>
        "---\n" + Tags("3dprint/master-spool") +
        $"brand: {Quoted(l.MasterSpoolBrand)}\n" +
        $"weight-grams: {Fmt(l.MasterSpoolWeight)}\n" +
        $"purchase-line: {Link(lineName)}\n" +
        "---\n" + MasterQuery + "\n";
}

static class VaultScan
{
    public static Scan Run(string notesDir)
    {
        var ordinals = new Dictionary<string, int>();
        var maxMaster = 0;
        foreach (var path in Directory.EnumerateFiles(notesDir, "*.md"))
        {
            var filament = ReadFrontmatterField(path, "filament:");
            var spoolId = ReadFrontmatterField(path, "spool-id:");
            if (filament is not null && spoolId is not null)
            {
                var number = new string(spoolId.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(number, out var n))
                    ordinals[filament] = Math.Max(ordinals.GetValueOrDefault(filament), n);
            }

            var file = Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("master-spool_ms", StringComparison.Ordinal))
            {
                var digits = new string(file["master-spool_ms".Length..].TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n)) maxMaster = Math.Max(maxMaster, n);
            }
        }
        return new Scan(ordinals, maxMaster);
    }

    static string? ReadFrontmatterField(string path, string prefix)
    {
        var inFrontmatter = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.TrimEnd();
            if (!inFrontmatter)
            {
                if (line == "---") inFrontmatter = true;
                continue;
            }
            if (line == "---") return null;
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                var value = line[prefix.Length..].Trim();
                return value.StartsWith("[[") && value.EndsWith("]]") ? value[2..^2] : value;
            }
        }
        return null;
    }
}

record Scan(Dictionary<string, int> Ordinals, int MaxMaster)
{
    public int NextOrdinalFor(string filamentNote)
    {
        Ordinals[filamentNote] = Ordinals.GetValueOrDefault(filamentNote) + 1;
        return Ordinals[filamentNote];
    }

    public string NextMasterLabel()
    {
        MaxMaster++;
        return "ms" + MaxMaster;
    }
}
