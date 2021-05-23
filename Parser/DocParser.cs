using System;
using System.IO;
using System.Text;
using System.Xml;

namespace ResumeParser.Parser
{
    public class DocParser
    {
        private bool showMessages;

        public DocParser(bool showMsg = false) 
        {
            showMessages = showMsg;
        }

        public bool ExtractText(string inFileName, string outFileName)
        {
            StreamWriter outFile = null;
            try
            {
                if (showMessages) Console.Write($"Extract text from '{inFileName}' into '{outFileName}'\n");
                const string wordmlNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                StringBuilder textBuilder = new StringBuilder();
                using (DocumentFormat.OpenXml.Packaging.WordprocessingDocument wdDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(inFileName, false))
                {
                    // Manage namespaces to perform XPath queries.  
                    NameTable nt = new NameTable();
                    XmlNamespaceManager nsManager = new XmlNamespaceManager(nt);
                    nsManager.AddNamespace("w", wordmlNamespace);

                    // Get the document part from the package.  
                    // Load the XML in the document part into an XmlDocument instance.  
                    XmlDocument xdoc = new XmlDocument(nt);
                    xdoc.Load(wdDoc.MainDocumentPart.GetStream());

                    XmlNodeList paragraphNodes = xdoc.SelectNodes("//w:p", nsManager);
                    foreach (XmlNode paragraphNode in paragraphNodes)
                    {
                        XmlNodeList textNodes = paragraphNode.SelectNodes(".//w:t", nsManager);
                        foreach (System.Xml.XmlNode textNode in textNodes)
                        {
                            textBuilder.Append(textNode.InnerText);
                        }
                        textBuilder.Append(Environment.NewLine);
                    }

                }
                outFile = new StreamWriter(outFileName, false, Encoding.UTF8);
                outFile.Write(textBuilder.ToString());
                return true;
            }
            catch (Exception exc)
            {
                if (showMessages) Console.WriteLine(exc);
                return false;
            }
            finally
            {
                if (outFile != null) outFile.Close();
            }
        }
    }
}
