namespace Hst.Imager.ConsoleApp.Presenters
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class TablePresenter
    {
        public static string Present(Table table)
        {
            var columnLengths = new List<int>();

            var columns = table.Columns.ToList();
            var columnAlignments = columns.Select(x => x.Alignment).ToList();
            var rows = table.Rows.ToList();

            UpdateLengths(columnLengths, columns.Select(x => x.Name));
            foreach (var row in rows)
            {
                UpdateLengths(columnLengths, row.Columns);
            }

            var outputBuilder = new StringBuilder();
            
            outputBuilder.AppendLine(PrintRow(columnLengths, columnAlignments, columns.Select(x => x.Name).ToList()));
            outputBuilder.AppendLine(string.Join("-|-", columnLengths.Select(x => new string('-', x))));
            
            foreach (var row in rows)
            {
                outputBuilder.AppendLine(PrintRow(columnLengths, columnAlignments, row.Columns.ToList()));
            }

            return outputBuilder.ToString();
        }

        private static string PrintRow(IList<int> columnLengths, IList<ColumnAlignment> columnAlignments,
            IList<string> columns)
        {
            var rowParts = new List<string>();
            
            for (var i = 0; i < columnLengths.Count; i++)
            {
                var columnLength = columnLengths[i];
                var alignment = i >= columns.Count ? ColumnAlignment.Left : columnAlignments[i];
                var column = i >= columns.Count ? string.Empty : columns[i];
                rowParts.Add(alignment == ColumnAlignment.Left
                    ? column.PadRight(columnLength)
                    : column.PadLeft(columnLength));

                if (i < columnLengths.Count - 1)
                {
                    rowParts.Add(" | ");
                }
            }

            return string.Join("", rowParts);
        }

        private static void UpdateLengths(IList<int> columnLengths, IEnumerable<string> columns)
        {
            var i = 0;
            foreach (var column in columns)
            {
                if (i >= columnLengths.Count)
                {
                    columnLengths.Add(0);
                }

                if (column.Length > columnLengths[i])
                {
                    columnLengths[i] = column.Length;
                }

                i++;
            }
        }
    }

    public class Table
    {
        public IEnumerable<Column> Columns { get; set; }
        public IEnumerable<Row> Rows { get; set; }
    }

    public enum ColumnAlignment
    {
        Left,
        Right
    }

    public class Column
    {
        public string Name { get; set; }
        public ColumnAlignment Alignment { get; set; }
    }

    public class Row
    {
        public IEnumerable<string> Columns { get; set; }
    }
}