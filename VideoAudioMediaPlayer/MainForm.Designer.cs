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
            mainSplitContainer = new SplitContainer();
            videoWebView = new Microsoft.Web.WebView2.WinForms.WebView2();
            transcriptionListBox = new ListBox();
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).BeginInit();
            mainSplitContainer.Panel1.SuspendLayout();
            mainSplitContainer.Panel2.SuspendLayout();
            mainSplitContainer.SuspendLayout();
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
            waveformPictureBox.PreviewKeyDown += waveformPictureBox_PreviewKeyDown;
            // 
            // mainSplitContainer
            // 
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Location = new Point(0, 0);
            mainSplitContainer.Name = "mainSplitContainer";
            // 
            // mainSplitContainer.Panel1
            // 
            mainSplitContainer.Panel1.Controls.Add(videoWebView);
            mainSplitContainer.Panel1.RightToLeft = RightToLeft.Yes;
            // 
            // mainSplitContainer.Panel2
            // 
            mainSplitContainer.Panel2.Controls.Add(transcriptionListBox);
            mainSplitContainer.Panel2.RightToLeft = RightToLeft.Yes;
            mainSplitContainer.Size = new Size(1662, 772);
            mainSplitContainer.SplitterDistance = 1162;
            mainSplitContainer.TabIndex = 8;
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
            // transcriptionListBox
            // 
            transcriptionListBox.Dock = DockStyle.Fill;
            transcriptionListBox.DrawMode = DrawMode.OwnerDrawFixed;
            transcriptionListBox.Location = new Point(0, 0);
            transcriptionListBox.Name = "transcriptionListBox";
            transcriptionListBox.Size = new Size(496, 772);
            transcriptionListBox.TabIndex = 0;
            transcriptionListBox.TabStop = false;
            transcriptionListBox.DrawItem += TranscriptionListBox_DrawItem;
            transcriptionListBox.SelectedIndexChanged += TranscriptionListBox_SelectedIndexChanged;
            transcriptionListBox.KeyDown += TranscriptionListBox_KeyDown;
            transcriptionListBox.KeyPress += TranscriptionListBox_KeyPress;
            // 
            // MainForm
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Desktop;
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(1662, 952);
            Controls.Add(mainSplitContainer);
            Controls.Add(waveformPictureBox);
            KeyPreview = true;
            Name = "MainForm";
            RightToLeft = RightToLeft.Yes;
            Text = "Video & Audio Playback";
            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;
            Load += MainForm_Load;
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            KeyDown += MainForm_KeyDown;
            ((System.ComponentModel.ISupportInitialize)waveformPictureBox).EndInit();
            mainSplitContainer.Panel1.ResumeLayout(false);
            mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).EndInit();
            mainSplitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)videoWebView).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.Timer playbackTimer;
        private PictureBox waveformPictureBox;
        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.ListBox transcriptionListBox;
        private Microsoft.Web.WebView2.WinForms.WebView2 videoWebView;
    }
}
