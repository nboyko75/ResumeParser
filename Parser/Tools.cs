using ResumeParser.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ResumeParser.Parser
{
    public class RegexPattern
    {
        public static string ResultGroup = "result";
        public static string ResultGroupPattern = @"?<result>";
        public static string DatePattern = @"((\d+)[-.\/](\d+)[-.\/](\d+))|((January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4})";
        public static string PhoneNumberPattern = @"(\+?(?<NatCode>1)\s*[-\/\.]?)?(\((?<AreaCode>\d{3})\)|(?<AreaCode>\d{3}))\s*[-\/\.]?\s*(?<Number1>\d{3})\s*[-\/\.]?\s*(?<Number2>\d{4})\s*(([xX]|[eE][xX][tT])\.?\s*(?<Ext>\d+))*";
        public static string AddressPattern = @"\b(\d{1,6} )?(.{2,25}\b(avenue|ave|court|ct|street|st|drive|dr|lane|ln|road|rd|blvd|plaza|parkway|pkwy|way)[.,])?(.{0,25} +\b\d{5}\b)";
        public static string EmailPattern = @"([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)";
        public static string ZipPattern = @"\b\d{5}(?:-\d{4})?\b";
        public static string LinkedInPattern = @"https:\/\/[a-z]{2,3}\.linkedin\.com\/\S*\b";
        public static string GithubPattern = @"https:\/\/github.com\/\S*\b";
        public static string GpaPattern = @"\bGPA\b\W+(?<result>[0-4]\.\d{1,2})";
        public static Dictionary<string, string> prop2pattern = new()
        {
            { "PhoneNumber", PhoneNumberPattern },
            { "Address", AddressPattern },
            { "BirthDate", DatePattern },
            { "Email", EmailPattern },
            { "Zip", ZipPattern },
            { "LinkedIn", LinkedInPattern },
            { "Github", GithubPattern },
            { "StartDate", DatePattern },
            { "EndDate", DatePattern },
            { "Gpa", GpaPattern }
        };
        public static string[] InstitutionWithTail = { "university", "institute" };

        public static string GetDictPattern(string propName, string dictValue) 
        {
            string result;
            if (propName == "Name")
            {
                result = $"\\b{dictValue.Trim()}\\b((\\s)+(\\w)+)?";
            }
            else if (propName == "Title") 
            {
                result = $"((\\s)+(\\w)+)*\\b{dictValue.Trim()}\\b((\\s)+(\\w)+)*";
            }
            else if (propName == "Institution")
            {
                if (InstitutionWithTail.Contains(dictValue.ToLower()))
                {
                    result = $"{dictValue.Trim()}\\b[\\s\\w]*";
                }
                else 
                {
                    result = $"[\\s\\w]*\\b{dictValue.Trim()}\\b";
                }
            }
            else
            {
                result = $"\\b{dictValue.Trim()}\\b";
            }
            return result;
        }
    }

    public class Tools
    {
        public static bool SetPropValue(Type objType, string propName, object obj, string newVal, Dictionary<int, List<string>> blockRowMap,
            int rowIdx, int rangeBeg, int rangeEnd)
        {
            PropertyInfo propInfo = objType.GetProperty(propName);
            string cat = objType.Name;
            string oldVal = (string)propInfo.GetValue(obj);
            bool toSetValue = true;
            if (JsonDataInfo.notFillAfterList.Contains(cat)) 
            {
                for (int i = rowIdx; i >= rangeBeg; i--) 
                {
                    if (blockRowMap.TryGetValue(i, out List<string> rowCats))
                    {
                        if (rowCats.Any(s => !s.StartsWith(cat)))
                        {
                            toSetValue = false;
                            break;
                        }
                    }
                }
            }
            if (JsonDataInfo.FillAfterMap.TryGetValue(propName, out string fillAfterPropName))
            {
                PropertyInfo fillAfterProp = objType.GetProperty(fillAfterPropName);
                string fillAfterPropValue = (string)fillAfterProp.GetValue(obj);
                if (string.IsNullOrEmpty(fillAfterPropValue))
                {
                    toSetValue = false;
                }
            }
            else if (JsonDataInfo.FillBeforeMap.TryGetValue(propName, out string fillBeforePropName))
            {
                PropertyInfo fillBeforeProp = objType.GetProperty(fillBeforePropName);
                string fillBeforePropValue = (string)fillBeforeProp.GetValue(obj);
                if (string.IsNullOrEmpty(fillBeforePropValue))
                {
                    string propArea = $"{cat}.{propName}";
                    if (blockRowMap.TryGetValue(rowIdx, out List<string> rowCats))
                    {
                        if (rowCats.Any(s => !s.StartsWith(cat)))
                        {
                            toSetValue = false;
                        }
                    }
                }
            }
            else if (oldVal != null)
            {
                if (JsonDataInfo.MultipleValueProps.Contains(propName))
                {
                    oldVal += ", ";
                }
                else
                {
                    toSetValue = false;
                }
            }
            if (toSetValue)
            {
                propInfo.SetValue(obj, string.Concat(oldVal, newVal));
            }
            return toSetValue;
        }

        public static bool CheckDir(string dir, bool toCreate)
        {
            bool result = dir.Length > 0;
            if (result)
            {
                if (!Directory.Exists(dir))
                {
                    if (toCreate)
                    {
                        Directory.CreateDirectory(dir);
                    }
                    else
                    {
                        Console.WriteLine($"Directory '{dir}' does not exists.");
                        result = false;
                    }
                }
            }
            return result;
        }

        public static bool CheckFile(string filePath)
        {
            bool result = filePath.Length > 0;
            if (result)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File '{filePath}' does not exists.");
                    result = false;
                }
            }
            return result;
        }

        public static List<string> ParseByPattern(string inputStr, string pattern) 
        {
            List<string> result = null;
            if (pattern.Contains(RegexPattern.ResultGroupPattern))
            {
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                Match match = regex.Match(inputStr);
                if (match.Success)
                {
                    GroupCollection groups = match.Groups;
                    string[] groupNames = regex.GetGroupNames();
                    if (groupNames.Contains(RegexPattern.ResultGroup))
                    {
                        result = new List<string>();
                        result.Add(groups[RegexPattern.ResultGroup].Value);
                    }
                }
            }
            else 
            {
                MatchCollection mc = Regex.Matches(inputStr, pattern);
                if (mc.Count > 0)
                {
                    result = new List<string>();
                    foreach (Match match in mc)
                    {
                        result.Add(match.Value);
                    }
                }
            }
            return result;
        }

        public static T GetItem<T>(List<T> lst, PropertyInfo[] adeddProps, int index, out bool isNew) where T : new()
        {
            isNew = false;
            int cnt = lst.Count;
            bool isIndex = index >= 0;
            int idx = isIndex ? index : cnt - 1;
            T lastItem;
            if (cnt == 0)
            {
                lastItem = new T();
                lst.Add(lastItem);
                isNew = true;
            }
            else 
            {
                lastItem = lst[idx];
                bool toAdd = false;
                if (!isIndex && adeddProps.Length > 0)
                {
                    PropertyInfo firstProp = adeddProps[0];
                    if (firstProp.Name != JsonDataInfo.LastPropName)
                    {
                        PropertyInfo descProp = typeof(T).GetProperty(JsonDataInfo.LastPropName);
                        if (descProp != null)
                        {
                            string descVal = (string)descProp.GetValue(lastItem);
                            if (!string.IsNullOrEmpty(descVal))
                            {
                                toAdd = true;
                            }
                        }
                    }
                    if (!toAdd)
                    {
                        for (int i = 0; i < adeddProps.Length; i++)
                        {
                            PropertyInfo propType = adeddProps[i];
                            if (!JsonDataInfo.MultipleValueProps.Contains(propType.Name))
                            {
                                bool check4Add = true;
                                if (JsonDataInfo.FillAfterMap.TryGetValue(propType.Name, out string fillAfterPropName))
                                {
                                    PropertyInfo fillAfterProp = typeof(T).GetProperty(fillAfterPropName);
                                    string fillAfterPropValue = (string)fillAfterProp.GetValue(lastItem);
                                    if (string.IsNullOrEmpty(fillAfterPropValue))
                                    {
                                        check4Add = false;
                                    }
                                }
                                if (check4Add)
                                {
                                    string oldValue = (string)propType.GetValue(lastItem);
                                    if (!string.IsNullOrEmpty(oldValue))
                                    {
                                        toAdd = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (toAdd) 
                {
                    lastItem = new T();
                    lst.Add(lastItem);
                    isNew = true;
                }
            }
            return lastItem;
        }

        public static bool AddNewValueByType<T>(List<T> lst, PropertyInfo adeddProp, string cat, string addedPropName, JsonData jsonData,
            string block, Dictionary<int, List<string>> blockRowMap, int rowIdx, int rangeBeg, int rangeEnd, int index) where T : new()
        {
            bool result = false;
            if (JsonDataInfo.SimpleClasses.Contains(cat))
            {
                if (cat == "Contact")
                {
                    result = SetPropValue(typeof(Contact), addedPropName, jsonData.contact, block, blockRowMap, rowIdx, rangeBeg, rangeEnd);
                }
            }
            else if (JsonDataInfo.ListClasses.Contains(cat))
            {
                PropertyInfo[] adeddProps = { typeof(T).GetProperty(addedPropName) };
                T lastItem = Tools.GetItem<T>(lst, adeddProps, index, out bool isNew);
                result = SetPropValue(typeof(T), addedPropName, lastItem, block, blockRowMap, rowIdx, rangeBeg, rangeEnd);
            }
            return result;
        }

        public static bool AddNewValue(string cat, string addedPropName, JsonData jsonData, string block, Dictionary<int, List<string>> blockRowMap,
            int rowIdx, int rangeBeg, int rangeEnd, int index = -1)
        {
            bool result = false;
            switch (cat)
            {
                case "Contact":
                    {
                        PropertyInfo adeddProp = typeof(Contact).GetProperty(addedPropName);
                        result = AddNewValueByType<Contact>(null, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
                case "Education":
                    {
                        PropertyInfo adeddProp = typeof(Education).GetProperty(addedPropName);
                        result = AddNewValueByType<Education>(jsonData.education, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
                case "Job":
                    {
                        PropertyInfo adeddProp = typeof(Job).GetProperty(addedPropName);
                        result = AddNewValueByType<Job>(jsonData.jobs, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
                case "Skill":
                    {
                        PropertyInfo adeddProp = typeof(Skill).GetProperty(addedPropName);
                        result = AddNewValueByType<Skill>(jsonData.skills, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
                case "Project":
                    {
                        PropertyInfo adeddProp = typeof(Project).GetProperty(addedPropName);
                        result = AddNewValueByType<Project>(jsonData.projects, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
                case "Award":
                    {
                        PropertyInfo adeddProp = typeof(Award).GetProperty(addedPropName);
                        result = AddNewValueByType<Award>(jsonData.awards, adeddProp, cat, addedPropName, jsonData, block, blockRowMap, rowIdx, rangeBeg, rangeEnd, index);
                        break;
                    }
            }
            return result;
        }

        public static int MinIndexBegin(Dictionary<string, CategoryRange> values)
        {
            return values.Values.ToList().Select(c => c.IndexBegin).Min();
        }

        public static void AddBlockRowMap(Dictionary<int, List<string>> dict, int idx, string propArea) 
        {
            List<string> mapStr;
            if (dict.TryGetValue(idx, out mapStr))
            {
                mapStr.Add(propArea);
            }
            else 
            {
                dict[idx] = new List<string> { propArea };
            }
        }
    }
}
