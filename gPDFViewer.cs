/* 
Author: Najmi (http://nunjimmimya.my/)
Heavyly inspired and copied from MediaElementPlayer
*/

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.UI.HtmlControls;
using BlogEngine.Core;
using BlogEngine.Core.Web.Controls;
using BlogEngine.Core.Web.Extensions;
using Page=System.Web.UI.Page;

/// <summary>
/// Insert PDF links in Preview (Embedded) Mode
/// </summary>
[Extension("Viewing PDF using Google Docs Hyperlinks (Beta)", "1.0", "<a href=\"http://www.nunjimmimya.my\">Nunjim Mimya</a>")]
public class gPDFViewer
{

    #region Private members
    private const string _extensionName = "gPDFViewer";
    private static readonly object _syncRoot = new object();
    static protected Dictionary<Guid, ExtensionSettings> _blogsSettings = new Dictionary<Guid, ExtensionSettings>();
    #endregion

    private const int _widthDefault = 600;
    private const int _heightDefault = 720;
	private const bool _enableAutoSize = false;
    private const string _folderDefault = "Media";

    /// <summary>
    /// Insert PDF embedded to the post
    /// </summary>
    static gPDFViewer()
    {
        Post.Serving += Publishable_Serving;
        BlogEngine.Core.Page.Serving += Publishable_Serving;
        InitSettings();
    }

    private static void InitSettings()
    {
        // call Settings getter so default settings are loaded on application start.
        var s = Settings;
    }

    private static ExtensionSettings Settings
    {
        get
        {
            Guid blogId = Blog.CurrentInstance.Id;
            if (!_blogsSettings.ContainsKey(blogId))
            {
                lock (_syncRoot)
                {
                    if (!_blogsSettings.ContainsKey(blogId))
                    {
                        _blogsSettings[blogId] = LoadExtensionSettingsForBlogInstance();
                    }
                }
            }
            return _blogsSettings[blogId];
        }
    }

    private static ExtensionSettings LoadExtensionSettingsForBlogInstance()
    {
        ExtensionSettings initialSettings = new ExtensionSettings(_extensionName);
        initialSettings.Help = @"
<ol>
	<li>Upload media files to your cloud storage</li>
	<li>Add short code to your media: [gviewer file=""yoururl/myfile.pdf""] for pdf or change the extension to other file that supported by Google Docs</li>
	<li>Customize with the following parameters:
		<ul>
			<li><b>width</b>: The exact width of the Document</li>
			<li><b>height</b>: The exact height of the Document</li>
		</ul>
	</li>
</ol>

<p>A complete example:<br />
[gviewer file=""yoururl/myfile.pdf"" width=""600"" height=""720""]
</p>
<h3>You must!</h3>
<ul>
 <li>Enable ""Use raw HTML Editor"" in write post to make sure complete [gviewer argument]</li>
</ul>
";
        initialSettings.IsScalar = true;

        initialSettings.AddParameter("width", "Default Width");
        initialSettings.AddValue("width", _widthDefault.ToString());

        initialSettings.AddParameter("height", "Default Height");
        initialSettings.AddValue("height", _heightDefault.ToString());

        return ExtensionManager.InitSettings(_extensionName, initialSettings);
    }

    private static void Publishable_Serving(object sender, ServingEventArgs e)
    {
        if (!ExtensionManager.ExtensionEnabled("gPDFViewer"))
            return;

        if (e.Location == ServingLocation.PostList || e.Location == ServingLocation.SinglePost || e.Location == ServingLocation.Feed || e.Location == ServingLocation.SinglePage) {
	
			HttpContext context = HttpContext.Current;			
	
			string regex = @"(gviewer)";
			List<ShortCode> shortCodes = GetShortCodes(e.Body, regex, true);
	
			if (shortCodes.Count == 0)
				return;
							
			ProcessMediaTags(e, shortCodes);
			
			// this won't happen on feeds
			if (context.CurrentHandler is Page) {
				Page page = (Page)context.CurrentHandler;
			}
		}
	}

