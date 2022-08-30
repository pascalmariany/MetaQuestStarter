using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Creates UIToolkit elements from a rich text.
    /// </summary>
    public static class RichTextParser
    {
        // Tries to parse text to XDocument word by word - outputs the longest successful string before failing
        static string ShowContentWithError(string errorContent)
        {
            string longestString = "";
            string previousLongestString = "";
            string[] lines = errorContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                string[] words = line.Split(new[] { " " }, StringSplitOptions.None);
                foreach (string word in words)
                {
                    longestString += word + " ";
                    try
                    {
                        XDocument.Parse("<content>" + longestString + "</content>");
                    }
                    catch
                    {
                        continue;
                    }
                    previousLongestString = longestString;
                }
                longestString += "\r\n";
            }
            return previousLongestString;
        }

        /// <summary>
        /// Preprocess rich text - add space around tags.
        /// </summary>
        /// <param name="inputText">Text with tags</param>
        /// <returns>Text with space around tags</returns>
        static string PreProcessRichText(string inputText)
        {
            string processed = inputText;
            processed = processed.Replace("<b>", " <b>");
            processed = processed.Replace("<i>", " <i>");
            processed = processed.Replace("<a ", " <a ");
            processed = processed.Replace("<style ", " <style ");
            processed = processed.Replace("<br", " <br");
            processed = processed.Replace("<wordwrap>", " <wordwrap>");

            processed = processed.Replace("</b>", "</b> ");
            processed = processed.Replace("</i>", "</i> ");
            processed = processed.Replace("</a>", "</a> ");
            processed = processed.Replace("</style>", "</style> ");
            processed = processed.Replace("</wordwrap>", " </wordwrap> ");

            return processed;
        }

        /// <summary>
        /// Helper function to detect if the string contains any characters in
        /// the Unicode range reserved for Chinese, Japanese and Korean characters.
        /// </summary>
        /// <param name="textLine">String to check for CJK letters.</param>
        /// <returns>True if it contains Chinese, Japanese or Korean characters.</returns>
        static bool NeedSymbolWrapping(string textLine)
        {
            // Unicode range for CJK letters.
            // Range chosen from StackOverflow: https://stackoverflow.com/a/42411925
            // Validated from sources:
            // https://www.unicode.org/faq/han_cjk.html
            // https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_(Unicode_block)
            return textLine.Any(c => (uint)c >= 0x4E00 && (uint)c <= 0x2FA1F);
        }

        /// <summary>
        /// Adds a new wrapping word Label to the target visualElement. Type can be BoldLabel, ItalicLabel or Label
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="textToAdd">The text inside the word label.</param>
        /// <param name="elementList">Redundant storage, mostly used for automated testing.</param>
        /// <param name="addToVisualElement">Parent container for the word Label.</param>
        static Label AddLabel<T>(string textToAdd, List<VisualElement> elementList, VisualElement addToVisualElement)
            where T : Label
        {
            Label wordLabel = null;
            Type LabelType = typeof(T);
            if (LabelType == typeof(ItalicLabel))
            {
                wordLabel = new ItalicLabel(textToAdd);
            }
            else if (LabelType == typeof(BoldLabel))
            {
                wordLabel = new BoldLabel(textToAdd);
            }
            else if (LabelType == typeof(TextLabel))
            {
                wordLabel = new TextLabel(textToAdd);
            }
            if (wordLabel == null)
            {
                Debug.LogError("Error: Unsupported Label type used. Use TextLabel, BoldLabel or ItalicLabel.");
                return null;
            }
            elementList.Add(wordLabel);
            addToVisualElement.Add(wordLabel);
            return wordLabel;
        }

        /// <summary>
        /// Transforms HTML tags to word element labels with different styles to enable rich text.
        /// </summary>
        /// <param name="htmlText"></param>
        /// <param name="targetContainer">
        /// The following need to set for the container's style:
        /// flex-direction: row;
        /// flex-wrap: wrap;
        /// </param>
        /// <returns>List of VisualElements made from the parsed text.</returns>
        public static List<VisualElement> RichTextToVisualElements(string htmlText, VisualElement targetContainer)
        {
            bool addError = false;
            string errorText = "";

            try
            {
                XDocument.Parse("<content>" + htmlText + "</content>");
            }
            catch (Exception e)
            {
                targetContainer.Clear();
                errorText = e.Message;
                htmlText = ShowContentWithError(htmlText);
                addError = true;
            }
            List<VisualElement> elements = new List<VisualElement>();

            targetContainer.Clear();
            bool boldOn = false; // <b> sets this on </b> sets off
            bool italicOn = false; // <i> </i>
            bool forceWordWrap = false;
            bool linkOn = false;
            string linkURL = "";
            string styleClass = "";
            bool addStyle = false;
            bool firstLine = true;
            bool lastLineHadText = false;
            // start streaming text per word to elements while retaining current style for each word block
            string[] lines = htmlText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                // Check if the line begins with whitespace and turn that into corresponding Label
                string initialWhiteSpaces = "";
                foreach (char singleCharacter in line)
                {
                    if (singleCharacter == ' ' || singleCharacter == '\t')
                    {
                        initialWhiteSpaces += singleCharacter;
                    }
                    else
                    {
                        break;
                    }
                }

                string processedLine = PreProcessRichText(line);

                // Separate the line into words
                string[] words = processedLine.Split(new[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);

                if (!lastLineHadText)
                {
                    if (!firstLine)
                        elements.Add(AddParagraphToElement(targetContainer));

                    if (initialWhiteSpaces.Length > 0)
                    {
                        WhiteSpaceLabel indentationLabel = new WhiteSpaceLabel(initialWhiteSpaces);
                        targetContainer.Add(indentationLabel);
                        elements.Add(indentationLabel);
                    }
                }

                if (!firstLine && lastLineHadText)
                {
                    elements.Add(AddLinebreakToElement(targetContainer));
                    lastLineHadText = false;
                    if (initialWhiteSpaces.Length > 0)
                    {
                        WhiteSpaceLabel indentationLabel = new WhiteSpaceLabel(initialWhiteSpaces);
                        targetContainer.Add(indentationLabel);
                        elements.Add(indentationLabel);
                    }
                }

                foreach (string word in words)
                {
                    // Wrap every character instead of word in case of Chinese and Japanese
                    // Note: override with <wordwrap>Force word wrapping here</wordwrap>

                    if (word == "" || word == " " || word == "   ") continue;
                    lastLineHadText = true;
                    string strippedWord = word;
                    bool removeBold = false;
                    bool removeItalic = false;
                    bool addParagraph = false;
                    bool removeLink = false;
                    bool removeWordWrap = false;

                    bool removeStyle = false;

                    strippedWord = strippedWord.Trim();

                    if (strippedWord.Contains("<b>"))
                    {
                        strippedWord = strippedWord.Replace("<b>", "");
                        boldOn = true;
                    }
                    if (strippedWord.Contains("<i>"))
                    {
                        strippedWord = strippedWord.Replace("<i>", "");
                        italicOn = true;
                    }
                    if (strippedWord.Contains("<wordwrap>"))
                    {
                        strippedWord = strippedWord.Replace("<wordwrap>", "");
                        forceWordWrap = true;
                    }
                    if (strippedWord.Contains("<a"))
                    {
                        strippedWord = strippedWord.Replace("<a", "");
                        linkOn = true;
                    }
                    if (strippedWord.Contains("<style"))
                    {
                        strippedWord = strippedWord.Replace("<style", "");
                        addStyle = true;
                    }

                    bool wrapCharacters = !forceWordWrap && NeedSymbolWrapping(word);
                    if (linkOn && strippedWord.Contains("href="))
                    {
                        strippedWord = strippedWord.Replace("href=", "");
                        int linkFrom = strippedWord.IndexOf("\"", StringComparison.Ordinal) + 1;
                        int linkTo = strippedWord.LastIndexOf("\"", StringComparison.Ordinal);
                        linkURL = strippedWord.Substring(linkFrom, linkTo - linkFrom);
                        strippedWord = strippedWord.Substring(linkTo + 2, (strippedWord.Length - 2) - linkTo);
                        strippedWord.Replace("\">", "");
                    }

                    if (addStyle && strippedWord.Contains("class="))
                    {
                        strippedWord = strippedWord.Replace("class=", "");
                        int styleFrom = strippedWord.IndexOf("\"", StringComparison.Ordinal) + 1;
                        int styleTo = strippedWord.LastIndexOf("\"", StringComparison.Ordinal);
                        styleClass = strippedWord.Substring(styleFrom, styleTo - styleFrom);
                        strippedWord = strippedWord.Substring(styleTo + 2, (strippedWord.Length - 2) - styleTo);
                        strippedWord.Replace("\">", "");
                    }

                    if (strippedWord.Contains("&"))
                    {
                        strippedWord = WebUtility.HtmlDecode(strippedWord);
                    }

                    if (strippedWord.Contains("</a>"))
                    {
                        strippedWord = strippedWord.Replace("</a>", "");
                        removeLink = true;
                    }
                    if (strippedWord.Contains("</style>"))
                    {
                        strippedWord = strippedWord.Replace("</style>", "");
                        removeStyle = true;
                    }
                    if (strippedWord.Contains("<br/>"))
                    {
                        strippedWord = strippedWord.Replace("<br/>", "");
                        addParagraph = true;
                    }
                    if (strippedWord.Contains("</b>"))
                    {
                        strippedWord = strippedWord.Replace("</b>", "");
                        removeBold = true;
                    }
                    if (strippedWord.Contains("</i>"))
                    {
                        strippedWord = strippedWord.Replace("</i>", "");
                        removeItalic = true;
                    }
                    if (strippedWord.Contains("</wordwrap>"))
                    {
                        strippedWord = strippedWord.Replace("</wordwrap>", "");
                        removeWordWrap = true;
                    }
                    if (boldOn && strippedWord != "")
                    {
                        if (wrapCharacters)
                        {
                            foreach (char character in strippedWord)
                            {
                                AddLabel<BoldLabel>(character.ToString(), elements, targetContainer);
                            }
                        }
                        else
                        {
                            AddLabel<BoldLabel>(strippedWord, elements, targetContainer);
                        }
                    }
                    else if (italicOn && strippedWord != "")
                    {
                        if (wrapCharacters)
                        {
                            foreach (char character in strippedWord)
                            {
                                AddLabel<ItalicLabel>(character.ToString(), elements, targetContainer);
                            }
                        }
                        else
                        {
                            AddLabel<ItalicLabel>(strippedWord, elements, targetContainer);
                        }
                    }
                    else if (addParagraph)
                    {
                        elements.Add(AddParagraphToElement(targetContainer));
                    }
                    else if (linkOn && !string.IsNullOrEmpty(linkURL))
                    {
                        var label = new HyperlinkLabel
                        {
                            text = strippedWord,
                            tooltip = linkURL
                        };

                        EventCallback<MouseUpEvent, string> linkCallback = null;
                        string actualPath = "";

                        // Link points to a relative directory
                        if (linkURL[0] == '.')
                        {
                            actualPath = Path.GetFullPath(linkURL);
                            actualPath = Uri.EscapeUriString(actualPath);
                            actualPath = "file://" + actualPath;
                            linkCallback = (evt, path) =>
                            {
                                Application.OpenURL(path);
                            };
                        }
                        else
                        {
                            actualPath = linkURL;
                            linkCallback = (evt, path) =>
                            {
                                TutorialEditorUtils.OpenUrl(path);
                            };
                        }
                        label.RegisterCallback<MouseUpEvent, string>(linkCallback, actualPath);
                        targetContainer.Add(label);
                        elements.Add(label);
                    }
                    else
                    {
                        if (strippedWord != "")
                        {
                            if (wrapCharacters)
                            {
                                foreach (char character in strippedWord)
                                {
                                    Label newLabel = AddLabel<TextLabel>(character.ToString(), elements, targetContainer);
                                    if (addStyle && !string.IsNullOrEmpty(styleClass))
                                    {
                                        newLabel.AddToClassList(styleClass);
                                    }
                                }
                            }
                            else
                            {
                                Label newLabel = AddLabel<TextLabel>(strippedWord, elements, targetContainer);
                                if (addStyle && !string.IsNullOrEmpty(styleClass))
                                {
                                    newLabel.AddToClassList(styleClass);
                                }
                            }
                        }
                    }

                    if (removeBold) boldOn = false;
                    if (removeItalic) italicOn = false;
                    if (removeLink)
                    {
                        linkOn = false;
                        linkURL = "";
                    }
                    if (removeStyle) addStyle = false;
                    if (removeWordWrap) forceWordWrap = false;
                }
                firstLine = false;
            }

            if (addError)
            {
                var label = new ParseErrorLabel()
                {
                    text = Localization.Tr("PARSE ERROR"),
                    tooltip = Localization.Tr("Click here to see more information in the console.")
                };
                label.RegisterCallback<MouseUpEvent>((e) => Debug.LogError(errorText));
                targetContainer.Add(label);
                elements.Add(label);
            }
            return elements;
        }

        static VisualElement AddLinebreakToElement(VisualElement elementTo)
        {
            Label wordLabel = new Label(" ");
            wordLabel.style.flexDirection = FlexDirection.Row;
            wordLabel.style.flexGrow = 1f;
            wordLabel.style.width = 3000f;
            wordLabel.style.height = 0f;
            elementTo.Add(wordLabel);
            return wordLabel;
        }

        static VisualElement AddParagraphToElement(VisualElement elementTo)
        {
            Label wordLabel = new Label(" ");
            wordLabel.style.flexDirection = FlexDirection.Row;
            wordLabel.style.flexGrow = 1f;
            wordLabel.style.width = 3000f;
            elementTo.Add(wordLabel);
            return wordLabel;
        }

        // Dummy classes so that we can customize the styles from a USS file.

        /// <summary>
        /// Label for the red parser error displayed where the parsing fails
        /// </summary>
        public class ParseErrorLabel : Label {}

        /// <summary>
        /// Text label for links
        /// </summary>
        public class HyperlinkLabel : Label {}

        /// <summary>
        /// Text label for text that wraps per word
        /// </summary>
        public class TextLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public TextLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label for white space used to indent lines
        /// </summary>
        public class WhiteSpaceLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public WhiteSpaceLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label with bold style
        /// </summary>
        public class BoldLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public BoldLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label with italic style
        /// </summary>
        public class ItalicLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public ItalicLabel(string text) : base(text)
            {
            }
        }
    }
}
