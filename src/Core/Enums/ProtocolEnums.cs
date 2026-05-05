namespace Core.Enums
{
    /// <summary>
    /// Rappresenta le versioni supportate del protocollo di comunicazione STEM.
    /// </summary>
    public enum ProtocolVersion : byte
    {
        /// <summary>
        /// Versione 1 del protocollo (attuale).
        /// </summary>
        V1 = 1
    }

    /// <summary>
    /// Rappresenta i tipi di crittografia supportati nel livello di trasporto.
    /// </summary>
    public enum CryptType : byte
    {
        /// <summary>
        /// Nessuna crittografia applicata al pacchetto.
        /// </summary>
        None = 0
    }
}
