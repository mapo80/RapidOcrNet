using System.CommandLine;
using Microsoft.Extensions.Configuration;
using RapidOcrNet;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Linq;

void PrintTable(List<Metric> metrics, double avgMs, int sumChars, int sumWords)
{
    var rows = metrics.Select(m => new[]
    {
        m.File,
        m.Ms.HasValue ? m.Ms.Value.ToString("0.0", CultureInfo.InvariantCulture) : "",
        m.Chars.ToString(),
        m.Words.ToString()
    }).ToList();
    rows.Add(new[] { "average", avgMs.ToString("0.0", CultureInfo.InvariantCulture), sumChars.ToString(), sumWords.ToString() });
    string[] headers = { "file", "ms", "chars", "words" };
    int[] widths = new int[4];
    for (int i = 0; i < 4; i++)
    {
        widths[i] = Math.Max(headers[i].Length, rows.Max(r => r[i].Length));
    }
    string top = "┌" + string.Join("┬", widths.Select(w => new string('─', w + 2))) + "┐";
    string mid = "├" + string.Join("┼", widths.Select(w => new string('─', w + 2))) + "┤";
    string bot = "└" + string.Join("┴", widths.Select(w => new string('─', w + 2))) + "┘";

    Console.WriteLine(top);
    Console.WriteLine($"│ {headers[0].PadRight(widths[0])} │ {headers[1].PadRight(widths[1])} │ {headers[2].PadRight(widths[2])} │ {headers[3].PadRight(widths[3])} │");
    Console.WriteLine(mid);
    for (int i = 0; i < metrics.Count; i++)
    {
        var r = rows[i];
        Console.WriteLine($"│ {r[0].PadRight(widths[0])} │ {r[1].PadLeft(widths[1])} │ {r[2].PadLeft(widths[2])} │ {r[3].PadLeft(widths[3])} │");
    }
    Console.WriteLine(mid);
    var last = rows[^1];
    Console.WriteLine($"│ {last[0].PadRight(widths[0])} │ {last[1].PadLeft(widths[1])} │ {last[2].PadLeft(widths[2])} │ {last[3].PadLeft(widths[3])} │");
    Console.WriteLine(bot);
}

var inputOption = new Option<string>(new[] { "--input", "-i" }, "Input file or directory") { IsRequired = true };
var outOption = new Option<string?>(new[] { "--out", "-o" }, () => null, "Output directory");
var jsonOption = new Option<bool>("--json", "Also write a JSON result for each file");
var csvOption = new Option<string?>("--csv", "CSV summary output path");
var patternOption = new Option<string>("--pattern", () => "*.jpg;*.jpeg;*.png;*.tif;*.tiff", "Search pattern for input directory");
var detOption = new Option<string?>("--det", "Override detector model");
var recOption = new Option<string?>("--rec", "Override recognizer model");
var clsOption = new Option<string?>("--cls", "Override classifier model");
var keysOption = new Option<string?>("--keys", "Override dictionary file");

var root = new RootCommand("RapidOcr demo CLI")
{
    inputOption,
    outOption,
    jsonOption,
    csvOption,
    patternOption,
    detOption,
    recOption,
    clsOption,
    keysOption
};

