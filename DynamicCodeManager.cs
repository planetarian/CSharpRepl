using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;

namespace ConsoleTest
{
    public static class DynamicCodeManager
    {

        #region internal statics and constants

        private static readonly string nl = Environment.NewLine;

        private static readonly Dictionary<string, string> conditionSnippet = new Dictionary<string, string>();
        private static readonly Dictionary<string, MethodDefinition> methodSnippet = new Dictionary<string, MethodDefinition>();
        private static readonly Dictionary<string, PropertyDefinition> propSnippet = new Dictionary<string, PropertyDefinition>();
        private const string dynamicConditionPrefix = "__dm_";
        private static readonly string myNamespace = typeof (DynamicCodeManager).Namespace;

        private static readonly List<string> references =
            new List<string>
                {
                    "System.dll",
                    "System.Core.dll",
                    "System.Data.dll",
                    "System.Xml.dll",
                    "mscorlib.dll",
                    "System.Windows.Forms.dll",
                    //"BrussFx.dll",
                    "EntityFramework.dll",
                    "EntityFramework.SqlServer.dll",
                    //"pIRC.dll"
                };

        private static readonly string header =
            "using System;" + nl +
            "using System.CodeDom.Compiler;" + nl +
            "using System.Collections.Generic;" + nl +
            "using System.Data;" + nl +
            "using System.IO;" + nl +
            "using System.Linq;" + nl +
            "using System.Text;" + nl +
            "using System.Reflection;" + nl +
            "using System.Security.Cryptography;" + nl +
            "using Microsoft.CSharp;" + nl +
            //"using BrussFx;" + nl +
            //"using BrussFx.Helpers;" + nl +
            //"using BrussFx.ModelBarcode;" + nl +
            //"using BrussFx.ModelHr;" + nl +
            //"using BrussFx.ModelMaint;" + nl +
            //"using BrussFx.ModelScrap;" + nl +
            //"using pIRC;" + nl +
            "namespace " + myNamespace + nl +
            "{" + nl +
            "  public class Dynamic : DynamicBase" + nl +
            "  {" + nl;

        private static readonly string methodDef
            = "    private Dictionary<string,object> vars { get { return Program.State; } }" + nl
            //+ "    private pIRC.Messaging.Messenger messenger { get { return pIRC.Messaging.Messenger.Default; } }" + nl
            ;

        private static readonly string propertyTemplate
            = "    public {0} {1} {2}" + nl;

        private static readonly string conditionTemplate
            = "    bool {0}{1}(params object[] p) {{ return {2}; }}" + nl;

        private static readonly string methodTemplate
            = "    {0} {1}({2}) {{"
            + "{3}" + nl
            + "    }}" + nl;

        private static readonly string codeEnd
            = "  }" + nl
            + "}";

        public static bool Ready { get; private set; }
        public static string CurrentMethodName { get; private set; }
        public static string Source { get; private set; }


        #endregion

        public static Assembly Assembly { get; private set; }

        static DynamicCodeManager()
        {
            Ready = true;
        }

        #region manage snippets

        public static void Clear()
        {
            methodSnippet.Clear();
            conditionSnippet.Clear();
            Assembly = null;
        }

        public static void Clear(string name)
        {
            if (conditionSnippet.ContainsKey(name))
            {
                Assembly = null;
                conditionSnippet.Remove(name);
            }
            else if (methodSnippet.ContainsKey(name))
            {
                Assembly = null;
                methodSnippet.Remove(name);
            }
        }

        public static void AddCondition(string conditionName, string booleanExpression)
        {
            if (conditionSnippet.ContainsKey(conditionName))
                throw new InvalidOperationException(string.Format("There is already a condition called '{0}'",
                                                                  conditionName));
            var src = new StringBuilder(header);
            // TODO: defines
            src.Append(methodDef);
            src.AppendFormat(conditionTemplate, dynamicConditionPrefix, conditionName, booleanExpression);
            src.Append(codeEnd);
            Compile(src.ToString()); //if the condition is invalid an exception will occur here 
            conditionSnippet[conditionName] = booleanExpression;
            Assembly = null;
        }

