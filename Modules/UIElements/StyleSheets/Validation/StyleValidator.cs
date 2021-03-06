// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine.UIElements.StyleSheets.Syntax;

namespace UnityEngine.UIElements.StyleSheets
{
    internal enum StyleValidationStatus
    {
        Ok,
        Error,
        Warning
    }

    internal struct StyleValidationResult
    {
        public StyleValidationStatus status;
        public string message;
        public string errorValue;
        public string hint;
        public bool success { get { return status == StyleValidationStatus.Ok; } }
    }

    internal class StyleValidator
    {
        internal const string kDefaultPropertiesPath = "StyleSheets/UIElements-properties.json";

        private StylePropertyInfoCache m_StylePropertyInfoCache;
        private StyleSyntaxParser m_SyntaxParser;
        private StyleMatcher m_StyleMatcher;

        public StyleValidator()
        {
            m_StylePropertyInfoCache = new StylePropertyInfoCache();
            m_SyntaxParser = new StyleSyntaxParser(m_StylePropertyInfoCache);
            m_StyleMatcher = new StyleMatcher();
        }

        public void LoadPropertiesDefinition(string json)
        {
            m_StylePropertyInfoCache.LoadJson(json);
        }

        public StyleValidationResult ValidateProperty(string name, string value)
        {
            var result = new StyleValidationResult() {status = StyleValidationStatus.Ok};

            // Bypass custom styles
            if (name.StartsWith("--"))
                return result;

            StylePropertyInfo propertyInfo;
            if (!m_StylePropertyInfoCache.TryGet(name, out propertyInfo))
            {
                string closestName = m_StylePropertyInfoCache.FindClosestPropertyName(name);
                result.status = StyleValidationStatus.Error;
                result.message = $"Unknown property '{name}'";
                if (!string.IsNullOrEmpty(closestName))
                    result.message = $"{result.message} (did you mean '{closestName}'?)";

                return result;
            }

            var syntaxTree = m_SyntaxParser.Parse(propertyInfo.syntax);
            if (syntaxTree == null)
            {
                result.status = StyleValidationStatus.Error;
                result.message = $"Invalid '{name}' property syntax '{propertyInfo.syntax}'";
                return result;
            }

            var matchResult = m_StyleMatcher.Match(syntaxTree, value);
            if (!matchResult.success)
            {
                result.errorValue = matchResult.errorValue;
                switch (matchResult.errorCode)
                {
                    case MatchResultErrorCode.Syntax:
                        result.status = StyleValidationStatus.Error;
                        if (IsUnitMissing(propertyInfo.syntax, value))
                            result.hint = "Property expects a unit. Did you forget to add px or %?";
                        else if (IsUnsupportedColor(propertyInfo.syntax))
                            result.hint = $"Unsupported color '{value}'.";
                        result.message = $"Expected ({propertyInfo.syntax}) but found '{matchResult.errorValue}'";
                        break;
                    case MatchResultErrorCode.EmptyValue:
                        result.status = StyleValidationStatus.Error;
                        result.message = $"Expected ({propertyInfo.syntax}) but found empty value";
                        break;
                    case MatchResultErrorCode.ExpectedEndOfValue:
                        result.status = StyleValidationStatus.Warning;
                        result.message = $"Expected end of value but found '{matchResult.errorValue}'";
                        break;
                    default:
                        Debug.LogAssertion($"Unexpected error code '{matchResult.errorCode}'");
                        break;
                }
            }

            return result;
        }

        // A simple check to give a better error message when a unit is missing
        private bool IsUnitMissing(string propertySyntax, string propertyValue)
        {
            float val;
            return float.TryParse(propertyValue, out val) && (propertySyntax.Contains("<length>") || propertySyntax.Contains("<percentage>"));
        }

        private bool IsUnsupportedColor(string propertySyntax)
        {
            return propertySyntax.StartsWith("<color>");
        }
    }
}
