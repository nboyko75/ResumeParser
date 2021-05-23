using System;
using System.Linq;
using System.Collections.Generic;

namespace ResumeParser.Parser
{
    public class BlockParser
    {
        private string[] sentenceDelimetes = { ".", "\n", "\t", "   " };

        public List<string> Parse(string txt) 
        {
            List<string> result = new List<string>();
            string[] blocks = txt.Split(sentenceDelimetes, StringSplitOptions.RemoveEmptyEntries);
            foreach (string block in blocks) 
            {
                string trimmedBlock = block.Trim();
                if (trimmedBlock.Length > 0) 
                {
                    if (trimmedBlock.Any(c => char.IsLetterOrDigit(c))) 
                    {
                        result.Add(trimmedBlock);
                    }
                }
            }
            return result;
        }
    }
}
