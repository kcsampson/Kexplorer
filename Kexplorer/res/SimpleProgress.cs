using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Kexplorer.res
{
    public partial class SimpleProgress : Form
    {
        private SimpleProgress()
        {
            InitializeComponent();
        }

        private InvokeDelegate cancel = null;

        private SimpleProgress(InvokeDelegate cancelCallback)
        {
            InitializeComponent();
            this.cancel = cancelCallback;

            this.Closing += new CancelEventHandler(ThreadedInfoForm_Closing);
        }


        private void ThreadedInfoForm_Closing(object sender, CancelEventArgs e)
        {
            if (this.cancel != null)
            {
                this.cancel();
            }
        }

        private string nextProgress = null;

        public void SetProgress( string progress ){

            nextProgress = progress;


            try
            {
                Invoke(new InvokeDelegate(this.DoSetProgress));
            }
            catch (InvalidOperationException)
            {
            }
        }


        public void DoClose()
        {
            try
            {
                Invoke(new InvokeDelegate(this.DoTClose));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void DoTClose()
        {
            this.Close();
        }

        private void DoSetProgress(){
            this.progressMessage.Text = nextProgress;
        }

        public static SimpleProgress StartProgress( InvokeDelegate cancelCallback
                                        ,string title
                                        ,string initialProgress ){

            var win = new SimpleProgress(cancelCallback);
            win.Text = title;
            win.progressMessage.Text = initialProgress;

            
            win.Show();


            return win;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }





    }
}
