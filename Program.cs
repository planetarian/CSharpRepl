using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ConsoleTest
{
    public struct Rx
    {
        public const string ReturnTypeId = "rettype";
        public const string ParamsId = "params";
        public const string MethodNameId = "name";
        public const string RemoveCommandId = "rem";
        public const string DefineCommandId = "def";
        public const string PropertyAccessorsId = "acc";
        public const string PropertyInitializerId = "init";

        public const string Identifier = @"[a-zA-Z_][\w_]*";
        public const string TypeFudgedInner = @"[a-zA-Z_0-9<>\[\],\.]+";

        public const string TypeFudged = // Can recurse generics, unreliably.
            Identifier + @"(\." + Identifier + @")*"
            + @"(<\s*" + TypeFudgedInner + @"(\s*,\s*" + TypeFudgedInner + @")*\s*>|\[,*\](\[,*\])*)?";
        public const string Type = // Cannot recurse generics.
            Identifier + @"(\." + Identifier + @")*"
            + @"(<\s*" + Identifier + @"(\s*,\s*" + Identifier + @")*\s*>|\[,*\](\[,*\])*)?";

        public const string Param = @"((ref|out|params)\s+)?" + TypeFudged + @"\s+" + Identifier;
        public const string Params = Param + @"(\s*,\s*" + Param + @")*";

        public const string Define =
            @"^\s*(define|create|method)\s+"
            + @"(?:(?<" + ReturnTypeId + @">" + TypeFudged + @")\s+)?"
            + @"(?<" + MethodNameId + @">" + Identifier + @")\s*"
            + @"(?:\(\s*(?<" + ParamsId + @">" + Params + @")\s*\))?\s*"
            + "$";

        public const string Prop =
            @"^\s*prop(?:erty)?\s+"
            + @"(?:(?<" + ReturnTypeId + @">" + TypeFudged + @"))\s+"
            + @"(?<" + MethodNameId + @">" + Identifier + @")\s*"
            + @"(?<" + PropertyAccessorsId + @">"
            + @"(?:\{\s*(?:get(?:;|\s*\{.*?\}\s*))?\s*(?:set(?:;|\s*\{.*?\}\s*))?\s*\})"
            + @"|"
            + @"\s*=\s*(.*);"
            + @")"
            + "$";
    }

    public class Program
    {
        //private const string methodCommandRegex =
            //@"(define|create|method) type(<type>|[])? name ( ref|out|params type(<type>|[])? name (, ref|out|params type name )))"

            /*
            @"^(?:(?<" + removeCommandRegexId + @">remove)|(?<" + defineCommandRegexId + @">define|create|method)) "
            + @"(?:(?<" + returnTypeRegexId + @">" + typeRegex + @") )?"
            + @"(?<"+methodNameRegexId+@">" + identifierRegex + @")"
            + @"(?:\s*\(\s*(?<" + paramsRegexId + @">" // spaces allowed before and after parenthesis (before first param)
            + methodCommandParamRegex
            + @"(?:\s*,\s*" // spaces allowed after first param (before comma) and after comma
            + methodCommandParamRegex
            + @")\s*)*" // spaces allowed after params (before comma)
            + @"\s*\))?"
            + @";?$";*/
        private const string cancelCommandRegex = @"^cancel;?$";
        
        public static Dictionary<string, object> State = new Dictionary<string, object>();
        public static int CommandId;
        private static string methodName;
        private static string returnType;
        private static string parameters;
        private static bool defineMode;
        static void Main()
        {
            while (true)
            {
                if (methodName == null)
                    methodName = "m_" + CommandId;
                
                Console.Write((defineMode? methodName : "") + "> ");
                string src = Console.ReadLine();
                Debug.Assert(src != null, "src != null");

                if (Regex.IsMatch(src, cancelCommandRegex, RegexOptions.Compiled))
                {
                    DynamicCodeManager.CancelMethod();
                    Console.WriteLine("Cancelled method" + (defineMode ? " " + methodName : "") + ".");
                    Console.WriteLine();
                    defineMode = false;
                    returnType = null;
                    parameters = null;
                    methodName = null;
                    continue;
                }

                // Ready to add a new method
                if (DynamicCodeManager.Ready)
                {
                    Match propMatch = Regex.Match(src, Rx.Prop, RegexOptions.Compiled);
                    if (propMatch.Success)
                    {
                        string propName = propMatch.Groups[Rx.MethodNameId].Value;
                        if (DynamicCodeManager.HasProperty(propName))
                        {
                            Console.WriteLine("Property {0} already exists.", propName);
                            Console.WriteLine();
                        }
                        else
                        {
                            DynamicCodeManager.AddProperty(propName, propMatch.Groups[Rx.PropertyAccessorsId].Value, propMatch.Groups[Rx.ReturnTypeId].Value);
                        }
                        continue;
                    }

                    Match match = Regex.Match(src, Rx.Define, RegexOptions.Compiled);
                    if (match.Success)
                    {
                        string tempMethodName = match.Groups[Rx.MethodNameId].Value;
                        if (match.Groups[Rx.RemoveCommandId].Success)
                        {
                            if (DynamicCodeManager.HasMethod(tempMethodName))
                            {
                                DynamicCodeManager.RemoveMethod(tempMethodName);
                                Console.WriteLine("Method " + tempMethodName + " removed.");
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.WriteLine("Method " + tempMethodName + " does not exist.");
                                Console.WriteLine();
                            }
                        }
                        else
                        {
                            if (DynamicCodeManager.HasMethod(tempMethodName))
                            {
                                Console.WriteLine("Method " + tempMethodName + " already exists.");
                                Console.WriteLine();
                                methodName = null;
                            }
                            else
                            {
                                methodName = tempMethodName;
                                if (match.Groups[Rx.ParamsId].Success)
                                    parameters = match.Groups[Rx.ParamsId].Value;
                                if (match.Groups[Rx.ReturnTypeId].Success)
                                    returnType = match.Groups[Rx.ReturnTypeId].Value;
                                defineMode = true;
                            }
                        }
                        continue;
                    }
                }

                try
                {
                    DynamicCodeManager.AddMethod(methodName, src, parameters, returnType);
                }
                catch (CompilerException ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine();
                    continue;
                }

                if (!DynamicCodeManager.Ready) continue;

                if (defineMode)
                {
                    Console.WriteLine("Method " + methodName + " defined.");
                    Console.WriteLine();
                }
                else
                {
                    var returnVal = DynamicCodeManager.InvokeMethod(methodName);
                    if (!defineMode)
                        DynamicCodeManager.RemoveMethod(methodName);

                    //Console.WriteLine();
                    Console.WriteLine("Output:");
                    Console.WriteLine((returnVal ?? "null").ToString());
                    Console.WriteLine();
                }

                CommandId++;
                methodName = null;
                returnType = null;
                parameters = null;
                defineMode = false;
            }
        }
    }
}
