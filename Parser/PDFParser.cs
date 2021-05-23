using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SelectPdf;

namespace ResumeParser.Parser
{
    public class PDFParser 
    {
        public PDFParser(bool showMsg = false)
        {
            showMessages = showMsg;
        }

        #region Fields
        private bool showMessages;
        #endregion

        #region Public methods
        public bool ExtractText(string inFileName, string outFileName) 
        {
            try
            {
                using (StreamWriter outFile = new StreamWriter(outFileName, false))
                {
                    PdfToText pdfToText = new PdfToText();
                    pdfToText.Load(inFileName);

                    // set the properties
                    pdfToText.Layout = SelectPdf.TextLayout.Reading;
                    int pageCount = pdfToText.GetPageCount();
                    pdfToText.StartPageNumber = 0;
                    pdfToText.EndPageNumber = pageCount - 1;

                    // extract the text
                    string text = pdfToText.GetText();
                    List<string> txtList = text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    IEnumerable<string> delDemoLinesList = txtList.Skip(7).Take(txtList.Count - 9);
                    outFile.Write(string.Join("\n", delDemoLinesList));
                }
                return true;
            }
            catch (Exception exc)
            {
                if (showMessages) Console.WriteLine(exc);
                return false;
            }
        }
        #endregion
    }
}