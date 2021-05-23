using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ResumeParser.Parser;
using ResumeParser.ML;
using ResumeParser.Classes;

namespace ResumeParser
{
    public enum ParseMode { NONE, TRAIN, PARSE }
    public class Config
    {
        public ParseMode Mode = ParseMode.NONE;
        public string TrainDataPath;
        public string PdfPath;
        public string DocPath;
        public string JsonPath;
        public string TxtPath;
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Config config = ParseArgs(args);
                if ((config == null) || (config.Mode == ParseMode.TRAIN && !Tools.CheckFile(config.TrainDataPath))
                    || (config.Mode == ParseMode.PARSE && (!Tools.CheckDir(config.PdfPath, false) && !Tools.CheckDir(config.DocPath, false))))
                {
                    DisplayUsage();
                    return;
                }

                MLEngine engine = new MLEngine(config.TrainDataPath, true);
                if (config.Mode == ParseMode.TRAIN)
                {
                    engine.PrepareToTrain();
                    engine.TrainModel();
                }
                else if (config.Mode == ParseMode.PARSE)
                {
                    Tools.CheckDir(config.JsonPath, true);

                    DirectoryInfo jsonDir = new DirectoryInfo(config.JsonPath);
                    config.TxtPath = config.JsonPath + "\\Txt";
                    Tools.CheckDir(config.TxtPath, true);
                    string modelDir = Path.GetDirectoryName(config.TrainDataPath);

                    if (config.PdfPath.Length > 0)
                    {
                        PDFParser pdfParser = new PDFParser(true);
                        DirectoryInfo pdfDir = new DirectoryInfo(config.PdfPath);
                        foreach (FileInfo file in pdfDir.GetFiles("*.pdf", SearchOption.AllDirectories))
                        {
                            pdfParser.ExtractText(file.FullName, $"{config.TxtPath}\\{file.Name}.txt");
                        }
                    }
                    if (config.DocPath.Length > 0)
                    {
                        DocParser docParser = new DocParser(true);
                        DirectoryInfo docDir = new DirectoryInfo(config.DocPath);
                        foreach (FileInfo file in docDir.GetFiles("*.docx", SearchOption.AllDirectories))
                        {
                            docParser.ExtractText(file.FullName, $"{config.TxtPath}\\{file.Name}.txt");
                        }
                    }

                    /* Read info jsons */
                    string searchOrderRaw = File.ReadAllText($"{modelDir}\\SearchOrder.json");
                    CategoryOrder catOrder = JsonConvert.DeserializeObject<CategoryOrder>(searchOrderRaw);
                    string markerRaw = File.ReadAllText($"{modelDir}\\Marker.json");
                    Marker marker = JsonConvert.DeserializeObject<Marker>(markerRaw);

                    BlockParser blockParser = new BlockParser();
                    DirectoryInfo txtDir = new DirectoryInfo(config.TxtPath);
                    engine.PrepareToPredict(true);
                    foreach (FileInfo file in txtDir.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                    {
                        JsonData jsonData = new JsonData();
                        Type jsonDataType = typeof(JsonData);
                        Dictionary<string, string[]> attrDict = new Dictionary<string, string[]>();
                        Dictionary<string, CategoryRange> ranges = new Dictionary<string, CategoryRange>();
                        using (StreamReader sr = file.OpenText())
                        {
                            Console.WriteLine("------------------------------------------------------------------------------------");
                            Console.WriteLine($"Parsing file '{file.Name}', start time: {DateTime.Now.ToString()} ");
                            List<string> blocks = blockParser.Parse(sr.ReadToEnd());
                            /* Fill markers */
                            List<int> markerIndexes = new List<int>();
                            foreach (MarkerInfo mi in marker.Items) 
                            {
                                for (int i = 0; i < blocks.Count; i++) 
                                {
                                    string block = blocks[i];
                                    for (int j = 0; j < mi.Markers.Length; j++) 
                                    {
                                        string markerStr = mi.Markers[j];
                                        if (block.Contains(markerStr)) 
                                        {
                                            string mCat = mi.Category;
                                            if (!string.IsNullOrEmpty(mi.Attribute)) 
                                            {
                                                mCat = string.Concat(mCat, ".", mi.Attribute);
                                            }
                                            ranges[mCat] = new CategoryRange { IndexBegin = i + 1 };
                                            markerIndexes.Add(i);
                                            break;
                                        }
                                    }
                                }
                            }
                            /* Set marker end index */
                            foreach (KeyValuePair<string, CategoryRange> pair in ranges)
                            {
                                CategoryRange fcatRange = pair.Value;
                                foreach (KeyValuePair<string, CategoryRange> pair2 in ranges)
                                {
                                    
                                    if (pair.Key != pair2.Key) 
                                    {
                                        CategoryRange catRange2 = pair2.Value;
                                        if (catRange2.IndexBegin > fcatRange.IndexBegin && (fcatRange.IndexEnd < 0 || fcatRange.IndexEnd >= catRange2.IndexBegin)) 
                                        {
                                            fcatRange.IndexEnd = catRange2.IndexBegin - 1;
                                            if (markerIndexes.Any(idx => idx == fcatRange.IndexEnd)) 
                                            {
                                                fcatRange.IndexEnd -= 1;
                                            }
                                        }
                                    }
                                }
                            }
                            /* set category range if category range does not exists and attribute range exists */
                            foreach (string cat in catOrder.OrderArray)
                            {
                                if (!ranges.ContainsKey(cat))
                                {
                                    CategoryRange newRange = new CategoryRange();
                                    bool attrRangeExists = false;
                                    foreach (KeyValuePair<string, CategoryRange> pair in ranges) 
                                    {
                                        if (pair.Key.StartsWith(cat)) 
                                        {
                                            CategoryRange necatRange = pair.Value;
                                            if (necatRange.IndexBegin >= 0 && necatRange.IndexBegin < newRange.IndexBegin) 
                                            {
                                                newRange.IndexBegin = necatRange.IndexBegin;
                                            }
                                            if (necatRange.IndexEnd >= 0 && necatRange.IndexEnd > newRange.IndexEnd)
                                            {
                                                newRange.IndexEnd = necatRange.IndexEnd;
                                            }
                                            attrRangeExists = true;
                                        }
                                    }
                                    if (attrRangeExists) 
                                    {
                                        ranges[cat] = newRange;
                                    }
                                }
                            }
                            /* set index of first and last ranges */
                            foreach (KeyValuePair<string, CategoryRange> pair in ranges)
                            {
                                CategoryRange lcatRange = pair.Value;
                                if (lcatRange.IndexBegin < 0)
                                {
                                    lcatRange.IndexBegin = 0;
                                }
                                if (lcatRange.IndexEnd < 0)
                                {
                                    lcatRange.IndexEnd = blocks.Count - 1;
                                }
                            }

                            /* Category cycle */
                            CategoryRange catRange;
                            foreach (string cat in catOrder.OrderArray) 
                            {
                                Type catType = Type.GetType(string.Concat("ResumeParser.Classes", ".", cat));
                                if (!ranges.TryGetValue(cat, out catRange)) 
                                {
                                    catRange = null;
                                };
                                /* Read dictionaries */
                                foreach (PropertyInfo prop in catType.GetProperties())
                                {
                                    string attrArea = $"{cat}.{prop.Name}";
                                    string dictFilePath = $"{modelDir}\\{attrArea}.txt";
                                    if (File.Exists(dictFilePath)) 
                                    {
                                        attrDict[attrArea] = File.ReadAllLines(dictFilePath);
                                    }
                                }

                                /* fields cycle */
                                List<string> catDescription = new List<string>();
                                PropertyInfo[] catProps = catType.GetProperties();
                                int checkDescIdx = 0;
                                for (int j = 0; j < catProps.Length; j++)
                                {
                                    PropertyInfo catProp = catProps[j];
                                    string attrArea = $"{cat}.{catProp.Name}";
                                    CategoryRange catPropRange = null;
                                    if (ranges.ContainsKey(attrArea))
                                    {
                                        catPropRange = ranges[attrArea];
                                    }
                                    List<string> rangeValue = new List<string>();

                                    /* Blocks cycle */
                                    CategoryRange currentRange = catPropRange ?? (catRange ?? null);
                                    bool hasRange = currentRange != null;
                                    bool hasPropRange = catPropRange != null;
                                    int catRangeBegin = hasRange ? catRange.IndexBegin : 0;
                                    int catRangeEnd = hasRange ? catRange.IndexEnd : 0;
                                    for (int i = catRangeBegin; i <= catRangeEnd; i++)
                                    {
                                        string block = blocks[i].Trim();
                                        /* check for empty range indexes */
                                        if (!hasRange) 
                                        {
                                            bool hasInterception = false;
                                            foreach (KeyValuePair<string, CategoryRange> pair in ranges) 
                                            {
                                                CategoryRange ckcatRange = pair.Value;
                                                if (ckcatRange.IndexBegin <= i && ckcatRange.IndexEnd >= i) 
                                                {
                                                    hasInterception = true;
                                                    break;
                                                }
                                            }
                                            if (hasInterception)
                                            {
                                                continue;
                                            }
                                            else 
                                            {
                                                if (j == checkDescIdx)
                                                {
                                                    catDescription.Add(block);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (catRange.IndexBegin <= i && i <= catRange.IndexEnd)
                                            {
                                                if (j == checkDescIdx)
                                                {
                                                    catDescription.Add(block);
                                                }
                                            }
                                            else 
                                            {
                                                continue;
                                            }
                                        }
                                        if ((cat == "Job" && catProp.Name == "Title" && block.Length > 70) || (cat == "Contact" && catProp.Name == "Name" &&
                                            (string.IsNullOrEmpty(jsonData.contact.Name) ? 0 : jsonData.contact.Name.Length) + block.Length > 70))
                                        {
                                            continue;
                                        }

                                        if (hasPropRange)
                                        {
                                            /* get all blocks from attribute range */
                                            if (catPropRange.IndexBegin <= i && i <= catPropRange.IndexEnd)
                                            {
                                                rangeValue.Add(block);
                                            }
                                            continue;
                                        }

                                        /* Parse Dates and Numbers */
                                        if (cat == "Contact")
                                        {
                                            List<string> phones = Tools.ParsePhoneNumber(block);
                                            if (phones != null)
                                            {
                                                string phoneStr = string.Join(", ", phones);
                                                Tools.SetPropValue(typeof(Contact), "PhoneNumber", jsonData.contact, phoneStr);
                                                break;
                                            }

                                            List<string> dates = Tools.ParseDate(block);
                                            if (dates != null)
                                            {
                                                string datesStr = dates[0];
                                                Tools.SetPropValue(typeof(Contact), "BirthDate", jsonData.contact, datesStr);
                                                break;
                                            }
                                        }
                                        else if (cat == "Study")
                                        {
                                            List<string> dates = Tools.ParseDate(block);
                                            if (dates != null)
                                            {
                                                PropertyInfo[] adeddProps = { typeof(Study).GetProperty("StartDate"), typeof(Study).GetProperty("EndDate") };
                                                Study lastItem = Tools.GetItem<Study>(jsonData.studies, adeddProps, -1, out var isNew);
                                                string dateBeg = dates[0];
                                                Tools.SetPropValue(typeof(Study), "StartDate", lastItem, dateBeg);
                                                if (dates.Count > 1) 
                                                {
                                                    string dateEnd = dates[1];
                                                    Tools.SetPropValue(typeof(Study), "EndDate", lastItem, dateEnd);
                                                }
                                                break;
                                            }
                                        }
                                        else if (cat == "Job")
                                        {
                                            List<string> dates = Tools.ParseDate(block);
                                            if (dates != null)
                                            {
                                                PropertyInfo[] adeddProps = { typeof(Job).GetProperty("StartDate"), typeof(Job).GetProperty("EndDate") };
                                                Job lastItem = Tools.GetItem<Job>(jsonData.jobs, adeddProps, -1, out var isNew);
                                                string dateBeg = dates[0];
                                                Tools.SetPropValue(typeof(Job), "StartDate", lastItem, dateBeg);
                                                if (dates.Count > 1)
                                                {
                                                    string dateEnd = dates[1];
                                                    Tools.SetPropValue(typeof(Job), "EndDate", lastItem, dateEnd);
                                                }
                                                break;
                                            }
                                        }

                                        /* Dict search */
                                        bool isInDict = false;
                                        if (attrDict.ContainsKey(attrArea)) 
                                        {
                                            if (attrDict[attrArea].Any(s => {
                                                try
                                                {
                                                    if (Regex.IsMatch(block, $"\\b{s.Trim()}\\b", RegexOptions.IgnoreCase))
                                                    {
                                                        return true;
                                                    }
                                                }
                                                catch
                                                {
                                                    /* if (s.Length > 3 && block.Contains(s, StringComparison.CurrentCultureIgnoreCase)) 
                                                    {
                                                        return true;
                                                    } */
                                                }
                                                return false;
                                            }))
                                            {
                                                bool isNew = Tools.AddNewValue(cat, catProp.Name, jsonData, block);
                                                if (isNew && cat == "Job" && catProp.Name == "Title") 
                                                {
                                                    Tools.AddNewValue(cat, "Description", jsonData, string.Join("\n", catDescription), jsonData.jobs.Count - 2);
                                                    catDescription.Clear();
                                                    catDescription.Add(block);
                                                    checkDescIdx = j;
                                                }
                                                isInDict = true;
                                            }
                                        }
                                        if (isInDict)
                                        {
                                            continue;
                                        }

                                        /* ML search */
                                        TextArea textArea = new TextArea { Title = cat, Description = block };
                                        AreaPrediction prediction = engine.SinglePredict(textArea);
                                        string[] areaParts = prediction.PredictedArea.Split('.');
                                        if (areaParts.Length == 2)
                                        {
                                            string predictCat = areaParts[0];
                                            if (prediction.PredictedArea == predictCat)
                                            {
                                                string predictProp = areaParts[1];
                                                Tools.AddNewValue(cat, predictProp, jsonData, block);
                                            }
                                        }
                                    }
                                    if (rangeValue.Count > 0) 
                                    {
                                        Tools.AddNewValue(cat, catProp.Name, jsonData, string.Join("\n", rangeValue));
                                    }
                                }
                                if (catDescription.Count > 0)
                                {
                                    Tools.AddNewValue(cat, "Description", jsonData, string.Join("\n", catDescription));
                                }
                            }

                            /* Batch Prediction */
                            /* List<TextArea> areas = new List<TextArea>();
                            foreach (string block in blocks) 
                            {
                                TextArea ta = new TextArea { Description = block };
                                areas.Add(ta);
                            }
                            IEnumerable<AreaPrediction> prefictedAreas = engine.Predict(areas);
                            foreach (AreaPrediction prediction in prefictedAreas) 
                            {
                                Console.Write($"{prediction.Description}\nPredicted area - {prediction.PredictedArea}\n");
                            }*/

                            string json = JsonConvert.SerializeObject(jsonData, Formatting.Indented);
                            File.WriteAllText($"{config.JsonPath}\\{Path.GetFileNameWithoutExtension(file.Name)}.json", json);

                            Console.WriteLine("------------------------------------------------------------------------------------");
                            Console.WriteLine($"File '{file.Name}' has parsed, end time: {DateTime.Now.ToString()} ");
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }
        }

        static void DisplayUsage()
        {
            Console.WriteLine();
            Console.WriteLine("Train mode usage:\tResumeParser -mode train -data TRAINDATAPATH");
            Console.WriteLine("Parse mode usage:\tResumeParser -mode parse -data TRAINDATAPATH -pdf PDFPATH -doc DOCPATH -json JSONPATH");
            Console.WriteLine();
            Console.WriteLine("\tmode\t 'train' or 'parse'");
            Console.WriteLine("\tdata\t the absolute path to the train data path, reguired");
            Console.WriteLine("\tpdf\t the absolute path to PDF files folder, optional");
            Console.WriteLine("\tdoc\t the absolute path to MS WORD files folder, optional");
            Console.WriteLine("\tjson\t the absolute path to JSON files folder, reguired");
            Console.WriteLine();
        }

        static Config ParseArgs(string[] args) 
        {
            if (args.Length < 4) 
            {
                return null;
            }

            Config result = new Config();
            List<string> keys = new List<string>();
            List<string> values = new List<string>();
            keys.Add(args[0]);
            values.Add(args[1]);
            keys.Add(args[2]);
            values.Add(args[3]);
            if (args.Length > 4)
            {
                keys.Add(args[4]);
                values.Add(args[5]);
            }
            if (args.Length > 6)
            {
                keys.Add(args[6]);
                values.Add(args[7]);
            }
            if (args.Length > 8)
            {
                keys.Add(args[8]);
                values.Add(args[9]);
            }
            for (int i = 0; i < keys.Count; i++)
            {
                switch (keys[i]) 
                {
                    case "-mode":
                        if (values[i] == "train")
                        {
                            result.Mode = ParseMode.TRAIN;
                        }
                        else if (values[i] == "parse")
                        {
                            result.Mode = ParseMode.PARSE;
                        }
                        else 
                        {
                            result.Mode = ParseMode.NONE;
                        }
                        break;
                    case "-data":
                        result.TrainDataPath = values[i];
                        break;
                    case "-pdf":
                        result.PdfPath = values[i];
                        break;
                    case "-doc":
                        result.DocPath = values[i];
                        break;
                    case "-json":
                        result.JsonPath = values[i];
                        break;
                }
            }
            if (result.Mode == ParseMode.NONE)
            {
                return null;
            }
            if ((result.Mode == ParseMode.PARSE) && ((string.IsNullOrEmpty(result.PdfPath) && string.IsNullOrEmpty(result.DocPath)) || string.IsNullOrEmpty(result.JsonPath)))
            {
                return null;
            }
            return result;
        }
    }
}
