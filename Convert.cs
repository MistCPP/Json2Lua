using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

class Convert
{
    private string _targetDicName = string.Empty;
    private string _tableName = string.Empty;
    private bool _isStartOut;
    private readonly List<string> _luaBuilder = new();

    private void ReadFile(string inPath)
    {
        _isStartOut = true;
        Working(inPath);
    }

    private void ReadFile(string inPath ,string targetTable)
    {
        _isStartOut = false;
        _targetDicName = targetTable;
        Working(inPath);
    }

    private void Working(string fileName)
    {
        _luaBuilder.Clear();
        _tableName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "");

        var fileContent = File.ReadAllText(fileName);
        var jsonContent = JObject.Parse(fileContent);

        _luaBuilder.Add($"Table.Levels.{_tableName} = {{");
        TraverseJToken(jsonContent, 0 , _isStartOut);
        _luaBuilder.Add("}");

        //=> just once
        for (int i = 1; i < _luaBuilder.Count; i++)
        {
            var line = _luaBuilder[i];
            if (line.EndsWith("},"))
            {
                var prevLine = _luaBuilder[i - 1];
                if (prevLine.EndsWith(" = {"))
                {
                    _luaBuilder[i - 1] = prevLine.Replace(" = {", " = nil,");
                    _luaBuilder.RemoveAt(i--);
                }
            }
        }
    }

    private void Print()
    {
        Console.WriteLine(string.Join(Environment.NewLine, _luaBuilder));
    }

    private void Save2File(string outPath)
    {
        var encoding = new UTF8Encoding(false);
        File.WriteAllText(outPath, string.Join(Environment.NewLine, _luaBuilder), encoding);
    }

    private void Dispose()
    {
        _luaBuilder.Clear();
    }

    private void TraverseJToken(JToken token, int indent,bool needPrint)
    {
        var intentStr = new string('\t', indent);
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                var currPropertyNeedPrint = needPrint || property.Name == _targetDicName;
                var tryParseResult = TryConvertToBaseTypeLuaStr(property.Value);
                if (string.IsNullOrEmpty(tryParseResult))
                {
                    if (needPrint)
                    {
                        _luaBuilder.Add($"{intentStr}{FormatPropertyName(property.Name)} = {{");
                    }
                    TraverseJToken(property.Value, indent + (currPropertyNeedPrint ? 1 : 0), currPropertyNeedPrint);
                    if (needPrint)
                    {
                        _luaBuilder.Add($"{intentStr}}},");
                    }
                }
                else if (needPrint)
                {
                    _luaBuilder.Add($"{intentStr}{FormatPropertyName(property.Name)} = {tryParseResult},");   
                }
            }
        }
        else if (token is JArray array)
        {
            foreach (var item in array)
            {
                var tryParseResult = TryConvertToBaseTypeLuaStr(item);
                if (!string.IsNullOrEmpty(tryParseResult))
                {
                    if (needPrint)
                    {
                        _luaBuilder.Add($"{intentStr}{tryParseResult},");
                    }
                }
                else
                {
                    if (needPrint)
                    {
                        _luaBuilder.Add($"{intentStr}{{");
                    }
                    TraverseJToken(item, indent + (needPrint ? 1 : 0), needPrint);
                    if (needPrint)
                    {
                        _luaBuilder.Add($"{intentStr}}},");
                    }
                }
            }
        }
    }

    private string FormatPropertyName(string strName)
    {
        return strName.StartsWith("_") ? strName[1..] : strName;
    }

    private string TryConvertToBaseTypeLuaStr(JToken token)
    {
        var jType = token.Type;
        switch (jType)
        {
            case JTokenType.None:
            case JTokenType.Object:
            case JTokenType.Array:
            case JTokenType.Constructor:
            case JTokenType.Property:
            case JTokenType.Comment:
            case JTokenType.Null:
            case JTokenType.Undefined:
            case JTokenType.Date:
            case JTokenType.Raw:
            case JTokenType.Bytes:
            case JTokenType.Guid:
            case JTokenType.Uri:
            case JTokenType.TimeSpan:
                return string.Empty;
            case JTokenType.Integer:
            case JTokenType.Float:
                return token.ToString();
            case JTokenType.String:
                return $"\"{token}\"";
            case JTokenType.Boolean:
                return token.ToString().ToLowerInvariant();
            default:
                return string.Empty;
        }
    }

    static void Main(string[] args)
    {
        if (args.Length == 2)
        {
            var inPath = args[0].Replace("\"", "");
            var targetTable = args[1];

            if (Directory.Exists(inPath))
            {
                var converter = new Convert();
                var dirName = $"{AppContext.BaseDirectory}\\TableOut\\";
                if (Directory.Exists(dirName))
                {
                    Directory.Delete(dirName,true);
                }
                Directory.CreateDirectory(dirName);

                foreach (var fileName in Directory.GetFiles(inPath))
                {
                    var outPath = $"{dirName}{Path.GetFileNameWithoutExtension(fileName).Replace(" ", "")}.lua.txt";
                    converter.ReadFile(fileName, targetTable);
                    converter.Save2File(outPath);
                    converter.Dispose();
                }
            }
            else
            {
                var converter = new Convert();
                converter.ReadFile(inPath, targetTable);
                converter.Print();
                converter.Dispose();
            }
        }
        else
        {
            string? inPath;
            while (!string.IsNullOrWhiteSpace(inPath = Console.ReadLine()))
            {
                inPath = inPath.Replace("\"", "");

                if (File.Exists(inPath))
                {
                    var converter = new Convert();
                    converter.ReadFile(inPath);
                    converter.Print();
                    converter.Dispose();
                }
                else
                {
                    Console.WriteLine($"File not found: {inPath}");
                }
            }
        }
    }
}
