namespace VideoAudioMediaPlayer
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            playbackTimer = new System.Windows.Forms.Timer(components);
            waveformPictureBox = new PictureBox();
            lblInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).BeginInit();
            SuspendLayout();
            // 
            // waveformPictureBox
            // 
            waveformPictureBox.Dock = DockStyle.Bottom;
            waveformPictureBox.Location = new Point(0, 730);
            waveformPictureBox.Name = "waveformPictureBox";
            waveformPictureBox.Size = new Size(1662, 222);
            waveformPictureBox.TabIndex = 1;
            waveformPictureBox.TabStop = false;
            waveformPictureBox.PreviewKeyDown += Generic_PreviewKeyDown;
            // 
            // lblInfo
            // 
            lblInfo.Dock = DockStyle.Top;
            lblInfo.Font = new Font("Arial", 13.875F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblInfo.Location = new Point(0, 0);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(1662, 59);
            lblInfo.TabIndex = 2;
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            lblInfo.PreviewKeyDown += Generic_PreviewKeyDown;
            // 
            // Form1
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Desktop;
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1662, 952);
            Controls.Add(waveformPictureBox);
            Controls.Add(lblInfo);
            KeyPreview = true;
            Name = "Form1";
            Text = "Video & Audio Playback";
            FormClosing += Form1_FormClosing;
            FormClosed += Form1_FormClosed;
            Load += Form1_Load;
            DragDrop += Form1_DragDrop;
            DragEnter += Form1_DragEnter;
            KeyDown += Form1_KeyDown;
            PreviewKeyDown += Generic_PreviewKeyDown;
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer playbackTimer;
        private PictureBox waveformPictureBox;
        private Label lblInfo;
    }
}
