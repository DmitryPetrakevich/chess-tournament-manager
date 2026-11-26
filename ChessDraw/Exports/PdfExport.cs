using System.Drawing.Printing;
using ChessDraw.Models;

namespace ChessDraw.Exports;

public static class PdfExport
{
    public static void Save(Tournament tournament, string path)
    {
        if (!PrinterSettings.InstalledPrinters.Cast<string>()
            .Any(p => p.Equals("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Включите компонент Windows «Microsoft Print to PDF» для экспорта PDF.");
        using var document = new PrintDocument();
        document.DocumentName = "ТУСУР — " + tournament.Name;
        document.PrinterSettings.PrinterName = "Microsoft Print to PDF";
        document.PrinterSettings.PrintToFile = true;
        document.PrinterSettings.PrintFileName = Path.GetFullPath(path);
        document.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
        document.DefaultPageSettings.Landscape = true;
        document.DefaultPageSettings.Margins = new Margins(46, 46, 40, 40);
        document.PrintController = new StandardPrintController();
        using var report = new TournamentReport(tournament);
        IReadOnlyList<ReportPage>? pages = null;
        int page = 0;
        document.PrintPage += (_, e) =>
        {
            var graphics = e.Graphics ?? throw new InvalidOperationException("Ошибка печати.");
            pages ??= report.Paginate(graphics, e.MarginBounds.Size);
            report.DrawPage(graphics, e.MarginBounds, pages[page], page + 1, pages.Count);
            e.HasMorePages = ++page < pages.Count;
        };
        document.Print();
    }
}
