using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;


namespace MarkdownRepository.Lib
{
    public class RssHelper
    {
        const string RSS_PATH = "/RssFeeds/rss.xml";
        public static void WriteItem(string title, string link, string description)
        {
            var fullPath = HttpContext.Current.Server.MapPath(RSS_PATH);
            if (File.Exists(fullPath))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(fullPath);
                    var channel = doc.DocumentElement.SelectSingleNode("/rss/channel");

                    XmlNodeList items = doc.DocumentElement.SelectNodes("/rss/channel/item");
                    foreach (XmlNode node in items)
                    {
                        XmlNode pubDate = node.SelectSingleNode("pubDate");
                        if (pubDate != null)
                        {
                            if (Convert.ToDateTime(pubDate.InnerText).AddDays(7) < DateTime.Now)
                                channel.RemoveChild(node);
                        }
                    }


                    XmlElement createNewItem = doc.CreateElement("item");
                    XmlElement titleElement = doc.CreateElement("title");
                    titleElement.InnerText = title;
                    XmlElement linkElement = doc.CreateElement("link");
                    linkElement.InnerText = link;
                    XmlElement descriptionElement = doc.CreateElement("description");
                    descriptionElement.InnerText = description;
                    XmlElement pubDateElement = doc.CreateElement("pubDate");
                    pubDateElement.InnerText = DateTime.Now.ToString();

                    createNewItem.AppendChild(titleElement);
                    createNewItem.AppendChild(linkElement);
                    createNewItem.AppendChild(descriptionElement);
                    createNewItem.AppendChild(pubDateElement);
                    
                    channel.AppendChild(createNewItem);
                    doc.Save(fullPath);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteError(typeof(RssHelper), ex);
                }
            }
        }
    }
}