    private static void ProcessMediaTags(ServingEventArgs e, List<ShortCode> shortCodes)
    {	
		// path to media
        string folder = Settings.GetSingleValue("folder");			
		string path = Utils.RelativeWebRoot + folder.TrimEnd(new char[] {'/'}) + "/";
		
		// override for feed
		if (e.Location == ServingLocation.Feed) {
			path = Utils.AbsoluteWebRoot + folder.TrimEnd(new char[] { '/' }) + "/";			
		}
					
		// do replacement for media
		foreach (ShortCode sc in shortCodes)
		{
            string tagName = sc.TagName;
            string key = sc.GetAttributeValue("key","");
            string w = sc.GetAttributeValue("width", "");
			string h = sc.GetAttributeValue("height", "");

            if (w == "")
			w = _widthDefault.ToString();
            if (h == "")
			h = _heightDefault.ToString();

			string code = "";
			switch(key)
            {
                case "file" : code = "<div align=\"center\"><iframe src=" + 
                                     "\"http://docs.google.com/viewer?url=" + Utils.AbsoluteWebRoot.ToString() + "/FILES/Media/" + sc.GetAttributeValue(key, "") + ".axdx&embedded=true\" " +
                                     "width=\"" + w + "\" height=\"" + h + "\" " + 
                                     "style=\"border: none;\"" + "></iframe></div>";
                              break;

                case "url" : code = "<div align=\"center\"><iframe src=" + 
                                    "\"http://docs.google.com/viewer?url=" + sc.GetAttributeValue(key, "") + "&embedded=true\" " +
                                    "width=\"" + w + "\" height=\"" + h + "\" " + 
                                    "style=\"border: none;\"" + "></iframe></div>";
                                           
                             break;
            }

            //code = "Key: " + key;

            e.Body = e.Body.Replace(sc.Text, code);
			
		}	
	}		
	
	public static List<ShortCode> GetShortCodes(string input)
    {
		return GetShortCodes(input, true);
	}
    public static List<ShortCode> GetShortCodes(string input, bool removeParagraphs)
    {
		return GetShortCodes(input, @"\w+", removeParagraphs);
	}
    public static List<ShortCode> GetShortCodes(string input, string regexMatchString, bool removeParagraphs)
    {
		List<ShortCode> shortCodes = new List<ShortCode>();

		// get the main tag [tag attr="value"]
		string find = @"\[(?<tag>" + regexMatchString + @")(?<attrs>[^\]]+)\]";
		if (removeParagraphs) {
			find = @"(<p>[\s\n]?)+" + find + @"([\s\n]?</p>)+";
		}
				
		MatchCollection matches = Regex.Matches(input, find);
		
		foreach (Match match in matches) {
			
			string tagName = match.Groups["tag"].Value;
			string attributes = match.Groups["attrs"].Value;

			ShortCode sc = new ShortCode(tagName, match.Value);

			// parse the attributes
			// attr="value"
			MatchCollection attrMatches = Regex.Matches(attributes, @"(?<attr>\w+)=""(?<val>[^""]+)""");			
			
			foreach (Match attrMatch in attrMatches) {
				sc.Attributes.Add(attrMatch.Groups["attr"].Value, attrMatch.Groups["val"].Value);
                sc.Attributes.Add("key",attrMatch.Groups["attr"].Value);
			}

			shortCodes.Add(sc);
		}
		return shortCodes;
	}

    public class ShortCode
    {
		public ShortCode(): this("","") {
		}
		
		public ShortCode(string tagName, string text) {
			_tagName = tagName;
			_text = text;
			_attributes = new Dictionary<string, string>();
		}
		
		private string _tagName;
		private string _text;
		private Dictionary<string, string> _attributes;

		public string TagName { get { return _tagName; } set { _tagName = value; } }
		public string Text { get { return _text; } set { _text = value; } }
		public Dictionary<string, string> Attributes { get { return _attributes; } set { _attributes = value; } }

		public string GetAttributeValue(string attributeName, string defaultValue) {
			if (Attributes.ContainsKey(attributeName))
				return Attributes[attributeName];
			else
				return defaultValue;
		}
	}	
}