        public static void AddMethod(string methodName, string methodSource, string parameters = null, string returnType = null)
        {
            if (CurrentMethodName != null && CurrentMethodName != methodName)
                throw new InvalidOperationException("A method is already under construction.");
            Assembly = null;
            CurrentMethodName = methodName;
            
            Source = (Source ?? "") + nl + "      " + methodSource;

            if (methodSnippet.ContainsKey(methodName))
                throw new InvalidOperationException(string.Format("There is already a method called '{0}'", methodName));
            if (methodName.StartsWith(dynamicConditionPrefix))
                throw new InvalidOperationException(
                    string.Format(
                        "'{0}' is not a valid method name because the '{1}' prefix is reserved for internal use with conditions",
                        methodName, dynamicConditionPrefix));

            methodSnippet[methodName] = new MethodDefinition(Source, parameters, returnType);
            try
            {
                Compile();
            }
            catch (CompilerException)
            {
                methodSnippet.Remove(methodName);
                if (Source.Lines().Length <= 1) // method will be empty
                    CancelMethod();
                throw;
            }

            if (Assembly == null)
            {
                methodSnippet.Remove(methodName);
                Ready = false;
                return;
            }

            CancelMethod();
            //Assembly = null;

            /*
            var src = new StringBuilder(codeStart);
            src.AppendFormat(methodTemplate, methodName, Source);
            src.Append(codeEnd);
            Trace.TraceError("SOURCE"+nl+"{0}", src);
            Assembly assy = Compile(src.ToString()); //if the condition is invalid an exception will occur here
            if (Assembly == null)
            {
                Ready = false;
                return;
            }

            Ready = true;
            methodSnippet[methodName] = Source;
            Source = "";
            Assembly = null;
            */
        }

        public static void AddProperty(string propertyName, string propertySource, string returnType)
        {
            if (propSnippet.ContainsKey(propertyName))
                throw new InvalidOperationException(String.Format("There is already a property called '{0}'", propertyName));
            propSnippet[propertyName] = new PropertyDefinition(propertySource, returnType);
            try
            {
                Compile();
            }
            catch (CompilerException)
            {
                propSnippet.Remove(propertyName);
            }
        }

        public static void CancelMethod()
        {
            Ready = true;
            CurrentMethodName = null;
            Source = "";
        }

        public static bool HasMethod(string methodName)
        {
            return methodSnippet != null && methodSnippet.ContainsKey(methodName);
        }

        public static bool HasProperty(string propertyName)
        {
            return propSnippet != null && propSnippet.ContainsKey(propertyName);
        }

        public static void RemoveMethod(string methodName)
        {
            if (!HasMethod(methodName))
                throw new InvalidOperationException("Method " + methodName + " is not defined.");
            methodSnippet.Remove(methodName);
        }

        #endregion

        #region use snippets

