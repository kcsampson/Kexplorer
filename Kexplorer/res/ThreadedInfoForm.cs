using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace Kexplorer.res
{
	
	/// <summary>
	/// Summary description for ThreadedInfoForm.
	/// </summary>
	public class ThreadedInfoForm : System.Windows.Forms.Form
	{
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;

		private System.Windows.Forms.Button OKCancelButton;

		public Button OKCancelBtn 
		{
			get { return this.OKCancelButton; }
		}

		public Label SizeLabel 
		{
			get { return this.labelSize; }
		}

		private System.Windows.Forms.Label labelSize;
		private System.Windows.Forms.Label labelFiles;
		private System.Windows.Forms.Label labelSubFolders;
		private System.Windows.Forms.Label labelName;


		public Label FilesLabel 
		{
			get { return this.labelFiles; }
		}

		public Label SubFolderLabel 
		{
			get { return this.labelSubFolders; }
		}

		public Label NameLabel 
		{
			get { return this.labelName; }
		}


		private InvokeDelegate cancelCallback = null;

		public InvokeDelegate CancelCallback
		{
			get { return this.cancelCallback; }
			set { this.cancelCallback = value; }
		}
			


	
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public ThreadedInfoForm( string folderName )
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			this.NameLabel.Text = folderName;


			this.Closing += new CancelEventHandler(ThreadedInfoForm_Closing);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.OKCancelButton = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.labelSize = new System.Windows.Forms.Label();
			this.labelFiles = new System.Windows.Forms.Label();
			this.labelSubFolders = new System.Windows.Forms.Label();
			this.labelName = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// OKCancelButton
			// 
			this.OKCancelButton.Location = new System.Drawing.Point(152, 200);
			this.OKCancelButton.Name = "OKCancelButton";
			this.OKCancelButton.Size = new System.Drawing.Size(184, 23);
			this.OKCancelButton.TabIndex = 0;
			this.OKCancelButton.Text = "Close / Cancel";
			this.OKCancelButton.Click += new System.EventHandler(this.OKCancelButton_Click);
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(16, 32);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(88, 16);
			this.label1.TabIndex = 1;
			this.label1.Text = "Name";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(16, 123);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(88, 16);
			this.label3.TabIndex = 3;
			this.label3.Text = "Size";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(16, 88);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(88, 16);
			this.label4.TabIndex = 4;
			this.label4.Text = "Files";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(16, 64);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(88, 16);
			this.label5.TabIndex = 5;
			this.label5.Text = "Sub-folders";
			this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// labelSize
			// 
			this.labelSize.Location = new System.Drawing.Point(136, 123);
			this.labelSize.Name = "labelSize";
			this.labelSize.Size = new System.Drawing.Size(88, 16);
			this.labelSize.TabIndex = 6;
			this.labelSize.Text = "Name";
			this.labelSize.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// labelFiles
			// 
			this.labelFiles.Location = new System.Drawing.Point(136, 88);
			this.labelFiles.Name = "labelFiles";
			this.labelFiles.Size = new System.Drawing.Size(88, 16);
			this.labelFiles.TabIndex = 7;
			this.labelFiles.Text = "Name";
			this.labelFiles.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// labelSubFolders
			// 
			this.labelSubFolders.Location = new System.Drawing.Point(136, 64);
			this.labelSubFolders.Name = "labelSubFolders";
			this.labelSubFolders.Size = new System.Drawing.Size(88, 16);
			this.labelSubFolders.TabIndex = 8;
			this.labelSubFolders.Text = "Name";
			this.labelSubFolders.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// labelName
			// 
			this.labelName.Location = new System.Drawing.Point(136, 32);
			this.labelName.Name = "labelName";
			this.labelName.Size = new System.Drawing.Size(368, 16);
			this.labelName.TabIndex = 9;
			this.labelName.Text = "Name";
			this.labelName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// ThreadedInfoForm
			// 
			this.AcceptButton = this.OKCancelButton;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(536, 262);
			this.Controls.Add(this.labelName);
			this.Controls.Add(this.labelSubFolders);
			this.Controls.Add(this.labelFiles);
			this.Controls.Add(this.labelSize);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.OKCancelButton);
			this.Name = "ThreadedInfoForm";
			this.Text = "Threaded Info";
			this.ResumeLayout(false);

		}
		#endregion

		private void OKCancelButton_Click(object sender, System.EventArgs e)
		{
			if ( this.cancelCallback != null )
			{
				this.cancelCallback();
			}
		}

		private void ThreadedInfoForm_Closing(object sender, CancelEventArgs e)
		{
			if ( this.cancelCallback != null )
			{
				this.cancelCallback();
			}
		}
	}
}
