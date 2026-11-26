using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using ChessDraw.Models;
using ChessDraw.Services;

namespace ChessDraw.Exports;

public static class ExcelExport
{
    private sealed record Cell(string? Text = null, decimal? Number = null, bool Header = false);

    public static void Save(Tournament t, string path)
    {
        var table = new List<List<Cell>>
        {
            new() { new Cell($"ТУСУР — шахматная жеребьёвка | {t.Name}") },
            new() { new Cell("Место", Header:true), new Cell("№", Header:true),
                new Cell("Игрок", Header:true), new Cell("Рейтинг", Header:true),
                new Cell("Очки", Header:true), new Cell("Бухгольц", Header:true),
                new Cell("Усеч. Бухгольц", Header:true), new Cell("Бергер", Header:true),
                new Cell("Победы за доской", Header:true) }
        };
        foreach (var s in StandingsService.Calculate(t))
            table.Add(new() { new(null, s.Place), new(null, s.Player.Number), new(s.Player.FullName),
                new(null, s.Player.Rating), new(null, s.Score), new(null, s.Buchholz),
                new(null, s.CutBuchholz), new(null, s.Berger), new(null, s.Wins) });

        if (t.System == TournamentSystem.RoundRobin)
            foreach (var row in table.Skip(1)) { row.RemoveAt(6); row.RemoveAt(5); }

        var pairings = new List<List<Cell>> { new() { new Cell($"Турнир: {t.Name}") } };
        foreach (var round in t.Rounds.OrderBy(r => r.Number))
        {
            pairings.Add(new() { new Cell($"Тур {round.Number}", Header:true) });
            pairings.Add(new() { new Cell("Стол", Header:true), new Cell("Белые", Header:true),
                new Cell("Чёрные", Header:true), new Cell("Результат", Header:true) });
            foreach (var g in round.Games)
                pairings.Add(new() { new(null, g.Board), new(GetPlayer(t, g.WhiteId)),
                    new(GetPlayer(t, g.BlackId)), new(ResultLabel(g.Result)) });
            foreach (var b in round.Byes)
                pairings.Add(new() { new Cell("—"), new(GetPlayer(t, b.PlayerId)),
                    new("Пропуск"), new(ByeLabel(b.Kind)) });
            pairings.Add(new());
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        Write(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
            <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
            <Default Extension="xml" ContentType="application/xml"/>
            <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
            <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        Write(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        Write(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
            <sheets><sheet name="Таблица" sheetId="1" r:id="rId1"/><sheet name="Жеребьёвка" sheetId="2" r:id="rId2"/></sheets>
            </workbook>
            """);
        Write(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
            <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);
        Write(zip, "xl/styles.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts>
            <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF214878"/><bgColor indexed="64"/></patternFill></fill></fills>
            <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
            <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
            <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1" applyFont="1"/></cellXfs>
            <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
            </styleSheet>
            """);
        Write(zip, "xl/worksheets/sheet1.xml", Sheet(table, new[] { 10, 8, 42, 12, 12, 15, 20, 12, 12 }));
        Write(zip, "xl/worksheets/sheet2.xml", Sheet(pairings, new[] { 10, 42, 42, 25 }));
    }

    private static string Sheet(List<List<Cell>> rows, int[] widths)
    {
        var b = new StringBuilder();
        b.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols>");
        for (int i = 0; i < widths.Length; i++)
            b.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i]}\" customWidth=\"1\"/>");
        b.Append("</cols><sheetData>");
        for (var r = 0; r < rows.Count; r++)
        {
            b.Append($"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var cell = rows[r][c];
                string reference = ToColumn(c + 1) + (r + 1);
                string style = cell.Header ? " s=\"1\"" : "";
                if (cell.Number is not null)
                    b.Append($"<c r=\"{reference}\"{style}><v>{cell.Number.Value.ToString(CultureInfo.InvariantCulture)}</v></c>");
                else
                    b.Append($"<c r=\"{reference}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(cell.Text ?? "")}</t></is></c>");
            }
            b.Append("</row>");
        }
        return b.Append("</sheetData></worksheet>").ToString();
    }

    private static string ToColumn(int n)
    {
        string result = "";
        while (n > 0) { n--; result = (char)('A' + n % 26) + result; n /= 26; }
        return result;
    }
    private static string GetPlayer(Tournament t, int id) => t.Players.First(p => p.Number == id).FullName;
    public static string ResultLabel(GameResult r) => r switch
    {
        GameResult.WhiteWin => "1:0", GameResult.BlackWin => "0:1", GameResult.Draw => "½:½",
        GameResult.WhiteForfeitWin => "+:- (тех. белых)",
        GameResult.BlackForfeitWin => "-:+ (тех. чёрных)",
        GameResult.DoubleForfeit => "-:-", _ => "Не сыграно"
    };
    public static string ByeLabel(ByeKind kind) => kind switch
    {
        ByeKind.PairingAllocated => "BYE (+1)", ByeKind.RequestedHalf => "Пропуск (+½)",
        ByeKind.RequestedZero => "Пропуск (0)", _ => "Свободен"
    };
    private static void Write(ZipArchive zip, string path, string contents)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        stream.Write(contents);
    }
}
