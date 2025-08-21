using System.CommandLine;
using Microsoft.Extensions.Configuration;
using RapidOcrNet;

var imgOption = new Option<FileInfo>(new[] {"-i", "--image"}, "Image to process") { IsRequired = true };
var keysOption = new Option<FileInfo?>("--keys", "Override dictionary file");
var root = new RootCommand("RapidOcr demo CLI") { imgOption, keysOption };

root.SetHandler((FileInfo image, FileInfo? keysFile) =>
{
    var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var det = Environment.GetEnvironmentVariable("RAPIDOCR_DET") ?? config["RapidOcr:DetModel"];
    var rec = Environment.GetEnvironmentVariable("RAPIDOCR_REC") ?? config["RapidOcr:RecModel"];
    var cls = Environment.GetEnvironmentVariable("RAPIDOCR_CLS") ?? config["RapidOcr:ClsModel"];
    var keys = keysFile?.FullName ?? Environment.GetEnvironmentVariable("RAPIDOCR_KEYS") ?? config["RapidOcr:LabelFile"];

    if (rec is null || det is null)
    {
        Console.Error.WriteLine("Recognizer and detector models are required. Set RAPIDOCR_REC and RAPIDOCR_DET or configure appsettings.json");
        return;
    }

    if (string.IsNullOrEmpty(keys))
    {
        keys = RapidOcr.AutoDiscoverLabelFile(rec);
    }

    using var ocr = new RapidOcr();
    ocr.InitModels(det, cls, rec, keys, 0);
    Console.WriteLine($"RapidOcr: DET={det} REC={rec} CLS={cls ?? "none"} KEYS={keys} (labels={ocr.LabelCount}, model_classes={ocr.ModelClassCount})");
    var res = ocr.Detect(image.FullName, RapidOcrOptions.Default);
    Console.WriteLine(res.StrRes);
}, imgOption, keysOption);

return root.InvokeAsync(args).Result;
