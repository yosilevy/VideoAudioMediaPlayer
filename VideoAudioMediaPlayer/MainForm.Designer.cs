using System.Diagnostics;

namespace VideoAudioMediaPlayer
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override async void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                Trace.WriteLine("disposing");
                components.Dispose();
                // final disposing sometimes gets stuck
                //this.Invoke((MethodInvoker)delegate
                //{
                //    this._mediaPlayer?.Dispose();
                //    this._libVLC?.Dispose();
                //});
                
                Trace.WriteLine("After disposing");
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
            mainVideoView = new LibVLCSharp.WinForms.VideoView();
            menuStrip1 = new MenuStrip();
            louderToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)mainVideoView).BeginInit();
            menuStrip1.SuspendLayout();
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
            waveformPictureBox.MouseClick += WaveformPictureBox_MouseClick;
            waveformPictureBox.PreviewKeyDown += Generic_PreviewKeyDown;
            // 
            // lblInfo
            // 
            lblInfo.BackColor = SystemColors.HotTrack;
            lblInfo.Dock = DockStyle.Top;
            lblInfo.Font = new Font("Arial", 13.875F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lblInfo.ForeColor = SystemColors.ButtonFace;
            lblInfo.Location = new Point(0, 42);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(1662, 59);
            lblInfo.TabIndex = 2;
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            lblInfo.PreviewKeyDown += Generic_PreviewKeyDown;
            // 
            // mainVideoView
            // 
            mainVideoView.BackColor = Color.Black;
            mainVideoView.Dock = DockStyle.Fill;
            mainVideoView.Location = new Point(0, 101);
            mainVideoView.MediaPlayer = null;
            mainVideoView.Name = "mainVideoView";
            mainVideoView.Size = new Size(1662, 629);
            mainVideoView.TabIndex = 4;
            mainVideoView.Click += mainVideoView_Click;
            mainVideoView.PreviewKeyDown += Generic_PreviewKeyDown;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(32, 32);
            menuStrip1.Items.AddRange(new ToolStripItem[] { louderToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1662, 42);
            menuStrip1.TabIndex = 5;
            menuStrip1.Text = "menuStrip1";
            // 
            // louderToolStripMenuItem
            // 
            louderToolStripMenuItem.Name = "louderToolStripMenuItem";
            louderToolStripMenuItem.Size = new Size(108, 38);
            louderToolStripMenuItem.Text = "Louder";
            louderToolStripMenuItem.Click += louderToolStripMenuItem_Click;
            // 
            // MainForm
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Desktop;
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1662, 952);
            Controls.Add(mainVideoView);
            Controls.Add(waveformPictureBox);
            Controls.Add(lblInfo);
            Controls.Add(menuStrip1);
            KeyPreview = true;
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "Video & Audio Playback";
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            Load += MainForm_Load;
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            KeyDown += MainForm_KeyDown;
            PreviewKeyDown += Generic_PreviewKeyDown;
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)mainVideoView).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.Timer playbackTimer;
        private PictureBox waveformPictureBox;
        private Label lblInfo;
        private LibVLCSharp.WinForms.VideoView mainVideoView;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem louderToolStripMenuItem;
    }
}
