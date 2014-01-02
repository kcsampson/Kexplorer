using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.IO;

namespace com.kimbonics.xslt
{
    class XSLT
    {
        static void Main(string[] args)
        {

            String outFile = "output.xml";
            if (args.Length < 2)
            {
                Console.WriteLine("Usage:  xslt <xsltFile> <inputXml> <outputFile> <parm1Name> = <parm1Value>  <parm2Name> = <parm2Value> .....");
            }
            else if (args.Length > 2)
            {
                outFile = args[2];
            }


            new XSLT().RunXSLT(args[0], args[1], outFile, MakeArgs(args));



        }


        static private XsltArgumentList MakeArgs( string[] args ){

            if ( args.Length > 3 ){
                var xsltArgs = new XsltArgumentList();
                var pos = 0;
                while ( args.Length > ( 3 + (3*pos) ) ){

                    if ( string.IsNullOrEmpty( args[ 3 + (3*pos) +1 ] ) ){
                        break;
                    } else if (   args[ 3 + (3*pos) +1 ] != "=" ){
                        break;
                    }
                    var parmName = args[3 + (3 * pos)];
                    var parmVal = args[3 + (3 * pos) + 2];

                    Console.WriteLine("Parm( " + pos.ToString() + ")  {" + parmName + ", " + parmVal + "}");


                    xsltArgs.AddParam(parmName,string.Empty,parmVal );



                    ++pos;
                }
                return xsltArgs;
            }

            return null;
        }

        /* KCS: 2012/03/19 - Changed to be able to pass args to the template. 
         * 
         * xslt <xsltFile> <inputXml> <outputFile> <parm1Name> = <parm1Value>  <parm2Name> = <parm2Value> .....
         */
        public void RunXSLT(String xsltFile, String xmlDataFile, String outFile, XsltArgumentList xsltArgs )
        {

            XPathDocument input = new XPathDocument(xmlDataFile);

            XslTransform myXslTrans = new XslTransform();
            XmlTextWriter output = null;
            try
            {

                   
                myXslTrans.Load(xsltFile);

                output = new XmlTextWriter(outFile, null);
                output.Formatting = Formatting.Indented;


                myXslTrans.Transform(input, xsltArgs, output);

            }
            catch (Exception e)
            {

                Console.WriteLine(e.StackTrace);
                Console.WriteLine("Error: " + e.Message);


            }
            finally
            {
                try { output.Close(); }
                catch (Exception) { }
            }
        }
    }
}

