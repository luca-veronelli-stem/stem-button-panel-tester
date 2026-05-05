using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Core.Interfaces.Data;
using Core.Models.Data;

namespace Data
{
    /// <summary>
    /// Cached implementation of IProtocolRepository that loads Excel data once at startup
    /// and shares it across all instances with the same recipient ID.
    /// This eliminates the blocking behavior when creating repositories during tests.
    /// </summary>
    internal class CachedExcelProtocolRepository : IProtocolRepository
    {
        private readonly uint _recipientId;
        private static readonly ImmutableDictionary<string, byte[]> _values;

        // Global cache shared across all instances
        private static readonly ConcurrentDictionary<string, ImmutableDictionary<string, ushort>> _commandsCache = new();
        private static readonly ConcurrentDictionary<uint, ImmutableDictionary<string, ushort>> _variablesCache = new();
        private static readonly SemaphoreSlim _commandsLock = new(1, 1);
        private static readonly SemaphoreSlim _variablesLock = new(1, 1);

        private readonly IExcelRepository _excelRepository;
        private readonly string _excelFilePath;

        static CachedExcelProtocolRepository()
        {
            // Valori fissi definiti nel protocollo
            _values = new Dictionary<string, byte[]>
            {
                { "OFF", new byte[] { 0x00, 0x00, 0x00, 0x00 } },
                { "ON", new byte[] { 0x00, 0x00, 0x00, 0x80 } },
                { "SINGLE_BLINK", new byte[] { 0x00, 0xFF, 0x80, 0x61 } }
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public CachedExcelProtocolRepository(
            IExcelRepository excelRepository,
            string excelFilePath,
            uint recipientId)
        {
            _excelRepository = excelRepository ?? throw new ArgumentNullException(nameof(excelRepository));
            _excelFilePath = !string.IsNullOrWhiteSpace(excelFilePath) ? excelFilePath : throw new ArgumentException("File path cannot be null or empty.", nameof(excelFilePath));
            _recipientId = recipientId;
        }

        public ushort GetCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException("Command name cannot be null or empty.", nameof(commandName));
            }

            ImmutableDictionary<string, ushort> commands = GetCommandsSync();
            if (commands.TryGetValue(commandName, out ushort value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Command '{commandName}' not found in Excel.");
        }

        public ushort GetVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("Variable name cannot be null or empty.", nameof(variableName));
            }

            ImmutableDictionary<string, ushort> variables = GetVariablesSync();
            if (variables.TryGetValue(variableName, out ushort value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Variable '{variableName}' not found in Excel.");
        }

        public byte[] GetValue(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName))
            {
                throw new ArgumentException("Value name cannot be null or empty.", nameof(valueName));
            }

            if (_values.TryGetValue(valueName, out byte[]? value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Value '{valueName}' not found.");
        }

        /// <summary>
        /// Pre-loads commands and variables for the specified recipient ID.
        /// This should be called at application startup to avoid blocking during tests.
        /// </summary>
        public static async Task PreloadAsync(IExcelRepository excelRepository, string excelFilePath, uint recipientId)
        {
            var repo = new CachedExcelProtocolRepository(excelRepository, excelFilePath, recipientId);

            // Force load by accessing the methods
            _ = await Task.Run(() =>
            {
                try
                {
                    repo.GetCommandsSync();
                    repo.GetVariablesSync();
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);
        }

        private ImmutableDictionary<string, ushort> GetCommandsSync()
        {
            // Check cache first (no lock needed for read)
            if (_commandsCache.TryGetValue(_excelFilePath, out ImmutableDictionary<string, ushort>? cached))
            {
                return cached;
            }

            // Use semaphore for async-compatible locking
            _commandsLock.Wait();
            try
            {
                // Double-check after acquiring lock
                if (_commandsCache.TryGetValue(_excelFilePath, out cached))
                {
                    return cached;
                }

                // Load and cache
                ImmutableDictionary<string, ushort> commands = Task.Run(() => LoadCommandsAsync()).GetAwaiter().GetResult();
                _commandsCache[_excelFilePath] = commands;
                return commands;
            }
            finally
            {
                _commandsLock.Release();
            }
        }

        private ImmutableDictionary<string, ushort> GetVariablesSync()
        {
            // Check cache first (no lock needed for read)
            if (_variablesCache.TryGetValue(_recipientId, out ImmutableDictionary<string, ushort>? cached))
            {
                return cached;
            }

            // Use semaphore for async-compatible locking
            _variablesLock.Wait();
            try
            {
                // Double-check after acquiring lock
                if (_variablesCache.TryGetValue(_recipientId, out cached))
                {
                    return cached;
                }

                // Load and cache
                ImmutableDictionary<string, ushort> variables = Task.Run(() => LoadVariablesAsync()).GetAwaiter().GetResult();
                _variablesCache[_recipientId] = variables;
                return variables;
            }
            finally
            {
                _variablesLock.Release();
            }
        }

        private async Task<ImmutableDictionary<string, ushort>> LoadCommandsAsync()
        {
            try
            {
                StemProtocolData protocolData = await _excelRepository.GetProtocolDataFromFileAsync(_excelFilePath).ConfigureAwait(false);

                return protocolData.Commands.ToImmutableDictionary(
                    cmd => cmd.Name,
                    cmd => ParseHexToUShort(cmd.CmdH, cmd.CmdL),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Error loading commands from Excel.", ex);
            }
        }

        private async Task<ImmutableDictionary<string, ushort>> LoadVariablesAsync()
        {
            try
            {
                StemProtocolData protocolData = await _excelRepository.GetDictionaryFromFileAsync(_excelFilePath, _recipientId).ConfigureAwait(false);

                return protocolData.Variables.ToImmutableDictionary(
                    var => var.Name,
                    var => ParseHexToUShort(var.AddrH, var.AddrL),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Error loading variables from Excel.", ex);
            }
        }

        private static ushort ParseHexToUShort(string highHex, string lowHex)
        {
            if (!ushort.TryParse(highHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort high) ||
                !ushort.TryParse(lowHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort low))
            {
                throw new FormatException($"Invalid hex format: High='{highHex}', Low='{lowHex}'");
            }

            return (ushort)((high << 8) | low);
        }
    }
}
