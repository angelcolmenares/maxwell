using System.Reflection;
using System.Text;

namespace Maxwell;
public static class EnumerableMarkdownExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Converts an <see cref="IEnumerable{T}"/> into a Markdown-formatted table string.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The source collection to convert.</param>
        /// <param name="rowSelector">
        /// Optional. A function that maps each element to an array of column values.
        /// Must be used together with <paramref name="headers"/>.
        /// </param>
        /// <param name="headers">
        /// Optional. Column header names. Required when <paramref name="rowSelector"/> is provided.
        /// If omitted, headers are inferred from the public properties of <typeparamref name="T"/>.
        /// </param>
        /// <returns>A string containing the Markdown table.</returns>
        public string ToMarkdownTable(
            Func<T, object[]>? rowSelector = null,
            string[]? headers = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var list = source.ToList();

            // --- Mode 1: Manual column selector ---
            if (rowSelector != null)
            {
                if (headers == null)
                    throw new ArgumentNullException(nameof(headers),
                        "'headers' must be provided when using 'rowSelector'.");

                var rows = list
                    .Select(item => rowSelector(item)
                        .Select(v => v?.ToString() ?? "")
                        .ToArray())
                    .ToList();

                return BuildTable(headers, rows);
            }

            // --- Mode 2: Automatic reflection over public properties ---
            var properties = typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray();

            var autoHeaders = properties
                .Select(p => p.Name)
                .ToArray();

            var autoRows = list
                .Select(item => properties
                    .Select(p => p.GetValue(item)?.ToString() ?? "")
                    .ToArray())
                .ToList();

            return BuildTable(autoHeaders, autoRows);
        }        

    }

    /// <summary>
        /// Builds a Markdown table string from headers and rows.
        /// When <paramref name="rows"/> is empty, only the header and separator are rendered.
        /// </summary>
        /// <param name="headers">Column header names.</param>
        /// <param name="rows">Data rows; each row is an array of cell values.</param>
        /// <returns>A Markdown-formatted table string.</returns>
        private static string BuildTable(string[] headers, List<string[]> rows)
        {
            // Column width = max of header length, longest cell value, and minimum of 3
            var columnWidths = headers.Select((header, index) =>
            {
                int maxCellWidth = rows.Count > 0
                    ? rows.Max(row => index < row.Length ? row[index].Length : 0)
                    : 0;

                return Math.Max(header.Length, Math.Max(maxCellWidth, 3));
            }).ToArray();

            var sb = new StringBuilder();

            // Header row
            sb.Append("| ");
            sb.Append(string.Join(" | ", headers.Select((h, i) => h.PadRight(columnWidths[i]))));
            sb.AppendLine(" |");

            // Separator row
            sb.Append("| ");
            sb.Append(string.Join(" | ", columnWidths.Select(w => new string('-', w))));
            sb.AppendLine(" |");

            // Data rows (empty collection renders nothing here)
            foreach (var row in rows)
            {
                sb.Append("| ");
                sb.Append(string.Join(" | ", headers.Select((_, i) =>
                {
                    var cell = i < row.Length ? row[i] : "";
                    return cell.PadRight(columnWidths[i]);
                })));
                sb.AppendLine(" |");
            }

            return sb.ToString();
        }

}
