using System.Collections.Immutable;
using System.Globalization;
using Core.Interfaces.Data;
using Core.Models.Data;

namespace Data
{
    /// <summary>
    /// Implementazione di IProtocolRepository
    /// Fornisce metodi per ottenere un comando o una variabile dal file Excel
    /// </summary>
    internal class ExcelStemProtocolRepository : IProtocolRepository
    {
        private readonly IExcelRepository _excelRepository;
        private readonly string _excelFilePath;
        private readonly uint _recipientId;

        private ImmutableDictionary<string, ushort>? _commands;
        private ImmutableDictionary<string, ushort>? _variables;
        private readonly object _commandsLock = new();
        private readonly object _variablesLock = new();
        private readonly ImmutableDictionary<string, byte[]> _values;

        public ExcelStemProtocolRepository(
            IExcelRepository excelRepository,
            string excelFilePath,
            uint recipientId)
        {
            _excelRepository = excelRepository ?? throw new ArgumentNullException(nameof(excelRepository));
            _excelFilePath = !string.IsNullOrWhiteSpace(excelFilePath) ? excelFilePath : throw new ArgumentException("File path cannot be null or empty.", nameof(excelFilePath));
            _recipientId = recipientId;

            // Valori fissi definiti nel protocollo
            _values = new Dictionary<string, byte[]>
            {
                { "OFF", new byte[] { 0x00, 0x00, 0x00, 0x00 } },
                { "ON", new byte[] { 0x00, 0x00, 0x00, 0x80 } },
                { "SINGLE_BLINK", new byte[] { 0x00, 0xFF, 0x80, 0x61 } }
            }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        }

        // Restituisce il codice del comando dato il suo nome
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

        // Restituisce l'indirizzo della variabile dato il suo nome
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

        // Restituisce il valore byte[] dato il suo nome
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

        // Thread-safe lazy loading of commands - runs on thread pool to avoid UI deadlock
        private ImmutableDictionary<string, ushort> GetCommandsSync()
        {
            if (_commands != null)
            {
                return _commands;
            }

            lock (_commandsLock)
            {
                if (_commands != null)
                {
                    return _commands;
                }

                try
                {
                    // Run on thread pool to avoid deadlock with UI synchronization context
                    _commands = Task.Run(() => LoadCommandsAsync()).GetAwaiter().GetResult();
                    return _commands;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXCEL] Error loading commands: {ex}");
                    throw;
                }
            }
        }

        // Thread-safe lazy loading of variables - runs on thread pool to avoid UI deadlock
        private ImmutableDictionary<string, ushort> GetVariablesSync()
        {
            if (_variables != null)
            {
                return _variables;
            }

            lock (_variablesLock)
            {
                if (_variables != null)
                {
                    return _variables;
                }

                try
                {
                    // Run on thread pool to avoid deadlock with UI synchronization context
                    _variables = Task.Run(() => LoadVariablesAsync()).GetAwaiter().GetResult();
                    return _variables;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EXCEL] Error loading variables: {ex}");
                    throw;
                }
            }
        }

        // Helper: Carica i comandi dal file Excel in un dizionario
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
                throw; // Re-throw the specific exception to allow for specific handling
            }
            catch (Exception ex)
            {
                // Wrap other exceptions for context, but let known ones pass through
                throw new Exception("Error loading commands from Excel.", ex);
            }
        }

        // Helper: Carica le variabili dal file Excel in un dizionario
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
                throw; // Re-throw the specific exception
            }
            catch (Exception ex)
            {
                throw new Exception("Error loading variables from Excel.", ex);
            }
        }

        // Helper: Converte due stringhe esadecimali in un ushort
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
