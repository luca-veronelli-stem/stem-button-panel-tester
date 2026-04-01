using Communication;
using Communication.Protocol;
using Core.Interfaces.Communication;
using Core.Interfaces.Data;
using Core.Interfaces.Infrastructure;
using Core.Interfaces.Services;
using Data;
using Infrastructure;
using Infrastructure.Lib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services;
using Services.Lib;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GUI.Windows
{
    internal static class Program
    {
        // P/Invoke for AddDllDirectory (Kernel32.dll) - Adds a directory to the DLL search path
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddDllDirectory(string lpPathName);

        // P/Invoke for SetDllDirectory (alternative fallback if needed)
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // P/Invoke for SetDefaultDllDirectories to opt-in to safer DLL search behavior
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000u;

        // Keep a handle to the explicitly-loaded embedded PCANBasic to prevent it being unloaded
        private static IntPtr s_pcanLibHandle = IntPtr.Zero;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            // Prepare base information and log path as early as possible
            var baseDir = AppContext.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);
            var logFile = Path.Combine(logsDir, $"startup_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.log");

            // Create an early LoggerFactory that writes to console/debug/file so we can trace startup issues
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
                builder.AddDebug();
                builder.AddProvider(new FileLoggerProvider(logFile));
            });

            var logger = loggerFactory.CreateLogger("Startup");
            logger.LogInformation("Application starting. BaseDirectory='{BaseDir}', ProcessId={Pid}, OS={OS}, Architecture={Arch}, Framework={Framework}",
                baseDir, Process.GetCurrentProcess().Id, RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture, RuntimeInformation.FrameworkDescription);

            try
            {
                // Write an initial probe with environment information
                GatherBasicEnvironmentProbe(baseDir, logFile, logger);

                // Extract native PCAN library from project's Resources.resx and add its folder to DLL search path
                ExtractPcanFromResx(logger);

                // Perform detailed probing for PCANBasic and native loadability
                PerformDetailedPcanProbes(logger);

                ApplicationConfiguration.Initialize();

                // Wire global exception handlers to capture startup/runtime crashes
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception in AppDomain");
                    TryWriteErrorFiles(Path.Combine(baseDir, "pcan-error.txt"), e.ExceptionObject, logger);
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    logger.LogError(e.Exception, "Unobserved task exception");
                    TryWriteErrorFiles(Path.Combine(baseDir, "pcan-error.txt"), e.Exception, logger);
                };

                Application.ThreadException += (s, e) =>
                {
                    logger.LogError(e.Exception, "UI thread exception");
                    TryWriteErrorFiles(Path.Combine(baseDir, "pcan-error.txt"), e.Exception, logger);
                };

                // Imposta il container di DI
                var services = new ServiceCollection();

                // Imposta logging for DI consumer code (re-use file provider so all logs end up in the same file)
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Trace);
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.AddProvider(new FileLoggerProvider(logFile));
                });

                // Imposta repositories
                string excelFilePath;
                // Prefer embedded resource only: extract and use embedded 'StemDictionaries.xlsx' from Resources.resx
                var resourcesDir = Path.Combine(baseDir, "Resources");
                Directory.CreateDirectory(resourcesDir);
                var excelOutPath = Path.Combine(resourcesDir, "StemDictionaries.xlsx");

                // Try ResourceManager first (key: "StemDictionaries"), then strongly-typed property
                byte[]? excelBytes = null;
                try
                {
                    var obj = Properties.Resources.ResourceManager.GetObject("StemDictionaries", Properties.Resources.Culture);
                    if (obj is byte[] b && b.Length > 0) excelBytes = b;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error while obtaining embedded resource via ResourceManager for 'StemDictionaries'");
                }

                if (excelBytes == null)
                {
                    try
                    {
                        var prop = typeof(Properties.Resources).GetProperty("StemDictionaries", BindingFlags.Public | BindingFlags.Static);
                        if (prop != null)
                        {
                            var val = prop.GetValue(null);
                            if (val is byte[] b2 && b2.Length > 0) excelBytes = b2;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error while obtaining embedded resource via strongly-typed Properties.Resources for 'StemDictionaries'");
                    }
                }

                if (excelBytes == null || excelBytes.Length == 0)
                {
                    logger.LogCritical("Embedded Excel resource 'StemDictionaries' not found in Resources.resx.");
                    throw new FileNotFoundException("Embedded Excel resource 'StemDictionaries' not found in Resources.resx.");
                }

                File.WriteAllBytes(excelOutPath, excelBytes);
                excelFilePath = excelOutPath;
                logger.LogInformation("Extracted embedded StemDictionaries.xlsx to {ExcelOutPath}", excelOutPath);

                services.AddTransient<IExcelRepository, ExcelRepository>();
                services.AddTransient<IProtocolRepositoryFactory>(sp =>
                    new ExcelProtocolRepositoryFactory(sp.GetRequiredService<IExcelRepository>(), excelFilePath));
                services.AddTransient<IProtocolRepository>(sp =>
                {
                    // Repository di default per recipientId = 0
                    var factory = sp.GetRequiredService<IProtocolRepositoryFactory>();
                    return factory.Create(0);
                });

                // Pre-load protocol repository to avoid blocking during tests
                logger.LogInformation("Pre-loading protocol repository to cache Excel data...");
                var preloadFactory = new ExcelProtocolRepositoryFactory(new ExcelRepository(), excelFilePath);

                // Pre-load for the most common recipient IDs used in testing
                var commonRecipientIds = new[] { 0x00030101u, 0x000A0101u, 0x000B0101u, 0x000C0101u };
                foreach (var recipientId in commonRecipientIds)
                {
                    try
                    {
                        await preloadFactory.PreloadAsync(recipientId).ConfigureAwait(false);
                        logger.LogInformation("Pre-loaded protocol data for recipientId=0x{RecipientId:X8}", recipientId);
                    }
                    catch (Exception preloadEx)
                    {
                        logger.LogWarning(preloadEx, "Failed to pre-load protocol repository for recipientId=0x{RecipientId:X8}", recipientId);
                    }
                }
                logger.LogInformation("Protocol repository pre-loading completed");

                // Imposta communication e infrastructure
                services.AddSingleton<IPcanApi, PcanApiWrapper>();
                services.AddSingleton<ICanAdapter, PcanAdapter>();
                services.AddSingleton<CanCommunicationManager>();
                services.AddTransient<Func<CanCommunicationManager>>(sp =>
                    () => sp.GetRequiredService<CanCommunicationManager>());
                services.AddSingleton<IProtocolManager, StemProtocolManager>();
                services.AddTransient<ICommunicationManagerFactory, CommunicationManagerFactory>();
                services.AddSingleton<ICommunicationService, CommunicationService>();

                // Imposta services
                services.AddSingleton<IBaptizeService, BaptizeService>();
                services.AddSingleton<IButtonPanelTestService>(sp =>
                {
                    var commService = sp.GetRequiredService<ICommunicationService>();
                    var baptizeService = sp.GetRequiredService<IBaptizeService>();
                    var protocolRepo = sp.GetRequiredService<IProtocolRepository>();
                    var logger = sp.GetService<ILogger<ButtonPanelTestService>>();
                    var canAdapter = sp.GetRequiredService<ICanAdapter>();

                    var service = new ButtonPanelTestService(commService, baptizeService, protocolRepo, logger);

                    // Configura l'adattatore CAN per abilitare il recovery automatico
                    service.SetCanAdapter(canAdapter);

                    return service;
                });

                // Registra Form1
                services.AddTransient<Form1>();

                // Costruisci il service provider and run
                await using var serviceProvider = services.BuildServiceProvider();
                logger.LogInformation("ServiceProvider built successfully. Running application.");
                Application.Run(serviceProvider.GetRequiredService<Form1>());
            }
            catch (Exception ex)
            {
                // Log the exception to file and console, rethrow if needed
                logger.LogCritical(ex, "Fatal error during application startup");
                try
                {
                    File.AppendAllText(logFile, $"\nFATAL: {DateTime.Now:O} - {ex}\n");
                }
                catch { }

                // Also create a concise probe file to help remote debugging
                try
                {
                    File.WriteAllText(Path.Combine(baseDir, "pcan-error.txt"), ex.ToString());
                }
                catch { }

                // Rethrow to allow default behavior (or exit)
                throw;
            }
            finally
            {
                logger.LogInformation("Application startup sequence completed (Main exit).");
            }
        }

        private static void GatherBasicEnvironmentProbe(string baseDir, string logFile, ILogger logger)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Probe generated: {DateTime.Now:O}");
                sb.AppendLine($"BaseDir: {baseDir}");
                sb.AppendLine($"CurrentDirectory: {Directory.GetCurrentDirectory()}");
                sb.AppendLine($"ProcessId: {Process.GetCurrentProcess().Id}");
                sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
                sb.AppendLine($"Arch: {RuntimeInformation.ProcessArchitecture}");
                sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
                sb.AppendLine($"User: {Environment.UserName}");
                sb.AppendLine($"Culture: {CultureInfo.CurrentCulture.Name}");
                sb.AppendLine("Environment variables PATH entries:");

                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var entries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < entries.Length; i++)
                {
                    sb.AppendLine($"  [{i}] {entries[i]}");
                }

                try
                {
                    sb.AppendLine("\nFiles in BaseDir:");
                    var files = Directory.GetFiles(baseDir);
                    foreach (var f in files.OrderBy(f => f)) sb.AppendLine("  " + Path.GetFileName(f));
                }
                catch (Exception ex) { sb.AppendLine("  (unable to enumerate base dir) " + ex.Message); }

                try
                {
                    sb.AppendLine("\nLoaded Process Modules:");
                    foreach (ProcessModule mod in Process.GetCurrentProcess().Modules)
                    {
                        sb.AppendLine($"  {mod.ModuleName} ({mod.FileName})");
                    }
                }
                catch (Exception ex) { sb.AppendLine("  (unable to enumerate process modules) " + ex.Message); }

                var probePath = Path.Combine(baseDir, "pcan-probe.txt");
                File.WriteAllText(probePath, sb.ToString());
                logger.LogInformation("Wrote basic environment probe to {ProbePath}", probePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write basic environment probe");
            }
        }

        private static void PerformDetailedPcanProbes(ILogger logger)
        {
            var baseDir = AppContext.BaseDirectory;
            var outDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(outDir);
            var probeDetails = new StringBuilder();
            probeDetails.AppendLine($"Detailed PCAN probe: {DateTime.Now:O}");

            // Look for PCANBasic.dll in base dir and images/resources
            var candidates = new List<string>();
            candidates.Add(Path.Combine(baseDir, "PCANBasic.dll"));
            candidates.Add(Path.Combine(baseDir, "Resources", "PCANBasic.dll"));
            candidates.Add(Path.Combine(baseDir, "x86", "PCANBasic.dll"));
            candidates.Add(Path.Combine(baseDir, "x64", "PCANBasic.dll"));

            // Add copies found in PATH
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var entries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                try
                {
                    var candidate = Path.Combine(entry, "PCANBasic.dll");
                    candidates.Add(candidate);
                }
                catch { }
            }

            // Make unique
            candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var c in candidates)
            {
                try
                {
                    probeDetails.AppendLine($"Candidate: {c}");
                    probeDetails.AppendLine($"  Exists: {File.Exists(c)}");
                    if (File.Exists(c))
                    {
                        try
                        {
                            var fi = new FileInfo(c);
                            probeDetails.AppendLine($"  Size: {fi.Length} bytes");
                            probeDetails.AppendLine($"  LastWrite: {fi.LastWriteTimeUtc:O}");
                        }
                        catch (Exception ex) { probeDetails.AppendLine("  (unable to stat file) " + ex.Message); }

                        // Try to LoadLibrary the candidate to see if kernel can load it (lightweight probe)
                        try
                        {
                            IntPtr h = LoadLibrary(c);
                            if (h != IntPtr.Zero)
                            {
                                probeDetails.AppendLine("  LoadLibrary: OK");
                                FreeLibrary(h);
                            }
                            else
                            {
                                var err = Marshal.GetLastWin32Error();
                                probeDetails.AppendLine($"  LoadLibrary: FAILED (Win32Err={err})");
                            }
                        }
                        catch (Exception ex)
                        {
                            probeDetails.AppendLine("  LoadLibrary probe failed: " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    probeDetails.AppendLine("  Candidate probe exception: " + ex.Message);
                }
            }

            // Also attempt to LoadLibrary by simple name (kernel will search DLL directories)
            try
            {
                probeDetails.AppendLine("\nProbing LoadLibrary(\"PCANBasic.dll\") (this relies on current DLL search path):");
                IntPtr h2 = LoadLibrary("PCANBasic.dll");
                if (h2 != IntPtr.Zero)
                {
                    probeDetails.AppendLine("  LoadLibrary by name: OK");
                    FreeLibrary(h2);
                }
                else
                {
                    var err = Marshal.GetLastWin32Error();
                    probeDetails.AppendLine($"  LoadLibrary by name: FAILED (Win32Err={err})");
                }
            }
            catch (Exception ex)
            {
                probeDetails.AppendLine("  LoadLibrary by name probe failed: " + ex.Message);
            }

            var probeFile = Path.Combine(outDir, "pcan-detailed-probe.txt");
            try
            {
                File.WriteAllText(probeFile, probeDetails.ToString());
                logger.LogInformation("Wrote detailed PCAN probe to {ProbeFile}", probeFile);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to write detailed PCAN probe to {ProbeFile}", probeFile);
            }
        }

        private static void TryWriteErrorFiles(string errorFile, object? exceptionObject, ILogger logger)
        {
            try
            {
                if (exceptionObject is Exception ex)
                    File.WriteAllText(errorFile, ex.ToString());
                else
                    File.WriteAllText(errorFile, exceptionObject?.ToString() ?? "Unknown error object");
            }
            catch (Exception writeEx)
            {
                logger.LogWarning(writeEx, "Unable to write error file '{ErrorFile}'", errorFile);
            }
        }

        /// <summary>
        /// Extracts an embedded resource DLL to a temp directory and adds the path for loading.
        /// Attempts pure manifest extraction first; if that fails and the resource is stored in Resources.resx
        /// it will extract from `Properties.Resources` (e.g. `StemDictionaries` key).
        /// </summary>
        /// <param name="resourceName">The name of the embedded resource (e.g., "Resources.StemDictionaries.xlsx" or "PCANBasic.dll").</param>
        private static string ExtractEmbeddedResource(string resourceName, ILogger? logger = null)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string namespacePrefix = assembly.GetName().Name + ".";
            string fullResourceName = namespacePrefix + resourceName;

            string tempDir = Path.Combine(Path.GetTempPath(), "StemDeviceManager_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string resourcePath = Path.Combine(tempDir, resourceName);

            if (File.Exists(resourcePath))
            {
                logger?.LogDebug("Embedded resource already exists at {Path}", resourcePath);
                return tempDir;
            }

            // Try manifest resource first (for files embedded as standalone EmbeddedResource)
            using (Stream? resourceStream = assembly.GetManifestResourceStream(fullResourceName))
            {
                if (resourceStream != null)
                {
                    using FileStream fileStream = new(resourcePath, FileMode.Create, FileAccess.Write);
                    resourceStream.CopyTo(fileStream);

                    logger?.LogInformation("Extracted manifest embedded resource '{Resource}' to '{OutPath}'", fullResourceName, resourcePath);

                    AppDomain.CurrentDomain.ProcessExit += (s, e) => Directory.Delete(tempDir, true);

                    return tempDir;
                }
            }

            // Fallback: resource may be stored inside Resources.resx (Properties.Resources)
            // Expect resourceName like "Resources.StemDictionaries.xlsx" -> key is last token before extension: "StemDictionaries"
            string fileNameNoExt = Path.GetFileNameWithoutExtension(resourceName) ?? resourceName;
            string[] tokens = fileNameNoExt.Split('.');
            string resourceKey = tokens.Length > 1 ? tokens[^1] : fileNameNoExt;

            // Try ResourceManager first
            try
            {
                var obj = Properties.Resources.ResourceManager.GetObject(resourceKey, Properties.Resources.Culture);
                if (obj is byte[] bytes && bytes.Length > 0)
                {
                    File.WriteAllBytes(resourcePath, bytes);
                    logger?.LogInformation("Extracted resource '{ResourceKey}' from Properties.Resources to '{OutPath}'", resourceKey, resourcePath);

                    AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { Directory.Delete(tempDir, true); } catch { } };

                    return tempDir;
                }

                // Try strongly-typed property (in case resource is exposed as byte[] property)
                var prop = typeof(Properties.Resources).GetProperty(resourceKey, BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    var val = prop.GetValue(null);
                    if (val is byte[] b2 && b2.Length > 0)
                    {
                        File.WriteAllBytes(resourcePath, b2);
                        logger?.LogInformation("Extracted strongly-typed resource '{ResourceKey}' to '{OutPath}'", resourceKey, resourcePath);

                        AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { Directory.Delete(tempDir, true); } catch { } };

                        return tempDir;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error while extracting embedded resource '{ResourceName}'", resourceName);
            }

            throw new FileNotFoundException($"Embedded resource '{fullResourceName}' not found.");
        }

        // New: Extract PCANBasic.dll from generated Resources (resx) and register its directory for native loading
        private static void ExtractPcanFromResx(ILogger logger)
        {
            const string resourceFileName = "PCANBasic.dll";
            string tempDir = Path.Combine(Path.GetTempPath(), "StemDeviceManager_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string outPath = Path.Combine(tempDir, resourceFileName);

            try
            {
                if (!File.Exists(outPath))
                {
                    byte[]? data = null;

                    // Try strongly-typed first
                    try
                    {
                        data = Properties.Resources.PCANBasic;
                        if (data == null || data.Length == 0) data = null;
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Properties.Resources.PCANBasic not available or failed");
                        data = null;
                    }

                    if (data == null)
                    {
                        // Try manifest or resources helper
                        try
                        {
                            var manifestDir = ExtractEmbeddedResource("Resources." + resourceFileName, logger);
                            var candidate = Path.Combine(manifestDir, "Resources." + resourceFileName);
                            if (File.Exists(candidate)) File.Copy(candidate, outPath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "Manifest extraction attempt for PCANBasic failed");
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(outPath, data);
                    }
                }

                if (!File.Exists(outPath))
                {
                    logger.LogWarning("PCANBasic.dll was not extracted to expected path '{OutPath}'", outPath);
                    throw new FileNotFoundException("Embedded resource 'PCANBasic' not found in Resources.resx.");
                }

                logger.LogInformation("PCANBasic.dll extracted to {OutPath}", outPath);

                // Clean up on exit - ensure the library handle is freed before deleting the temp dir
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    try
                    {
                        if (s_pcanLibHandle != IntPtr.Zero)
                        {
                            try { FreeLibrary(s_pcanLibHandle); } catch { }
                            s_pcanLibHandle = IntPtr.Zero;
                        }

                        Directory.Delete(tempDir, true);
                    }
                    catch { }
                };

                // Prefer secure default search order then add our directory
                try
                {
                    if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS))
                    {
                        var setErr = Marshal.GetLastWin32Error();
                        logger.LogWarning("SetDefaultDllDirectories failed (Win32Err={Win32}) - continuing", setErr);
                    }
                    else
                    {
                        logger.LogDebug("SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS) succeeded");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "SetDefaultDllDirectories call threw");
                }

                // Add our tempDir to the DLL search path
                if (!AddDllDirectory(tempDir))
                {
                    logger.LogWarning("AddDllDirectory returned false for {TempDir}; attempting SetDllDirectory", tempDir);
                    if (!SetDllDirectory(tempDir))
                    {
                        var win32Err = Marshal.GetLastWin32Error();
                        logger.LogError("Failed to set DLL directory. Win32Error={Win32}", win32Err);
                        throw new System.ComponentModel.Win32Exception(win32Err, "Failed to set DLL directory.");
                    }
                }

                // Explicitly load the extracted PCANBasic.dll using full path to ensure the embedded copy is loaded
                try
                {
                    s_pcanLibHandle = LoadLibrary(outPath);
                    if (s_pcanLibHandle != IntPtr.Zero)
                    {
                        logger.LogInformation("Explicitly loaded PCANBasic.dll from extracted path: {OutPath} (handle=0x{Handle:X})", outPath, s_pcanLibHandle.ToInt64());
                    }
                    else
                    {
                        var loadErr = Marshal.GetLastWin32Error();
                        logger.LogWarning("Explicit LoadLibrary for extracted PCANBasic.dll failed (Win32Err={Win32}); kernel may still load the system copy by name. Continuing.", loadErr);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Explicit LoadLibrary(outPath) threw while attempting to load embedded PCANBasic.dll");
                }

                logger.LogInformation("DLL search path updated to include {TempDir}", tempDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting or registering PCANBasic.dll");
                throw;
            }
        }
    }

    /// <summary>
    /// Very small file logger provider used to capture startup logs to a file without adding external dependencies.
    /// Thread-safe and minimal.
    /// </summary>
    internal sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _path;
        private readonly object _sync = new();
        private bool _disposed;

        public FileLoggerProvider(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(_path, _sync);

        public void Dispose()
        {
            _disposed = true;
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _path;
            private readonly object _sync;

            public FileLogger(string path, object sync)
            {
                _path = path;
                _sync = sync;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId,
                TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var sb = new StringBuilder();
                sb.Append('[').Append(DateTime.Now.ToString("o")).Append("] ");
                sb.Append(logLevel.ToString()).Append(" - ");
                sb.Append(formatter(state, exception));
                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append(exception.ToString());
                }
                sb.AppendLine();

                try
                {
                    lock (_sync)
                    {
                        File.AppendAllText(_path, sb.ToString());
                    }
                }
                catch
                {
                    // Avoid throwing from logger
                }
            }
        }
    }
}