        public static object InvokeMethod(string methodName, params object[] p)
        {
            DynamicBase dynamicMethod = null;
            if (Assembly == null)
                //{
                Compile();
            var instance = Assembly.CreateInstance(myNamespace + ".Dynamic");
            dynamicMethod = instance as DynamicBase;
            //}
            try
            {
                return dynamicMethod.InvokeMethod(methodName, p);
            }
            catch (TargetInvocationException ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Runtime Error[s]:");
                sb.AppendLine();
                Exception innerEx = ex;
                while ((innerEx = innerEx.InnerException) != null)
                {
                    sb.Append("  ");
                    sb.AppendLine(innerEx.Message);
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }

        public static bool Evaluate(string conditionName, params object[] p)
        {
            DynamicBase dynamicCondition = null;
            if (Assembly == null)
            {
                Compile();
                dynamicCondition = Assembly.CreateInstance(myNamespace + ".Dynamic") as DynamicBase;
            }
            return dynamicCondition.EvaluateCondition(conditionName, p);
        }

        public static double Transform(string functionName, params object[] p)
        {
            DynamicBase dynamicCondition = null;
            if (Assembly == null)
            {
                Compile();
                dynamicCondition = Assembly.CreateInstance(myNamespace + ".Dynamic") as DynamicBase;
            }
            return dynamicCondition.Transform(functionName, p);
        }

        #endregion

        #region support routines

        public static string ProduceConditionName(Guid conditionId)
        {
            var cn = new StringBuilder();
            foreach (char c in conditionId.ToString().Where(char.IsLetterOrDigit))
                cn.Append(c);
            //string conditionName = cn.ToString();
            return string.Format("_dm_{0}", cn);
        }

        private static void Compile()
        {
            if (Assembly != null) return;

            var src = new StringBuilder(header);
            foreach (KeyValuePair<string, PropertyDefinition> kvp in propSnippet)
                src.AppendFormat(propertyTemplate, kvp.Value.ReturnType, kvp.Key, kvp.Value.Source);
            src.Append(methodDef);
            foreach (KeyValuePair<string, string> kvp in conditionSnippet)
                src.AppendFormat(conditionTemplate, dynamicConditionPrefix, kvp.Key, kvp.Value);
            foreach (KeyValuePair<string, MethodDefinition> kvp in methodSnippet)
                src.AppendFormat(methodTemplate, kvp.Value.ReturnType, kvp.Key, kvp.Value.Parameters, kvp.Value.Source);
            src.Append(codeEnd);
            Trace.TraceError("SOURCE\r\n{0}", src);
            Assembly = Compile(src.ToString());
        }

        private static Assembly Compile(string sourceCode)
        {
            var cp = new CompilerParameters();
            cp.ReferencedAssemblies.AddRange(references.ToArray());
            cp.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);
            cp.CompilerOptions = "/target:library /optimize";
            cp.GenerateExecutable = false;
            cp.GenerateInMemory = true;
            CompilerResults cr = (new CSharpCodeProvider()).CompileAssemblyFromSource(cp, sourceCode);
            if (cr.Errors.Count > 0)
            {
                var errList = cr.Errors.Cast<CompilerError>().Where(error => !error.IsWarning).ToList();
                if (errList.Count == 1 && errList[0].ErrorNumber == "CS0161")
                    return null;
                if (errList.Count > 0)
                {
                    //Source = "";
                    Source = Source.RemoveLine(Source.Lines().Length - 1);
                    throw new CompilerException(cr.Errors, sourceCode);
                }
            }
            return cr.CompiledAssembly;
        }

        #endregion

        public static bool HasItem(string methodName)
        {
            return conditionSnippet.ContainsKey(methodName) || methodSnippet.ContainsKey(methodName);
        }
    }

    public class MethodDefinition
    {
        private const string defaultParams = "params object[] p";

        public string ReturnType { get; private set; }
        public string Parameters { get; private set; }
        public string Source { get; private set; }

        public MethodDefinition (string source, string parameters = null, string returnType = null)
        {
            Source = source;
            Parameters = parameters ?? defaultParams;
            ReturnType = returnType ?? "object";
        }
    }

    public class PropertyDefinition
    {
        public string ReturnType { get; private set; }
        public string Source { get; private set; }

        public PropertyDefinition(string source, string returnType)
        {
            Source = source;
            ReturnType = returnType;
        }
    }

    public class CompilerException : Exception
    {
        public CompilerErrorCollection Errors { get; private set; }

        public CompilerException(CompilerErrorCollection errors, string source)
            : base(FormatMessage(errors, source))
        {
            Errors = errors;
        }

        private static string FormatMessage(CompilerErrorCollection errors, string source)
        {
            var sb = new StringBuilder();
            //sb.AppendLine();
            sb.AppendLine("Compiler Error[s]:");
            sb.AppendLine();

            foreach (CompilerError error in errors)
            {
                sb.AppendLine(source.GetLine(error.Line - 1));
                sb.Append("  Col ");
                sb.Append(error.Column);
                sb.Append(", ");
                sb.Append("Error ");
                sb.Append(error.ErrorNumber);
                sb.Append(": ");
                sb.AppendLine(error.ErrorText);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    internal static class Extensions
    {
        public static string[] Lines(this string str)
        {
            return str.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        }

        public static string GetLine(this string str, int line)
        {
            return str.Lines().Length > line ? str.Lines()[line] : null;
        }

        public static string RemoveLine(this string str, int line)
        {
            var lines = new List<string>();
            for (int i = 0; i < str.Lines().Length; i++)
            {
                if (i != line)
                    lines.Add(str.Lines()[i]);
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
