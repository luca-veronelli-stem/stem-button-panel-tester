using Core.Enums;
using Core.Interfaces.GUI;
using Core.Models;
using Core.Models.Services;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace GUI.Windows.Views
{
    public partial class ButtonPanelTestUserControl : UserControl, IButtonPanelTestView
    {
        // Gestori eventi invocati quando l'utente avvia o arresta il collaudo
        public event EventHandler<ButtonPanelType>? OnPanelTypeChanged;
        public event EventHandler? OnStartTestClicked;
        public event EventHandler? OnStopTestClicked;
        public event EventHandler? OnSaveNewFileClicked;
        public event EventHandler? OnSaveExistingFileClicked;

        private ButtonPanelType? selectedPanelType;
        private ButtonPanelTestType? selectedTestType;
        private string? _lastSavedFilePath;
        private List<ButtonIndicator> _buttonIndicators = [];
        private readonly Dictionary<ButtonPanelType, List<RectangleF>> _buttonRegions = new()
        {
            {
                ButtonPanelType.DIS0023789, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0025205, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0026166 , new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            },
            {
                ButtonPanelType.DIS0026182, new List<RectangleF>
                {
                    RectangleF.FromLTRB(0.17f, 0.10f, 0.23f, 0.15f),
                    RectangleF.FromLTRB(0.32f, 0.10f, 0.38f, 0.15f),
                    RectangleF.FromLTRB(0.47f, 0.10f, 0.53f, 0.15f),
                    RectangleF.FromLTRB(0.62f, 0.10f, 0.68f, 0.15f),
                    RectangleF.FromLTRB(0.17f, 0.85f, 0.23f, 0.90f),
                    RectangleF.FromLTRB(0.32f, 0.85f, 0.38f, 0.90f),
                    RectangleF.FromLTRB(0.47f, 0.85f, 0.53f, 0.90f),
                    RectangleF.FromLTRB(0.62f, 0.85f, 0.68f, 0.90f)
                }
            }
        };

        // Costruttore del controllo
        public ButtonPanelTestUserControl()
        {
            try
            {
                // Popola la pagina con gli elementi grafici
                InitializeComponent();

                // Popola il pannello con i pulsanti per selezionare il tipo di pulsantiera
                CreateSelectPanelButtons();

                // Popola il pannello con i pulsanti per selezionare il tipo di collaudo
                ButtonPanelTestType[] testTypes = [.. Enum.GetValues<ButtonPanelTestType>().Cast<ButtonPanelTestType>()];
                CreateSelectTestButtons(testTypes);

                // Associa i gestori agli eventi di click
                buttonStartTest.Click += (s, e) => OnStartTestClicked?.Invoke(this, EventArgs.Empty);
                buttonStopTest.Click += (s, e) => OnStopTestClicked?.Invoke(this, EventArgs.Empty);
                buttonSaveExistingFile.Click += (s, e) => OnSaveExistingFileClicked?.Invoke(this, EventArgs.Empty);
                buttonSaveNewFile.Click += (s, e) => OnSaveNewFileClicked?.Invoke(this, EventArgs.Empty);

                // Associa il metodo per colorare gli indicatori
                pictureBoxImage.Paint += PictureBoxImage_Paint;
                pictureBoxImage.SizeChanged += (s, e) => pictureBoxImage.Invalidate();
            }
            catch (Exception ex)
            {
                ShowError($"Errore durante l'inizializzazione del controllo: {ex.Message}");
            }
        }

        // Metodo per creare dinamicamente i toggle buttons di selezione pulsantiera
        private void CreateSelectPanelButtons()
        {
            ButtonPanelType[] buttonPaneltypes = [.. Enum.GetValues<ButtonPanelType>().Cast<ButtonPanelType>()];
            int buttonHeight = 150;
            int spacing = 10;

            for (int i = 0; i < buttonPaneltypes.Length; i++)
            {
                Button btn = new()
                {
                    Text = buttonPaneltypes[i].ToString(),
                    Tag = buttonPaneltypes[i],
                    Location = new Point(10, 10 + i * (buttonHeight + spacing)),
                    Size = new Size(180, buttonHeight),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SystemColors.Control,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                btn.Click += ButtonSelectPanel_Click;
                MakeRounded(btn, 10);
                panelButtonPanelSelection.Controls.Add(btn);
            }
        }

        // Metodo per creare dinamicamente i toggle buttons di selezione collaudo
        private void CreateSelectTestButtons(ButtonPanelTestType[] testTypes)
        {
            // Spazio tra i pulsanti e dai bordi
            int spacing = 10;
            int buttonHeight = panelTestSelection.Height - 2 * spacing;
            // Altezza pulsante con margini simmetrici
            int buttonWidth = (panelTestSelection.Width - (testTypes.Length + 1) * spacing) / testTypes.Length;

            // Calcola la larghezza totale del gruppo di pulsanti (pulsanti + spazi tra loro)
            int totalGroupWidth = testTypes.Length * buttonWidth + (testTypes.Length - 1) * spacing;

            // Calcola la posizione X iniziale per centrare orizzontalmente il gruppo
            int startX = (panelTestSelection.Width - totalGroupWidth) / 2;

            // Calcola la posizione Y per centrare verticalmente ciascun pulsante
            int startY = (panelTestSelection.Height - buttonHeight) / 2;

            for (int i = 0; i < testTypes.Length; i++)
            {
                Button btn = new()
                {
                    Text = testTypes[i].ToString(),
                    Width = buttonWidth,
                    Height = buttonHeight,
                    Tag = testTypes[i],
                    Location = new Point(startX + i * (buttonWidth + spacing), startY),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SystemColors.Control,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                btn.Click += ButtonSelectTest_Click;
                MakeRounded(btn, 10);
                panelTestSelection.Controls.Add(btn);
            }

            // Seleziona il primo per default (Complete)
            if (panelTestSelection.Controls.Count > 0 && panelTestSelection.Controls[0] is Button firstBtn)
            {
                ButtonSelectTest_Click(firstBtn, EventArgs.Empty);
            }
        }

        // Metodo helper per rendere un button con angoli arrotondati
        private static void MakeRounded(Button btn, int radius)
        {
            GraphicsPath path = new();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(btn.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(btn.Width - radius * 2, btn.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, btn.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            btn.Region = new Region(path);

            // Disabilita il bordo standard perché non segue gli angoli arrotondati
            btn.FlatAppearance.BorderSize = 0;

            // Aggiungi un handler Paint per disegnare il bordo arrotondato
            btn.Paint += (sender, e) =>
            {
                if (sender is not Button b) return;

                // Disegna il bordo solo se il pulsante NON è selezionato (BackColor != colore selezione)
                bool isSelected = b.BackColor == Color.FromArgb(0, 70, 128);
                if (isSelected) return;

                using GraphicsPath borderPath = new();
                int r = radius;
                int w = b.Width - 1;
                int h = b.Height - 1;

                borderPath.AddArc(0, 0, r * 2, r * 2, 180, 90);
                borderPath.AddArc(w - r * 2, 0, r * 2, r * 2, 270, 90);
                borderPath.AddArc(w - r * 2, h - r * 2, r * 2, r * 2, 0, 90);
                borderPath.AddArc(0, h - r * 2, r * 2, r * 2, 90, 90);
                borderPath.CloseFigure();

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using Pen pen = new(Color.LightGray, 1);
                e.Graphics.DrawPath(pen, borderPath);
            };
        }

        // Gestore per l'evento click su selezione pulsantiera
        private void ButtonSelectPanel_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag == null)
                {
                    throw new InvalidOperationException("Tipo di pulsantiera sconosciuto");
                }

                selectedPanelType = (ButtonPanelType)btn.Tag;

                // Cambia il colore del bottone selezionato
                btn.BackColor = Color.FromArgb(0, 70, 128);
                btn.ForeColor = Color.White;

                // Resetta gli altri bottoni
                foreach (var b in panelButtonPanelSelection.Controls.OfType<Button>())
                {
                    if (b != btn)
                    {
                        b.BackColor = SystemColors.Control;
                        b.ForeColor = Color.Black;
                        b.Invalidate(); // Forza il ridisegno del bordo
                    }
                }

                UpdateImage(selectedPanelType.Value);
                UpdateButtonIndicators(selectedPanelType.Value);
                UpdateTestButtons(selectedPanelType.Value);

                OnPanelTypeChanged?.Invoke(this, selectedPanelType.Value);
            }
        }

        // Gestore per click su selezione collaudo
        private void ButtonSelectTest_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.Tag == null)
            {
                throw new InvalidOperationException("Tipo di pulsantiera sconosciuto");
            }

            // Imposta il tipo di test selezionato
            selectedTestType = (ButtonPanelTestType)btn.Tag;

            // Cambia il colore del bottone selezionato
            btn.BackColor = Color.FromArgb(0, 70, 128);
            btn.ForeColor = Color.White;

            // Resetta gli altri bottoni
            foreach (var b in panelTestSelection.Controls.OfType<Button>())
            {
                if (b != btn)
                {
                    b.BackColor = SystemColors.Control;
                    b.ForeColor = Color.Black;
                    b.Invalidate(); // Forza il ridisegno del bordo
                }
            }
        }

        // Restituisce i test disponibili per un tipo di pulsantiera
        private static ButtonPanelTestType[] GetAvailableTests(ButtonPanelType panelType)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);
            List<ButtonPanelTestType> availableTestTypes = [.. Enum.GetValues<ButtonPanelTestType>().Cast<ButtonPanelTestType>()];

            // Rimuovi Led se non supportato
            if (!panel.HasLed)
            {
                availableTestTypes.Remove(ButtonPanelTestType.Led);
            }

            return [.. availableTestTypes];
        }

        // Metodo helper per aggiornare i pulsanti dei test disponibili in base alla pulsantiera
        private void UpdateTestButtons(ButtonPanelType panelType)
        {
            ButtonPanelTestType[] availableTests = GetAvailableTests(panelType);
            panelTestSelection.Controls.Clear();
            CreateSelectTestButtons(availableTests);
        }

        // Metodo per aggiornare l'immagine basata sul tipo selezionato
        private void UpdateImage(ButtonPanelType panelType)
        {
            try
            {
                // First try embedded resources in Properties.Resources (key matches enum name)
                byte[]? imgBytes = null;
                try
                {
                    var obj = Properties.Resources.ResourceManager.GetObject(panelType.ToString(), Properties.Resources.Culture);
                    if (obj is byte[] b && b.Length > 0) imgBytes = b;
                }
                catch { }

                if (imgBytes == null)
                {
                    var prop = typeof(Properties.Resources).GetProperty(panelType.ToString(), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                    {
                        var val = prop.GetValue(null);
                        if (val is byte[] b2 && b2.Length > 0) imgBytes = b2;
                    }
                }

                if (imgBytes != null)
                {
                    using var ms = new System.IO.MemoryStream(imgBytes);
                    // Dispose previous image to avoid file/handle locks
                    pictureBoxImage.Image?.Dispose();
                    pictureBoxImage.Image = Image.FromStream(ms);
                }
                else
                {
                    // Fallback to file-based loading (same relative path as before)
                    var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "images", "ButtonPanels", $"{panelType}.jpg");
                    if (System.IO.File.Exists(filePath))
                    {
                        pictureBoxImage.Image?.Dispose();
                        pictureBoxImage.Image = Image.FromFile(filePath);
                    }
                    else
                    {
                        ShowError($"Immagine pannello non trovata: {filePath}");
                        pictureBoxImage.Image = null;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Errore durante il caricamento dell'immagine della pulsantiera: {ex.Message}");
            }
        }

        // Metodo per aggiornare gli indicatori dei pulsanti
        private void UpdateButtonIndicators(ButtonPanelType panelType)
        {
            if (!_buttonRegions.TryGetValue(panelType, out var regions))
            {
                _buttonIndicators.Clear();
                pictureBoxImage.Invalidate();
                return;
            }

            _buttonIndicators = [.. regions.Select(r => new ButtonIndicator
            {
                Bounds = r,
                State = IndicatorState.Idle
            })];

            pictureBoxImage.Invalidate();
        }

        // Gestore per disegnare gli indicatori sui pulsanti
        private void PictureBoxImage_Paint(object? sender, PaintEventArgs e)
        {
            if (_buttonIndicators.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            foreach (var indicator in _buttonIndicators)
            {
                var rect = new Rectangle(
                    (int)(indicator.Bounds.X * pictureBoxImage.Width),
                    (int)(indicator.Bounds.Y * pictureBoxImage.Height),
                    (int)(indicator.Bounds.Width * pictureBoxImage.Width),
                    (int)(indicator.Bounds.Height * pictureBoxImage.Height)
                );

                Color fillColor = indicator.State switch
                {
                    IndicatorState.Waiting => Color.FromArgb(180, Color.Yellow),
                    IndicatorState.Success => Color.FromArgb(180, Color.LimeGreen),
                    IndicatorState.Failed => Color.FromArgb(180, Color.Red),
                    _ => Color.FromArgb(120, Color.White)
                };

                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillRectangle(brush, rect);
                }

                using var pen = new Pen(Color.Black, 1);
                g.DrawRectangle(pen, rect);
            }
        }

        // Restituisce il tipo di pulsantiera selezionato
        public ButtonPanelType GetSelectedPanelType()
        {
            if (selectedPanelType.HasValue)
            {
                return selectedPanelType.Value;
            }

            throw new InvalidOperationException("Nessun tipo di pulsantiera selezionato.");
        }

        // Restituisce il tipo di test selezionato
        public ButtonPanelTestType GetSelectedTestType()
        {
            if (selectedTestType.HasValue)
            {
                return selectedTestType.Value;
            }

            throw new InvalidOperationException("Nessun tipo di collaudo selezionato");
        }

        // Imposta l'indicatore in attesa
        public void SetButtonWaiting(int buttonIndex)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetButtonWaiting(buttonIndex));
                return;
            }

            if (buttonIndex < _buttonIndicators.Count)
            {
                _buttonIndicators[buttonIndex].State = IndicatorState.Waiting;
                pictureBoxImage.Invalidate();
            }
        }

        // Imposta l'indicatore con il risultato
        public void SetButtonResult(int buttonIndex, bool success)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetButtonResult(buttonIndex, success));
                return;
            }

            if (buttonIndex < _buttonIndicators.Count)
            {
                _buttonIndicators[buttonIndex].State = success ? IndicatorState.Success : IndicatorState.Failed;
                pictureBoxImage.Invalidate();
            }
        }

        // Reimposta gli indicatori
        public void ResetAllIndicators()
        {
            if (InvokeRequired)
            {
                Invoke(ResetAllIndicators);
                return;
            }

            foreach (var ind in _buttonIndicators)
                ind.State = IndicatorState.Idle;

            pictureBoxImage.Invalidate();
        }

        // Mostra un prompt all'utente in richTextBoxTestProgress
        public async Task ShowPromptAsync(string message)
        {
            if (InvokeRequired)
            {
                await Invoke(() => ShowPromptAsync(message));
                return;
            }

            richTextBoxTestProgress.SelectionStart = richTextBoxTestProgress.TextLength;
            richTextBoxTestProgress.SelectionLength = 0;
            richTextBoxTestProgress.SelectionColor = Color.Yellow;
            richTextBoxTestProgress.AppendText(message + Environment.NewLine);
            richTextBoxTestProgress.SelectionColor = richTextBoxTestProgress.ForeColor;
            richTextBoxTestProgress.ScrollToCaret();
            await Task.CompletedTask;
        }

        // Mostra un messaggio di progresso
        public void ShowProgress(string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => ShowProgress(message));
                return;
            }

            richTextBoxTestProgress.AppendText(message + Environment.NewLine);
            richTextBoxTestProgress.ScrollToCaret();
        }

        // Aggiorna il colore dell'ultimo prompt visualizzato
        public void UpdateLastPromptColor(string lastMessage, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateLastPromptColor(lastMessage, color));
                return;
            }

            int startIndex = richTextBoxTestProgress.TextLength - lastMessage.Length - 1;

            // Select the text
            richTextBoxTestProgress.SelectionStart = startIndex;
            richTextBoxTestProgress.SelectionLength = lastMessage.Length;

            // Apply the new color
            richTextBoxTestProgress.SelectionColor = color;

            // Deselect to avoid highlighting
            richTextBoxTestProgress.SelectionLength = 0;
            richTextBoxTestProgress.SelectionStart = richTextBoxTestProgress.TextLength;
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            if (InvokeRequired)
            {
                Invoke(() => DisplayResults(results));
                return;
            }

            try
            {
                // Pulisci i risultati precedenti
                richTextBoxTestResult.Clear();

                // Mostra header comune ai collaudi
                if (results.Count > 0)
                {
                    richTextBoxTestResult.AppendText($"Risultati collaudo pulsantiera [{results[0].PanelType}]" + Environment.NewLine);

                    // Mostra UUID se disponibile
                    if (results[0].DeviceUuid is { Length: 12 } uuid)
                    {
                        richTextBoxTestResult.AppendText("UUID Dispositivo: ");
                        richTextBoxTestResult.SelectionColor = Color.Cyan;
                        richTextBoxTestResult.AppendText(BitConverter.ToString(uuid));
                        richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                        richTextBoxTestResult.AppendText(Environment.NewLine);
                    }

                    richTextBoxTestResult.AppendText(Environment.NewLine);
                }

                foreach (var result in results)
                {
                    // Mostra nome del collaudo
                    richTextBoxTestResult.AppendText($"Collaudo {result.TestType}: ");

                    // Determina stato e colore
                    string status;
                    Color statusColor;
                    if (result.Interrupted)
                    {
                        status = "INTERROTTO";
                        statusColor = Color.Orange;
                    }
                    else
                    {
                        status = result.Passed ? "PASSATO" : "FALLITO";
                        statusColor = result.Passed ? Color.LimeGreen : Color.Red;
                    }

                    richTextBoxTestResult.SelectionColor = statusColor;
                    richTextBoxTestResult.AppendText(status);
                    richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                    richTextBoxTestResult.AppendText(Environment.NewLine);

                    // Gestisci il messaggio
                    string[] lines = result.Message.Split('\n');
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Contains("interrotto"))
                        {
                            richTextBoxTestResult.SelectionColor = Color.Orange;
                            richTextBoxTestResult.AppendText(line);
                            richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                        }
                        else
                        {
                            int colonIndex = line.LastIndexOf(':');
                            if (colonIndex != -1)
                            {
                                string before = string.Concat(line.AsSpan(0, colonIndex + 1), " ");
                                string after = line[(colonIndex + 1)..].Trim();

                                richTextBoxTestResult.AppendText(before);

                                bool subPassed = after.Contains("PASSATO");
                                Color subColor = subPassed ? Color.LimeGreen : Color.Red;

                                richTextBoxTestResult.SelectionColor = subColor;
                                richTextBoxTestResult.AppendText(after);
                                richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
                            }
                            else
                            {
                                richTextBoxTestResult.AppendText(line);
                            }
                        }

                        richTextBoxTestResult.AppendText(Environment.NewLine);
                    }

                    richTextBoxTestResult.AppendText(Environment.NewLine);
                }

                // Scrolla alla fine
                richTextBoxTestResult.ScrollToCaret();

                // Resetta gli indicatori dei pulsanti allo stato di default
                ResetAllIndicators();

                // Pulisci la progress box e mostra un messaggio di completamento
                richTextBoxTestProgress.Clear();
                richTextBoxTestProgress.AppendText("Collaudo completato. Pronto per un nuovo collaudo." + Environment.NewLine);
                richTextBoxTestProgress.AppendText("Seleziona tipo di Pulsantiera e tipo di collaudo." + Environment.NewLine);
                richTextBoxTestProgress.AppendText("Per iniziare premere avvia collaudo, se si verificano problemi premere arresta collaudo." + Environment.NewLine);
            }
            catch (Exception ex)
            {
                ShowError($"Errore durante la visualizzazione dei risultati: {ex.Message}");
            }
        }

        /// <summary>
        /// Mostra nella richTextBoxTestResult che il collaudo è in corso.
        /// </summary>
        public void ShowTestInProgress()
        {
            if (InvokeRequired)
            {
                Invoke(ShowTestInProgress);
                return;
            }

            richTextBoxTestResult.Clear();
            richTextBoxTestResult.SelectionColor = Color.Yellow;
            richTextBoxTestResult.AppendText("Collaudo in corso...");
            richTextBoxTestResult.SelectionColor = richTextBoxTestResult.ForeColor;
        }

        // Mostra eventuali messaggi di errore
        public void ShowError(string message)
        {
            MessageBox.Show(ParentForm, message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Chiedi una conferma all'utente
        public Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType)
        {
            if (InvokeRequired)
            {
                return (Task<bool>)Invoke(() => ShowConfirmAsync(message, testType));
            }

            string title = $"Conferma collaudo {testType}";
            var result = MessageBox.Show(ParentForm, message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            return Task.FromResult(result);
        }

        // Mostra la finestra di dialogo per salvare un nuovo file
        public string? ShowSaveNewFileDialog()
        {
            string? filePath = null;

            Thread staThread = new(() =>
            {
                using SaveFileDialog sfd = new();
                sfd.Filter = "File di testo (*.txt)|*.txt|Tutti i file (*.*)|*.*";
                sfd.DefaultExt = "txt";
                sfd.Title = "Salva Risultati Collaudo";
                sfd.FileName = $"Risultati_Collaudo_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    filePath = sfd.FileName;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            return filePath;
        }

        // Mostra la finestra di dialogo per selezionare un file esistente
        public string? ShowOpenExistingFileDialog()
        {
            string? filePath = null;

            Thread staThread = new(() =>
            {
                using OpenFileDialog ofd = new();
                ofd.Filter = "File di testo (*.txt)|*.txt|Tutti i file (*.*)|*.*";
                ofd.DefaultExt = "txt";
                ofd.Title = "Seleziona file esistente a cui aggiungere i risultati";
                ofd.CheckFileExists = true;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    filePath = ofd.FileName;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            return filePath;
        }

        // Imposta il path dell'ultimo file salvato
        public void SetLastSavedFilePath(string? filePath)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetLastSavedFilePath(filePath));
                return;
            }

            _lastSavedFilePath = filePath;

            if (string.IsNullOrEmpty(filePath))
            {
                labelLastSavedFile.Text = "Nessun file selezionato";
                labelLastSavedFile.ForeColor = SystemColors.ControlDarkDark;
            }
            else
            {
                labelLastSavedFile.Text = $"Ultimo file: {filePath}";
                labelLastSavedFile.ForeColor = Color.LimeGreen;
            }
        }

        // Ottiene il path dell'ultimo file salvato
        public string? GetLastSavedFilePath()
        {
            return _lastSavedFilePath;
        }

        // Restituisce il testo dei risultati del collaudo
        public string GetResultsText() => richTextBoxTestResult.Text;

        // Mostra un messaggio all'utente
        public void ShowMessage(string message, string title)
        {
            MessageBoxIcon icon = MessageBoxIcon.Information;

            if (title.Contains("errore", StringComparison.CurrentCultureIgnoreCase))
            {
                icon = MessageBoxIcon.Error;
            }

            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        }

        /// <summary>
        /// Mostra un dialogo di conferma per risultati non salvati.
        /// Restituisce true se l'utente vuole salvare (non procedere), false se vuole procedere senza salvare.
        /// </summary>
        public bool ShowUnsavedResultsWarning()
        {
            if (InvokeRequired)
            {
                return (bool)Invoke(ShowUnsavedResultsWarning);
            }

            var result = MessageBox.Show(
                ParentForm,
                "⚠️ ATTENZIONE ⚠️\n\n" +
                "L'ultimo collaudo non è stato salvato.\n\n" +
                "Vuoi salvare i risultati prima di procedere?\n\n" +
                "Sì = Torna indietro per salvare\n" +
                "No = Procedi senza salvare",
                "Risultati non salvati",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        // L'interfaccia richiede questo metodo ma la UI non mostra più lo stato del battezzamento.
        // Implementazione no-op per mantenere la compatibilità.
        public void SetBaptizeStatus(BaptizeStatus status)
        {
            // Intentionally empty: baptize status label removed from the UI.
        }

        /// <summary>
        /// Mostra un dialogo di avviso per l'interruzione della comunicazione CAN.
        /// </summary>
        public void ShowCommunicationLostDialog()
        {
            if (InvokeRequired)
            {
                Invoke(ShowCommunicationLostDialog);
                return;
            }

            MessageBox.Show(
                ParentForm,
                "⚠️ COMUNICAZIONE INTERROTTA ⚠️\n\n" +
                "La comunicazione con la pulsantiera è stata interrotta.\n\n" +
                "Per continuare:\n" +
                "1. Staccare il cavo USB del dispositivo CAN\n" +
                "2. Attendere qualche secondo\n" +
                "3. Riattaccare il cavo USB\n" +
                "4. Riavviare il collaudo\n\n" +
                "Il test corrente è stato interrotto.",
                "Comunicazione Interrotta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    internal static class EmbeddedResourceHelper
    {
        public static Stream? GetResourceStream(Assembly asm, string resourcePathSuffix)
        {
            // resourcePathSuffix expected like "Resources.Images.filename.jpg"
            var names = asm.GetManifestResourceNames();
            // Try to find matching resource name ending with the suffix
            var matched = names.FirstOrDefault(n => n.EndsWith(resourcePathSuffix, StringComparison.OrdinalIgnoreCase));
            if (matched == null) return null;
            return asm.GetManifestResourceStream(matched);
        }
    }
}