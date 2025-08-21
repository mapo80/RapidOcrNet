using System.Globalization;
using System.Linq;
using System.Text;

namespace RapidOcrNet.BenchCli;

public record MetricsResult(
    int WordsOk,
    double WordAcc,
    int CharsTotal,
    int CharsOk,
    double CharAcc,
    int AccentTotal,
    int AccentErrors,
    double AccentErrRate,
    Dictionary<string,int> Confusions);

public static class Metrics
{
    public static MetricsResult Evaluate(string pred, string gt)
    {
        int wordsOk = pred == gt ? 1 : 0;
        int charsTotal = gt.Length;
        int dist = LevenshteinDistance(pred, gt);
        int charsOk = Math.Max(0, charsTotal - dist);
        double charAcc = 1.0 - dist / Math.Max(charsTotal, 1.0);

        var (accentTotal, accentErr, conf) = AccentErrors(pred, gt);
        double accentErrRate = accentTotal == 0 ? 0 : accentErr / (double)accentTotal;
        return new MetricsResult(wordsOk, wordsOk, charsTotal, charsOk, charAcc, accentTotal, accentErr, accentErrRate, conf);
    }

    public static int LevenshteinDistance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[a.Length, b.Length];
    }

    private static (int total, int errors, Dictionary<string,int> conf) AccentErrors(string pred, string gt)
    {
        var conf = new Dictionary<string, int>();
        int total = 0, errors = 0;
        int len = Math.Min(pred.Length, gt.Length);
        for (int i = 0; i < len; i++)
        {
            var g = gt[i];
            if (!HasDiacritic(g)) continue;
            total++;
            var p = pred[i];
            if (RemoveDiacritics(p) == RemoveDiacritics(g) && p != g)
            {
                errors++;
                var key = $"{g}->{p}";
                conf[key] = conf.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }
        for (int i = len; i < gt.Length; i++)
        {
            var g = gt[i];
            if (HasDiacritic(g)) { total++; errors++; }
        }
        return (total, errors, conf);
    }

    public static string RemoveDiacritics(char c) => RemoveDiacritics(c.ToString());
    public static string RemoveDiacritics(string text)
    {
        var norm = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in norm)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool HasDiacritic(char c) => RemoveDiacritics(c) != c.ToString();
}

public class MetricsAggregator
{
    private int _words = 0;
    private int _wordsOk = 0;
    private int _chars = 0;
    private int _charsOk = 0;
    private int _accTotal = 0;
    private int _accErr = 0;
    private double _ms = 0;
    private readonly Dictionary<string,int> _conf = new();

    public void Add(MetricsResult r)
    {
        _words += 1;
        _wordsOk += r.WordsOk;
        _chars += r.CharsTotal;
        _charsOk += r.CharsOk;
        _accTotal += r.AccentTotal;
        _accErr += r.AccentErrors;
        foreach (var kv in r.Confusions)
        {
            _conf[kv.Key] = _conf.TryGetValue(kv.Key, out var c) ? c + kv.Value : kv.Value;
        }
    }

    public void AddTime(double ms) => _ms += ms;

    public double CharAcc => _chars == 0 ? 0 : _charsOk / (double)_chars;
    public double WordAcc => _words == 0 ? 0 : _wordsOk / (double)_words;
    public double AccentErrRate => _accTotal == 0 ? 0 : _accErr / (double)_accTotal;
    public double AvgMs => _words == 0 ? 0 : _ms / _words;

    public IEnumerable<KeyValuePair<string,int>> TopConfusions(int top)
        => _conf.OrderByDescending(kv => kv.Value).Take(top);
}
