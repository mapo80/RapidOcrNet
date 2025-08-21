using RapidOcrNet;
using RapidOcrNet.BenchCli;
using Xunit.Abstractions;

namespace RapidOcrNet.ModelsBench.Tests;

public class PerformanceTests
{
    private readonly ITestOutputHelper _output;
    public PerformanceTests(ITestOutputHelper output) => _output = output;

    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string DetPath => Environment.GetEnvironmentVariable("RAPIDOCR_DET") ?? Path.Combine(Repo, "RapidOcrNet", "models", "en_PP-OCRv3_det_infer_opt.onnx");
    private static string ClsPath => Environment.GetEnvironmentVariable("RAPIDOCR_CLS") ?? Path.Combine(Repo, "RapidOcrNet", "models", "ch_ppocr_mobile_v2.0_cls_infer_opt.onnx");
    private static string KeysPath(string rec) => Directory.GetFiles(Path.GetDirectoryName(Path.Combine(Repo, rec))!, "*.txt").FirstOrDefault() ?? Path.Combine(Repo, "models", "rec", "ppocr_keys_v1.txt");

    private static IEnumerable<string> RecModels()
    {
        var env = Environment.GetEnvironmentVariable("RAPIDOCR_REC");
        if (!string.IsNullOrEmpty(env)) return new[] { env };
        return new[] { "models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx", "models/rec/it_mobile_v2.0_rec_infer.onnx" };
    }

    [Fact]
    [Trait("Perf","True")]
    public void MeasureLatency()
    {
        foreach (var rec in RecModels())
        {
            using var ocr = new RapidOcr();
            var recPath = Path.Combine(Repo, rec);
            ocr.InitModels(DetPath, ClsPath, recPath, KeysPath(rec), 0);
            var sw = new System.Diagnostics.Stopwatch();
            int count = 0;
            foreach (var (bmp, text) in AccentFixtures.Generate().Take(20))
            {
                using var image = bmp;
                sw.Start();
                ocr.Detect(image, RapidOcrOptions.Default);
                sw.Stop();
                count++;
            }
            var avg = sw.Elapsed.TotalMilliseconds / count;
            _output.WriteLine($"{Path.GetFileName(rec)} avg_ms={avg:F1}");
        }
    }
}
