namespace ClipsFormsExample
{
    partial class ClipsFormsExample
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ClipsFormsExample));
            this.panel1 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.factsGrid = new System.Windows.Forms.DataGridView();
            this.outputBox = new System.Windows.Forms.TextBox();
            this.buttonsPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.nextButton = new System.Windows.Forms.Button();
            this.resetButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.clipsOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.splitContainer1);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(986, 606);
            this.panel1.TabIndex = 2;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.factsGrid);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.buttonsPanel);
            this.splitContainer1.Panel2.Controls.Add(this.outputBox);
            this.splitContainer1.Size = new System.Drawing.Size(986, 606);
            this.splitContainer1.SplitterDistance = 463;
            this.splitContainer1.TabIndex = 2;
            // 
            // factsGrid
            // 
            this.factsGrid.AllowUserToAddRows = false;
            this.factsGrid.AllowUserToDeleteRows = false;
            this.factsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.factsGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.factsGrid.Location = new System.Drawing.Point(0, 0);
            this.factsGrid.Name = "factsGrid";
            this.factsGrid.RowHeadersVisible = false;
            this.factsGrid.Size = new System.Drawing.Size(463, 606);
            this.factsGrid.TabIndex = 0;
            // 
            // outputBox
            // 
            this.outputBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.outputBox.Font = new System.Drawing.Font("Lucida Console", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.outputBox.Location = new System.Drawing.Point(0, 0);
            this.outputBox.Multiline = true;
            this.outputBox.Name = "outputBox";
            this.outputBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.outputBox.Size = new System.Drawing.Size(519, 450);
            this.outputBox.TabIndex = 1;
            // 
            // buttonsPanel
            // 
            this.buttonsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonsPanel.AutoScroll = true;
            this.buttonsPanel.BackColor = System.Drawing.SystemColors.ControlLight;
            this.buttonsPanel.Location = new System.Drawing.Point(0, 450);
            this.buttonsPanel.Name = "buttonsPanel";
            this.buttonsPanel.Padding = new System.Windows.Forms.Padding(10);
            this.buttonsPanel.Size = new System.Drawing.Size(519, 156);
            this.buttonsPanel.TabIndex = 2;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.nextButton);
            this.panel2.Controls.Add(this.resetButton);
            this.panel2.Controls.Add(this.openButton);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel2.Location = new System.Drawing.Point(0, 606);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(988, 54);
            this.panel2.TabIndex = 6;
            // 
            // nextButton
            // 
            this.nextButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.nextButton.Location = new System.Drawing.Point(855, 12);
            this.nextButton.Name = "nextButton";
            this.nextButton.Size = new System.Drawing.Size(120, 30);
            this.nextButton.TabIndex = 8;
            this.nextButton.Text = "Анализ";
            this.nextButton.UseVisualStyleBackColor = true;
            this.nextButton.Click += new System.EventHandler(this.nextBtn_Click);
            // 
            // resetButton
            // 
            this.resetButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.resetButton.Location = new System.Drawing.Point(729, 12);
            this.resetButton.Name = "resetButton";
            this.resetButton.Size = new System.Drawing.Size(120, 30);
            this.resetButton.TabIndex = 7;
            this.resetButton.Text = "Сброс";
            this.resetButton.UseVisualStyleBackColor = true;
            this.resetButton.Click += new System.EventHandler(this.resetBtn_Click);
            // 
            // openButton
            // 
            this.openButton.Location = new System.Drawing.Point(12, 12);
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(180, 30);
            this.openButton.TabIndex = 5;
            this.openButton.Text = "Загрузить доп. правила...";
            this.openButton.UseVisualStyleBackColor = true;
            this.openButton.Click += new System.EventHandler(this.openFile_Click);
            // 
            // clipsOpenFileDialog
            // 
            this.clipsOpenFileDialog.Filter = "CLIPS files|*.clp|All files|*.*";
            this.clipsOpenFileDialog.Multiselect = true;
            this.clipsOpenFileDialog.Title = "Открыть файлы правил";
            // 
            // ClipsFormsExample
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(988, 660);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            // this.Icon = ... (Закомментировано, если нет иконки)
            this.MinimumSize = new System.Drawing.Size(660, 300);
            this.Name = "ClipsFormsExample";
            this.Text = "Экспертная система 1С без кэфиков";
            this.panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView factsGrid;
        private System.Windows.Forms.TextBox outputBox;
        private System.Windows.Forms.FlowLayoutPanel buttonsPanel;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button nextButton;
        private System.Windows.Forms.Button resetButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.OpenFileDialog clipsOpenFileDialog;
    }
}