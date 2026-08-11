using System;

using Microsoft.UI.Xaml;

using MarkdownEditor.Helpers;
using MarkdownEditor.Services;

namespace MarkdownEditor
{
    /// <summary>
    /// Application entry point.
    /// Initializes services in dependency order before showing the main window.
    /// </summary>
    public partial class App : Application
    {
        // ------------------------------------------------------------------
        // Global service accessors
        // ------------------------------------------------------------------

        public static ILoggingService? Logger { get; private set; }
        public static ISettingsService? Settings { get; private set; }
        public static IAutosaveService? Autosave { get; private set; }
        public static IMarkdownFileService? Files { get; private set; }
        public static ITemplateService? Templates { get; private set; }
        public static IPdfExportService? PdfExport { get; private set; }

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private Window? _window;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        public App()
        {
            InitializeComponent();

            this.UnhandledException += OnUnhandledException;
        }

        // ------------------------------------------------------------------
        // Launch
        // ------------------------------------------------------------------

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                InitializeServices();
                LogStartupEnvironment();

                _window = new MainWindow();
                _window.Activate();
            }
            catch (Exception ex)
            {
                Logger?.Error("Fatal error during application startup.", ex, "Startup");
                throw;
            }
        }

        // ------------------------------------------------------------------
        // Service initialization
        // ------------------------------------------------------------------

        private void InitializeServices()
        {
            // 1. Portable folder layout must come first.
            AppFolders.Initialize();

            // 2. Logger depends on AppFolders only.
            Logger = new LoggingService(LogLevel.Debug);
            Logger.Info("==== Markdown Editor starting ====", "Startup");

            // 3. Settings depend on AppFolders and Logger.
            Settings = new SettingsService(Logger);

            // 4. Autosave depends on Logger and Settings.
            Autosave = new AutosaveService(Logger, Settings);

            // 5. File service depends on Logger and Settings.
            Files = new MarkdownFileService(Logger, Settings);

            // 6. Templates for md files
            Templates = new TemplateService(Logger);

            // 7. ODF Exports
            PdfExport = new PdfExportService(Logger);

            Logger.Info("All services initialized.", "Startup");
        }

        // ------------------------------------------------------------------
        // Startup diagnostics
        // ------------------------------------------------------------------

        private void LogStartupEnvironment()
        {
            if (Logger == null)
            {
                return;
            }

            try
            {
                RuntimeEnvironmentInfo env = PortableModeDetector.GetEnvironment();

                Logger.Info($"Portable mode      : {env.IsPortable}", "Startup");
                Logger.Info($"AppFolder writable : {env.IsAppFolderWritable}", "Startup");
                Logger.Info($"App root           : {env.AppRoot}", "Startup");
                Logger.Info($"Data root          : {env.DataRoot}", "Startup");
                Logger.Info($"Profile root       : {env.ProfileRoot}", "Startup");
                Logger.Info($"Drive              : {env.DriveLetter} [{env.DriveKind}]", "Startup");
                Logger.Info($"Drive format       : {env.DriveFormat}", "Startup");
                Logger.Info($"Drive label        : {env.DriveLabel}", "Startup");
                Logger.Info($"User               : {env.UserName}", "Startup");
                Logger.Info($"Machine            : {env.MachineName}", "Startup");
                Logger.Info($"OS                 : {env.OSVersion}", "Startup");
                Logger.Info($"Architecture       : {env.ProcessArchitecture}", "Startup");
                Logger.Info($"Summary            : {env}", "Startup");

                if (Logger is LoggingService concreteLogger)
                {
                    Logger.Info($"Log file           : {concreteLogger.CurrentLogFilePath}", "Startup");
                }

                if (Settings != null)
                {
                    Logger.Info($"Settings file      : {Settings.SettingsFilePath}", "Startup");
                    Logger.Info($"Theme              : {Settings.Current.Theme}", "Startup");
                    Logger.Info($"Autosave interval  : {Settings.Current.AutosaveIntervalSeconds}s", "Startup");
                    Logger.Info($"Max backups        : {Settings.Current.MaxBackups}", "Startup");
                }

                if (Autosave != null)
                {
                    Logger.Info($"Autosave folder    : {Autosave.AutosaveFolderPath}", "Startup");
                }

                if (Templates != null)
                {
                    Logger.Info($"Templates folder   : {Templates.TemplatesFolderPath}", "Startup");
                }

                if (PdfExport != null)
                {
                    Logger.Info("PDF export service ready.", "Startup");
                }


            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to log startup environment: {ex.Message}", "Startup");
            }
        }

        // ------------------------------------------------------------------
        // Global exception handler
        // ------------------------------------------------------------------

        private void OnUnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                Logger?.Error(
                    "Unhandled application exception: " + e.Message,
                    e.Exception,
                    "App");
            }
            catch
            {
                // Logging must never throw inside an exception handler.
            }

            // Mark as handled to keep the app alive when safe to do so.
            e.Handled = true;
        }
    }
}