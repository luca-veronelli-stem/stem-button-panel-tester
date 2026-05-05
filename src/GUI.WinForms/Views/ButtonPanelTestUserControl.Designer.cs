namespace GUI.Windows.Views
{
    partial class ButtonPanelTestUserControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelButtonPanelSelection = new Panel();
            pictureBoxImage = new PictureBox();
            panelTestSelection = new Panel();
            richTextBoxTestProgress = new RichTextBox();
            buttonStartTest = new Button();
            richTextBoxTestResult = new RichTextBox();
            buttonStopTest = new Button();
            buttonSaveExistingFile = new Button();
            buttonSaveNewFile = new Button();
            labelLastSavedFile = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBoxImage).BeginInit();
            SuspendLayout();
            // 
            // panelButtonPanelSelection
            // 
            panelButtonPanelSelection.Dock = DockStyle.Left;
            panelButtonPanelSelection.Location = new Point(3, 3);
            panelButtonPanelSelection.MinimumSize = new Size(200, 0);
            panelButtonPanelSelection.Name = "panelButtonPanelSelection";
            panelButtonPanelSelection.Padding = new Padding(3);
            panelButtonPanelSelection.Size = new Size(200, 794);
            panelButtonPanelSelection.TabIndex = 7;
            // 
            // pictureBoxImage
            // 
            pictureBoxImage.Location = new Point(209, 3);
            pictureBoxImage.Name = "pictureBoxImage";
            pictureBoxImage.Size = new Size(700, 300);
            pictureBoxImage.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxImage.TabIndex = 8;
            pictureBoxImage.TabStop = false;
            // 
            // panelTestSelection
            // 
            panelTestSelection.Location = new Point(209, 309);
            panelTestSelection.Name = "panelTestSelection";
            panelTestSelection.Size = new Size(700, 60);
            panelTestSelection.TabIndex = 0;
            // 
            // richTextBoxTestProgress
            // 
            richTextBoxTestProgress.BackColor = SystemColors.Desktop;
            richTextBoxTestProgress.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            richTextBoxTestProgress.ForeColor = SystemColors.HighlightText;
            richTextBoxTestProgress.Location = new Point(209, 377);
            richTextBoxTestProgress.Name = "richTextBoxTestProgress";
            richTextBoxTestProgress.ReadOnly = true;
            richTextBoxTestProgress.Size = new Size(700, 323);
            richTextBoxTestProgress.TabIndex = 10;
            richTextBoxTestProgress.Text = "Seleziona tipo di Pulsantiera e tipo di collaudo.\nPer Iniziare premere avvia collaudo, se si verificano problemi premere arresta collaudo.\n";
            // 
            // buttonStartTest
            // 
            buttonStartTest.Location = new Point(209, 719);
            buttonStartTest.Name = "buttonStartTest";
            buttonStartTest.Size = new Size(160, 35);
            buttonStartTest.TabIndex = 0;
            buttonStartTest.Text = "Avvia collaudo";
            buttonStartTest.UseVisualStyleBackColor = true;
            // 
            // richTextBoxTestResult
            // 
            richTextBoxTestResult.BackColor = SystemColors.Desktop;
            richTextBoxTestResult.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            richTextBoxTestResult.ForeColor = SystemColors.HighlightText;
            richTextBoxTestResult.Location = new Point(915, 3);
            richTextBoxTestResult.MinimumSize = new Size(400, 0);
            richTextBoxTestResult.Name = "richTextBoxTestResult";
            richTextBoxTestResult.ReadOnly = true;
            richTextBoxTestResult.Size = new Size(482, 697);
            richTextBoxTestResult.TabIndex = 11;
            richTextBoxTestResult.Text = "I risultati verrano visualizzati qui.";
            // 
            // buttonStopTest
            // 
            buttonStopTest.Location = new Point(417, 719);
            buttonStopTest.Name = "buttonStopTest";
            buttonStopTest.Size = new Size(172, 35);
            buttonStopTest.TabIndex = 12;
            buttonStopTest.Text = "Arresta collaudo";
            buttonStopTest.UseVisualStyleBackColor = true;
            // 
            // buttonSaveExistingFile
            // 
            buttonSaveExistingFile.Location = new Point(915, 719);
            buttonSaveExistingFile.Name = "buttonSaveExistingFile";
            buttonSaveExistingFile.Size = new Size(235, 35);
            buttonSaveExistingFile.TabIndex = 13;
            buttonSaveExistingFile.Text = "Salva file esistente";
            buttonSaveExistingFile.UseVisualStyleBackColor = true;
            // 
            // buttonSaveNewFile
            // 
            buttonSaveNewFile.Location = new Point(1162, 719);
            buttonSaveNewFile.Name = "buttonSaveNewFile";
            buttonSaveNewFile.Size = new Size(235, 35);
            buttonSaveNewFile.TabIndex = 14;
            buttonSaveNewFile.Text = "Salva nuovo file";
            buttonSaveNewFile.UseVisualStyleBackColor = true;
            // 
            // labelLastSavedFile
            // 
            labelLastSavedFile.AutoEllipsis = true;
            labelLastSavedFile.Font = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            labelLastSavedFile.ForeColor = SystemColors.ControlDarkDark;
            labelLastSavedFile.Location = new Point(915, 757);
            labelLastSavedFile.Name = "labelLastSavedFile";
            labelLastSavedFile.Size = new Size(482, 40);
            labelLastSavedFile.TabIndex = 15;
            labelLastSavedFile.Text = "Nessun file selezionato";
            // 
            // ButtonPanelTestUserControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            Controls.Add(labelLastSavedFile);
            Controls.Add(buttonSaveNewFile);
            Controls.Add(buttonSaveExistingFile);
            Controls.Add(buttonStopTest);
            Controls.Add(richTextBoxTestResult);
            Controls.Add(buttonStartTest);
            Controls.Add(richTextBoxTestProgress);
            Controls.Add(panelTestSelection);
            Controls.Add(pictureBoxImage);
            Controls.Add(panelButtonPanelSelection);
            MinimumSize = new Size(1400, 800);
            Name = "ButtonPanelTestUserControl";
            Padding = new Padding(3);
            Size = new Size(1403, 800);
            ((System.ComponentModel.ISupportInitialize)pictureBoxImage).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private Panel panelButtonPanelSelection;
        private PictureBox pictureBoxImage;
        private Panel panelTestSelection;
        private RichTextBox richTextBoxTestProgress;
        private Button buttonStartTest;
        private RichTextBox richTextBoxTestResult;
        private Button buttonStopTest;
        private Button buttonSaveExistingFile;
        private Button buttonSaveNewFile;
        private Label labelLastSavedFile;
    }
}
