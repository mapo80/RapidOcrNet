using RapidOcrNet;
using RapidOcrNet.BenchCli;
using SkiaSharp;
using Xunit.Abstractions;

namespace RapidOcrNet.ModelsBench.Tests;

public class AccuracyTests
{
    private readonly ITestOutputHelper _output;
    public AccuracyTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> ModelData()
    {
        var env = Environment.GetEnvironmentVariable("RAPIDOCR_REC");
        if (!string.IsNullOrEmpty(env))
        {
            yield return new object[] { env };
        }
        else
        {
            yield return new object[] { "models/rec/latin_PP-OCRv3_mobile_rec_infer.onnx" };
            yield return new object[] { "models/rec/it_mobile_v2.0_rec_infer.onnx" };
        }
    }

    private static string Repo => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static string DetPath => Environment.GetEnvironmentVariable("RAPIDOCR_DET") ?? Path.Combine(Repo, "RapidOcrNet", "models", "en_PP-OCRv3_det_infer_opt.onnx");
    private static string ClsPath => Environment.GetEnvironmentVariable("RAPIDOCR_CLS") ?? Path.Combine(Repo, "RapidOcrNet", "models", "ch_ppocr_mobile_v2.0_cls_infer_opt.onnx");
    private static string KeysPath(string rec) => Directory.GetFiles(Path.GetDirectoryName(Path.Combine(Repo, rec))!, "*.txt").FirstOrDefault() ?? Path.Combine(Repo, "models", "rec", "ppocr_keys_v1.txt");

    [Theory]
    [MemberData(nameof(ModelData))]
    public void RecognizesAccents(string recModel)
    {
        using var ocr = new RapidOcr();
        var recPath = Path.Combine(Repo, recModel);
        ocr.InitModels(DetPath, ClsPath, recPath, KeysPath(recModel), 0);
        foreach (var (bmp, text) in AccentFixtures.Generate())
        {
            using var image = bmp;
            var res = ocr.Detect(image, RapidOcrOptions.Default);
            var pred = res.StrRes.Replace("\r", "").Replace("\n", "");
            var m = Metrics.Evaluate(pred, text);
            _output.WriteLine($"{text} -> {pred} charAcc={m.CharAcc:F2} wordAcc={m.WordAcc:F2} accents={m.AccentErrRate:F2}");
        }
    }
}
