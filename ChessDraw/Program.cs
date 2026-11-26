using ChessDraw.Services;
using ChessDraw.UI;
using System.Text;

namespace ChessDraw;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                SelfTests.Run(args.Contains("--quiet"));
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ReportException(e.Exception, "Ошибка интерфейса");
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) LogException(ex, "Необработанная ошибка");
            };
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            if (args.Contains("--quiet"))
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-failure.txt"), ex.ToString());
                Environment.ExitCode = 1;
            }
            else ReportException(ex, "Не удалось запустить ТУСУР — шахматная жеребьёвка");
        }
    }

    private static void ReportException(Exception ex, string caption)
    {
        var location = LogException(ex, caption);
        try
        {
            MessageBox.Show($"{caption}:\n\n{ex.Message}\n\nПодробности: {location}",
                "ChessDraw — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { /* Logging must remain safe if the UI subsystem itself failed. */ }
    }

    private static string LogException(Exception ex, string caption)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChessDraw");
        var file = Path.Combine(folder, "startup-errors.log");
        try
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {caption}\n{ex}\n\n", Encoding.UTF8);
            return file;
        }
        catch
        {
            // Fall back to a folder the application can usually write to.
            file = Path.Combine(Path.GetTempPath(), "ChessDraw-startup-errors.log");
            try { File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {caption}\n{ex}\n\n", Encoding.UTF8); }
            catch { return "не удалось записать файл ошибок"; }
            return file;
        }
    }
}
