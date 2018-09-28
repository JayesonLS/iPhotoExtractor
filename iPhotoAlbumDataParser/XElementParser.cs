/*
Based on https://github.com/asciamanna/iTunesLibraryParser/blob/master/ITunesLibraryParser/XElementParser.cs
which is distributed under the following license (additions are released to the public domain):

MIT License

Copyright(c) 2018 Anthony Sciamanna

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace iPhotoAlbumDataParser
{
    internal static class XElementParser
    {
        internal static XElement GetElementForKey(XElement element, string keyValue)
        {
            return (from keyNode in element.Elements("key")
                    where keyNode.Value == keyValue
                    select keyNode.NextNode as XElement).FirstOrDefault();
        }

        internal static bool ParseBoolean(XElement element, string keyValue)
        {
            return (from keyNode in element.Elements("key")
                    where keyNode.Value == keyValue
                    select (keyNode.NextNode as XElement).Name).FirstOrDefault() == "true";
        }

        internal static string ParseStringValue(XElement element, string keyValue)
        {
            return (from key in element.Elements("key")
                    where key.Value == keyValue
                    select (key.NextNode as XElement).Value).FirstOrDefault();
        }

        internal static long? ParseNullableLongValue(XElement xelement, string keyValue)
        {
            var stringValue = ParseStringValue(xelement, keyValue);
            return string.IsNullOrEmpty(stringValue) ? (long?)null : long.Parse(stringValue);
        }

        internal static int? ParseNullableIntValue(XElement xelement, string keyValue)
        {
            var stringValue = ParseStringValue(xelement, keyValue);
            return string.IsNullOrEmpty(stringValue) ? (int?)null : int.Parse(stringValue);
        }

        internal static double? ParseNullableDoubleValue(XElement xelement, string keyValue)
        {
            var stringValue = ParseStringValue(xelement, keyValue);
            return string.IsNullOrEmpty(stringValue) ? (double?)null : double.Parse(stringValue);
        }

        internal static DateTime? ParseNullableDateValue(XElement xelement, string keyValue)
        {
            var stringValue = ParseStringValue(xelement, keyValue);
            return string.IsNullOrEmpty(stringValue) ? (DateTime?)null : DateTime.SpecifyKind(DateTime.Parse(stringValue, CultureInfo.InvariantCulture), DateTimeKind.Utc).ToLocalTime();
        }

        internal static int ParseIntValue(XElement xelement, string keyValue)
        {
            var stringValue = ParseStringValue(xelement, keyValue);
            return int.Parse(stringValue);
        }

        internal static List<int> ParseIntArray(XElement xelement, string keyValue)
        {
            return GetElementForKey(xelement, keyValue).Descendants().Select(d => d.Value).Select(n => int.Parse(n)).ToList();
        }

        internal static Dictionary<int, T> ParseKeyDictPairs<T>(XElement containerXmlElement, Func<XElement, T> func)
        {
            Dictionary<int, T> result = new Dictionary<int, T>();

            var tmp = containerXmlElement.Elements("key").ToArray();

            foreach (XElement keyNode in containerXmlElement.Elements("key"))
            {
                int key = int.Parse(keyNode.Value);
                XElement valueNode = keyNode.NextNode as XElement;
                T value = func(valueNode);
                result[key] = value;
            }

            return result;
        }
    }
}