root.SetHandler(context =>
{
    var inputPath = context.ParseResult.GetValueForOption(inputOption)!;
    var outDir = context.ParseResult.GetValueForOption(outOption);
    var json = context.ParseResult.GetValueForOption(jsonOption);
    var csv = context.ParseResult.GetValueForOption(csvOption);
    var pattern = context.ParseResult.GetValueForOption(patternOption)!;
    var detFile = context.ParseResult.GetValueForOption(detOption);
    var recFile = context.ParseResult.GetValueForOption(recOption);
    var clsFile = context.ParseResult.GetValueForOption(clsOption);
    var keysFile = context.ParseResult.GetValueForOption(keysOption);

    var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var det = detFile ?? Environment.GetEnvironmentVariable("RAPIDOCR_DET") ?? config["RapidOcr:DetModel"];
    if (string.IsNullOrEmpty(det) || !File.Exists(det))
    {
        det = Path.Combine(AppContext.BaseDirectory, "models", "en_PP-OCRv3_det_infer_opt.onnx");
    }
    var rec = recFile ?? Environment.GetEnvironmentVariable("RAPIDOCR_REC") ?? config["RapidOcr:RecModel"];
    var cls = clsFile ?? Environment.GetEnvironmentVariable("RAPIDOCR_CLS") ?? config["RapidOcr:ClsModel"];
    var keys = keysFile ?? Environment.GetEnvironmentVariable("RAPIDOCR_KEYS") ?? config["RapidOcr:LabelFile"];

    if (rec is null)
    {
        Console.Error.WriteLine("Recognizer model is required. Set RAPIDOCR_REC or use --rec option.");
        return;
    }

    if (string.IsNullOrEmpty(keys))
    {
        keys = RapidOcr.AutoDiscoverLabelFile(rec);
    }

    using var ocr = new RapidOcr();
    ocr.InitModels(det!, cls, rec, keys, 0);
    Console.WriteLine($"RapidOcr: DET={det} REC={rec} CLS={cls ?? "none"} KEYS={keys} (labels={ocr.LabelCount}, model_classes={ocr.ModelClassCount})");

    inputPath = Path.GetFullPath(inputPath);
    bool isDir = Directory.Exists(inputPath);
    var files = new List<FileInfo>();
    if (isDir)
    {
        var patterns = pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in patterns)
        {
            files.AddRange(Directory.EnumerateFiles(inputPath, p, SearchOption.TopDirectoryOnly)
                .Where(f => (File.GetAttributes(f) & FileAttributes.Hidden) == 0)
                .Select(f => new FileInfo(f)));
        }
        files = files.OrderBy(f => f.Name).ToList();
    }
    else if (File.Exists(inputPath))
    {
        var fi = new FileInfo(inputPath);
        if ((fi.Attributes & FileAttributes.Hidden) == 0)
            files.Add(fi);
    }
    else
    {
        Console.Error.WriteLine($"Input not found: {inputPath}");
        return;
    }

    if (files.Count == 0)
    {
        Console.Error.WriteLine("No files to process.");
        return;
    }

    DirectoryInfo outPath;
    if (!string.IsNullOrEmpty(outDir))
        outPath = Directory.CreateDirectory(outDir!);
    else
        outPath = Directory.CreateDirectory(isDir ? Path.Combine(inputPath, "_ocr") : Path.Combine(Path.GetDirectoryName(inputPath)!, "_ocr"));

    var csvPath = csv ?? Path.Combine(outPath.FullName, "summary.csv");

    var metrics = new List<Metric>();
    foreach (var file in files)
    {
        try
        {
            using var bmp = SKBitmap.Decode(file.FullName);
            if (bmp == null) throw new Exception("decode error");
            var sw = Stopwatch.StartNew();
            var res = ocr.Detect(bmp, RapidOcrOptions.Default);
            sw.Stop();

            var text = res.StrRes;
            var ms = sw.Elapsed.TotalMilliseconds;

            File.WriteAllText(Path.Combine(outPath.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".txt"), text);

            if (json)
            {
                var words = res.TextBlocks.Select(tb =>
                {
                    int x = tb.BoxPoints.Min(p => p.X);
                    int y = tb.BoxPoints.Min(p => p.Y);
                    int w = tb.BoxPoints.Max(p => p.X) - x;
                    int h = tb.BoxPoints.Max(p => p.Y) - y;
                    double conf = tb.CharScores.Average();
                    return new { text = tb.GetText(), conf, bbox = new[] { x, y, w, h } };
                }).ToArray();

                var jsonObj = new
                {
                    file = file.Name,
                    text,
                    words,
                    meta = new { ms, det, rec, keys }
                };

                var jsonStr = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(outPath.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".json"), jsonStr);
            }

            metrics.Add(new Metric(file.Name, ms, text.Length, res.TextBlocks.Length));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{file.Name}: {ex.Message}");
            metrics.Add(new Metric(file.Name, null, 0, 0));
        }
    }

    var avgMs = metrics.Where(m => m.Ms.HasValue).Select(m => m.Ms!.Value).DefaultIfEmpty().Average();
    var sumChars = metrics.Sum(m => m.Chars);
    var sumWords = metrics.Sum(m => m.Words);

    using (var writer = new StreamWriter(csvPath))
    {
        writer.WriteLine("file,ms,chars,words");
        foreach (var m in metrics)
        {
            var msStr = m.Ms.HasValue ? m.Ms.Value.ToString("0.0", CultureInfo.InvariantCulture) : "";
            writer.WriteLine($"{m.File},{msStr},{m.Chars},{m.Words}");
        }
        writer.WriteLine($"average,{avgMs.ToString("0.0", CultureInfo.InvariantCulture)},{sumChars},{sumWords}");
    }

    PrintTable(metrics, avgMs, sumChars, sumWords);
});

return root.InvokeAsync(args).Result;

record Metric(string File, double? Ms, int Chars, int Words);

