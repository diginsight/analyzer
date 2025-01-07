using Newtonsoft.Json;
using System.Text;

namespace Diginsight.Analyzer.Common;

[JsonArray]
public sealed class FormattableStringCollection : List<string>
{
    public FormattableStringCollection(IEnumerable<string> underlying)
        : base(underlying) { }

    public override string ToString()
    {
        StringBuilder sb = new ("[");
        using (IEnumerator<string> enumerator = GetEnumerator())
        {
            if (enumerator.MoveNext())
            {
                sb.Append($"'{enumerator.Current}'");
                while (enumerator.MoveNext())
                {
                    sb.Append($", '{enumerator.Current}'");
                }
            }
        }

        sb.Append(']');
        return sb.ToString();
    }
}
