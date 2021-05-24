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
                    DirectoryInfo txtDir = new DirectoryInfo(config.TxtPath);
                    string modelDir = Path.GetDirectoryName(config.TrainDataPath);

                    /* clear json and txt folder */
                    foreach (FileInfo file in txtDir.GetFiles())
                    {
                        file.Delete();
                    }
                    foreach (FileInfo file in jsonDir.GetFiles())
                    {
                        file.Delete();
                    }

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

                    Dictionary<string, string[]> propDict = new Dictionary<string, string[]>();
                    /* Read dictionaries */
                    foreach (string cat in catOrder.OrderArray)
                    {
                        Type catType = Type.GetType(string.Concat("ResumeParser.Classes", ".", cat));
                        foreach (PropertyInfo prop in catType.GetProperties())
                        {
                            string propArea = $"{cat}.{prop.Name}";
                            string dictFilePath = $"{modelDir}\\{propArea}.txt";
                            if (File.Exists(dictFilePath))
                            {
                                propDict[propArea] = File.ReadAllLines(dictFilePath);
                            }
                        }
                        /* if dictionary uses dict of another property */
                        foreach (KeyValuePair<string, string> pair in JsonDataInfo.DictMap) 
                        {
                            propDict[pair.Key] = propDict[pair.Value];
                        }
                    }

                    BlockParser blockParser = new BlockParser();
                    engine.PrepareToPredict(true);
                    foreach (FileInfo file in txtDir.GetFiles("*.txt", SearchOption.TopDirectoryOnly))
                    {
                        JsonData jsonData = new JsonData();
                        Type jsonDataType = typeof(JsonData);
                        Dictionary<string, CategoryRange> ranges = new Dictionary<string, CategoryRange>();
                        Dictionary<int, List<string>> blockRowMap = new Dictionary<int, List<string>>();
                        using (StreamReader sr = file.OpenText())
                        {
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
                                        int markerIdx = block.IndexOf(markerStr);
                                        if (markerIdx >= 0)
                                        {
                                            string restRowStr = block.Substring(markerIdx + markerStr.Length);
                                            blocks[i] = restRowStr;
                                            string mCat = mi.Category;
                                            if (!string.IsNullOrEmpty(mi.Attribute)) 
                                            {
                                                mCat = string.Concat(mCat, ".", mi.Attribute);
                                            }
                                            ranges[mCat] = new CategoryRange { IndexBegin = i };
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
                                        }
                                    }
                                }
                            }
                            /* set first category - contact and education */
                            if (!ranges.ContainsKey("Contact"))
                            {
                                int minTitleIdx = Tools.MinIndexBegin(ranges);
                                if (minTitleIdx > 0) 
                                {
                                    CategoryRange newRange = new CategoryRange { IndexBegin = 0, IndexEnd = minTitleIdx - 1 };
                                    ranges["Contact"] = newRange;
                                }
                            }
                            /* set category range if category range does not exists and prop range exists */
                            foreach (string cat in catOrder.OrderArray)
                            {
                                if (!ranges.ContainsKey(cat))
                                {
                                    CategoryRange newRange = new CategoryRange();
                                    bool propRangeExists = false;
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
                                            propRangeExists = true;
                                        }
                                    }
                                    if (propRangeExists) 
                                    {
                                        ranges[cat] = newRange;
                                    }
                                }
                            }
                            /* set index of first and last range */
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

                                /* fields cycle */
                                PropertyInfo[] catProps = catType.GetProperties();
                                for (int j = 0; j < catProps.Length; j++)
                                {
                                    PropertyInfo catProp = catProps[j];
                                    string propArea = $"{cat}.{catProp.Name}";
                                    CategoryRange catPropRange = null;
                                    if (ranges.ContainsKey(propArea))
                                    {
                                        catPropRange = ranges[propArea];
                                    }
                                    List<string> rangeValue = new List<string>();

                                    /* Blocks cycle */
                                    CategoryRange currentRange = catPropRange ?? (catRange ?? null);
                                    bool hasRange = currentRange != null;
                                    bool hasPropRange = catPropRange != null;
                                    int catRangeBegin = hasRange ? catRange.IndexBegin : 0;
                                    int catRangeEnd = hasRange ? catRange.IndexEnd : blocks.Count - 1;
                                    for (int i = catRangeBegin; i <= catRangeEnd; i++)
                                    {
                                        string block = blocks[i].Trim();
                                        /* check for interception with another ranges */
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
                                        }
                                        else
                                        {
                                            if (!(catRange.IndexBegin <= i && i <= catRange.IndexEnd))
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

                                        bool isFound = false;
                                        /* Parse Dates and Numbers */
                                        if (cat == "Contact")
                                        {
                                            if (RegexPattern.prop2pattern.ContainsKey(catProp.Name)) 
                                            {
                                                List<string> res = Tools.ParseByPattern(block, RegexPattern.prop2pattern[catProp.Name]);
                                                if (res != null)
                                                {
                                                    string resStr = (JsonDataInfo.DateProps.Contains(catProp.Name)) ? res[0] : string.Join(", ", res);
                                                    Tools.SetPropValue(typeof(Contact), catProp.Name, jsonData.contact, resStr);
                                                    Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                    isFound = true;
                                                    break;
                                                }
                                            }
                                        }
                                        else if (cat == "Education")
                                        {
                                            bool isStartDate = catProp.Name == "StartDate";
                                            bool isEndDate = catProp.Name == "EndDate";
                                            if (RegexPattern.prop2pattern.ContainsKey(catProp.Name))
                                            {
                                                List<string> res = Tools.ParseByPattern(block, RegexPattern.prop2pattern[catProp.Name]);
                                                if (res != null)
                                                {
                                                    PropertyInfo[] adeddProps = { typeof(Education).GetProperty("StartDate"), typeof(Education).GetProperty("EndDate") };
                                                    Education lastItem = Tools.GetItem<Education>(jsonData.education, adeddProps, -1, out var isNew);
                                                    if (isStartDate || isEndDate)
                                                    {
                                                        if (isStartDate)
                                                        {
                                                            string dateBeg = res[0];
                                                            Tools.SetPropValue(typeof(Education), catProp.Name, lastItem, dateBeg);
                                                            Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                        }
                                                        if (isEndDate && res.Count > 1)
                                                        {
                                                            string dateEnd = res[1];
                                                            Tools.SetPropValue(typeof(Education), catProp.Name, lastItem, dateEnd);
                                                            Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                        }
                                                    }
                                                    else 
                                                    {
                                                        string resStr = (JsonDataInfo.DateProps.Contains(catProp.Name)) ? res[0] : string.Join(", ", res);
                                                        Tools.SetPropValue(typeof(Education), catProp.Name, lastItem, resStr);
                                                        Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                    }
                                                    isFound = true;
                                                    break;
                                                }
                                            }
                                        }
                                        else if (cat == "Job")
                                        {
                                            bool isStartDate = catProp.Name == "StartDate";
                                            bool isEndDate = catProp.Name == "EndDate";
                                            if (isStartDate || isEndDate)
                                            {
                                                List<string> dates = Tools.ParseByPattern(block, RegexPattern.DatePattern);
                                                if (dates != null)
                                                {
                                                    PropertyInfo[] adeddProps = { typeof(Job).GetProperty("StartDate"), typeof(Job).GetProperty("EndDate") };
                                                    Job lastItem = Tools.GetItem<Job>(jsonData.jobs, adeddProps, -1, out var isNew);
                                                    if (isStartDate)
                                                    {
                                                        string dateBeg = dates[0];
                                                        Tools.SetPropValue(typeof(Job), "StartDate", lastItem, dateBeg);
                                                    }
                                                    if (isEndDate && dates.Count > 1)
                                                    {
                                                        string dateEnd = dates[1];
                                                        Tools.SetPropValue(typeof(Job), "EndDate", lastItem, dateEnd);
                                                    }
                                                    Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                    isFound = true;
                                                    break;
                                                }
                                            }
                                        }

                                        /* Dict search */
                                        if (propDict.ContainsKey(propArea))
                                        {
                                            string dictValue = null;
                                            foreach (string val in propDict[propArea]) 
                                            {
                                                string pattern = RegexPattern.GetDictPattern(catProp.Name, val);
                                                try
                                                {
                                                    Match dictMatch = Regex.Match(block, pattern, RegexOptions.IgnoreCase);
                                                    if (dictMatch.Success)
                                                    {
                                                        dictValue = dictMatch.Value.Trim();
                                                        break;
                                                    }
                                                }
                                                catch { }
                                            }
                                            if (dictValue != null)
                                            {
                                                Tools.AddNewValue(cat, catProp.Name, jsonData, dictValue);
                                                Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                isFound = true;
                                            }
                                        }
                                        if (!isFound)
                                        {
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
                                                    Tools.AddBlockRowMap(blockRowMap, i, propArea);
                                                    isFound = true;
                                                }
                                            }
                                        }
                                        if (!isFound && catProp.Name == "Description" && !blockRowMap.ContainsKey(i)) 
                                        {
                                            Tools.AddNewValue(cat, catProp.Name, jsonData, block);
                                        }
                                    }
                                    if (rangeValue.Count > 0) 
                                    {
                                        Tools.AddNewValue(cat, catProp.Name, jsonData, string.Join(", ", rangeValue));
                                    }
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
