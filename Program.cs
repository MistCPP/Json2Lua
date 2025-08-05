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
        TraverseJToken(jsonContent, 0);
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

        Console.WriteLine(string.Join(Environment.NewLine,_luaBuilder));
        _luaBuilder.Clear();
    }

    private void TraverseJToken(JToken token, int indent)
    {
        var breakFlag = false;
        var intentStr = new string('\t', indent);
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                var isFirst = _isStartOut;
                _isStartOut = _isStartOut || property.Name == _targetDicName;
                breakFlag = breakFlag || (!isFirst && _isStartOut);

                var tryParseResult = TryConvertToBaseTypeLuaStr(property.Value);
                if (string.IsNullOrEmpty(tryParseResult))
                {
                    if (_isStartOut && !breakFlag)
                    {
                        _luaBuilder.Add($"{intentStr}{FormatPropertyName(property.Name)} = {{");
                    }
                    TraverseJToken(property.Value, indent + (_isStartOut ? 1 : 0));
                    if (_isStartOut && !breakFlag)
                    {
                        _luaBuilder.Add($"{intentStr}}},");
                    }
                }
                else if (_isStartOut && !breakFlag)
                {
                    _luaBuilder.Add($"{intentStr}{FormatPropertyName(property.Name)} = {tryParseResult},");   
                }
            }
        }
        else if (token is JArray array)
        {
            var idx = 0;

            foreach (var item in array)
            {
                if (_isStartOut && !breakFlag)
                {
                    _luaBuilder.Add($"{intentStr}{{");
                }
                TraverseJToken(item, indent + (_isStartOut ? 1 : 0));
                if (_isStartOut && !breakFlag)
                {
                    _luaBuilder.Add($"{intentStr}}},");
                }
                ++idx;
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
                Console.WriteLine("Directory");
            }
            else
            {
                var converter = new Convert();
                converter.ReadFile(inPath, targetTable);
            }
        }
        else
        {
            string? inPath;
            while (!string.IsNullOrWhiteSpace(inPath = Console.ReadLine()))
            {
                inPath = inPath.Replace("\"", "");

                if (Directory.Exists(inPath))
                {
                    Console.WriteLine("Directory");
                }
                else
                {
                    var converter = new Convert();
                    converter.ReadFile(inPath);
                }
            }
        }
    }
}
