namespace Kinovea.Root
{
    partial class FormSplashScreen
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
        	this.lblInfo = new System.Windows.Forms.Label();
        	this.lblVersion = new System.Windows.Forms.Label();
        	this.pictureBox1 = new System.Windows.Forms.PictureBox();
        	((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
        	this.SuspendLayout();
        	// 
        	// lblInfo
        	// 
        	this.lblInfo.AutoSize = true;
        	this.lblInfo.BackColor = System.Drawing.Color.White;
        	this.lblInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        	this.lblInfo.ForeColor = System.Drawing.Color.Black;
        	this.lblInfo.Location = new System.Drawing.Point(16, 286);
        	this.lblInfo.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
        	this.lblInfo.Name = "lblInfo";
        	this.lblInfo.Size = new System.Drawing.Size(61, 17);
        	this.lblInfo.TabIndex = 0;
        	this.lblInfo.Text = "loading";
        	// 
        	// lblVersion
        	// 
        	this.lblVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        	this.lblVersion.AutoSize = true;
        	this.lblVersion.BackColor = System.Drawing.Color.White;
        	this.lblVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
        	this.lblVersion.ForeColor = System.Drawing.Color.DimGray;
        	this.lblVersion.Location = new System.Drawing.Point(459, 286);
        	this.lblVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
        	this.lblVersion.Name = "lblVersion";
        	this.lblVersion.Size = new System.Drawing.Size(61, 17);
        	this.lblVersion.TabIndex = 1;
        	this.lblVersion.Text = "version";
        	// 
        	// pictureBox1
        	// 
        	this.pictureBox1.BackgroundImage = global::Kinovea.Root.Properties.Resources.splash;
        	this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
        	this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        	this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
        	this.pictureBox1.Enabled = false;
        	this.pictureBox1.Location = new System.Drawing.Point(0, 0);
        	this.pictureBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
        	this.pictureBox1.Name = "pictureBox1";
        	this.pictureBox1.Size = new System.Drawing.Size(600, 308);
        	this.pictureBox1.TabIndex = 2;
        	this.pictureBox1.TabStop = false;
        	// 
        	// FormSplashScreen
        	// 
        	this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
        	this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        	this.BackColor = System.Drawing.Color.White;
        	this.ClientSize = new System.Drawing.Size(600, 308);
        	this.ControlBox = false;
        	this.Controls.Add(this.lblVersion);
        	this.Controls.Add(this.lblInfo);
        	this.Controls.Add(this.pictureBox1);
        	this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
        	this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
        	this.MaximizeBox = false;
        	this.MinimizeBox = false;
        	this.Name = "FormSplashScreen";
        	this.ShowInTaskbar = false;
        	this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        	((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
        	this.ResumeLayout(false);
        	this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}