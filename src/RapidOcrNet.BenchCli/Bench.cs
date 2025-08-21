using System.Text;
using System.Text.Json;
using RapidOcrNet;

namespace RapidOcrNet.BenchCli;

public class BenchOptions
{
    public required string Images { get; init; }
    public required string Rec { get; init; }
    public string? Det { get; init; }
    public string? Cls { get; init; }
    public required string OutDir { get; init; }
    public bool SaveJson { get; init; }
    public int Runs { get; init; } = 3;
}

public static class Bench
{
    public static async Task RunAsync(BenchOptions opts)
    {
        var det = opts.Det ?? AutoDiscover("det*infer*.onnx");
        var cls = opts.Cls ?? AutoDiscover("cls*infer*.onnx");
        var keys = RapidOcr.AutoDiscoverLabelFile(opts.Rec);
        var modelName = Path.GetFileNameWithoutExtension(opts.Rec);
        var timestampDir = Path.Combine(opts.OutDir, DateTime.Now.ToString("yyyyMMdd-HHmm"));
        Directory.CreateDirectory(timestampDir);
        var csvPath = Path.Combine(timestampDir, $"summary_{modelName}.csv");
        var mdPath = Path.Combine(timestampDir, $"summary_{modelName}.md");
        var jsonDir = Path.Combine(timestampDir, "per_image");
        if (opts.SaveJson) Directory.CreateDirectory(jsonDir);

        var csv = new StringBuilder();
        csv.AppendLine("file,model_name,words,words_ok,word_acc,chars,char_ok,char_acc,accents_total,accents_errors,accents_err_rate,avg_ms");

        var agg = new MetricsAggregator();

        IEnumerable<string> images = EnumerateImages(opts.Images);
        using var ocr = new RapidOcr();
        ocr.InitModels(det, cls, opts.Rec, keys, 0);

        foreach (var img in images)
        {
            var expected = Path.GetFileNameWithoutExtension(img);
            double totalMs = 0;
            string predText = string.Empty;
            for (int i = 0; i < opts.Runs; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var res = ocr.Detect(img, RapidOcrOptions.Default);
                sw.Stop();
                totalMs += sw.Elapsed.TotalMilliseconds;
                predText = res.StrRes.Replace("\r", "").Replace("\n", "");
                if (opts.SaveJson && i == 0)
                {
                    var jsonObj = new
                    {
                        file = Path.GetFileName(img),
                        text = predText,
                        blocks = res.TextBlocks.Select(b => new
                        {
                            text = b.GetText(),
                            box = b.BoxPoints,
                            score = b.CharScores?.DefaultIfEmpty().Average() ?? 0
                        })
                    };
                    await File.WriteAllTextAsync(Path.Combine(jsonDir, Path.GetFileNameWithoutExtension(img) + ".json"), JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true }));
                }
            }

            var avgMs = totalMs / opts.Runs;
            var metrics = Metrics.Evaluate(predText, expected);
            agg.Add(metrics);
            csv.AppendLine(string.Join(',', Path.GetFileName(img), modelName, 1, metrics.WordsOk, metrics.WordAcc.ToString("F2"), metrics.CharsTotal, metrics.CharsOk, metrics.CharAcc.ToString("F2"), metrics.AccentTotal, metrics.AccentErrors, metrics.AccentErrRate.ToString("F2"), avgMs.ToString("F2")));
        }

        await File.WriteAllTextAsync(csvPath, csv.ToString());

        var md = new StringBuilder();
        md.AppendLine($"# Summary for {modelName}\n");
        md.AppendLine("| metric | value |");
        md.AppendLine("|---|---|");
        md.AppendLine($"| char_acc | {agg.CharAcc:F3} |");
        md.AppendLine($"| word_acc | {agg.WordAcc:F3} |");
        md.AppendLine($"| accents_err_rate | {agg.AccentErrRate:F3} |");
        md.AppendLine($"| avg_ms | {agg.AvgMs:F1} |");
        md.AppendLine("\n## Top errors\n");
        foreach (var kv in agg.TopConfusions(10))
        {
            md.AppendLine($"- {kv.Key} : {kv.Value}");
        }
        await File.WriteAllTextAsync(mdPath, md.ToString());
    }

    private static IEnumerable<string> EnumerateImages(string path)
    {
        if (File.Exists(path)) return new[] { path };
        return Directory.EnumerateFiles(path, "*.png").Concat(Directory.EnumerateFiles(path, "*.jpg"));
    }

    private static string AutoDiscover(string pattern)
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var searchDirs = new[]
        {
            Path.Combine(repo, "models"),
            Path.Combine(repo, "RapidOcrNet", "models")
        };
        var files = searchDirs.Where(Directory.Exists).SelectMany(d => Directory.GetFiles(d, pattern, SearchOption.AllDirectories));
        var file = files.FirstOrDefault();
        if (file == null) throw new FileNotFoundException($"Could not find model {pattern}");
        return file;
    }

}
