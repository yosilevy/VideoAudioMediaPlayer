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
            infoListBox = new System.Windows.Forms.ListBox();
            videoWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)videoWebView).BeginInit();
            SuspendLayout();
            // 
            // waveformPictureBox
            // 
            waveformPictureBox.Dock = DockStyle.Bottom;
            waveformPictureBox.Location = new Point(0, 772);
            waveformPictureBox.Name = "waveformPictureBox";
            waveformPictureBox.Size = new Size(1662, 180);
            waveformPictureBox.TabIndex = 1;
            waveformPictureBox.TabStop = false;
            waveformPictureBox.MouseClick += WaveformPictureBox_MouseClick;
            waveformPictureBox.MouseDoubleClick += waveformPictureBox_MouseDoubleClick;
            // 
            // infoListBox
            // 
            infoListBox.Dock = DockStyle.Right;
            infoListBox.Width = 500;
            infoListBox.Name = "infoListBox";
            infoListBox.TabIndex = 0;
            infoListBox.TabStop = false;
            infoListBox.SelectedIndexChanged += InfoListBox_SelectedIndexChanged;
            // 
            // videoWebView
            // 
            videoWebView.AllowExternalDrop = true;
            videoWebView.CreationProperties = null;
            videoWebView.DefaultBackgroundColor = Color.Black;
            videoWebView.Dock = DockStyle.Fill;
            videoWebView.Location = new Point(0, 0);
            videoWebView.Name = "videoWebView";
            videoWebView.Size = new Size(1162, 772);
            videoWebView.TabIndex = 7;
            videoWebView.ZoomFactor = 1D;
            // 
            // MainForm
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Desktop;
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1662, 952);
            Controls.Add(videoWebView);
            Controls.Add(infoListBox);
            Controls.Add(waveformPictureBox);
            KeyPreview = true;
            Name = "MainForm";
            Text = "Video & Audio Playback";
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            Load += MainForm_Load;
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)videoWebView).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer playbackTimer;
        private PictureBox waveformPictureBox;
        private System.Windows.Forms.ListBox infoListBox;
        private Microsoft.Web.WebView2.WinForms.WebView2 videoWebView;
    }
}
