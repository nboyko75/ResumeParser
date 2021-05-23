using ResumeParser.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ResumeParser.Parser
{
    public class Tools
    {
        public static void SetPropValue(Type objType, string propName, object obj, string newVal)
        {
            PropertyInfo attrProp = objType.GetProperty(propName);
            string oldVal = (string)attrProp.GetValue(obj);
            if (oldVal != null)
            {
                oldVal = oldVal + "\n";
            }
            attrProp.SetValue(obj, string.Concat(oldVal, newVal));
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

        public static List<string> ParseDate(string inputStr)
        {
            string pattern = @"(\d+)[-.\/](\d+)[-.\/](\d+)";
            return ParseByPattern(inputStr, pattern);
        }

        public static List<string> ParsePhoneNumber(string inputStr) 
        {
            string pattern = @"^(\+?(?<NatCode>1)\s*[-\/\.]?)?(\((?<AreaCode>\d{3})\)|(?<AreaCode>\d{3}))\s*[-\/\.]?\s*(?<Number1>\d{3})\s*[-\/\.]?\s*(?<Number2>\d{4})\s*(([xX]|[eE][xX][tT])\.?\s*(?<Ext>\d+))*$";
            return ParseByPattern(inputStr, pattern);
        }

        public static List<string> ParseByPattern(string inputStr, string pattern) 
        {
            List<string> result = null;
            MatchCollection mc = Regex.Matches(inputStr, pattern);
            if (mc.Count > 0)
            {
                result = new List<string>();
                foreach (Match match in mc)
                {
                    result.Add(match.Value);
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
                if (!isIndex)
                {
                    for (int i = 0; i < adeddProps.Length; i++)
                    {
                        PropertyInfo propType = adeddProps[i];
                        if (propType.Name != "Description")
                        {
                            string oldValue = (string)propType.GetValue(lastItem);
                            if (oldValue != null)
                            {
                                toAdd = true;
                                break;
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
            string block, int index) where T : new()
        {
            bool isNew = false;
            if (JsonDataInfo.SimpleClasses.Contains(cat))
            {
                if (cat == "Contact")
                {
                    SetPropValue(typeof(Contact), addedPropName, jsonData.contact, block);
                }
            }
            else if (JsonDataInfo.ListClasses.Contains(cat))
            {
                PropertyInfo[] adeddProps = { typeof(T).GetProperty(addedPropName) };
                T lastItem = Tools.GetItem<T>(lst, adeddProps, index, out isNew);
                SetPropValue(typeof(T), addedPropName, lastItem, block);
            }
            return isNew;
        }

        public static bool AddNewValue(string cat, string addedPropName, JsonData jsonData, string block, int index = -1)
        {
            bool result = false;
            switch (cat)
            {
                case "Contact":
                    {
                        PropertyInfo adeddProp = typeof(Contact).GetProperty(addedPropName);
                        result = AddNewValueByType<Contact>(null, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
                case "Study":
                    {
                        PropertyInfo adeddProp = typeof(Study).GetProperty(addedPropName);
                        result = AddNewValueByType<Study>(jsonData.studies, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
                case "Job":
                    {
                        PropertyInfo adeddProp = typeof(Job).GetProperty(addedPropName);
                        result = AddNewValueByType<Job>(jsonData.jobs, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
                case "Skill":
                    {
                        PropertyInfo adeddProp = typeof(Skill).GetProperty(addedPropName);
                        result = AddNewValueByType<Skill>(jsonData.skills, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
                case "Project":
                    {
                        PropertyInfo adeddProp = typeof(Project).GetProperty(addedPropName);
                        result = AddNewValueByType<Project>(jsonData.projects, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
                case "Award":
                    {
                        PropertyInfo adeddProp = typeof(Award).GetProperty(addedPropName);
                        result = AddNewValueByType<Award>(jsonData.awards, adeddProp, cat, addedPropName, jsonData, block, index);
                        break;
                    }
            }
            return result;
        }
    }
}
