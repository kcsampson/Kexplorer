using System;
using System.IO;
using System.Windows.Forms;
using Kexplorer.scripting;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;



namespace Kexplorer.scripts
{
	/// <summary>
	/// Run Beyond Compare
	/// </summary>
	public class XSLTRunScript : BaseFileScript
	{


       
        public static string XSLT_RUN_SCRIPT_NAME = "XSLT Run";
		public XSLTRunScript()
		{
           
			
			this.Description = "Run XSLT on the selected XSLT with previous selected XML";

            this.LongName = XSLT_RUN_SCRIPT_NAME;

			this.Active = false;

			this.ValidExtensions = new string[]{".xslt"};
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{

			ScriptRunParams runParams = (ScriptRunParams)this.ScriptHelper.VARS[
																	"SetAsXmlInputToXsltScript"];



            RunXslt(files[0].FullName, runParams.Files[0].FullName, folder.FullPath + "\\output.xml");
			



		}



        public void RunXslt(String xsltFile, String xmlDataFile, String outputXml)
        {

            XPathDocument input = new XPathDocument(xmlDataFile);

            XslTransform myXslTrans = new XslTransform() ;
            XmlTextWriter output = null;
            try
            {
                myXslTrans.Load(xsltFile);

                output = new XmlTextWriter(outputXml, null);
                output.Formatting = Formatting.Indented;


                myXslTrans.Transform(input, null, output);




            }
            catch (Exception e)
            {
                String msg = e.Message;
                if (msg.IndexOf("InnerException") >0)
                {
                    msg = e.InnerException.Message;
                }

                MessageBox.Show("Error: " + msg + "\n" + e.StackTrace, "Xslt Error File: " + xsltFile, MessageBoxButtons.OK, MessageBoxIcon.Error);
                

            }
            finally
            {
                try { output.Close(); }
                catch (Exception) { }
            }


        }


	}
}
