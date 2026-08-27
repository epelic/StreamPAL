using System.Globalization;
using System.IO.Compression;
using System.IO;
using System.Security;
using System.Text;
using StreamForge.Models;

namespace StreamForge.Services;

public static class StatisticsExcelExporter
{
    public static void Export(string path, SourceInstance instance, IReadOnlyList<ListenerSample> samples)
    {
        var streams = instance.Encoders.Where(e => samples.Any(s => s.Streams.ContainsKey(e.Id))).ToList();
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
        Add(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Add(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Ascoltatori 72h\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Add(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
        Add(archive, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Aptos\"/></font><font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Aptos\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF167D69\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\"/></cellStyleXfs><cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"22\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\"/></cellXfs></styleSheet>");
        var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols><col min=\"1\" max=\"1\" width=\"21\" customWidth=\"1\"/><col min=\"2\" max=\"100\" width=\"18\" customWidth=\"1\"/></cols><sheetData>");
        xml.Append("<row r=\"1\">").Append(TextCell("A1", $"StreamPAL · {instance.Name} · storico ascoltatori 72 ore", 2)).Append("</row><row r=\"2\">");
        var headers = new[] { "Data e ora", "Totale" }.Concat(streams.Select(x => x.Name)).ToArray();
        for (var c = 0; c < headers.Length; c++) xml.Append(TextCell(Cell(c, 2), headers[c], 2));
        xml.Append("</row>");
        for (var r = 0; r < samples.Count; r++)
        {
            var sample = samples[r]; var row = r + 3; xml.Append($"<row r=\"{row}\">");
            xml.Append($"<c r=\"A{row}\" s=\"1\"><v>{sample.TimestampUtc.ToLocalTime().ToOADate().ToString(CultureInfo.InvariantCulture)}</v></c>");
            xml.Append(NumberCell($"B{row}", sample.Total));
            for (var c = 0; c < streams.Count; c++) xml.Append(NumberCell(Cell(c + 2, row), sample.Streams.GetValueOrDefault(streams[c].Id)));
            xml.Append("</row>");
        }
        xml.Append("</sheetData><mergeCells count=\"1\"><mergeCell ref=\"A1:").Append(Cell(headers.Length - 1, 1)).Append("\"/></mergeCells><autoFilter ref=\"A2:").Append(Cell(headers.Length - 1, Math.Max(2, samples.Count + 2))).Append("\"/><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"2\" topLeftCell=\"A3\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews></worksheet>");
        Add(archive, "xl/worksheets/sheet1.xml", xml.ToString());
    }

    private static string TextCell(string cell, string value, int style = 0) => $"<c r=\"{cell}\" t=\"inlineStr\" s=\"{style}\"><is><t>{SecurityElement.Escape(value)}</t></is></c>";
    private static string NumberCell(string cell, int value) => $"<c r=\"{cell}\"><v>{value}</v></c>";
    private static string Cell(int column, int row) { var name = ""; for (var n = column + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name; return name + row; }
    private static void Add(ZipArchive archive, string name, string content) { var entry = archive.CreateEntry(name, CompressionLevel.Optimal); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(content); }
}
